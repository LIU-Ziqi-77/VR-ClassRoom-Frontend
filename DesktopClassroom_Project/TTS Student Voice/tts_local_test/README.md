# Azure Mandarin TTS Local Test Toolkit

This is a local evaluation toolkit for testing Azure Mandarin Chinese Text-to-Speech voices before Unity / Quest 3 integration. It generates short classroom utterances, varies voice/prosody by student profile, caches clips, writes metadata, and builds a simple listening page.

It does not integrate with Unity and it does not clone real children's voices.

## Setup

From this folder:

```bash
cd tts_local_test
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
```

Edit `.env`:

```bash
AZURE_SPEECH_KEY=your_key_here
AZURE_SPEECH_REGION=your_region_here
```

`.env` is ignored by Git. Do not put Azure keys in any config JSON or Python file.

## Small Test Run

Generate five clips:

```bash
python scripts/generate_tts.py --limit 5
```

Run the same command again:

```bash
python scripts/generate_tts.py --limit 5
```

The second run should print `cache hit` for files that already exist and should not call Azure for those clips.

## Full Generation

Generate every configured utterance for every configured student profile:

```bash
python scripts/generate_tts.py
```

Generate only one profile:

```bash
python scripts/generate_tts.py --profile student_b_shy
```

Override the Azure voice for all selected profiles:

```bash
python scripts/generate_tts.py --voice zh-CN-XiaoxiaoNeural
```

Try every candidate voice in `config/azure_voices.json` with every selected profile's prosody:

```bash
python scripts/generate_tts.py --all-voices
```

Force regeneration even if cached files exist:

```bash
python scripts/generate_tts.py --force-regenerate
```

## Outputs

Generated audio is saved in:

```text
output/audio/
```

Metadata is written to:

```text
output/metadata.csv
output/metadata.json
```

The review page is written to:

```text
output/review.html
```

Open `output/review.html` in a browser to listen and compare clips. You can also regenerate it manually:

```bash
python scripts/review_cache.py
```

## Caching

Each clip gets a deterministic cache key from:

- Mandarin text
- utterance key
- profile id
- Azure voice name
- pitch
- rate
- volume
- audio format

The filename uses:

```text
{profile_id}__{utterance_key}__{hash}.wav
```

If the same combination already exists, the generator records `cache_hit=true`, sets `azure_called=false`, and skips synthesis. Use `--force-regenerate` when you intentionally want to replace cached audio.

## Audio Format

The default format is `wav`, requested from Azure as 24 kHz, 16-bit, mono PCM WAV:

```text
Riff24Khz16BitMonoPcm
```

This is easy to inspect locally and import into Unity later. The script also supports `--format mp3` for quick review, but WAV is the recommended default for the Unity evaluation path.

## Editing Test Content

Utterances live in:

```text
config/utterances.json
```

Student voice profiles live in:

```text
config/voice_profiles.json
```

Candidate Azure voices live in:

```text
config/azure_voices.json
```

Voice names are intentionally configurable. Xiaoxiao is only a baseline, not a final decision.

## Hesitation Pauses

Profiles may include:

```json
"hesitation_break_ms": 300
```

When a text contains `……` or `...`, the generator can turn that ellipsis into an SSML break, for example:

```xml
嗯<break time="300ms"/>是尺子吗？
```

Disable this behavior with:

```bash
python scripts/generate_tts.py --disable-hesitation-breaks
```

## Clearing Generated Files

Preview what you are clearing, then confirm at the prompt:

```bash
python scripts/clear_cache.py
```

Skip the confirmation:

```bash
python scripts/clear_cache.py --yes
```

Delete audio but keep metadata and review page:

```bash
python scripts/clear_cache.py --keep-metadata
```

## What To Listen For

When comparing clips, listen for:

- Whether the voice sounds like a plausible classroom child rather than an adult narrator.
- Whether short answers like `尺子！` sound too sharp, too formal, or naturally spontaneous.
- Whether uncertain utterances sound hesitant without becoming robotic.
- Whether excited behavior like `老师老师，我知道！` has enough energy without clipping or sounding synthetic.
- Whether the pitch/rate changes create student variety without making the Mandarin unnatural.
- Whether the final WAV quality is clean enough for later Unity import.

After choosing preferred combinations, copy the winning `profile_id`, `azure_voice_name`, `pitch`, `rate`, and `volume` values into the future Unity integration plan.
