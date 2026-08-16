#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import logging
import re
import shutil
import socket
import subprocess
import threading
import time
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

LOG = logging.getLogger("cometen-irl-alerts.leds")

try:
    import gpiod  # type: ignore
except ImportError:
    gpiod = None

OFF = "off"
ON = "on"
SLOW = "slow"
FAST = "fast"


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

        self.relay_base_url = str(config.get("relay_base_url", "")).strip()
        self.bluetooth_match = str(
            self.settings.get(
                "bluetooth_sink_match",
                config.get("remote_audio_sink_match", "WPS200"),
            )
        ).strip() or "WPS200"
        self.bluetooth_watchdog_service = str(
            self.settings.get("bluetooth_watchdog_service", "cometen-wps200.service")
        ).strip()
        self.camera_device = str(
            self.settings.get("camera_device", "/dev/usb_capture")
        ).strip()
        self.camera_status_url = str(
            self.settings.get("camera_status_url", "http://127.0.0.1/stat")
        ).strip()
        self.camera_app = str(self.settings.get("camera_app", "publish")).strip()
        self.camera_stream = str(self.settings.get("camera_stream", "live")).strip()
        self.live_process = str(self.settings.get("live_process", "belacoder")).strip()

        self._outputs: dict[str, GpioOutput] = {}
        self._patterns = {"green": SLOW, "blue": SLOW, "yellow": SLOW, "red": OFF}
        self._last_rendered: dict[str, bool | None] = {
            "green": None,
            "blue": None,
            "yellow": None,
            "red": None,
        }
        self._lock = threading.Lock()
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None
        self._ever_online = False
        self._ever_camera = False

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

    def _relay_reachable(self) -> bool:
        if not self.relay_base_url:
            return False
        parsed = urlparse(self.relay_base_url)
        host = parsed.hostname
        if not host:
            return False
        port = parsed.port or (443 if parsed.scheme == "https" else 80)
        try:
            with socket.create_connection((host, port), timeout=2.0):
                return True
        except OSError:
            return False

    def _bluetooth_sink_connected(self) -> bool:
        pw_cli = shutil.which("pw-cli")
        if pw_cli is None:
            return False
        try:
            completed = subprocess.run(
                [pw_cli, "ls", "Node"],
                check=True,
                timeout=5,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
        except Exception:
            return False
        blocks = re.split(
            r"(?=^\s*id\s+\d+,\s+type\s+PipeWire:Interface:Node/3\s*$)",
            completed.stdout,
            flags=re.MULTILINE,
        )
        match_text = self.bluetooth_match.lower()
        for block in blocks:
            if 'media.class = "Audio/Sink"' not in block:
                continue
            if match_text in block.lower():
                return True
        return False

    def _watchdog_active(self) -> bool:
        if not self.bluetooth_watchdog_service:
            return False
        systemctl = shutil.which("systemctl")
        if systemctl is None:
            return False
        try:
            return subprocess.run(
                [systemctl, "is-active", "--quiet", self.bluetooth_watchdog_service],
                check=False,
                timeout=5,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            ).returncode == 0
        except Exception:
            return False

    def _camera_active_from_device(self) -> bool | None:
        if not self.camera_device:
            return None
        try:
            device = Path(self.camera_device)
            if device.exists():
                return True
            if device.is_symlink():
                return False
        except Exception:
            return None
        return None

    def _camera_active_from_stat(self) -> bool | None:
        if not self.camera_status_url:
            return None
        try:
            request = urllib.request.Request(
                self.camera_status_url,
                headers={"User-Agent": "CometenIRLAlerts/status-leds"},
            )
            with urllib.request.urlopen(request, timeout=2.0) as response:
                body = response.read()
            root = ET.fromstring(body)
        except Exception:
            return None

        wanted_app = self.camera_app.lower()
        wanted_stream = self.camera_stream.lower()
        for application in root.findall(".//application"):
            app_name = (application.findtext("name") or "").strip().lower()
            if wanted_app and app_name != wanted_app:
                continue
            for stream in application.findall(".//stream"):
                stream_name = (stream.findtext("name") or "").strip().lower()
                if stream_name == wanted_stream:
                    return True
        return False

    def _camera_active_from_ss(self) -> bool | None:
        ss = shutil.which("ss")
        if ss is None:
            return None
        try:
            completed = subprocess.run(
                [ss, "-Htn"],
                check=True,
                timeout=5,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                text=True,
            )
        except Exception:
            return None
        for line in completed.stdout.splitlines():
            if ":1935" not in line:
                continue
            if "127.0.0.1" in line or "[::1]" in line:
                continue
            return True
        return False

    def _camera_active(self) -> bool | None:
        device_status = self._camera_active_from_device()
        if device_status is True:
            return True

        status = self._camera_active_from_stat()
        if status is not None:
            return status

        status = self._camera_active_from_ss()
        if status is not None:
            return status

        return device_status

    def _live_process_active(self) -> bool:
        if not self.live_process:
            return False
        pgrep = shutil.which("pgrep")
        if pgrep is None:
            return False
        try:
            return subprocess.run(
                [pgrep, "-x", self.live_process],
                check=False,
                timeout=5,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            ).returncode == 0
        except Exception:
            return False

    def _probe(self) -> None:
        online = self._relay_reachable()
        bluetooth = self._bluetooth_sink_connected()
        watchdog = self._watchdog_active()
        camera = self._camera_active()
        live_process = self._live_process_active()

        if online:
            self._ever_online = True
            self._set_pattern("green", ON)
        else:
            self._set_pattern("green", FAST if self._ever_online else SLOW)

        if bluetooth:
            self._set_pattern("blue", ON)
        else:
            self._set_pattern("blue", SLOW if watchdog else FAST)

        if camera is True:
            self._ever_camera = True
            self._set_pattern("yellow", ON)
        elif camera is False:
            self._set_pattern("yellow", FAST if self._ever_camera else SLOW)
        else:
            self._set_pattern("yellow", SLOW)

        if live_process and camera is True:
            self._set_pattern("red", ON)
        elif live_process and camera is False:
            self._set_pattern("red", FAST)
        elif live_process:
            self._set_pattern("red", SLOW)
        else:
            self._set_pattern("red", OFF)

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
        return 0
    finally:
        controller.stop()


if __name__ == "__main__":
    raise SystemExit(main())
