#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import logging
import shutil
import socket
import subprocess
import threading
import time
from pathlib import Path
from typing import Any

LOG = logging.getLogger("cometen-irl-alerts.leds")

try:
    import gpiod  # type: ignore
except ImportError:
    gpiod = None

OFF = "off"
ON = "on"
SLOW = "slow"
FAST = "fast"

VIDEO_STATUS_DEFAULT = Path("/run/cometen-irl-video-status.json")
INTERNET_TARGETS = (
    ("1.1.1.1", 443),
    ("8.8.8.8", 53),
    ("9.9.9.9", 443),
)


class GpioOutput:
    def __init__(self, line_name: str, active_high: bool = True) -> None:
        if gpiod is None:
            raise RuntimeError("Python libgpiod is missing. Install package python3-libgpiod.")

        gpiofind = shutil.which("gpiofind")
        if gpiofind is None:
            raise RuntimeError("gpiofind was not found. Install package gpiod.")

        found = subprocess.run(
            [gpiofind, line_name],
            check=True,
            timeout=5,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ).stdout.strip()

        parts = found.split()
        if len(parts) != 2:
            raise RuntimeError(f"Could not resolve GPIO line {line_name!r}: {found!r}")

        chip_name, offset_text = parts
        self.line_name = line_name
        self.active_high = active_high
        self.chip = gpiod.Chip(chip_name)
        self.line = self.chip.get_line(int(offset_text))
        self.line.request(
            consumer="cometen-irl-alerts-leds",
            type=gpiod.LINE_REQ_DIR_OUT,
            default_vals=[0 if active_high else 1],
        )
        self.last_value: bool | None = None

    def set(self, on: bool) -> None:
        if self.last_value is on:
            return
        physical = 1 if (on == self.active_high) else 0
        self.line.set_value(physical)
        self.last_value = on

    def close(self) -> None:
        try:
            self.set(False)
        except Exception:
            pass
        try:
            self.line.release()
        except Exception:
            pass
        try:
            self.chip.close()
        except Exception:
            pass


class StatusLedController:
    """Front-panel status LEDs for the BELABOX itself.

    Semantics are deliberately local and simple:
      green  = Internet reachable
      blue   = configured Bluetooth speaker connected
      yellow = real video input/feed present
      red    = BELABOX encoder pipeline actively sending/processing that input

    The relay/webhotel is intentionally NOT part of the LED logic.
    """

    def __init__(self, config: dict[str, Any], force_enabled: bool = False) -> None:
        raw_settings = config.get("status_leds", {})
        self.settings = raw_settings if isinstance(raw_settings, dict) else {}

        self.enabled = force_enabled or bool(
            config.get("status_leds_enabled", self.settings.get("enabled", False))
        )
        self.active_high = bool(self.settings.get("active_high", True))
        self.poll_seconds = max(0.5, float(self.settings.get("poll_seconds", 2.0)))
        self.lamp_test_enabled = bool(self.settings.get("lamp_test", True))
        self.lamp_test_seconds = max(
            0.05, min(2.0, float(self.settings.get("lamp_test_seconds", 0.30)))
        )
        self.line_names = {
            "green": str(self.settings.get("green_line", "PIN_32")),
            "blue": str(self.settings.get("blue_line", "PIN_36")),
            "yellow": str(self.settings.get("yellow_line", "PIN_38")),
            "red": str(self.settings.get("red_line", "PIN_40")),
        }

        self.bluetooth_match = str(
            self.settings.get(
                "bluetooth_sink_match",
                config.get("remote_audio_sink_match", "WPS200"),
            )
        ).strip() or "WPS200"

        self.video_status_path = Path(
            str(self.settings.get("video_status_path", VIDEO_STATUS_DEFAULT))
        )
        self.video_status_stale_seconds = max(
            1.0, float(self.settings.get("video_probe_stale_seconds", 3.0))
        )

        self._outputs: dict[str, GpioOutput] = {}
        self._patterns = {"green": OFF, "blue": OFF, "yellow": OFF, "red": OFF}
        self._last_rendered: dict[str, bool | None] = {
            "green": None,
            "blue": None,
            "yellow": None,
            "red": None,
        }
        self._last_states: dict[str, bool | None] = {
            "internet": None,
            "bluetooth": None,
            "video_input": None,
            "output": None,
        }
        self._lock = threading.Lock()
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    def _open_gpio(self) -> None:
        opened: dict[str, GpioOutput] = {}
        try:
            for color, line_name in self.line_names.items():
                opened[color] = GpioOutput(line_name, self.active_high)
        except Exception:
            for output in opened.values():
                output.close()
            raise
        self._outputs = opened

    def _set_pattern(self, color: str, pattern: str) -> None:
        with self._lock:
            self._patterns[color] = pattern

    def _get_patterns(self) -> dict[str, str]:
        with self._lock:
            return dict(self._patterns)

    @staticmethod
    def _pattern_value(pattern: str, now: float) -> bool:
        if pattern == ON:
            return True
        if pattern == OFF:
            return False
        if pattern == FAST:
            return int(now / 0.125) % 2 == 0
        return int(now / 0.50) % 2 == 0

    def _render(self) -> None:
        patterns = self._get_patterns()
        now = time.monotonic()
        for color, output in self._outputs.items():
            value = self._pattern_value(patterns.get(color, OFF), now)
            if self._last_rendered.get(color) == value:
                continue
            output.set(value)
            self._last_rendered[color] = value

    def lamp_test(self) -> None:
        if not self._outputs:
            return
        for output in self._outputs.values():
            output.set(False)
        for color in ("green", "blue", "yellow", "red"):
            self._outputs[color].set(True)
            time.sleep(self.lamp_test_seconds)
            self._outputs[color].set(False)
        for output in self._outputs.values():
            output.set(True)
        time.sleep(self.lamp_test_seconds)
        for output in self._outputs.values():
            output.set(False)
        self._last_rendered = {name: None for name in self._last_rendered}

    @staticmethod
    def _internet_reachable() -> bool:
        """Test general Internet reachability, independent of the IRL relay/webhotel."""
        for host, port in INTERNET_TARGETS:
            try:
                with socket.create_connection((host, port), timeout=1.25):
                    return True
            except OSError:
                continue
        return False

    def _bluetooth_connected(self) -> bool:
        """Return BlueZ connection state for the configured speaker name."""
        bluetoothctl = shutil.which("bluetoothctl")
        if bluetoothctl is None:
            return False

        try:
            completed = subprocess.run(
                [bluetoothctl, "devices"],
                check=False,
                timeout=4,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                text=True,
            )
        except Exception:
            return False

        wanted = self.bluetooth_match.lower()
        addresses: list[str] = []
        for line in completed.stdout.splitlines():
            parts = line.strip().split(maxsplit=2)
            if len(parts) < 3 or parts[0] != "Device":
                continue
            if wanted in parts[2].lower():
                addresses.append(parts[1])

        for address in addresses:
            try:
                info = subprocess.run(
                    [bluetoothctl, "info", address],
                    check=False,
                    timeout=4,
                    stdin=subprocess.DEVNULL,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.DEVNULL,
                    text=True,
                ).stdout
            except Exception:
                continue
            if "Connected: yes" in info:
                return True

        return False

    def _video_status(self) -> dict[str, Any] | None:
        try:
            payload = json.loads(self.video_status_path.read_text(encoding="utf-8"))
            if not isinstance(payload, dict):
                return None
            updated = float(payload.get("updated_unix", 0) or 0)
            if updated <= 0:
                return None
            if time.time() - updated > self.video_status_stale_seconds:
                return None
            return payload
        except Exception:
            return None

    def _log_transition(self, name: str, value: bool) -> None:
        if self._last_states.get(name) == value:
            return
        self._last_states[name] = value
        LOG.info("LED state %s=%s", name, "ON" if value else "OFF")

    def _probe(self) -> None:
        internet = self._internet_reachable()
        bluetooth = self._bluetooth_connected()
        video = self._video_status()

        video_input = bool(video and video.get("source_present") is True)
        output = bool(
            video
            and video.get("encoder_running") is True
            and video.get("pipeline_active") is True
        )

        self._set_pattern("green", ON if internet else OFF)
        self._set_pattern("blue", ON if bluetooth else OFF)
        self._set_pattern("yellow", ON if video_input else OFF)
        self._set_pattern("red", ON if output else OFF)

        self._log_transition("internet", internet)
        self._log_transition("bluetooth", bluetooth)
        self._log_transition("video_input", video_input)
        self._log_transition("output", output)

    def _run(self) -> None:
        next_probe = 0.0
        while not self._stop_event.is_set():
            now = time.monotonic()
            if now >= next_probe:
                try:
                    self._probe()
                except Exception:
                    LOG.exception("Status LED probe failed")
                next_probe = now + self.poll_seconds
            try:
                self._render()
            except Exception:
                LOG.exception("Status LED update failed")
                return
            self._stop_event.wait(0.10)

    def start(self) -> None:
        if not self.enabled:
            LOG.info("Status LEDs are disabled")
            return
        try:
            self._open_gpio()
        except Exception as exception:
            LOG.warning("Status LEDs disabled: %s", exception)
            self.enabled = False
            return

        LOG.info(
            "Status LEDs enabled: green=%s blue=%s yellow=%s red=%s",
            self.line_names["green"],
            self.line_names["blue"],
            self.line_names["yellow"],
            self.line_names["red"],
        )

        if self.lamp_test_enabled:
            try:
                self.lamp_test()
            except Exception:
                LOG.exception("Status LED lamp test failed")

        self._stop_event.clear()
        self._thread = threading.Thread(
            target=self._run,
            name="cometen-status-leds",
            daemon=True,
        )
        self._thread.start()

    def stop(self) -> None:
        self._stop_event.set()
        thread = self._thread
        self._thread = None
        if thread is not None:
            thread.join(timeout=3)
        for output in self._outputs.values():
            output.close()
        self._outputs.clear()


def load_config(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        loaded = json.load(handle)
    if not isinstance(loaded, dict):
        raise ValueError("config.json must contain a JSON object")
    return loaded


def main() -> int:
    parser = argparse.ArgumentParser(description="Cometen IRL Alerts status LEDs")
    parser.add_argument("config", nargs="?", default="config.json")
    parser.add_argument(
        "--test",
        action="store_true",
        help="Force-enable GPIO and run only the startup lamp test",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    config = load_config(Path(args.config).resolve())
    controller = StatusLedController(config, force_enabled=args.test)

    if args.test:
        try:
            controller._open_gpio()
            controller.lamp_test()
        finally:
            controller.stop()
        return 0

    controller.start()
    if not controller.enabled:
        return 1

    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        pass
    finally:
        controller.stop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
