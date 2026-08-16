#!/usr/bin/env python3

from __future__ import annotations

import json
import logging
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

LOG = logging.getLogger("cometen-irl-heartbeat")

DEFAULT_HEARTBEAT_INTERVAL_SECONDS = 30.0
DEFAULT_RATE_LIMIT_BACKOFF_SECONDS = 60.0


class HeartbeatRateLimited(RuntimeError):
    def __init__(self, message: str, retry_after: float | None = None) -> None:
        super().__init__(message)
        self.retry_after = retry_after


def load_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise FileNotFoundError(f"Missing configuration file: {path}")

    with path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)

    for key in ("relay_base_url", "receiver_token"):
        if not str(config.get(key, "")).strip():
            raise ValueError(f"Missing required configuration value: {key}")

    return config


def parse_retry_after(value: str | None) -> float | None:
    if not value:
        return None

    try:
        seconds = float(value.strip())
    except (TypeError, ValueError):
        return None

    return max(1.0, min(300.0, seconds))


def post_heartbeat(base_url: str, token: str, receiver_id: str, timeout: float) -> None:
    payload = json.dumps(
        {
            "receiver_id": receiver_id,
            "version": "0.5.1",
        },
        ensure_ascii=False,
    ).encode("utf-8")

    request = urllib.request.Request(
        f"{base_url}/heartbeat.php",
        data=payload,
        method="POST",
        headers={
            "Accept": "application/json",
            "Content-Type": "application/json",
            "X-Cometen-Token": token,
            "User-Agent": "CometenIRLHeartbeat/0.5.1",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            body = response.read().decode("utf-8", errors="replace")
            if response.status < 200 or response.status >= 300:
                raise RuntimeError(f"Relay returned HTTP {response.status}: {body}")
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")

        if error.code == 429:
            retry_after = parse_retry_after(error.headers.get("Retry-After"))
            raise HeartbeatRateLimited(
                f"Relay returned HTTP 429: {body}",
                retry_after=retry_after,
            ) from error

        raise RuntimeError(f"Relay returned HTTP {error.code}: {body}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"Relay connection failed: {error.reason}") from error


def run(config_path: Path) -> None:
    config = load_config(config_path)
    base_url = str(config["relay_base_url"]).rstrip("/")
    token = str(config["receiver_token"]).strip()
    receiver_id = str(config.get("heartbeat_receiver_id", "belabox")).strip() or "belabox"
    interval = max(
        5.0,
        min(
            300.0,
            float(config.get("heartbeat_interval_seconds", DEFAULT_HEARTBEAT_INTERVAL_SECONDS)),
        ),
    )
    timeout = max(1.0, min(10.0, float(config.get("heartbeat_timeout_seconds", 5.0))))

    LOG.info(
        "Cometen IRL heartbeat started: receiver_id=%s interval=%.1fs relay=%s",
        receiver_id,
        interval,
        base_url,
    )

    failure_logged = False

    while True:
        started = time.monotonic()
        next_delay = interval

        try:
            post_heartbeat(base_url, token, receiver_id, timeout)
            if failure_logged:
                LOG.info("Heartbeat connection restored")
                failure_logged = False
        except KeyboardInterrupt:
            LOG.info("Heartbeat stopped")
            return
        except HeartbeatRateLimited as exception:
            next_delay = max(
                interval,
                exception.retry_after or DEFAULT_RATE_LIMIT_BACKOFF_SECONDS,
            )

            if not failure_logged:
                LOG.warning(
                    "Heartbeat rate limited: %s; backing off for %.1fs",
                    exception,
                    next_delay,
                )
                failure_logged = True
        except Exception as exception:
            if not failure_logged:
                LOG.warning("Heartbeat send failed: %s", exception)
                failure_logged = True

        elapsed = time.monotonic() - started
        time.sleep(max(0.1, next_delay - elapsed))


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    config_path = Path(sys.argv[1] if len(sys.argv) > 1 else "config.json").resolve()

    try:
        run(config_path)
    except Exception:
        LOG.exception("Heartbeat service could not start")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
