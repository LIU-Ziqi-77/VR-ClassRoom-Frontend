# DTT Desktop Monitor

This monitor is for local research/demo testing.

## Start the dashboard

From the Unity project root:

```bash
python3 Tools/dtt_monitor_server.py
```

Open:

```text
http://127.0.0.1:8088
```

The server listens for Unity/Quest monitor packets on UDP `5060`.

## Start ASR from the dashboard

Use the `ASR Process` panel:

- `Quest IP`: the Quest 3 headset IP, for example `192.168.1.6`
- `Port`: `5055`

Click `Start ASR`.

Manual ASR startup still works:

```bash
cd "Voice Recognition/sherpa_streaming_test"
.venv/bin/python stream_test.py --unity-host 192.168.1.6 --unity-port 5055
```

Manual ASR sends monitor events to `127.0.0.1:5060` by default.

## Monitor Quest 3 screen

The dashboard can start `scrcpy` if it is installed and the Quest is visible through ADB.

Quest mirrors VR apps as a stereo side-by-side image. Use:

- `Left eye`: crops to `2064:2208:0:0`
- `Right eye`: crops to `2064:2208:2064:0`
- `Full stereo`: shows the original two-eye view

The dashboard starts scrcpy with:

```bash
scrcpy --max-size 1280 --video-bit-rate 8M --stay-awake --audio-source=playback --audio-dup
```

`--audio-dup` keeps audio playing in the headset while also trying to play it on the computer. Some Android/Quest apps can opt out of playback capture; if that happens, video still works but computer-side audio may be silent.

Useful checks:

```bash
adb devices
scrcpy --max-size 1280 --video-bit-rate 8M --stay-awake --crop=2064:2208:0:0 --audio-source=playback --audio-dup
```

On macOS, if needed:

```bash
brew install android-platform-tools scrcpy
```

If ADB/scrcpy is unreliable over Wi-Fi, use a USB-C cable first and accept the headset's USB debugging prompt.
