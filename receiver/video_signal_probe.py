#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any

STATUS_PATH = Path("/run/cometen-irl-video-status.json")
DEFAULT_PROCESS = "belacoder"
DEFAULT_DEVICES = ("/dev/usb_capture", "/dev/hdmirx", "/dev/hdmi_capture")
HDMI_RX_DEVICE = "/dev/hdmirx"
PIPELINE_PATH = Path("/tmp/belacoder_pipeline")


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


def hdmi_rx_signal_present(device: str = HDMI_RX_DEVICE) -> tuple[bool, str]:
    """Return real HDMI-RX signal state using V4L2 DV timings.

    ROCK 5B+ keeps /dev/hdmirx present even with no cable/feed. Therefore
    device existence alone must not turn the yellow VIDEO LED on.
    """
    if not Path(device).exists():
        return False, "device-missing"

    v4l2_ctl = shutil.which("v4l2-ctl")
    if v4l2_ctl is None:
        return False, "v4l2-ctl-missing"

    try:
        completed = subprocess.run(
            [v4l2_ctl, "-d", device, "--query-dv-timings"],
            check=False,
            timeout=3,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )
    except Exception as exception:
        return False, f"query-error:{exception}"

    output = completed.stdout or ""
    if completed.returncode != 0:
        return False, "no-lock"

    # Require non-zero active timings as an extra guard against a false success.
    width = 0
    height = 0
    for line in output.splitlines():
        stripped = line.strip().lower()
        if stripped.startswith("active width:"):
            try:
                width = int(stripped.split(":", 1)[1].strip())
            except Exception:
                width = 0
        elif stripped.startswith("active height:"):
            try:
                height = int(stripped.split(":", 1)[1].strip())
            except Exception:
                height = 0

    if width > 0 and height > 0:
        return True, f"locked:{width}x{height}"

    return False, "zero-timings"


def local_source_state(devices: dict[str, str]) -> tuple[bool, str, str]:
    """Return (source_present, resolved_device, detail) for local video inputs."""
    for configured, resolved in devices.items():
        if configured == HDMI_RX_DEVICE:
            present, detail = hdmi_rx_signal_present(configured)
            if present:
                return True, resolved, detail
            continue

        return True, resolved, "device-present"

    # If HDMI-RX exists but has no lock, expose the device path for diagnostics
    # while correctly reporting source_present=false.
    if HDMI_RX_DEVICE in devices:
        present, detail = hdmi_rx_signal_present(HDMI_RX_DEVICE)
        return present, devices[HDMI_RX_DEVICE], detail

    return False, "", "no-local-source"


def rtmp_source_state(config: dict[str, Any]) -> tuple[bool | None, str, str]:
    """Return RTMP publisher state from the local nginx-rtmp stat endpoint.

    The Action camera/Mimo path publishes to /publish/live. nginx-rtmp only
    lists the stream while a publisher is actually connected, so this gives us
    a real source-presence signal without relying on a local /dev/video node.
    """
    settings = config.get("status_leds", {})
    if not isinstance(settings, dict):
        settings = {}

    status_url = str(settings.get("camera_status_url", "http://127.0.0.1/stat")).strip()
    app_name = str(settings.get("camera_app", "publish")).strip() or "publish"
    stream_name = str(settings.get("camera_stream", "live")).strip() or "live"
    descriptor = f"rtmp:{app_name}/{stream_name}"

    if not status_url:
        return None, descriptor, "rtmp-stat-disabled"

    try:
        request = urllib.request.Request(
            status_url,
            headers={"User-Agent": "CometenIRLAlerts/video-probe"},
        )
        with urllib.request.urlopen(request, timeout=1.5) as response:
            body = response.read()
        root = ET.fromstring(body)
    except Exception:
        return None, descriptor, "rtmp-stat-unavailable"

    wanted_app = app_name.lower()
    wanted_stream = stream_name.lower()
    for application in root.findall(".//application"):
        current_app = (application.findtext("name") or "").strip().lower()
        if current_app != wanted_app:
            continue
        for stream in application.findall(".//stream"):
            current_stream = (stream.findtext("name") or "").strip().lower()
            if current_stream == wanted_stream:
                return True, descriptor, f"rtmp-publisher:{app_name}/{stream_name}"

    return False, descriptor, f"rtmp-missing:{app_name}/{stream_name}"


def pipeline_source_kind() -> str:
    """Identify the currently generated BELABOX input pipeline when possible."""
    try:
        text = PIPELINE_PATH.read_text(encoding="utf-8", errors="ignore").lower()
    except Exception:
        return "unknown"

    if "rtmpsrc" in text:
        return "rtmp"
    if "v4l2src" in text:
        return "local"
    return "unknown"


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
        encoder_running = bool(roots)
        mode = pipeline_source_kind() if encoder_running else "stopped"

        devices = resolved_devices(config)
        local_present, local_device, local_detail = local_source_state(devices)
        local_pipeline_active, pipeline_device, owner_pid = find_open_video_device(tree, devices)
        rtmp_present, rtmp_device, rtmp_detail = rtmp_source_state(config)

        source_present = local_present
        source_detail = local_detail
        source_device = local_device
        pipeline_active = local_pipeline_active

        if encoder_running and mode == "rtmp":
            # RTMP pipelines do not hold a /dev/video node open. Validate the
            # publisher instead and combine it with the running encoder state.
            source_present = rtmp_present is True
            source_detail = rtmp_detail
            source_device = rtmp_device
            pipeline_active = rtmp_present is True
            pipeline_device = rtmp_device if pipeline_active else ""
            owner_pid = roots[0] if pipeline_active else None
        elif encoder_running and mode == "local":
            # Keep the proven V4L2/HDMI logic for USB and HDMI capture inputs.
            source_present = local_present
            source_detail = local_detail
            source_device = local_device
            pipeline_active = local_pipeline_active
        elif encoder_running:
            # Unknown pipeline format: prefer a confirmed local device handle,
            # otherwise accept a confirmed RTMP publisher as the active source.
            if local_pipeline_active:
                source_present = local_present
                source_detail = local_detail
                source_device = local_device
                pipeline_active = True
            elif rtmp_present is True:
                source_present = True
                source_detail = rtmp_detail
                source_device = rtmp_device
                pipeline_active = True
                pipeline_device = rtmp_device
                owner_pid = roots[0]
            else:
                pipeline_active = False
        else:
            # Encoder stopped: yellow LED means any real camera source is ready.
            if rtmp_present is True:
                source_present = True
                source_detail = rtmp_detail
                source_device = rtmp_device
            elif local_present:
                source_present = True
                source_detail = local_detail
                source_device = local_device
            elif rtmp_present is False:
                source_present = False
                source_detail = rtmp_detail
                source_device = rtmp_device
            else:
                source_present = local_present
                source_detail = local_detail
                source_device = local_device
            pipeline_active = False
            pipeline_device = ""
            owner_pid = None

        # Yellow LED semantics:
        # - Encoder stopped: show whether a REAL source is available.
        # - Encoder running: require the selected BELABOX input pipeline to be active.
        video_active = pipeline_active if encoder_running else source_present

        write_status(
            {
                "version": 4,
                "updated_unix": time.time(),
                "encoder_running": encoder_running,
                "source_present": source_present,
                "source_detail": source_detail,
                "pipeline_source": mode,
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
                    "version": 4,
                    "updated_unix": time.time(),
                    "encoder_running": False,
                    "source_present": False,
                    "source_detail": "probe-error",
                    "pipeline_source": "unknown",
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
