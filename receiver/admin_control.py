#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import re
import subprocess
from pathlib import Path
from typing import Any

MAC_RE = re.compile(r"^(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")
SOURCE_NAME_RE = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")
HELPER = Path("/usr/local/sbin/cometen-irl-admin-helper")
ACTIONS = {
    "admin_browser_audio_get",
    "admin_browser_audio_list",
    "bt_status",
    "bt_list",
    "bt_scan",
    "bt_pair",
    "bt_connect",
    "bt_disconnect",
    "bt_remove",
    "bt_default",
}


def handles(action_text: str) -> bool:
    action = (action_text or "").strip().split(" ", 1)[0].lower()
    return action in ACTIONS


def _load_config(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError("config.json must contain a JSON object")
    return value


def _save_config(path: Path, config: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        json.dump(config, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    os.replace(temporary, path)


def _normalize_source_name(value: str) -> str:
    name = (value or "").strip().lower()
    if not SOURCE_NAME_RE.fullmatch(name):
        raise ValueError("ugyldig Browser Audio-kildenavn")
    return name


def _browser_sources(config: dict[str, Any]) -> list[dict[str, Any]]:
    raw = config.get("browser_audio_sources")
    sources: list[dict[str, Any]] = []

    if isinstance(raw, list):
        for item in raw:
            if not isinstance(item, dict):
                continue
            try:
                name = _normalize_source_name(str(item.get("name", "")))
            except ValueError:
                continue
            url = str(item.get("url", "")).strip()
            sources.append(
                {
                    "name": name,
                    "url": url,
                    "enabled": bool(item.get("enabled", True)),
                }
            )

    if not sources:
        legacy_url = str(config.get("browser_audio_url", "")).strip()
        if legacy_url:
            sources.append(
                {
                    "name": "soundalerts",
                    "url": legacy_url,
                    "enabled": bool(config.get("browser_audio_enabled", False)),
                }
            )

    return sources


def _browser_audio_get(config_path: Path, source_name: str = "soundalerts") -> str:
    config = _load_config(config_path)
    name = _normalize_source_name(source_name or "soundalerts")
    master = 1 if bool(config.get("browser_audio_enabled", False)) else 0

    source = next(
        (item for item in _browser_sources(config) if item["name"] == name),
        None,
    )
    if source is None:
        return f"BROWSER|{master}|{name}|0|"

    enabled = 1 if bool(source.get("enabled", True)) else 0
    url = str(source.get("url", "")).strip()

    # Control-result messages are limited to 220 characters. Keep the source
    # name plus URL inside that limit. Web Admin caps URLs at 170 chars so the
    # full response remains intact.
    return f"BROWSER|{master}|{name}|{enabled}|{url}"[:220]


def _browser_audio_list(config_path: Path) -> str:
    config = _load_config(config_path)
    master = 1 if bool(config.get("browser_audio_enabled", False)) else 0
    sources = _browser_sources(config)

    # Names are intentionally compact. The Web Admin caps new names at 20
    # characters, while existing longer names are truncated only in this list
    # response. A source is fetched by its returned name afterwards.
    entries: list[str] = []
    prefix = f"BROWSERLIST|{master}|"

    for source in sources[:8]:
        name = str(source.get("name", "")).strip().lower()
        enabled = "1" if bool(source.get("enabled", True)) else "0"
        record = f"{name}~{enabled}"

        projected = len(prefix) + len(";".join([*entries, record]))
        if projected > 218:
            break
        entries.append(record)

    return prefix + ";".join(entries)


def _validate_mac(value: str) -> str:
    mac = (value or "").strip().upper()
    if not MAC_RE.fullmatch(mac):
        raise ValueError("ugyldig Bluetooth MAC-adresse")
    return mac


def _run_helper(action: str, argument: str = "", timeout: int = 35) -> str:
    if not HELPER.is_file():
        raise RuntimeError(
            "BELABOX admin helper mangler. Kjør belabox/install-admin-helper.sh først"
        )

    command = ["sudo", "-n", str(HELPER), action]
    if argument:
        command.append(argument)

    completed = subprocess.run(
        command,
        check=False,
        timeout=timeout,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )

    output = (completed.stdout or "").strip()
    error = (completed.stderr or "").strip()
    if completed.returncode != 0:
        detail = error or output or f"rc={completed.returncode}"
        raise RuntimeError(f"Bluetooth admin-feil: {detail[:160]}")
    return (output or "OK")[:220]


def _set_default(config: dict[str, Any], config_path: Path, mac: str) -> str:
    result = _run_helper("default", mac)
    parts = result.split("|", 2)
    if len(parts) < 3 or parts[0] != "BTDEFAULT":
        raise RuntimeError(f"Uventet svar fra Bluetooth helper: {result}")

    saved_mac = _validate_mac(parts[1])
    name = parts[2].strip() or saved_mac

    fresh = _load_config(config_path)
    fresh["remote_audio_sink"] = "auto"
    fresh["remote_audio_sink_match"] = name
    fresh["browser_audio_sink_match"] = name

    raw_leds = fresh.get("status_leds")
    leds = raw_leds if isinstance(raw_leds, dict) else {}
    leds["bluetooth_sink_match"] = name
    fresh["status_leds"] = leds

    _save_config(config_path, fresh)

    # Keep the long-running receiver's current config in sync immediately.
    config.clear()
    config.update(fresh)

    return f"BTDEFAULT|{saved_mac}|{name}"[:220]


def handle(
    config: dict[str, Any],
    event: dict[str, Any],
    config_path: Path,
) -> str:
    action_text = str(event.get("message", "")).strip()
    parts = action_text.split(" ", 1)
    action = parts[0].strip().lower() if parts else ""
    argument = parts[1].strip() if len(parts) > 1 else ""

    if action == "admin_browser_audio_list":
        return _browser_audio_list(config_path)

    if action == "admin_browser_audio_get":
        return _browser_audio_get(config_path, argument or "soundalerts")

    if action in {"bt_status", "bt_list", "bt_scan"}:
        helper_action = action.removeprefix("bt_")
        return _run_helper(helper_action, timeout=20 if action == "bt_scan" else 10)

    if action in {"bt_pair", "bt_connect", "bt_disconnect", "bt_remove", "bt_default"}:
        mac = _validate_mac(argument)
        if action == "bt_default":
            return _set_default(config, config_path, mac)
        helper_action = action.removeprefix("bt_")
        return _run_helper(helper_action, mac, timeout=35 if action == "bt_pair" else 15)

    raise ValueError(f"Unsupported BELABOX admin action: {action or '<empty>'}")
