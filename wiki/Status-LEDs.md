# Status LEDs

Cometen IRL System uses four physical status LEDs on the BELABOX / ROCK 5B+ enclosure.

## LED meanings

### Green - ONLINE / INTERNET

- slow blink - internet/relay connectivity has not yet been established
- solid - BELABOX is online and connected to the internet; relay is reachable
- fast blink - internet/relay connectivity was lost after the system had been online
- off - box or receiver service is off

### Blue - BLUETOOTH AUDIO

- solid - the configured Bluetooth audio device exists as a PipeWire `Audio/Sink`
- slow blink - the configured Bluetooth audio sink is missing but its reconnect/watchdog service is active
- fast blink - the configured Bluetooth audio sink is missing and its reconnect/watchdog service is inactive

The blue LED is not tied to one speaker model. WPS200, Soundcore or another compatible Bluetooth audio device may be used according to local configuration.

### Yellow - VIDEO INPUT

Yellow indicates whether the current video source is available.

Supported source types include:

```text
Local device input:
/dev/usb_capture
/dev/hdmirx
/dev/hdmi_capture

Network input:
RTMP publisher / rtmpsrc
```

RTMP is detected separately from device files. The video probe can use the configured nginx-rtmp status endpoint and stream name, fall back to an external established publisher on TCP port `1935`, and inspect the active BELABOX pipeline for `rtmpsrc`.

Typical states:

- solid - a supported source is available
- fast blink - encoder is running but its active source is unavailable, including USB/V4L2 failure or RTMP publisher loss
- off - no supported source is available

### Red - BELABOX ENCODER / OUTPUT

- solid - `belacoder` is running and the active video input/pipeline is healthy
- fast blink - `belacoder` is running but the active video input/pipeline is missing
- slow blink - encoder is running but video state cannot be determined
- off - BELABOX encoder is stopped

Yellow and red are intentionally separate. If the source remains available while BELABOX is stopped, yellow can remain solid while red is off.

## Detailed documentation

The canonical detailed guide, including GPIO pins, resistor values, install/update commands, configuration and the root video probe, is maintained here:

https://github.com/la1ona/CometenIRLSystem/blob/main/docs/STATUS_LEDS.md
