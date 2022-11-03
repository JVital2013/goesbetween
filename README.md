# GoesBetween - Goesrecv to rtl_tcp bridge
GoesBetween connects to goesrecv, extracts IQ samples (aka "baseband"), and sends over the network via rtl_tcp. Clients like GNURadio, SDR#, SDR++, and SatDump can then connect to GoesBetween to monitor the spectrum around your satellite downlink, do parallel decoding via SatDump, and more!

![Goesrecv to GoesBetween to SatDump](https://user-images.githubusercontent.com/24253715/199785651-6cda174b-3395-4f8a-a722-0dde2fb34f3d.PNG)

This readme is a WIP, so check back later!

```
Example usage:

  Defaults:               ./goesbetween
  Same as Defaults:       ./goesbetween -H 127.0.0.1 -i 5000 -o 1234
  Custom goesrecv host:   ./goesbetween -H 10.0.0.53
  Default with Debugging: ./goesbetween -d

Options:

  -d, --debug:    Print debugging statements on error
  -h, --help:     Display this help page
  -H, --host:     goesrecv IPv4 address (default: 127.0.0.1)
  -i, --inport:   goesrecv sample publisher port (default: 5000)
  -o, --outport:  RTL_TCP port for clients (default: 1234)
```

### Basic instructions:

1. Make sure your goesrecv.conf contains a [rtlsdr.sample_publisher] or [airspy.sample_publisher] section. Scroll to the "Recommended goesrecv.conf configuration" section for details on that. 

    Make note of the following items from your goesrecv.conf file:
    - sample_rate
    - The port in the bind address for `rtlsdr.sample_publisher` or `airspy.sample_publisher`
    - (Not in goesrecv.conf) The IP address of the computer running goesrecv
    
      ![goesrecv-settings](https://user-images.githubusercontent.com/24253715/199787724-c34985b4-aa27-4625-82ae-0a89d3198238.PNG)

2. Invoke goesbetween from the command line, replacing the IP with the IP address of your goesrecv computer, and the port with the port of goesrecv's rtlsdr.sample_publisher: `goesbetween -H 10.0.0.2 -i 5000`.

    *You can skip any parameters that match the default option, or include extra arguments as needed. For example, if you're running goesbetween on the same computer as goesrecv and the rtlsdr.sample_publisher port is 5000, you can omit all command line arguments.*

3. Connect your SDR software to the IP address and RTL_TCP port of goesbetween (default port is 1234). Also make sure to match your sample rate to the sample rate found in step 2.

   ![rtltcp-client-settings](https://user-images.githubusercontent.com/24253715/199792787-884ac862-a76a-4d17-aff7-2ada96bb6f4b.PNG)
   
4. GoesBetween can only handle one client at a time. When a client disconnects from it, another one can reconnect. Press Ctrl+C (or otherwise close GoesBetween) to exit.
   
***
You won't be able to "tune" the SDR with goesbetween. It only lets you "see" the same spectrum that goesrecv sees for troubleshooting purposes. Because of this, your SDR software will not display the correct frequency, gain settings, or even tuner.

While I have not tested it, this should work if you're using an AirSpy or one of the forks of goestools that work with HackRF, SoapySDR, and others.

The recommended setup is to run goesrecv on one machine, then have your SDR application and goesbetween running together on another. You can also have goesrecv and goesbetween running together on one machine, and your SDR application on another. All 3 pieces of software can run on the same machine too - whatever works best for you.

*While you can technically run goesrecv, goesbetween, and your SDR applications on 3 separate machines, this is not recommended due to the network requirements necessary to handle that much traffic.*

## Recommended goesrecv.conf configuration
You can have more sections to your conf file than this, and the SDR-specific settings will differ based on the SDR you're using. I have several sections enabled for goesrecv-monitor (https://github.com/sam210723/goesrecv-monitor), as well as graphite/statsd (https://hub.docker.com/r/graphiteapp/graphite-statsd/).
```ini
# Change rtlsdr to airspy if you're using an AirSpy, etc.
[demodulator]
mode = "hrit"
source = "rtlsdr" 

# Change rtlsdr to airspy if you're using an AirSpy, etc.
# Other settings may vary based on your setup
[rtlsdr]
frequency = 1694100000
sample_rate = 2400000
gain = 5

# Change rtlsdr to airspy if you're using an AirSpy, etc.
# Publishes IQ samples coming straight from the SDR over
# nanomsg
[rtlsdr.sample_publisher]
bind = "tcp://0.0.0.0:5000"
send_buffer = 2097152

[costas]
max_deviation = 200e3

# Used by goesrecv-monitor to render constellation diagram
[clock_recovery.sample_publisher]
bind = "tcp://0.0.0.0:5002"
send_buffer = 2097152

# Used to pass data to goesproc, goeslrit, etc
[decoder.packet_publisher]
bind = "tcp://0.0.0.0:5004"
send_buffer = 1048576

# Used by goesrecv-monitor for statistics
[demodulator.stats_publisher]
bind = "tcp://0.0.0.0:6001"

# Used by goesrecv-monitor for statistics
[decoder.stats_publisher]
bind = "tcp://0.0.0.0:6002"

# Used by graphite/statsd
[monitor]
statsd_address = "udp4://localhost:8125"
```
