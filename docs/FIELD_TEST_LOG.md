# Field Test Log

This file records real-world Cometen IRL System / BELABOX observations where the cause is known or strongly verified.

The goal is to keep operational incidents separate from design documentation so future troubleshooting can compare symptoms against previously confirmed causes.

## 2026-08-21 - RTMP source signal lost because modem lost power

**Observed symptom**

The RTMP-linked video source disappeared / lost signal during IRL operation.

**Confirmed cause**

The modem supplying the network path had run out of power.

**Classification**

Network/power failure outside the BELABOX video pipeline itself.

**What this means for diagnostics**

A lost RTMP source does not automatically mean that the camera, BELABOX encoder, SRT transport or Streamer.bot watchdog has failed. Power state of the modem/network equipment must be checked early when an RTMP source suddenly disappears.

Recommended troubleshooting order for this symptom:

1. Confirm modem/router is powered and online.
2. Confirm the RTMP source device still has network connectivity.
3. Confirm the RTMP publisher has reconnected.
4. Confirm BELABOX sees the source again.
5. Only then continue with encoder/SRT/OBS diagnostics if the source does not recover.

**Operational note**

For mobile/IRL use, modem battery/power should be treated as a monitored dependency alongside the BELABOX power source and camera power.
