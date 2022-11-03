/* 
 * Copyright 2022 Jamie Vital
 * This software is licensed under the GNU General Public License
 * 
 * This file is part of goesbetween.
 * Goesbetween is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * Vitality GOES is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * You should have received a copy of the GNU General Public License
 * along with goesbetween.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Net;
using System.Net.Sockets;

class GoesBetween
{
    private static string goesrecvIP = "127.0.0.1", goesrecvPort = "5000", rtltcpPort = "1234";
    private static bool debug = false;

    private static Socket? socket;
    private static IPEndPoint? endpoint;
    private static TcpListener? listener;
    private static TcpClient? client;
    private static NetworkStream? stream;

    static void Main(string[] args)
    {
        if(LoadSettings(args) != 0) return;

        Console.CancelKeyPress += delegate {
            Cleanup();
        };

        while (true)
        {
            Cleanup();
            if (InitNetwork() != 0) continue;
            Transport();
        }
    }

    static int LoadSettings(string[] args)
    {
        //Load Settings
        int currentArg = 0;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "-h":
                case "--help":
                    Help();
                    return 1;

                case "-d":
                case "--debug":
                    debug = true;
                    break;

                case "-H":
                case "--host":
                    if (currentArg == args.Length - 1)
                    {
                        Console.WriteLine("Please specify the goesrecv host IP. See --help for more\n");
                        return 1;
                    }

                    //TODO: Verify valid IP
                    goesrecvIP = args[currentArg + 1];
                    break;

                case "-i":
                case "--inport":
                    if (currentArg == args.Length - 1)
                    {
                        Console.WriteLine("Please specify the goesrecv iq sample port. See --help for more\n");
                        return 1;
                    }

                    //TODO: Verify valid port
                    goesrecvPort = args[currentArg + 1];
                    break;

                case "-o":
                case "--outport":
                    if (currentArg == args.Length - 1)
                    {
                        Console.WriteLine("Please specify the output RTL-TCP port. See --help for more\n");
                        return 1;
                    }

                    //TODO: Verify valid port
                    rtltcpPort = args[currentArg + 1];
                    break;
            }
            currentArg++;
        }

        return 0;
    }

    static int InitNetwork()
    {
        //Set up network objects
        byte[] res = new byte[8];
        byte[] magic = { 0x52, 0x54, 0x4C, 0x30, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01 };
        byte[] nninit = { 0x00, 0x53, 0x50, 0x00, 0x00, 0x21, 0x00, 0x00 };
        byte[] nnires = { 0x00, 0x53, 0x50, 0x00, 0x00, 0x20, 0x00, 0x00 };

        socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        endpoint = new IPEndPoint(IPAddress.Any, Int32.Parse(rtltcpPort));
        listener = new TcpListener(endpoint);

        //Wait for RTL_TCP Client
        try
        {
            Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Waiting for RTL_TCP Client...");
            listener.Start();
            client = listener.AcceptTcpClient();
            stream = client.GetStream();
            stream.Write(magic, 0, 12);
            Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Client Connected!");
        }
        catch (Exception e)
        {
            if (debug)
            {
                Console.WriteLine("\n" + e.Message + "\n");
                Console.WriteLine("\n" + e.StackTrace + "\n");
            }

            Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Could not connect to client");
            return 1;
        }

        //Connect to goesproc
        try
        {
            Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Connecting to goesrecv host...");
            socket.Connect(goesrecvIP, Int32.Parse(goesrecvPort));
            socket.Send(nninit);
            socket.Receive(res);

            if (!Enumerable.SequenceEqual(nnires, res))
            {
                Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Invalid goesproc header");
                return 1;
            }
        }
        catch (Exception e)
        {
            if (debug)
            {
                Console.WriteLine("\n" + e.Message + "\n");
                Console.WriteLine("\n" + e.StackTrace + "\n");
            }

            Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Could Not Connect to goesrecv host");
            return 1;
        }

        Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Connected to goesresv host!");
        return 0;
    }

    static void Transport()
    {
        if (socket == null || stream == null) return;

        byte[] res = new byte[8];
        byte[] dres = new byte[65536];
        byte[] buffer = new byte[65536];

        Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Bridging goesrecv IQ samples to RTL_TCP client...");
        int startReadingAt, num, remainingBytesToWrite, totalBytes = 0, bytesBeforeHeader = 0;
        do
        {
            try
            {
                //Receive all available bytes
                num = socket.Receive(dres);
                remainingBytesToWrite = num;
                startReadingAt = 0;

                //Loop through all nanomsg headers
                while (remainingBytesToWrite > bytesBeforeHeader)
                {
                    //Write Information before header
                    if (bytesBeforeHeader > 0)
                    {
                        stream.Write(dres, startReadingAt, bytesBeforeHeader);
                        Buffer.BlockCopy(dres, startReadingAt, buffer, totalBytes, bytesBeforeHeader);
                        totalBytes += bytesBeforeHeader;
                    }

                    //Get next nanomsg packet length
                    Array.Copy(dres, bytesBeforeHeader + startReadingAt, res, 0, 8);
                    if (BitConverter.IsLittleEndian) Array.Reverse(res);
                    startReadingAt += bytesBeforeHeader + 8;
                    remainingBytesToWrite = num - startReadingAt;
                    bytesBeforeHeader = (int)BitConverter.ToUInt64(res, 0);
                }

                //No more headers in bytes we have; write the rest of the bytes
                Buffer.BlockCopy(dres, startReadingAt, buffer, totalBytes, remainingBytesToWrite);
                bytesBeforeHeader -= remainingBytesToWrite;
                totalBytes += remainingBytesToWrite;
            }
            catch (Exception e)
            {
                if(debug)
                {
                    Console.WriteLine("\n" + e.Message + "\n");
                    Console.WriteLine("\n" + e.StackTrace + "\n");
                }
                
                Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Connection to goesrecv closed");
                return;
            }

            try
            {
                stream.Write(Array.ConvertAll(buffer, thisByte => (byte)(thisByte ^ 0x80)), 0, totalBytes);
            }
            catch (Exception e)
            {
                if (debug)
                {
                    Console.WriteLine("\n" + e.Message + "\n");
                    Console.WriteLine("\n" + e.StackTrace + "\n");
                }

                Console.WriteLine("[" + DateTime.Now.ToString("G") + "] RTL_TCP client disconnected");
                return;
            }

            totalBytes = 0;

        } while (num != 0);

        Console.WriteLine("[" + DateTime.Now.ToString("G") + "] Connection to goesrecv closed");
    }

    static void Cleanup()
    {
        if(socket != null) socket.Close();
        if(stream != null) stream.Close();
        if(listener != null) listener.Stop();
    }

    static void Help()
    {
        Console.WriteLine("goesbetween 0.9 - 2022 Jamie Vital");
        Console.WriteLine("Bridges IQ samples from goesrecv to any RTL_TCP client\n");
        Console.WriteLine("Example usage:\n");
        Console.WriteLine("  Defaults:               ./goesbetween");
        Console.WriteLine("  Same as Defaults:       ./goesbetween -H 127.0.0.1 -i 5000 -o 1234");
        Console.WriteLine("  Custom goesrecv host:   ./goesbetween -H 10.0.0.53");
        Console.WriteLine("  Default with Debugging: ./goesbetween -d\n");

        Console.WriteLine("Options:\n");
        Console.WriteLine("  -d, --debug:    Print debugging statements on error");
        Console.WriteLine("  -h, --help:     Display this help page");
        Console.WriteLine("  -H, --host:     goesrecv IPv4 address (default: 127.0.0.1)");
        Console.WriteLine("  -i, --inport:   goesrecv sample publisher port (default: 5000)");
        Console.WriteLine("  -o, --outport:  RTL_TCP port for clients (default: 1234)\n");
    }
}