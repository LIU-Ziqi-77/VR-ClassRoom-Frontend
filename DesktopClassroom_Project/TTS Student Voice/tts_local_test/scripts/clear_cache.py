#!/usr/bin/env python3
"""Clear generated audio and metadata for the local TTS evaluation cache."""

from __future__ import annotations

import argparse
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_AUDIO_DIR = PROJECT_ROOT / "output" / "audio"
DEFAULT_OUTPUT_DIR = PROJECT_ROOT / "output"


def resolve_path(path_value: str | Path) -> Path:
    path = Path(path_value)
    if path.is_absolute():
        return path
    return (Path.cwd() / path).resolve()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Clear cached generated TTS files.")
    parser.add_argument("--audio-dir", default=str(DEFAULT_AUDIO_DIR), help="Audio cache directory.")
    parser.add_argument("--keep-metadata", action="store_true", help="Only delete generated audio files.")
    parser.add_argument("--yes", action="store_true", help="Do not prompt for confirmation.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    audio_dir = resolve_path(args.audio_dir)
    output_dir = audio_dir.parent

    if not args.yes:
        answer = input(f"Clear generated files under {output_dir}? Type 'yes' to continue: ")
        if answer.strip().lower() != "yes":
            print("Canceled.")
            return 0

    deleted = 0
    if audio_dir.exists():
        for path in audio_dir.iterdir():
            if path.name == ".gitkeep":
                continue
            if path.is_file():
                path.unlink()
                deleted += 1

    if not args.keep_metadata:
        for name in ["metadata.csv", "metadata.json", "review.html"]:
            path = output_dir / name
            if path.exists():
                path.unlink()
                deleted += 1

    audio_dir.mkdir(parents=True, exist_ok=True)
    (audio_dir / ".gitkeep").touch()
    print(f"Deleted {deleted} generated file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
