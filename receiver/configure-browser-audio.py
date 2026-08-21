#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any


def load_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise FileNotFoundError(
            f"Mangler {path}. Kopier config.example.json til config.json først."
        )

    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    if not isinstance(data, dict):
        raise ValueError("config.json må inneholde et JSON-objekt.")

    return data


def save_config(path: Path, data: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    os.replace(temporary, path)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Konfigurer Cometen IRL Browser Audio uten å erstatte resten av config.json."
    )
    parser.add_argument(
        "--config",
        default=str(Path(__file__).with_name("config.json")),
        help="Sti til receiver/config.json",
    )
    parser.add_argument(
        "--url",
        default="",
        help="Browser Source URL. Utelat for å lime inn URL interaktivt.",
    )
    parser.add_argument(
        "--sink",
        default="",
        help="Navn som skal matches mot PipeWire Audio/Sink. Standard: remote_audio_sink_match/WPS200.",
    )
    parser.add_argument(
        "--disable",
        action="store_true",
        help="Deaktiver Browser Audio uten å slette URL-en.",
    )
    args = parser.parse_args()

    config_path = Path(args.config).resolve()
    config = load_config(config_path)

    if args.disable:
        config["browser_audio_enabled"] = False
        save_config(config_path, config)
        print("IRL Browser Audio er deaktivert i config.json.")
        return 0

    url = args.url.strip()
    if not url:
        print("Lim inn Browser Source URL fra Sound Alerts.")
        print("URL-en lagres kun lokalt i receiver/config.json (gitignored).")
        url = input("Browser Source URL: ").strip()

    if not url.startswith(("https://", "http://")):
        raise ValueError("URL-en må starte med https:// eller http://")

    sink = args.sink.strip()
    if not sink:
        sink = (
            str(
                config.get(
                    "browser_audio_sink_match",
                    config.get("remote_audio_sink_match", "WPS200"),
                )
            ).strip()
            or "WPS200"
        )

    config["browser_audio_enabled"] = True
    config["browser_audio_url"] = url
    config["browser_audio_sink_match"] = sink

    config.setdefault("browser_audio_browser", "auto")
    config.setdefault(
        "browser_audio_profile_directory",
        "~/.cache/cometen-irl-browser-audio/chromium-profile",
    )
    config.setdefault("browser_audio_sink_wait_seconds", 120)
    config.setdefault("browser_audio_sink_check_seconds", 5)
    config.setdefault("browser_audio_width", 1280)
    config.setdefault("browser_audio_height", 720)
    config.setdefault("browser_audio_disable_gpu", True)
    config.setdefault("browser_audio_extra_args", [])

    save_config(config_path, config)

    print()
    print("IRL Browser Audio er konfigurert.")
    print(f"Audio sink-match: {sink}")
    print("Browser Source URL er lagret lokalt og vises ikke her.")
    print()
    print("Neste steg:")
    print("  bash install-browser-audio.sh")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
