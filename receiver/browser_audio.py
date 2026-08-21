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
from dataclasses import dataclass
from pathlib import Path
from typing import Any

LOG = logging.getLogger("cometen-irl-browser-audio")
STOP_REQUESTED = False
SOURCE_NAME_RE = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")


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


def normalize_source_name(value: str) -> str:
    name = (value or "").strip().lower()
    if not SOURCE_NAME_RE.fullmatch(name):
        raise ValueError(
            "Browser Audio source name must match "
            "[a-z0-9][a-z0-9_-]{0,31}"
        )
    return name


def configured_sources(config: dict[str, Any]) -> list[dict[str, Any]]:
    raw = config.get("browser_audio_sources")
    result: list[dict[str, Any]] = []
    seen: set[str] = set()

    if isinstance(raw, list):
        for item in raw:
            if not isinstance(item, dict):
                continue
            try:
                name = normalize_source_name(str(item.get("name", "")))
            except ValueError:
                LOG.warning("Browser Audio: ignored source with invalid name")
                continue
            if name in seen:
                LOG.warning("Browser Audio: ignored duplicate source '%s'", name)
                continue
            url = str(item.get("url", "")).strip()
            if not url.startswith(("https://", "http://")):
                LOG.warning("Browser Audio: ignored source '%s' with invalid URL", name)
                continue
            result.append(
                {
                    "name": name,
                    "url": url,
                    "enabled": bool(item.get("enabled", True)),
                    "generation": int(item.get("generation", 0) or 0),
                }
            )
            seen.add(name)
        return result

    legacy_url = str(config.get("browser_audio_url", "")).strip()
    if legacy_url.startswith(("https://", "http://")):
        return [
            {
                "name": "soundalerts",
                "url": legacy_url,
                "enabled": True,
                "generation": 0,
            }
        ]
    return []


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
        "No Chromium/Chrome browser found. Run install-browser-runtime.sh "
        "or configure browser_audio_browser explicitly."
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


def profile_directory(config: dict[str, Any], source_name: str) -> Path:
    base = expand_path(
        str(
            config.get(
                "browser_audio_profile_directory",
                "~/.cache/cometen-irl-browser-audio/chromium-profile",
            )
        )
    )
    return (base / source_name).resolve()


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
        "--disable-dev-shm-usage",
        "--autoplay-policy=no-user-gesture-required",
        f"--user-data-dir={profile_dir}",
        f"--window-size={width},{height}",
    ]

    if os.geteuid() == 0:
        browser_args.append("--no-sandbox")

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
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        return
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=3)


@dataclass
class SourceRuntime:
    name: str
    fingerprint: str
    process: subprocess.Popen[bytes]


def source_fingerprint(config: dict[str, Any], source: dict[str, Any]) -> str:
    material = {
        "name": source["name"],
        "url": source["url"],
        "enabled": bool(source["enabled"]),
        "generation": int(source.get("generation", 0)),
        "browser": str(config.get("browser_audio_browser", "auto")),
        "profile": str(config.get("browser_audio_profile_directory", "")),
        "width": int(config.get("browser_audio_width", 1280)),
        "height": int(config.get("browser_audio_height", 720)),
        "disable_gpu": bool(config.get("browser_audio_disable_gpu", True)),
        "extra_args": config.get("browser_audio_extra_args", []),
    }
    return json.dumps(material, sort_keys=True, ensure_ascii=False)


def start_source(
    config: dict[str, Any],
    source: dict[str, Any],
    sink_id: str,
) -> SourceRuntime:
    name = str(source["name"])
    browser = resolve_browser(config)
    xvfb_run = resolve_xvfb_run()
    profile_dir = profile_directory(config, name)
    profile_dir.mkdir(parents=True, exist_ok=True)
    try:
        profile_dir.chmod(0o700)
    except OSError:
        pass

    set_default_sink(sink_id)
    command = build_browser_command(
        config=config,
        browser=browser,
        xvfb_run=xvfb_run,
        url=str(source["url"]),
        profile_dir=profile_dir,
    )
    safe_command = [
        "--app=<browser-source-url>" if str(part).startswith("--app=") else part
        for part in command
    ]
    LOG.info(
        "Browser Audio [%s]: starting %s",
        name,
        " ".join(shlex.quote(x) for x in safe_command),
    )

    env = os.environ.copy()
    env["PULSE_PROP"] = f"application.name=Cometen IRL Browser Audio {name}"
    process = subprocess.Popen(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=None,
        env=env,
        start_new_session=True,
    )
    return SourceRuntime(
        name=name,
        fingerprint=source_fingerprint(config, source),
        process=process,
    )


def stop_runtime(runtime: SourceRuntime, reason: str) -> None:
    LOG.info("Browser Audio [%s]: stopping (%s)", runtime.name, reason)
    terminate_process(runtime.process)


def desired_map(config: dict[str, Any]) -> dict[str, dict[str, Any]]:
    if not bool(config.get("browser_audio_enabled", False)):
        return {}
    return {
        str(source["name"]): source
        for source in configured_sources(config)
        if bool(source.get("enabled", True))
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Cometen IRL Browser Audio")
    parser.add_argument(
        "--config",
        default=str(Path(__file__).with_name("config.json")),
        help="Path to receiver config.json",
    )
    args = parser.parse_args()

    config_path = Path(args.config).resolve()
    runtimes: dict[str, SourceRuntime] = {}
    restart_after: dict[str, float] = {}
    current_sink: str | None = None
    sink_missing_logged = False
    last_state_signature = ""
    check_seconds = 2.0

    LOG.info("Cometen IRL Browser Audio supervisor starting")

    try:
        while not STOP_REQUESTED:
            try:
                config = load_config(config_path)
                desired = desired_map(config)
                sink_match = (
                    str(
                        config.get(
                            "browser_audio_sink_match",
                            config.get("remote_audio_sink_match", "WPS200"),
                        )
                    ).strip()
                    or "WPS200"
                )
                check_seconds = max(
                    2.0, float(config.get("browser_audio_sink_check_seconds", 5))
                )
            except Exception as error:
                LOG.exception("Browser Audio: could not load runtime config: %s", error)
                time.sleep(5.0)
                continue

            state_signature = json.dumps(
                {
                    "master": bool(config.get("browser_audio_enabled", False)),
                    "sources": [
                        {
                            "name": s["name"],
                            "enabled": s["enabled"],
                            "generation": s.get("generation", 0),
                        }
                        for s in configured_sources(config)
                    ],
                    "sink": sink_match,
                },
                sort_keys=True,
            )
            if state_signature != last_state_signature:
                LOG.info(
                    "Browser Audio: config changed; master=%s enabled_sources=%s sink=%s",
                    "on" if bool(config.get("browser_audio_enabled", False)) else "off",
                    ",".join(desired.keys()) or "<none>",
                    sink_match,
                )
                last_state_signature = state_signature

            for name, runtime in list(runtimes.items()):
                source = desired.get(name)
                if source is None:
                    stop_runtime(runtime, "disabled/removed")
                    runtimes.pop(name, None)
                    restart_after.pop(name, None)
                    continue
                wanted_fingerprint = source_fingerprint(config, source)
                if wanted_fingerprint != runtime.fingerprint:
                    stop_runtime(runtime, "configuration changed")
                    runtimes.pop(name, None)
                    restart_after[name] = 0.0

            if not desired:
                current_sink = None
                sink_missing_logged = False
                time.sleep(check_seconds)
                continue

            try:
                sink_id = resolve_audio_sink(sink_match)
            except Exception as error:
                LOG.warning("Browser Audio: sink check failed: %s", error)
                sink_id = None

            if not sink_id:
                if not sink_missing_logged:
                    LOG.warning(
                        "Browser Audio: audio sink '%s' is unavailable; waiting",
                        sink_match,
                    )
                    sink_missing_logged = True
                time.sleep(check_seconds)
                continue

            sink_missing_logged = False
            if current_sink is None:
                current_sink = sink_id
                set_default_sink(sink_id)
                LOG.info(
                    "Browser Audio: resolved %s as PipeWire node %s and set it as default",
                    sink_match,
                    sink_id,
                )
            elif sink_id != current_sink:
                LOG.info(
                    "Browser Audio: %s reappeared as node %s (was %s); "
                    "restarting sources to rebind audio",
                    sink_match,
                    sink_id,
                    current_sink,
                )
                current_sink = sink_id
                set_default_sink(sink_id)
                for name, runtime in list(runtimes.items()):
                    stop_runtime(runtime, "audio sink changed")
                    runtimes.pop(name, None)
                    restart_after[name] = 0.0
            else:
                try:
                    set_default_sink(sink_id)
                except Exception as error:
                    LOG.warning("Browser Audio: could not refresh default sink: %s", error)

            for name, runtime in list(runtimes.items()):
                exit_code = runtime.process.poll()
                if exit_code is None:
                    continue
                LOG.warning(
                    "Browser Audio [%s]: browser stopped with exit code %s",
                    name,
                    exit_code,
                )
                runtimes.pop(name, None)
                restart_after[name] = time.monotonic() + 5.0

            now = time.monotonic()
            for name, source in desired.items():
                if name in runtimes:
                    continue
                if now < restart_after.get(name, 0.0):
                    continue
                try:
                    runtimes[name] = start_source(config, source, current_sink)
                    restart_after.pop(name, None)
                except Exception as error:
                    LOG.exception("Browser Audio [%s] failed to start: %s", name, error)
                    restart_after[name] = time.monotonic() + 5.0

            time.sleep(check_seconds)
    finally:
        for runtime in list(runtimes.values()):
            stop_runtime(runtime, "service stopping")
        LOG.info("Cometen IRL Browser Audio supervisor stopped")

    return 0


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )
    signal.signal(signal.SIGTERM, request_stop)
    signal.signal(signal.SIGINT, request_stop)
    sys.exit(main())
