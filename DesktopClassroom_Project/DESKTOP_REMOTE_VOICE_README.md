# Desktop Classroom Remote Voice Demo

This folder is a copied desktop-version Unity project. It keeps the existing
classroom, student avatars, procedural student behavior, and VRM mouth shapes,
then adds a lightweight researcher remote-voice path.

The recommended remote path is now **Route B: preset lines + existing Azure
voice package**. The researcher presses buttons in a browser; Unity receives a
small command over WebSocket and plays the matching local WAV from
`Assets/Resources/DTTStudentVoices`.

## What was added

- Unity-side runtime bootstrap:
  - `Assets/Scripts/DesktopRemoteVoice/DesktopClassroomBootstrap.cs`
  - Automatically enables desktop scene setup and the UDP remote voice receiver.
- Unity-side UDP receiver:
  - `Assets/Scripts/DesktopRemoteVoice/DesktopRemoteVoiceReceiver.cs`
  - Listens on UDP port `5066`.
  - Finds students automatically.
  - Routes remote voice by 1-based student index or student id/name.
- Unity-side per-student streaming voice player:
  - `Assets/Scripts/DesktopRemoteVoice/RemoteStudentVoicePlayer.cs`
  - Plays researcher microphone/virtual microphone audio through the selected student.
  - Drives approximate mouth movement from live audio volume.
  - Triggers speaking body motion while audio is active.
- Small extension to:
  - `Assets/Scripts/StudentBehaviorController.cs`
  - Adds `BeginExternalSpeaking()` and `EndExternalSpeaking()`.
- Researcher-side Python tool:
  - `Tools/DesktopRemoteVoice/researcher_voice_client.py`
  - Captures a microphone or virtual microphone.
  - Sends audio chunks to Unity over UDP.
  - Sends simple behavior commands.
- Preset-line public-network relay:
  - `Tools/PresetLineRelay/server.js`
  - Serves the researcher button console.
  - Relays `preset_line` and `behavior` JSON commands over WebSocket.
- Unity preset-line relay client:
  - `Assets/Scripts/DesktopRemoteVoice/DesktopPresetLineRelayClient.cs`
  - Connects to the relay server.
  - Plays existing local WAV clips from `Resources/DTTStudentVoices`.

## What you need to do

1. For the recommended preset-line route, no live voice changer is needed.
   - The three Azure-generated child voice packs are already imported under
     `Assets/Resources/DTTStudentVoices`.
   - The relay sends only a command such as `student 1 + teacher_i_know`.
   - Unity plays `Resources/DTTStudentVoices/student_01_xiaoxiao_girl/teacher_i_know.wav`.

2. If you still want to test the older live-microphone UDP route, install Python dependencies:

   ```bash
   python3 -m pip install sounddevice numpy
   ```

3. If the researcher and trainee are not on the same LAN, use the preset-line relay.
   - Current Render service: `https://vr-classroom-relay.onrender.com/`
   - Unity relay URL: `wss://vr-classroom-relay.onrender.com/ws?role=unity&room=demo&token=49929d9cf29a50c34610c7d52ad4d050`
   - Researcher console: `https://vr-classroom-relay.onrender.com/console`

4. Open the copied project in Unity:

   ```text
   /Users/liuziqi/Downloads/VR-main/vrclass_Special_Edu/DesktopClassroom_Project
   ```

## How to run locally on one computer

### Recommended: preset-line buttons

For remote/public-network testing, use:

```text
Researcher console:
https://vr-classroom-relay.onrender.com/console

Room:
demo
```

The `wss://.../ws?...` URL is for Unity's WebSocket client. It is not a normal
browser page, so opening it directly in Chrome will not show the control UI.

1. Start the relay from the copied project root:

   ```bash
   node Tools/PresetLineRelay/server.js
   ```

2. Open the researcher console:

   ```text
   http://127.0.0.1:8787/console
   ```

3. Open Unity project:

   ```text
   /Users/liuziqi/Downloads/VR-main/vrclass_Special_Edu/DesktopClassroom_Project
   ```

4. Open scene:

   ```text
   Assets/Scenes/HighSchoolClassroom_Demo.unity
   ```

5. Press Play.

6. In the browser console:
   - click `连接`
   - select 莉莉, 卢卡, or 贝拉
   - click a preset line such as `老师老师，我知道！`
   - Unity should play the matching local WAV from the selected student's voice profile.

### Optional: live microphone UDP test

1. Open the project folder above in Unity.
2. Open:

   ```text
   Assets/Scenes/HighSchoolClassroom_Demo.unity
   ```

3. Press Play.
4. Confirm the top-left overlay says:

   ```text
   Desktop Remote Voice Receiver
   UDP port: 5066 | running: True
   ```

5. In a terminal from the copied project root:

   ```bash
   python3 Tools/DesktopRemoteVoice/researcher_voice_client.py --list-devices
   ```

6. Start the researcher client:

   ```bash
   python3 Tools/DesktopRemoteVoice/researcher_voice_client.py --host 127.0.0.1 --student 1
   ```

7. Commands inside the researcher client:

   ```text
   1 / 2 / 3          select student
   r                  start/stop live voice for selected student
   b raise_hand 5     selected student raises hand for 5 seconds
   b distracted 6     selected student looks distracted
   b talk 5           selected student turns/talks to nearby classmate
   b hit_desk 3       selected student hits desk
   b stop             stop current behavior
   q                  quit
   ```

## How to run with a voice changer

This section applies only to the optional live microphone UDP route, not the
recommended preset-line route.

1. Start the voice changer.
2. Set its output to a virtual microphone.
3. List devices:

   ```bash
   python3 Tools/DesktopRemoteVoice/researcher_voice_client.py --list-devices
   ```

4. Start with the selected input device:

   ```bash
   python3 Tools/DesktopRemoteVoice/researcher_voice_client.py --host 127.0.0.1 --student 1 --device "YOUR VIRTUAL MIC NAME"
   ```

If the tool rejects the device name, use the numeric device index printed by
`--list-devices`.

## Desktop classroom controls

- Right mouse button + mouse: look around.
- Right mouse button + WASD: move camera.
- Existing demo shortcuts still work when the right mouse button is not held:
  - `1/2/3`: select student locally.
  - `Q`: test procedural speaking.
  - `W`: raise hand.
  - `D`: distracted.
  - `S`: stop selected student.
  - `X`: stop all students.

## Current limitations

- This is a research-demo UDP implementation, not a production WebRTC system.
- It is best on a local network or VPN. Wide-area Internet use may need relay,
  NAT traversal, or WebRTC later.
- Mouth movement is amplitude-based and approximate.
- Voice changing is external through a virtual microphone.
- Audio is not encrypted.
- The system assumes one active researcher voice stream at a time per selected
  student.
