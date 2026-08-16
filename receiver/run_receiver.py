#!/usr/bin/env python3

from __future__ import annotations

import logging
import sys
from pathlib import Path
from typing import Any

import receiver
from status_leds import StatusLedController

LOG = logging.getLogger("cometen-irl-alerts")


def read_temperature() -> str:
    try:
        raw = float(Path("/sys/class/thermal/thermal_zone0/temp").read_text(encoding="utf-8").strip())
        if raw > 1000:
            raw /= 1000.0
        return f"{int(round(raw))}C"
    except Exception:
        return "Temp?"


def read_fan_state() -> str:
    base = Path("/sys/class/thermal/cooling_device0")
    try:
        state = int((base / "cur_state").read_text(encoding="utf-8").strip())
        maximum = int((base / "max_state").read_text(encoding="utf-8").strip())
        return f"Fan{state}/{maximum}"
    except Exception:
        return "Fan?"


def build_expanded_status(config: dict[str, Any]) -> str:
    match_text = str(config.get("remote_audio_sink_match", "WPS200")).strip() or "WPS200"

    try:
        sink = receiver.resolve_audio_sink(config)
        audio = f"{match_text} OK n{sink}"
    except Exception:
        audio = f"{match_text} OFF"

    wifi = receiver.current_wifi_status()
    if wifi.startswith("WiFi "):
        wifi = wifi[5:]
    uptime = receiver.format_uptime()

    status_probe = StatusLedController(config)
    try:
        encoder = status_probe._live_process_active()
    except Exception:
        encoder = False

    try:
        video = status_probe._camera_active(encoder)
    except Exception:
        video = None

    if video is True:
        video_text = "VIDEO OK"
    elif encoder:
        video_text = "VIDEO LOST"
    elif video is False:
        video_text = "VIDEO OFF"
    else:
        video_text = "VIDEO ?"

    if encoder and video is True:
        live_text = "LIVE OK"
    elif encoder:
        live_text = "ENC ON"
    else:
        live_text = "LIVE OFF"

    return (
        f"IRL: SYS OK | {read_temperature()} {read_fan_state()} | {audio} | "
        f"{video_text} | {live_text} | WiFi {wifi} | Up {uptime}"
    )[:220]


def install_expanded_status() -> None:
    original_handle_control = receiver.handle_control

    def handle_control(config: dict[str, Any], event: dict[str, Any], config_dir: Path) -> str:
        action = str(event.get("message", "")).strip().lower()
        if action == "status":
            message = build_expanded_status(config)
            LOG.info("Remote control: expanded status requested: %s", message)
            return message
        return original_handle_control(config, event, config_dir)

    receiver.handle_control = handle_control


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    config_path = Path(sys.argv[1] if len(sys.argv) > 1 else "config.json").resolve()

    try:
        config = receiver.load_config(config_path)
    except Exception:
        logging.getLogger("cometen-irl-alerts").exception("Receiver could not load config")
        return 1

    install_expanded_status()

    leds = StatusLedController(config)
    leds.start()

    try:
        return receiver.main()
    finally:
        leds.stop()


if __name__ == "__main__":
    raise SystemExit(main())
