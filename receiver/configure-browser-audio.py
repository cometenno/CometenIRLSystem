#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
from pathlib import Path
from typing import Any

SOURCE_NAME_RE = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")


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


def normalize_name(value: str) -> str:
    name = (value or "").strip().lower()
    if not SOURCE_NAME_RE.fullmatch(name):
        raise ValueError(
            "Kildenavn må matche [a-z0-9][a-z0-9_-]{0,31}, "
            "for eksempel soundalerts eller blerp."
        )
    return name


def ensure_sources(config: dict[str, Any]) -> list[dict[str, Any]]:
    raw = config.get("browser_audio_sources")
    if isinstance(raw, list):
        sources = [item for item in raw if isinstance(item, dict)]
        config["browser_audio_sources"] = sources
        return sources

    sources: list[dict[str, Any]] = []
    legacy_url = str(config.get("browser_audio_url", "")).strip()
    if legacy_url.startswith(("https://", "http://")):
        sources.append(
            {
                "name": "soundalerts",
                "url": legacy_url,
                "enabled": True,
                "generation": 0,
            }
        )
    config["browser_audio_sources"] = sources
    return sources


def find_source(sources: list[dict[str, Any]], name: str) -> dict[str, Any] | None:
    for item in sources:
        if str(item.get("name", "")).strip().lower() == name:
            return item
    return None


def source_summary(sources: list[dict[str, Any]]) -> str:
    if not sources:
        return "<ingen kilder>"
    parts = []
    for item in sources:
        name = str(item.get("name", "?"))
        state = "on" if bool(item.get("enabled", True)) else "off"
        parts.append(f"{name}:{state}")
    return ", ".join(parts)


def set_defaults(config: dict[str, Any], sink: str) -> None:
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


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Konfigurer Cometen IRL Browser Audio med flere Browser Source-kilder."
    )
    parser.add_argument(
        "--config",
        default=str(Path(__file__).with_name("config.json")),
        help="Sti til receiver/config.json",
    )
    parser.add_argument("--name", default="soundalerts", help="Kildenavn.")
    parser.add_argument("--url", default="", help="Browser Source URL.")
    parser.add_argument("--sink", default="", help="PipeWire sink-match.")
    parser.add_argument("--remove", default="", help="Slett kilden med dette navnet.")
    parser.add_argument("--enable", default="", help="Aktiver kilden med dette navnet.")
    parser.add_argument("--disable-source", default="", help="Deaktiver kilden.")
    parser.add_argument("--restart-source", default="", help="Tving restart av kilden.")
    parser.add_argument("--list", action="store_true", help="Vis kilder uten URL-er.")
    parser.add_argument(
        "--disable",
        action="store_true",
        help="Slå av Browser Audio master uten å slette kildene.",
    )
    args = parser.parse_args()

    config_path = Path(args.config).resolve()
    config = load_config(config_path)
    sources = ensure_sources(config)

    if args.list:
        print("Browser Audio master:", "on" if bool(config.get("browser_audio_enabled", False)) else "off")
        print("Kilder:", source_summary(sources))
        return 0

    if args.disable:
        config["browser_audio_enabled"] = False
        save_config(config_path, config)
        print("IRL Browser Audio master er slått av. Kildene er beholdt.")
        return 0

    for option, new_state, verb in (
        (args.enable, True, "aktivert"),
        (args.disable_source, False, "deaktivert"),
    ):
        if option:
            name = normalize_name(option)
            source = find_source(sources, name)
            if source is None:
                raise ValueError(f"Fant ikke Browser Audio-kilden '{name}'.")
            source["enabled"] = new_state
            config["browser_audio_enabled"] = True
            save_config(config_path, config)
            print(f"Browser Audio-kilden '{name}' er {verb}.")
            return 0

    if args.restart_source:
        name = normalize_name(args.restart_source)
        source = find_source(sources, name)
        if source is None:
            raise ValueError(f"Fant ikke Browser Audio-kilden '{name}'.")
        source["generation"] = int(source.get("generation", 0) or 0) + 1
        save_config(config_path, config)
        print(f"Restart er bestilt for Browser Audio-kilden '{name}'.")
        return 0

    if args.remove:
        name = normalize_name(args.remove)
        before = len(sources)
        sources[:] = [
            item
            for item in sources
            if str(item.get("name", "")).strip().lower() != name
        ]
        if len(sources) == before:
            raise ValueError(f"Fant ikke Browser Audio-kilden '{name}'.")
        if name == "soundalerts":
            config["browser_audio_url"] = ""
        save_config(config_path, config)
        print(f"Browser Audio-kilden '{name}' er slettet.")
        return 0

    name = normalize_name(args.name)
    url = args.url.strip()
    if not url:
        print(f"Lim inn Browser Source URL for '{name}'.")
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

    source = find_source(sources, name)
    if source is None:
        if len(sources) >= 8:
            raise ValueError("Maks 8 Browser Audio-kilder er tillatt.")
        source = {"name": name, "url": url, "enabled": True, "generation": 0}
        sources.append(source)
    else:
        source["url"] = url
        source["enabled"] = True
        source["generation"] = int(source.get("generation", 0) or 0) + 1

    config["browser_audio_enabled"] = True
    if name == "soundalerts":
        config["browser_audio_url"] = url
    set_defaults(config, sink)
    save_config(config_path, config)

    print()
    print("IRL Browser Audio er konfigurert.")
    print(f"Kilde: {name}")
    print(f"Audio sink-match: {sink}")
    print("Browser Source URL er lagret lokalt og vises ikke her.")
    print("Kilder:", source_summary(sources))
    print()
    print("Neste steg:")
    print("  bash install-browser-audio.sh")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
