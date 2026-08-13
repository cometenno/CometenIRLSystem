#!/usr/bin/env python3

from __future__ import annotations

import logging
import sys
from pathlib import Path

import receiver
from status_leds import StatusLedController


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    config_path = Path(sys.argv[1] if len(sys.argv) > 1 else "config.json").resolve()

    try:
        config = receiver.load_config(config_path)
    except Exception:
        logging.getLogger("cometen-irl-alerts").exception("Receiver could not load config")
        return 1

    leds = StatusLedController(config)
    leds.start()

    try:
        return receiver.main()
    finally:
        leds.stop()


if __name__ == "__main__":
    raise SystemExit(main())
