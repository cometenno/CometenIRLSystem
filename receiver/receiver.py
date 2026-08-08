#!/usr/bin/env python3

from __future__ import annotations

import json
import logging
import os
import shlex
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

LOG = logging.getLogger("cometen-irl-alerts")


def load_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise FileNotFoundError(
            f"Missing configuration file: {path}. Copy config.example.json to config.json first."
        )

    with path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)

    required = ("relay_base_url", "receiver_token")
    missing = [name for name in required if not str(config.get(name, "")).strip()]
    if missing:
        raise ValueError(f"Missing required configuration values: {', '.join(missing)}")

    return config


def request_json(
    method: str,
    url: str,
    token: str,
    timeout: float,
    payload: dict[str, Any] | None = None,
) -> dict[str, Any]:
    data = None
    headers = {
        "Accept": "application/json",
        "X-Cometen-Token": token,
        "User-Agent": "CometenIRLAlerts/0.3",
    }

    if payload is not None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(url, data=data, headers=headers, method=method)

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read().decode("utf-8")
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Relay returned HTTP {error.code}: {body}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"Relay connection failed: {error.reason}") from error

    try:
        decoded = json.loads(body)
    except json.JSONDecodeError as error:
        raise RuntimeError("Relay returned invalid JSON") from error

    if not isinstance(decoded, dict):
        raise RuntimeError("Relay returned an unexpected response")

    return decoded


def command_from_config(value: Any, default: list[str]) -> list[str]:
    if isinstance(value, list) and value:
        return [str(item) for item in value]

    if isinstance(value, str) and value.strip():
        return shlex.split(value)

    return list(default)


class AudioKeepalive:
    def __init__(self, config: dict[str, Any]) -> None:
        self.enabled = bool(config.get("audio_keepalive_enabled", True))
        self.command = command_from_config(
            config.get("audio_keepalive_command"),
            [
                "pw-cat",
                "--playback",
                "--rate=48000",
                "--channels=2",
                "--format=s16",
                "-",
            ],
        )
        self.input_path = Path(str(config.get("audio_keepalive_input", "/dev/zero")))
        self.restart_seconds = max(
            2.0, float(config.get("audio_keepalive_restart_seconds", 5))
        )
        self.process: subprocess.Popen[bytes] | None = None
        self.input_handle: Any = None
        self.next_start_at = 0.0

    def _close_input(self) -> None:
        if self.input_handle is None:
            return

        try:
            self.input_handle.close()
        except Exception:
            pass
        finally:
            self.input_handle = None

    def _log_previous_exit(self) -> None:
        if self.process is None:
            return

        exit_code = self.process.poll()
        details = ""
        if self.process.stderr is not None:
            try:
                details = self.process.stderr.read().decode("utf-8", errors="replace").strip()
            except Exception:
                details = ""

        if details:
            LOG.warning(
                "Audio keepalive stopped with exit code %s: %s",
                exit_code,
                details,
            )
        else:
            LOG.warning("Audio keepalive stopped with exit code %s", exit_code)

        self.process = None
        self._close_input()

    def ensure_running(self) -> None:
        if not self.enabled:
            return

        if self.process is not None and self.process.poll() is None:
            return

        now = time.monotonic()
        if now < self.next_start_at:
            return

        if self.process is not None:
            self._log_previous_exit()

        executable = shutil.which(self.command[0])
        if executable is None:
            LOG.warning("Audio keepalive command was not found: %s", self.command[0])
            self.next_start_at = now + 30.0
            return

        if not self.input_path.exists():
            LOG.warning("Audio keepalive input was not found: %s", self.input_path)
            self.next_start_at = now + 30.0
            return

        command = [executable, *self.command[1:]]

        try:
            self.input_handle = self.input_path.open("rb", buffering=0)
            self.process = subprocess.Popen(
                command,
                stdin=self.input_handle,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.PIPE,
            )
            self.next_start_at = now + self.restart_seconds
            LOG.info("Audio keepalive started through PipeWire")
        except Exception:
            LOG.exception("Audio keepalive could not start")
            self.process = None
            self._close_input()
            self.next_start_at = now + self.restart_seconds

    def stop(self) -> None:
        process = self.process
        self.process = None

        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=2)

        self._close_input()
        LOG.info("Audio keepalive stopped")


def build_player_command(config: dict[str, Any], sound_path: Path) -> list[str]:
    configured = config.get("audio_player")

    if isinstance(configured, list) and configured:
        command = [str(item).replace("{file}", str(sound_path)) for item in configured]
        if not any("{file}" in str(item) for item in configured):
            command.append(str(sound_path))
        return command

    if isinstance(configured, str) and configured.strip():
        parts = shlex.split(configured)
        command = [part.replace("{file}", str(sound_path)) for part in parts]
        if "{file}" not in configured:
            command.append(str(sound_path))
        return command

    for candidate in ("pw-play", "paplay", "aplay"):
        executable = shutil.which(candidate)
        if executable:
            return [executable, str(sound_path)]

    raise RuntimeError(
        "No supported audio player found. Install pw-play, paplay or aplay, "
        "or configure audio_player explicitly."
    )


def resolve_sound(config: dict[str, Any], event: dict[str, Any], config_dir: Path) -> Path:
    sounds_directory = Path(str(config.get("sounds_directory", "sounds")))
    if not sounds_directory.is_absolute():
        sounds_directory = config_dir / sounds_directory
    sounds_directory = sounds_directory.resolve()

    requested = os.path.basename(str(event.get("sound", "")).strip())
    event_type = str(event.get("type", "test")).strip().lower()
    sound_map = config.get("sound_map", {})

    candidates = [
        requested,
        os.path.basename(str(sound_map.get(event_type, ""))) if isinstance(sound_map, dict) else "",
        os.path.basename(str(config.get("default_sound", "test.wav"))),
    ]

    for candidate in candidates:
        if not candidate:
            continue
        path = (sounds_directory / candidate).resolve()
        if path.parent == sounds_directory and path.is_file():
            return path

    raise FileNotFoundError(f"No local sound file found for event type '{event_type}'")


def play_event(config: dict[str, Any], event: dict[str, Any], config_dir: Path) -> None:
    sound_path = resolve_sound(config, event, config_dir)
    command = build_player_command(config, sound_path)
    timeout = max(5.0, float(config.get("audio_player_timeout_seconds", 30)))

    LOG.info(
        "Playing event id=%s type=%s user=%s sound=%s",
        event.get("id", ""),
        event.get("type", ""),
        event.get("user", ""),
        sound_path.name,
    )

    subprocess.run(
        command,
        check=True,
        timeout=timeout,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
    )


def run_wpctl(*arguments: str) -> str:
    executable = shutil.which("wpctl")
    if executable is None:
        raise RuntimeError("wpctl was not found; PipeWire/WirePlumber control is unavailable")

    completed = subprocess.run(
        [executable, *arguments],
        check=True,
        timeout=10,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return completed.stdout.strip()


def current_volume_percent(sink: str) -> int:
    output = run_wpctl("get-volume", sink)
    for token in output.replace(":", " ").split():
        try:
            value = float(token)
        except ValueError:
            continue
        return max(0, int(round(value * 100)))
    raise RuntimeError(f"Could not parse wpctl volume output: {output}")


def handle_control(config: dict[str, Any], event: dict[str, Any]) -> None:
    if not bool(config.get("remote_control_enabled", True)):
        raise RuntimeError("IRL remote control is disabled in config")

    action = str(event.get("message", "")).strip().lower()
    value = int(event.get("amount", 0) or 0)
    step = max(1, min(25, int(config.get("remote_volume_step_percent", 5))))
    max_volume = max(1, min(100, int(config.get("remote_volume_max_percent", 100))))
    sink = str(config.get("remote_audio_sink", "@DEFAULT_AUDIO_SINK@")).strip()
    if not sink:
        sink = "@DEFAULT_AUDIO_SINK@"

    if action == "volume_set":
        value = max(0, min(max_volume, value))
        run_wpctl("set-volume", sink, f"{value / 100.0:.2f}")
        LOG.info("Remote control: volume set to %s%%", value)
        return

    if action == "volume_up":
        current = current_volume_percent(sink)
        value = min(max_volume, current + step)
        run_wpctl("set-volume", sink, f"{value / 100.0:.2f}")
        LOG.info("Remote control: volume increased to %s%%", value)
        return

    if action == "volume_down":
        current = current_volume_percent(sink)
        value = max(0, current - step)
        run_wpctl("set-volume", sink, f"{value / 100.0:.2f}")
        LOG.info("Remote control: volume decreased to %s%%", value)
        return

    if action == "mute":
        run_wpctl("set-mute", sink, "1")
        LOG.info("Remote control: muted")
        return

    if action == "unmute":
        run_wpctl("set-mute", sink, "0")
        LOG.info("Remote control: unmuted")
        return

    raise ValueError(f"Unsupported remote control action: {action or '<empty>'}")


def acknowledge(
    base_url: str,
    token: str,
    timeout: float,
    event: dict[str, Any],
) -> None:
    response = request_json(
        "POST",
        f"{base_url}/acknowledge.php",
        token,
        timeout,
        {
            "id": str(event.get("id", "")),
            "lease_id": str(event.get("lease_id", "")),
        },
    )

    if not response.get("ok"):
        raise RuntimeError(f"Relay rejected acknowledgement: {response}")


def run(config_path: Path) -> None:
    config = load_config(config_path)
    config_dir = config_path.parent.resolve()
    base_url = str(config["relay_base_url"]).rstrip("/")
    token = str(config["receiver_token"])
    timeout = max(2.0, float(config.get("request_timeout_seconds", 10)))
    interval = max(0.25, float(config.get("poll_interval_seconds", 0.75)))
    batch_size = max(1, min(10, int(config.get("batch_size", 5))))
    backoff = interval
    keepalive = AudioKeepalive(config)

    LOG.info("Cometen IRL Alert Receiver started")
    LOG.info("Relay: %s", base_url)
    keepalive.ensure_running()

    try:
        while True:
            keepalive.ensure_running()

            try:
                response = request_json(
                    "GET",
                    f"{base_url}/poll.php?limit={batch_size}",
                    token,
                    timeout,
                )

                if not response.get("ok"):
                    raise RuntimeError(f"Relay rejected poll: {response}")

                events = response.get("events", [])
                if not isinstance(events, list):
                    raise RuntimeError("Relay returned an invalid event list")

                for event in events:
                    if not isinstance(event, dict):
                        continue

                    try:
                        event_type = str(event.get("type", "")).strip().lower()
                        if event_type == "control":
                            handle_control(config, event)
                        else:
                            play_event(config, event, config_dir)
                    except Exception:
                        LOG.exception("Event handling failed for event %s", event.get("id", ""))
                    finally:
                        try:
                            acknowledge(base_url, token, timeout, event)
                        except Exception:
                            LOG.exception("Could not acknowledge event %s", event.get("id", ""))

                backoff = interval
                if not events:
                    time.sleep(interval)

            except KeyboardInterrupt:
                LOG.info("Receiver stopped")
                return
            except Exception:
                LOG.exception("Receiver loop failed")
                time.sleep(backoff)
                backoff = min(15.0, max(interval, backoff * 2.0))
    finally:
        keepalive.stop()


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    config_path = Path(sys.argv[1] if len(sys.argv) > 1 else "config.json").resolve()

    try:
        run(config_path)
    except Exception:
        LOG.exception("Receiver could not start")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
