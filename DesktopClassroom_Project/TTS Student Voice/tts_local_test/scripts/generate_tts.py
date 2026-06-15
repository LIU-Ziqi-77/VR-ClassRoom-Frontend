#!/usr/bin/env python3
"""Generate cached Azure Mandarin TTS clips for local voice evaluation."""

from __future__ import annotations

import argparse
import csv
import hashlib
import html
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PROFILES = PROJECT_ROOT / "config" / "voice_profiles.json"
DEFAULT_UTTERANCES = PROJECT_ROOT / "config" / "utterances.json"
DEFAULT_VOICES = PROJECT_ROOT / "config" / "azure_voices.json"
DEFAULT_AUDIO_DIR = PROJECT_ROOT / "output" / "audio"
METADATA_FIELDS = [
    "cache_key",
    "file_path",
    "utterance_category",
    "utterance_key",
    "text",
    "profile_id",
    "profile_label",
    "azure_voice_name",
    "pitch",
    "rate",
    "volume",
    "audio_format",
    "cache_hit",
    "azure_called",
    "generation_success",
    "error_message",
    "created_at",
]


def load_dotenv_fallback(env_path: Path) -> None:
    """Load .env without requiring python-dotenv to be installed first."""
    if not env_path.exists():
        return

    try:
        from dotenv import load_dotenv

        load_dotenv(env_path)
        return
    except ImportError:
        pass

    for line in env_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        os.environ.setdefault(key, value)


def resolve_path(path_value: str | Path) -> Path:
    path = Path(path_value)
    if path.is_absolute():
        return path
    return (Path.cwd() / path).resolve()


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def flatten_utterances(config: dict[str, list[dict[str, str]]]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for category, utterances in config.items():
        for item in utterances:
            rows.append(
                {
                    "category": category,
                    "key": item["key"],
                    "text": item["text"],
                }
            )
    return rows


def load_voice_names(path: Path) -> list[str]:
    voices = load_json(path)
    return [voice["voice_name"] for voice in voices]


def build_plan(
    profiles: list[dict[str, Any]],
    utterances: list[dict[str, str]],
    profile_filter: str | None,
    voice_override: str | None,
    all_voices: bool,
    candidate_voice_names: list[str],
) -> list[tuple[dict[str, Any], dict[str, str]]]:
    selected_profiles = [
        profile for profile in profiles if profile_filter is None or profile["profile_id"] == profile_filter
    ]
    if profile_filter and not selected_profiles:
        raise ValueError(f"Profile '{profile_filter}' was not found.")

    expanded_profiles: list[dict[str, Any]] = []
    for profile in selected_profiles:
        if all_voices:
            for voice_name in candidate_voice_names:
                copy = dict(profile)
                copy["azure_voice_name"] = voice_name
                expanded_profiles.append(copy)
        else:
            copy = dict(profile)
            if voice_override:
                copy["azure_voice_name"] = voice_override
            expanded_profiles.append(copy)

    return [(profile, utterance) for profile in expanded_profiles for utterance in utterances]


def cache_key_for(
    text: str,
    utterance_key: str,
    profile_id: str,
    azure_voice_name: str,
    pitch: str,
    rate: str,
    volume: str,
    audio_format: str,
) -> str:
    payload = {
        "text": text,
        "utterance_key": utterance_key,
        "profile_id": profile_id,
        "azure_voice_name": azure_voice_name,
        "pitch": pitch,
        "rate": rate,
        "volume": volume,
        "audio_format": audio_format,
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def output_filename(profile_id: str, utterance_key: str, cache_key: str, audio_format: str) -> str:
    return f"{profile_id}__{utterance_key}__{cache_key[:8]}.{audio_format}"


def ssml_content_for(text: str, break_ms: int) -> str:
    if break_ms <= 0:
        return html.escape(text, quote=False)

    break_tag = f'<break time="{break_ms}ms"/>'
    if "……" in text:
        return break_tag.join(html.escape(part, quote=False) for part in text.split("……"))
    if "..." in text:
        return break_tag.join(html.escape(part, quote=False) for part in text.split("..."))
    return html.escape(text, quote=False)


def build_ssml(profile: dict[str, Any], text: str, disable_hesitation_breaks: bool) -> str:
    break_ms = 0 if disable_hesitation_breaks else int(profile.get("hesitation_break_ms", 0) or 0)
    content = ssml_content_for(text, break_ms)
    voice_name = html.escape(profile["azure_voice_name"], quote=True)
    pitch = html.escape(profile.get("pitch", "+0%"), quote=True)
    rate = html.escape(profile.get("rate", "+0%"), quote=True)
    volume = html.escape(profile.get("volume", "+0%"), quote=True)
    return (
        '<speak version="1.0" xml:lang="zh-CN" '
        'xmlns="http://www.w3.org/2001/10/synthesis">\n'
        f'  <voice name="{voice_name}">\n'
        f'    <prosody pitch="{pitch}" rate="{rate}" volume="{volume}">{content}</prosody>\n'
        "  </voice>\n"
        "</speak>"
    )


def azure_output_format(audio_format: str):
    import azure.cognitiveservices.speech as speechsdk

    if audio_format == "wav":
        return speechsdk.SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm
    if audio_format == "mp3":
        return speechsdk.SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3
    raise ValueError(f"Unsupported audio format: {audio_format}")


def synthesize_to_file(
    speech_key: str,
    speech_region: str,
    ssml: str,
    output_path: Path,
    audio_format: str,
) -> tuple[bool, str]:
    import azure.cognitiveservices.speech as speechsdk

    speech_config = speechsdk.SpeechConfig(subscription=speech_key, region=speech_region)
    speech_config.set_speech_synthesis_output_format(azure_output_format(audio_format))
    audio_config = speechsdk.audio.AudioOutputConfig(filename=str(output_path))
    synthesizer = speechsdk.SpeechSynthesizer(speech_config=speech_config, audio_config=audio_config)
    result = synthesizer.speak_ssml_async(ssml).get()

    if result.reason == speechsdk.ResultReason.SynthesizingAudioCompleted:
        return True, ""

    if result.reason == speechsdk.ResultReason.Canceled:
        cancellation = speechsdk.SpeechSynthesisCancellationDetails(result)
        error = f"Azure synthesis canceled: {cancellation.reason}"
        if cancellation.error_details:
            error += f" | {cancellation.error_details}"
        return False, error

    return False, f"Azure synthesis failed: {result.reason}"


def bool_for_csv(value: bool) -> str:
    return "true" if value else "false"


def metadata_paths(audio_dir: Path) -> tuple[Path, Path]:
    output_dir = audio_dir.parent
    return output_dir / "metadata.csv", output_dir / "metadata.json"


def load_existing_metadata(json_path: Path) -> dict[str, dict[str, Any]]:
    if not json_path.exists():
        return {}
    try:
        data = json.loads(json_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}
    rows = [row for row in data if isinstance(row, dict) and row.get("cache_key")]
    return {row["cache_key"]: row for row in rows}


def write_metadata(rows_by_key: dict[str, dict[str, Any]], csv_path: Path, json_path: Path) -> None:
    rows = sorted(rows_by_key.values(), key=lambda row: (row.get("profile_id", ""), row.get("utterance_key", ""), row.get("cache_key", "")))
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with csv_path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=METADATA_FIELDS, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field, "") for field in METADATA_FIELDS})

    with json_path.open("w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=2)


def make_row(
    cache_key: str,
    output_path: Path,
    audio_dir: Path,
    utterance: dict[str, str],
    profile: dict[str, Any],
    audio_format: str,
    cache_hit: bool,
    azure_called: bool,
    generation_success: bool,
    error_message: str,
) -> dict[str, Any]:
    try:
        file_path = str(output_path.relative_to(PROJECT_ROOT))
    except ValueError:
        file_path = str(output_path)

    return {
        "cache_key": cache_key,
        "file_path": file_path,
        "utterance_category": utterance["category"],
        "utterance_key": utterance["key"],
        "text": utterance["text"],
        "profile_id": profile["profile_id"],
        "profile_label": profile.get("profile_label", ""),
        "azure_voice_name": profile["azure_voice_name"],
        "pitch": profile.get("pitch", "+0%"),
        "rate": profile.get("rate", "+0%"),
        "volume": profile.get("volume", "+0%"),
        "audio_format": audio_format,
        "cache_hit": bool_for_csv(cache_hit),
        "azure_called": bool_for_csv(azure_called),
        "generation_success": bool_for_csv(generation_success),
        "error_message": error_message,
        "created_at": datetime.now(timezone.utc).isoformat(),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate cached Azure Mandarin TTS clips.")
    parser.add_argument("--profiles", default=str(DEFAULT_PROFILES), help="Path to voice_profiles.json.")
    parser.add_argument("--utterances", default=str(DEFAULT_UTTERANCES), help="Path to utterances.json.")
    parser.add_argument("--voices", default=str(DEFAULT_VOICES), help="Path to azure_voices.json for --all-voices.")
    parser.add_argument("--output", default=str(DEFAULT_AUDIO_DIR), help="Output audio directory.")
    parser.add_argument("--format", default="wav", choices=["wav", "mp3"], help="Audio format to request from Azure.")
    parser.add_argument("--force-regenerate", action="store_true", help="Regenerate even when a cached file exists.")
    parser.add_argument("--limit", type=int, default=None, help="Limit total planned clips for small test runs.")
    parser.add_argument("--profile", default=None, help="Only generate one profile_id.")
    parser.add_argument("--voice", default=None, help="Override every selected profile with one Azure voice name.")
    parser.add_argument("--all-voices", action="store_true", help="Test every candidate voice with every selected profile.")
    parser.add_argument("--replace-metadata", action="store_true", help="Replace metadata instead of updating existing rows.")
    parser.add_argument("--disable-hesitation-breaks", action="store_true", help="Do not insert SSML breaks for ellipsis text.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    audio_dir = resolve_path(args.output)
    profiles_path = resolve_path(args.profiles)
    utterances_path = resolve_path(args.utterances)
    voices_path = resolve_path(args.voices)
    env_path = PROJECT_ROOT / ".env"

    audio_dir.mkdir(parents=True, exist_ok=True)
    csv_path, json_path = metadata_paths(audio_dir)

    try:
        profiles = load_json(profiles_path)
        utterances = flatten_utterances(load_json(utterances_path))
        candidate_voice_names = load_voice_names(voices_path)
        plan = build_plan(
            profiles=profiles,
            utterances=utterances,
            profile_filter=args.profile,
            voice_override=args.voice,
            all_voices=args.all_voices,
            candidate_voice_names=candidate_voice_names,
        )
    except Exception as exc:
        print(f"Configuration error: {exc}", file=sys.stderr)
        return 1

    if args.limit is not None:
        plan = plan[: max(args.limit, 0)]

    if not plan:
        print("No clips were selected. Check --limit, --profile, and config files.")
        return 0

    rows_by_key = {} if args.replace_metadata else load_existing_metadata(json_path)
    total = len(plan)
    print(f"Generating {total} planned clip(s) into {audio_dir}")

    load_dotenv_fallback(env_path)
    speech_key = os.getenv("AZURE_SPEECH_KEY")
    speech_region = os.getenv("AZURE_SPEECH_REGION")
    missing_credentials_warned = False
    missing_sdk_warned = False
    had_generation_error = False

    for index, (profile, utterance) in enumerate(plan, start=1):
        cache_key = cache_key_for(
            text=utterance["text"],
            utterance_key=utterance["key"],
            profile_id=profile["profile_id"],
            azure_voice_name=profile["azure_voice_name"],
            pitch=profile.get("pitch", "+0%"),
            rate=profile.get("rate", "+0%"),
            volume=profile.get("volume", "+0%"),
            audio_format=args.format,
        )
        output_path = audio_dir / output_filename(profile["profile_id"], utterance["key"], cache_key, args.format)

        prefix = f"[{index}/{total}] {profile['profile_id']} | {utterance['key']}"
        if output_path.exists() and not args.force_regenerate:
            print(f"{prefix}: cache hit -> {output_path.name}")
            row = make_row(
                cache_key,
                output_path,
                audio_dir,
                utterance,
                profile,
                args.format,
                cache_hit=True,
                azure_called=False,
                generation_success=True,
                error_message="",
            )
            rows_by_key[cache_key] = row
            continue

        if not speech_key or not speech_region:
            error_message = (
                "Missing Azure credentials. Create tts_local_test/.env with "
                "AZURE_SPEECH_KEY and AZURE_SPEECH_REGION, or export them in your shell."
            )
            if not missing_credentials_warned:
                print(error_message, file=sys.stderr)
                print("Cache misses were recorded as errors. No Azure calls were made.", file=sys.stderr)
                missing_credentials_warned = True
            row = make_row(
                cache_key,
                output_path,
                audio_dir,
                utterance,
                profile,
                args.format,
                cache_hit=False,
                azure_called=False,
                generation_success=False,
                error_message=error_message,
            )
            rows_by_key[cache_key] = row
            had_generation_error = True
            continue

        try:
            import azure.cognitiveservices.speech  # noqa: F401
        except ImportError:
            error_message = (
                "Missing dependency: azure-cognitiveservices-speech. "
                "Run: pip install -r requirements.txt"
            )
            if not missing_sdk_warned:
                print(error_message, file=sys.stderr)
                missing_sdk_warned = True
            row = make_row(
                cache_key,
                output_path,
                audio_dir,
                utterance,
                profile,
                args.format,
                cache_hit=False,
                azure_called=False,
                generation_success=False,
                error_message=error_message,
            )
            rows_by_key[cache_key] = row
            had_generation_error = True
            continue

        ssml = build_ssml(profile, utterance["text"], args.disable_hesitation_breaks)
        print(f"{prefix}: calling Azure voice={profile['azure_voice_name']} pitch={profile.get('pitch')} rate={profile.get('rate')}")
        success = False
        error_message = ""
        try:
            success, error_message = synthesize_to_file(
                speech_key=speech_key,
                speech_region=speech_region,
                ssml=ssml,
                output_path=output_path,
                audio_format=args.format,
            )
            if not success and output_path.exists() and output_path.stat().st_size == 0:
                output_path.unlink()
        except Exception as exc:
            error_message = str(exc)
            success = False

        if success:
            print(f"{prefix}: generated -> {output_path.name}")
        else:
            print(f"{prefix}: ERROR {error_message}", file=sys.stderr)
            had_generation_error = True

        row = make_row(
            cache_key,
            output_path,
            audio_dir,
            utterance,
            profile,
            args.format,
            cache_hit=False,
            azure_called=True,
            generation_success=success,
            error_message=error_message,
        )
        rows_by_key[cache_key] = row

    write_metadata(rows_by_key, csv_path, json_path)
    print(f"Metadata written: {csv_path}")
    print(f"Metadata written: {json_path}")

    try:
        from review_cache import build_review_page

        review_path = audio_dir.parent / "review.html"
        build_review_page(metadata_path=json_path, utterances_path=utterances_path, output_path=review_path)
        print(f"Review page written: {review_path}")
    except Exception as exc:
        print(f"Warning: could not update review.html automatically: {exc}", file=sys.stderr)

    return 1 if had_generation_error else 0


if __name__ == "__main__":
    raise SystemExit(main())
