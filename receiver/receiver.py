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
        "User-Agent": "CometenIRLAlerts/0.1",
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

    LOG.info("Cometen IRL Alert Receiver started")
    LOG.info("Relay: %s", base_url)

    while True:
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
                    play_event(config, event, config_dir)
                except Exception:
                    LOG.exception("Alert playback failed for event %s", event.get("id", ""))
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
