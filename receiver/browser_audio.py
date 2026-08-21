#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import logging
import os
import re
import shlex
import shutil
import signal
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

LOG = logging.getLogger("cometen-irl-browser-audio")

STOP_REQUESTED = False


def request_stop(signum: int, frame: object) -> None:
    del signum, frame
    global STOP_REQUESTED
    STOP_REQUESTED = True


def load_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise FileNotFoundError(f"Missing configuration file: {path}")

    with path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)

    if not isinstance(config, dict):
        raise ValueError("config.json must contain a JSON object")

    return config


def expand_path(value: str) -> Path:
    return Path(os.path.expandvars(os.path.expanduser(value))).resolve()


def resolve_browser(config: dict[str, Any]) -> str:
    configured = str(config.get("browser_audio_browser", "auto")).strip()

    if configured and configured.lower() != "auto":
        expanded = os.path.expandvars(os.path.expanduser(configured))
        if os.path.sep in expanded:
            path = Path(expanded)
            if path.is_file() and os.access(path, os.X_OK):
                return str(path)
        executable = shutil.which(expanded)
        if executable:
            return executable
        raise RuntimeError(f"Configured browser was not found: {configured}")

    for candidate in (
        "chromium",
        "chromium-browser",
        "google-chrome-stable",
        "google-chrome",
    ):
        executable = shutil.which(candidate)
        if executable:
            return executable

    raise RuntimeError(
        "No Chromium/Chrome browser found. Install 'chromium' or configure "
        "browser_audio_browser explicitly."
    )


def resolve_xvfb_run() -> str:
    executable = shutil.which("xvfb-run")
    if not executable:
        raise RuntimeError(
            "xvfb-run was not found. Install the 'xvfb' package before "
            "starting IRL Browser Audio."
        )
    return executable


def resolve_audio_sink(match_text: str) -> str | None:
    executable = shutil.which("pw-cli")
    if executable is None:
        raise RuntimeError("pw-cli was not found; PipeWire discovery is unavailable")

    completed = subprocess.run(
        [executable, "ls", "Node"],
        check=True,
        timeout=10,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )

    blocks = re.split(
        r"(?=^\s*id\s+\d+,\s+type\s+PipeWire:Interface:Node/3\s*$)",
        completed.stdout,
        flags=re.MULTILINE,
    )

    for block in blocks:
        if 'media.class = "Audio/Sink"' not in block:
            continue
        if match_text.lower() not in block.lower():
            continue
        match = re.search(r"^\s*id\s+(\d+),", block, flags=re.MULTILINE)
        if match:
            return match.group(1)

    return None


def set_default_sink(sink_id: str) -> None:
    executable = shutil.which("wpctl")
    if executable is None:
        raise RuntimeError("wpctl was not found; cannot set PipeWire default sink")

    subprocess.run(
        [executable, "set-default", sink_id],
        check=True,
        timeout=10,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
    )


def wait_for_sink(match_text: str, timeout_seconds: float) -> str:
    started = time.monotonic()
    next_log = 0.0

    while not STOP_REQUESTED:
        try:
            sink_id = resolve_audio_sink(match_text)
        except subprocess.CalledProcessError as error:
            details = (error.stderr or "").strip()
            LOG.warning("PipeWire sink lookup failed: %s", details or error)
            sink_id = None

        if sink_id:
            set_default_sink(sink_id)
            LOG.info(
                "Browser Audio: resolved %s as PipeWire node %s and set it as default",
                match_text,
                sink_id,
            )
            return sink_id

        elapsed = time.monotonic() - started
        if elapsed >= timeout_seconds:
            raise RuntimeError(
                f"Audio sink matching '{match_text}' was not found within "
                f"{timeout_seconds:.0f} seconds"
            )

        if elapsed >= next_log:
            LOG.info(
                "Browser Audio: waiting for audio sink '%s' (%.0fs/%.0fs)",
                match_text,
                elapsed,
                timeout_seconds,
            )
            next_log = elapsed + 10.0

        time.sleep(1.0)

    raise RuntimeError("Stop requested while waiting for audio sink")


def build_browser_command(
    config: dict[str, Any],
    browser: str,
    xvfb_run: str,
    url: str,
    profile_dir: Path,
) -> list[str]:
    width = max(320, int(config.get("browser_audio_width", 1280)))
    height = max(240, int(config.get("browser_audio_height", 720)))

    browser_args = [
        browser,
        "--no-first-run",
        "--no-default-browser-check",
        "--disable-sync",
        "--disable-background-timer-throttling",
        "--disable-backgrounding-occluded-windows",
        "--disable-renderer-backgrounding",
        "--autoplay-policy=no-user-gesture-required",
        f"--user-data-dir={profile_dir}",
        f"--window-size={width},{height}",
    ]

    if bool(config.get("browser_audio_disable_gpu", True)):
        browser_args.append("--disable-gpu")

    extra = config.get("browser_audio_extra_args", [])
    if isinstance(extra, str) and extra.strip():
        browser_args.extend(shlex.split(extra))
    elif isinstance(extra, list):
        browser_args.extend(str(item) for item in extra if str(item).strip())

    browser_args.append(f"--app={url}")

    server_args = f"-screen 0 {width}x{height}x24 -nolisten tcp -ac"

    return [
        xvfb_run,
        "-a",
        "--server-args",
        server_args,
        *browser_args,
    ]


def terminate_process(process: subprocess.Popen[bytes], timeout: float = 8.0) -> None:
    if process.poll() is not None:
        return

    process.terminate()
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=3)


def run_browser(
    config: dict[str, Any],
    url: str,
    sink_match: str,
    initial_sink: str,
) -> int:
    browser = resolve_browser(config)
    xvfb_run = resolve_xvfb_run()

    profile_dir = expand_path(
        str(
            config.get(
                "browser_audio_profile_directory",
                "~/.cache/cometen-irl-browser-audio/chromium-profile",
            )
        )
    )
    profile_dir.mkdir(parents=True, exist_ok=True)
    try:
        profile_dir.chmod(0o700)
    except OSError:
        pass

    command = build_browser_command(
        config=config,
        browser=browser,
        xvfb_run=xvfb_run,
        url=url,
        profile_dir=profile_dir,
    )

    env = os.environ.copy()
    env.setdefault("PULSE_PROP", "application.name=Cometen IRL Browser Audio")

    safe_command = [
        "--app=<browser-source-url>" if str(part).startswith("--app=") else part
        for part in command
    ]
    LOG.info("Browser Audio: starting %s", " ".join(shlex.quote(x) for x in safe_command))

    process = subprocess.Popen(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=None,
        env=env,
    )

    check_seconds = max(
        2.0, float(config.get("browser_audio_sink_check_seconds", 5))
    )
    current_sink = initial_sink
    sink_missing_logged = False

    while not STOP_REQUESTED:
        exit_code = process.poll()
        if exit_code is not None:
            LOG.warning("Browser Audio: browser stopped with exit code %s", exit_code)
            return int(exit_code)

        time.sleep(check_seconds)

        try:
            sink_id = resolve_audio_sink(sink_match)
        except Exception as error:
            LOG.warning("Browser Audio: sink check failed: %s", error)
            continue

        if not sink_id:
            if not sink_missing_logged:
                LOG.warning(
                    "Browser Audio: audio sink '%s' disappeared; waiting for reconnect",
                    sink_match,
                )
                sink_missing_logged = True
            continue

        sink_missing_logged = False

        if sink_id != current_sink:
            LOG.info(
                "Browser Audio: %s reappeared as node %s (was %s); restarting browser "
                "to rebind audio",
                sink_match,
                sink_id,
                current_sink,
            )
            set_default_sink(sink_id)
            terminate_process(process)
            return 75

        try:
            set_default_sink(sink_id)
        except Exception as error:
            LOG.warning("Browser Audio: could not refresh default sink: %s", error)

    LOG.info("Browser Audio: stop requested")
    terminate_process(process)
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Cometen IRL Browser Audio")
    parser.add_argument(
        "--config",
        default=str(Path(__file__).with_name("config.json")),
        help="Path to receiver config.json",
    )
    args = parser.parse_args()

    config_path = Path(args.config).resolve()
    config = load_config(config_path)

    if not bool(config.get("browser_audio_enabled", False)):
        LOG.error(
            "Browser Audio is disabled. Set browser_audio_enabled=true in %s",
            config_path,
        )
        return 2

    url = str(config.get("browser_audio_url", "")).strip()
    if not url.startswith(("https://", "http://")):
        LOG.error(
            "browser_audio_url is missing or invalid in %s. "
            "Use the Sound Alerts Browser Source URL.",
            config_path,
        )
        return 2

    sink_match = (
        str(
            config.get(
                "browser_audio_sink_match",
                config.get("remote_audio_sink_match", "WPS200"),
            )
        ).strip()
        or "WPS200"
    )
    wait_seconds = max(
        10.0, float(config.get("browser_audio_sink_wait_seconds", 120))
    )

    LOG.info(
        "Cometen IRL Browser Audio starting: sink=%s source=%s",
        sink_match,
        "<configured browser source>",
    )

    while not STOP_REQUESTED:
        try:
            sink_id = wait_for_sink(sink_match, wait_seconds)
            exit_code = run_browser(config, url, sink_match, sink_id)
        except Exception as error:
            if STOP_REQUESTED:
                break
            LOG.exception("Browser Audio failed: %s", error)
            time.sleep(5.0)
            continue

        if STOP_REQUESTED:
            break

        if exit_code == 75:
            time.sleep(1.0)
        else:
            LOG.warning(
                "Browser Audio: restarting browser in 5 seconds after exit code %s",
                exit_code,
            )
            time.sleep(5.0)

    LOG.info("Cometen IRL Browser Audio stopped")
    return 0


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )
    signal.signal(signal.SIGTERM, request_stop)
    signal.signal(signal.SIGINT, request_stop)
    sys.exit(main())
