#!/usr/bin/env python3
"""Build a static HTML listening page from generated TTS metadata."""

from __future__ import annotations

import argparse
import html
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_METADATA = PROJECT_ROOT / "output" / "metadata.json"
DEFAULT_UTTERANCES = PROJECT_ROOT / "config" / "utterances.json"
DEFAULT_OUTPUT = PROJECT_ROOT / "output" / "review.html"


def resolve_path(path_value: str | Path) -> Path:
    path = Path(path_value)
    if path.is_absolute():
        return path
    return (Path.cwd() / path).resolve()


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def category_order(utterances_path: Path) -> list[str]:
    data = load_json(utterances_path)
    return list(data.keys())


def audio_src_for(file_path: str, review_path: Path) -> str:
    path = Path(file_path)
    if not path.is_absolute():
        path = PROJECT_ROOT / path
    try:
        return Path(html.escape(str(path.relative_to(review_path.parent)))).as_posix()
    except ValueError:
        return path.as_uri()


def build_review_page(metadata_path: Path, utterances_path: Path, output_path: Path) -> None:
    rows = load_json(metadata_path) if metadata_path.exists() else []
    rows = [row for row in rows if row.get("generation_success") == "true"]
    grouped: dict[str, dict[str, dict[str, list[dict[str, Any]]]]] = defaultdict(lambda: defaultdict(lambda: defaultdict(list)))
    for row in rows:
        grouped[row.get("profile_id", "unknown_profile")][row.get("azure_voice_name", "unknown_voice")][
            row.get("utterance_category", "uncategorized")
        ].append(row)

    categories = category_order(utterances_path) if utterances_path.exists() else []
    category_rank = {category: i for i, category in enumerate(categories)}
    profile_counts = Counter(row.get("profile_id", "unknown_profile") for row in rows)
    voice_counts = Counter(row.get("azure_voice_name", "unknown_voice") for row in rows)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    parts: list[str] = [
        "<!doctype html>",
        '<html lang="zh-CN">',
        "<head>",
        '  <meta charset="utf-8">',
        '  <meta name="viewport" content="width=device-width, initial-scale=1">',
        "  <title>Azure Mandarin TTS Review</title>",
        "  <style>",
        "    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 24px; color: #1f2933; background: #fbfcfd; }",
        "    h1 { margin-bottom: 4px; }",
        "    h2 { margin-top: 34px; border-bottom: 2px solid #9fb3c8; padding-bottom: 6px; }",
        "    h3 { margin: 18px 0 6px; color: #334e68; }",
        "    h4 { margin: 16px 0 8px; color: #52606d; }",
        "    table { width: 100%; border-collapse: collapse; margin-bottom: 18px; }",
        "    th, td { border: 1px solid #d9e2ec; padding: 8px; vertical-align: top; font-size: 14px; }",
        "    th { background: #f0f4f8; text-align: left; }",
        "    code { font-size: 12px; }",
        "    audio { width: 240px; max-width: 100%; }",
        "    .meta { color: #52606d; margin-top: 0; }",
        "    .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 18px; margin: 18px 0 26px; }",
        "    .panel { background: white; border: 1px solid #d9e2ec; padding: 14px; }",
        "    .panel h2 { margin-top: 0; font-size: 18px; border: 0; padding: 0; }",
        "    .voice-block { background: white; border: 1px solid #bcccdc; padding: 12px; margin: 16px 0 22px; }",
        "    .voice-meta { color: #52606d; margin: 0 0 8px; }",
        "    .note { background: #eef7ff; border-left: 4px solid #4098d7; padding: 10px 12px; }",
        "  </style>",
        "</head>",
        "<body>",
        "  <h1>Azure Mandarin TTS Review</h1>",
        f"  <p class=\"meta\">Generated from <code>{html.escape(str(metadata_path))}</code>. Successful clips: {len(rows)}</p>",
        "  <p class=\"note\">Student profiles are virtual classroom personalities. Azure voices are the underlying TTS voice names, such as <code>zh-CN-XiaoxiaoNeural</code>.</p>",
    ]

    if not rows:
        parts.append("  <p>No successful generated clips found yet.</p>")
    else:
        parts.append("  <section class=\"summary\">")
        parts.append("    <div class=\"panel\"><h2>Student Profiles</h2><table><thead><tr><th>Profile</th><th>Clips</th></tr></thead><tbody>")
        for profile_id, count in sorted(profile_counts.items()):
            parts.append(f"      <tr><td><code>{html.escape(profile_id)}</code></td><td>{count}</td></tr>")
        parts.append("    </tbody></table></div>")
        parts.append("    <div class=\"panel\"><h2>Azure Voices Tested</h2><table><thead><tr><th>Voice</th><th>Clips</th></tr></thead><tbody>")
        for voice_name, count in sorted(voice_counts.items()):
            parts.append(f"      <tr><td><code>{html.escape(voice_name)}</code></td><td>{count}</td></tr>")
        parts.append("    </tbody></table></div>")
        parts.append("  </section>")

    for profile_id in sorted(grouped):
        profile_rows = [
            row
            for voice_rows in grouped[profile_id].values()
            for category_rows in voice_rows.values()
            for row in category_rows
        ]
        profile_label = profile_rows[0].get("profile_label", "") if profile_rows else ""
        parts.append(f"  <h2>{html.escape(profile_id)}: {html.escape(profile_label)}</h2>")
        for voice_name in sorted(grouped[profile_id]):
            voice_rows = [row for category_rows in grouped[profile_id][voice_name].values() for row in category_rows]
            first_row = voice_rows[0]
            prosody = f"pitch {first_row.get('pitch', '')}, rate {first_row.get('rate', '')}, volume {first_row.get('volume', '')}"
            parts.append("  <section class=\"voice-block\">")
            parts.append(f"    <h3>Azure voice: <code>{html.escape(voice_name)}</code></h3>")
            parts.append(f"    <p class=\"voice-meta\">{html.escape(prosody)} · {len(voice_rows)} clip(s)</p>")
            for category in sorted(grouped[profile_id][voice_name], key=lambda c: category_rank.get(c, 999)):
                parts.append(f"    <h4>{html.escape(category)}</h4>")
                parts.append("    <table>")
                parts.append(
                    "      <thead><tr>"
                    "<th>Utterance</th><th>Text</th><th>Audio</th><th>File</th>"
                    "</tr></thead>"
                )
                parts.append("      <tbody>")
                for row in sorted(grouped[profile_id][voice_name][category], key=lambda r: r.get("utterance_key", "")):
                    src = audio_src_for(row.get("file_path", ""), output_path)
                    parts.append("        <tr>")
                    parts.append(f"          <td><code>{html.escape(row.get('utterance_key', ''))}</code></td>")
                    parts.append(f"          <td lang=\"zh-CN\">{html.escape(row.get('text', ''))}</td>")
                    parts.append(f"          <td><audio controls preload=\"none\" src=\"{src}\"></audio></td>")
                    parts.append(f"          <td><code>{html.escape(row.get('file_path', ''))}</code></td>")
                    parts.append("        </tr>")
                parts.append("      </tbody>")
                parts.append("    </table>")
            parts.append("  </section>")

    parts.extend(["</body>", "</html>", ""])
    output_path.write_text("\n".join(parts), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate output/review.html from metadata.json.")
    parser.add_argument("--metadata", default=str(DEFAULT_METADATA), help="Path to metadata.json.")
    parser.add_argument("--utterances", default=str(DEFAULT_UTTERANCES), help="Path to utterances.json.")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT), help="Path to review.html.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    metadata_path = resolve_path(args.metadata)
    utterances_path = resolve_path(args.utterances)
    output_path = resolve_path(args.output)
    build_review_page(metadata_path=metadata_path, utterances_path=utterances_path, output_path=output_path)
    print(f"Review page written: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
