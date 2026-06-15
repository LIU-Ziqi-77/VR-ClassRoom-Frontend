# Preset Line Relay

This is the cloud/public-network path for route B:

```text
Researcher browser buttons
  -> WebSocket relay
  -> Unity desktop classroom
  -> local Resources/DTTStudentVoices/*.wav playback
```

No live audio is sent through the relay. It only forwards small JSON commands.

## Local Run

From this folder:

```bash
node server.js
```

Open:

```text
http://127.0.0.1:8787/console
```

Unity default relay URL:

```text
ws://127.0.0.1:8787/ws?role=unity&room=demo
```

## Public Deployment

Any Node 18+ host that supports WebSocket upgrade should work.

Recommended minimal environment variables:

```text
PORT=8787
RELAY_TOKEN=choose-a-private-token
```

On most platforms, `PORT` is assigned automatically. `RELAY_TOKEN` is optional
but recommended for remote studies.

After deployment, use:

```text
https://YOUR-SERVICE/console
```

Unity relay URL:

```text
wss://YOUR-SERVICE/ws?role=unity&room=demo&token=YOUR_TOKEN
```

You can pass this to a Unity build with:

```bash
./DesktopClassroomBuild --relay-url "wss://YOUR-SERVICE/ws?role=unity&room=demo&token=YOUR_TOKEN"
```

For Editor testing, change `DesktopPresetLineRelayClient.relayUrl` in the
Inspector or temporarily edit its default string.

## Message Examples

Preset line:

```json
{
  "type": "preset_line",
  "studentIndex": 1,
  "voiceProfileId": "student_01_xiaoxiao_girl",
  "utteranceKey": "teacher_i_know",
  "text": "老师老师，我知道！"
}
```

Behavior:

```json
{
  "type": "behavior",
  "studentIndex": 2,
  "behavior": "distracted",
  "duration": 6
}
```

