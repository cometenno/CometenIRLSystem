#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path
from typing import Any

STATUS_PATH = Path("/run/cometen-irl-video-status.json")
DEFAULT_PROCESS = "belacoder"
DEFAULT_DEVICES = ("/dev/usb_capture", "/dev/hdmirx", "/dev/hdmi_capture")


def load_config(path: Path) -> dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8") as handle:
            value = json.load(handle)
        return value if isinstance(value, dict) else {}
    except Exception:
        return {}


def process_name(pid: int) -> str:
    try:
        return Path(f"/proc/{pid}/comm").read_text(encoding="utf-8").strip()
    except Exception:
        return ""


def process_ppid(pid: int) -> int | None:
    try:
        fields = Path(f"/proc/{pid}/stat").read_text(encoding="utf-8").split()
        return int(fields[3])
    except Exception:
        return None


def all_pids() -> list[int]:
    try:
        return [int(entry.name) for entry in Path("/proc").iterdir() if entry.name.isdigit()]
    except Exception:
        return []


def process_tree_roots(name: str) -> list[int]:
    return [pid for pid in all_pids() if process_name(pid) == name]


def process_tree(roots: list[int]) -> list[int]:
    pids = all_pids()
    parent_map: dict[int, list[int]] = {}
    for pid in pids:
        ppid = process_ppid(pid)
        if ppid is not None:
            parent_map.setdefault(ppid, []).append(pid)

    result: list[int] = []
    seen: set[int] = set()
    queue = list(roots)
    while queue:
        pid = queue.pop(0)
        if pid in seen:
            continue
        seen.add(pid)
        result.append(pid)
        queue.extend(parent_map.get(pid, []))
    return result


def resolved_devices(config: dict[str, Any]) -> dict[str, str]:
    settings = config.get("status_leds", {})
    if not isinstance(settings, dict):
        settings = {}

    candidates: list[str] = []
    configured = str(settings.get("camera_device", "")).strip()
    if configured:
        candidates.append(configured)
    candidates.extend(DEFAULT_DEVICES)

    devices: dict[str, str] = {}
    seen: set[str] = set()
    for candidate in candidates:
        if candidate in seen:
            continue
        seen.add(candidate)
        try:
            path = Path(candidate)
            if path.exists():
                devices[candidate] = os.path.realpath(candidate)
        except Exception:
            continue
    return devices


def find_open_video_device(
    pids: list[int],
    devices: dict[str, str],
) -> tuple[bool, str, int | None]:
    wanted = set(devices.values())
    for pid in pids:
        fd_dir = Path(f"/proc/{pid}/fd")
        try:
            fds = list(fd_dir.iterdir())
        except Exception:
            continue
        for fd in fds:
            try:
                target = os.path.realpath(str(fd))
            except Exception:
                continue
            if target in wanted:
                return True, target, pid
    return False, "", None


def first_available_device(devices: dict[str, str]) -> str:
    for resolved in devices.values():
        return resolved
    return ""


def write_status(payload: dict[str, Any]) -> None:
    tmp = STATUS_PATH.with_suffix(".tmp")
    tmp.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")
    os.chmod(tmp, 0o644)
    os.replace(tmp, STATUS_PATH)


def run(config_path: Path) -> None:
    config = load_config(config_path)
    settings = config.get("status_leds", {})
    if not isinstance(settings, dict):
        settings = {}

    process = str(settings.get("live_process", DEFAULT_PROCESS)).strip() or DEFAULT_PROCESS
    interval = max(0.25, min(5.0, float(settings.get("video_probe_seconds", 0.5))))

    while True:
        roots = process_tree_roots(process)
        tree = process_tree(roots) if roots else []
        devices = resolved_devices(config)

        source_present = bool(devices)
        source_device = first_available_device(devices)
        pipeline_active, pipeline_device, owner_pid = find_open_video_device(tree, devices)
        encoder_running = bool(roots)

        # Yellow LED semantics:
        # - Encoder stopped intentionally: video input may still be present, so keep yellow on.
        # - Encoder running: require the encoder pipeline to actually hold the video device open.
        video_active = pipeline_active if encoder_running else source_present

        write_status(
            {
                "version": 2,
                "updated_unix": time.time(),
                "encoder_running": encoder_running,
                "source_present": source_present,
                "pipeline_active": pipeline_active,
                "active": video_active,
                "device": pipeline_device or source_device,
                "pid": owner_pid,
                "process": process,
            }
        )
        time.sleep(interval)


def main() -> int:
    config_path = Path(sys.argv[1] if len(sys.argv) > 1 else "config.json").resolve()
    try:
        run(config_path)
    except KeyboardInterrupt:
        return 0
    except Exception as exception:
        try:
            write_status(
                {
                    "version": 2,
                    "updated_unix": time.time(),
                    "encoder_running": False,
                    "source_present": False,
                    "pipeline_active": False,
                    "active": False,
                    "device": "",
                    "pid": None,
                    "process": DEFAULT_PROCESS,
                    "error": str(exception),
                }
            )
        except Exception:
            pass
        raise
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
