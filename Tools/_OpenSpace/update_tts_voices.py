#!/usr/bin/env python3
"""
Fetch TTS voices from the API and regenerate:
  Resources/Prototypes/_OpenSpace/tts-voices.yml
  Resources/Locale/ru-RU/_OpenSpace/tts/tts-voices.ftl

Usage:
  python Tools/update_tts_voices.py --token <TOKEN>
  TTS_TOKEN=<TOKEN> python Tools/update_tts_voices.py
"""

import argparse
import os
import sys
from pathlib import Path

try:
    import requests
except ImportError:
    print("requests not found — run: pip install requests", file=sys.stderr)
    sys.exit(1)

API_URL = "https://ntts.fdev.team/api/v1/tts/speakers"

# Script lives at Tools/OpenSpase/ — repo root is two levels up
REPO_ROOT = Path(__file__).parent.parent.parent
YAML_PATH = REPO_ROOT / "Resources/Prototypes/_OpenSpace/tts-voices.yml"
FTL_PATH  = REPO_ROOT / "Resources/Locale/ru-RU/_OpenSpace/tts/tts-voices.ftl"


def parse_args() -> str:
    parser = argparse.ArgumentParser(description="Regenerate TTS voice files from the API.")
    parser.add_argument("--token", default=os.environ.get("TTS_TOKEN", ""), help="Bearer token")
    args = parser.parse_args()
    if not args.token:
        parser.error("Provide --token or set TTS_TOKEN env variable.")
    return args.token


def fetch_voices(token: str) -> list[dict]:
    resp = requests.get(API_URL, headers={"Authorization": f"Bearer {token}"}, timeout=15)
    if not resp.ok:
        print(f"API error {resp.status_code}: {resp.text}", file=sys.stderr)
        sys.exit(1)

    data = resp.json()
    voices = data.get("voices", [])
    voices += data.get("custom_voices", [])
    return voices


def loc_key(speaker: str) -> str:
    return f"tts-voice-name-{speaker}"


def build_yaml(voices: list[dict]) -> str:
    lines = []
    for voice in voices:
        speaker = voice["speakers"][0]
        gender  = voice.get("gender", "none")
        key     = loc_key(speaker)

        lines.append(f"- type: ttsVoice")
        lines.append(f"  name: {key}")
        lines.append(f"  speaker: {speaker}")
        lines.append(f"  id: {speaker}")
        lines.append(f"  gender: {gender}")
        lines.append("")  # blank line between entries

    return "\n".join(lines)


def build_ftl(voices: list[dict]) -> str:
    lines = []
    for voice in voices:
        speaker = voice["speakers"][0]
        name    = voice["name"]
        key     = loc_key(speaker)
        lines.append(f"{key} = {name}")
    return "\n".join(lines) + "\n"


def write_file(path: str, content: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)


def main() -> None:
    token  = parse_args()
    voices = fetch_voices(token)

    if not voices:
        print("No voices returned by the API.")
        sys.exit(0)

    write_file(YAML_PATH, build_yaml(voices))
    write_file(FTL_PATH,  build_ftl(voices))

    print(f"Written {len(voices)} voices to:")
    print(f"  {YAML_PATH}")
    print(f"  {FTL_PATH}")


if __name__ == "__main__":
    main()
