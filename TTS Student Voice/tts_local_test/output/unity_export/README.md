# Unity Voice Export

Final student-to-voice mapping:

| System student ID | Chinese name | Azure voice |
|---|---|---|
| `Ele_student1` | 可可 | `zh-CN-XiaoxiaoNeural` |
| `Ele_student2` | 李奥 | `zh-CN-YunjianNeural` |
| `Ele_student3` | 安娜 | `zh-CN-XiaoyiNeural` |

Audio files are in:

```text
output/unity_export/audio/
```

Filename format:

```text
{student_id}__{utterance_key}.wav
```

Examples:

```text
Ele_student1__ruler_short.wav
Ele_student2__teacher_i_know.wav
Ele_student3__i_dont_know.wav
```

Use `unity_voice_manifest.json` or `unity_voice_manifest.csv` to map each file back to:

- `student_id`
- `student_name_zh`
- `utterance_key`
- Mandarin text
- Azure voice name
- pitch / rate / volume settings

These WAV files are 24 kHz, 16-bit, mono PCM, generated for local evaluation and suitable for first-pass Unity import testing.
