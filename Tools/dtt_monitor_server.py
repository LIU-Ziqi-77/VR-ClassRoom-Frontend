#!/usr/bin/env python3
"""Desktop monitor for the Quest DTT Unity prototype.

Features:
- UDP listener for Unity status/log packets on port 5060.
- Local web dashboard with Server-Sent Events.
- Optional process controls for ASR and scrcpy.

No third-party Python dependencies are required.
"""

from __future__ import annotations

import argparse
import json
import os
import queue
import shutil
import socket
import subprocess
import sys
import threading
import time
from collections import deque
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, urlparse


ROOT_DIR = Path(__file__).resolve().parents[1]
ASR_DIR = ROOT_DIR / "Voice Recognition" / "sherpa_streaming_test"
DEFAULT_ASR_PYTHON = ASR_DIR / ".venv" / "bin" / "python"


INDEX_HTML = r"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>DTT VR Monitor</title>
  <style>
    :root {
      color-scheme: dark;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: #101216;
      color: #eef1f6;
    }
    body { margin: 0; background: #101216; }
    header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 14px 18px; border-bottom: 1px solid #2b3038; background: #171a20;
    }
    h1 { font-size: 18px; margin: 0; font-weight: 650; }
    main { display: grid; grid-template-columns: 360px 1fr; gap: 14px; padding: 14px; }
    section {
      background: #171a20; border: 1px solid #2b3038; border-radius: 6px;
      padding: 12px; min-width: 0;
    }
    h2 { font-size: 14px; margin: 0 0 10px; color: #cbd2df; }
    .grid { display: grid; grid-template-columns: 120px 1fr; gap: 7px 10px; font-size: 13px; }
    .label { color: #9ca6b8; }
    .value { color: #f5f7fb; overflow-wrap: anywhere; }
    .status { white-space: pre-wrap; line-height: 1.35; }
    .row { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
    input {
      width: 132px; background: #0f1116; border: 1px solid #333a46;
      border-radius: 5px; color: #eef1f6; padding: 7px 8px;
    }
    button {
      background: #2d6cdf; color: white; border: 0; border-radius: 5px;
      padding: 8px 10px; cursor: pointer; font-weight: 600;
    }
    button.secondary { background: #363d49; }
    button.danger { background: #a63d40; }
    #logs {
      height: calc(100vh - 228px); min-height: 420px; overflow: auto;
      background: #0c0e12; border: 1px solid #252a33; border-radius: 5px;
      padding: 10px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 12px; line-height: 1.45;
    }
    .log { border-bottom: 1px solid #1b2028; padding: 4px 0; white-space: pre-wrap; overflow-wrap: anywhere; }
    .warn { color: #ffd166; }
    .error { color: #ff6b6b; }
    .ok { color: #8bd17c; }
    @media (max-width: 900px) {
      main { grid-template-columns: 1fr; }
      #logs { height: 420px; }
    }
  </style>
</head>
<body>
  <header>
    <h1>DTT VR Monitor</h1>
    <div id="connection" class="value">connecting...</div>
  </header>
  <main>
    <div>
      <section>
        <h2>State Machine</h2>
        <div class="grid">
          <div class="label">Student</div><div id="active_student" class="value">-</div>
          <div class="label">Scenario</div><div id="scenario" class="value">-</div>
          <div class="label">Step</div><div id="step" class="value">-</div>
          <div class="label">Expected</div><div id="expected" class="value">-</div>
          <div class="label">Waiting</div><div id="waiting" class="value">-</div>
          <div class="label">Selected Aid</div><div id="selected_aid" class="value">-</div>
          <div class="label">Held Aid</div><div id="held_aid" class="value">-</div>
          <div class="label">Device</div><div id="device" class="value">-</div>
          <div class="label">Status</div><div id="status" class="value status">-</div>
        </div>
      </section>
      <section style="margin-top:14px">
        <h2>ASR Process</h2>
        <div class="row">
          <input id="unityHost" placeholder="Quest IP" value="192.168.1.6">
          <input id="unityPort" placeholder="Port" value="5055">
          <button onclick="startAsr()">Start ASR</button>
          <button class="danger" onclick="stopAsr()">Stop</button>
        </div>
      </section>
      <section style="margin-top:14px">
        <h2>Quest Screen</h2>
        <div class="row">
          <button class="secondary" onclick="adbDevices()">ADB Devices</button>
          <button onclick="startScrcpy('left')">Left eye</button>
          <button onclick="startScrcpy('right')">Right eye</button>
          <button class="secondary" onclick="startScrcpy('full')">Full stereo</button>
          <button class="danger" onclick="stopScrcpy()">Stop</button>
        </div>
      </section>
    </div>
    <section>
      <h2>Live Logs</h2>
      <div id="logs"></div>
    </section>
  </main>
  <script>
    const logs = document.getElementById('logs');
    function setText(id, value) { document.getElementById(id).textContent = value || '-'; }
    function logLine(text, cls='') {
      const div = document.createElement('div');
      div.className = 'log ' + cls;
      div.textContent = text;
      logs.appendChild(div);
      logs.scrollTop = logs.scrollHeight;
      while (logs.children.length > 600) logs.removeChild(logs.firstChild);
    }
    function updateStatus(s) {
      setText('active_student', s.active_student);
      setText('scenario', s.scenario);
      setText('step', `${s.current_step_index || 0}/${s.step_count || 0} ${s.current_step_label || ''}`);
      setText('expected', s.expected_event);
      setText('waiting', s.is_waiting ? 'yes' : 'no');
      setText('selected_aid', s.selected_aid);
      setText('held_aid', s.held_aid);
      setText('device', `${s.device_name || '-'} ${s.platform || ''}`);
      setText('status', s.status);
    }
    async function post(path, body={}) {
      const res = await fetch(path, {method:'POST', headers:{'content-type':'application/json'}, body: JSON.stringify(body)});
      const data = await res.json();
      if (!res.ok) logLine(JSON.stringify(data), 'error');
      return data;
    }
    async function startAsr() {
      const unity_host = document.getElementById('unityHost').value.trim();
      const unity_port = Number(document.getElementById('unityPort').value.trim() || 5055);
      await post('/api/asr/start', {unity_host, unity_port});
    }
    async function stopAsr() { await post('/api/asr/stop'); }
    async function adbDevices() {
      const data = await fetch('/api/quest/devices').then(r => r.json());
      logLine('[adb devices]\\n' + (data.output || ''), data.ok ? 'ok' : 'warn');
    }
    async function startScrcpy(view_mode='left') { await post('/api/quest/scrcpy/start', {view_mode}); }
    async function stopScrcpy() { await post('/api/quest/scrcpy/stop'); }
    const source = new EventSource('/events');
    source.onopen = () => setText('connection', 'connected');
    source.onerror = () => setText('connection', 'reconnecting...');
    source.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      if (msg.type === 'status') updateStatus(msg);
      else if (msg.type === 'workflow_log') logLine(`[Unity] ${msg.message}`, msg.level === 'warn' ? 'warn' : '');
      else if (msg.type === 'ignored_event') logLine(`[Ignored ${msg.intent || ''}] ${msg.message}`, 'warn');
      else if (msg.type === 'voice_intent') logLine(`[Voice] ${msg.intent || ''} text="${msg.text || ''}" student=${msg.student_id || ''}`);
      else if (msg.type === 'asr_intent') logLine(`[ASR Intent] ${msg.intent || ''} triggered=${msg.triggered} text="${msg.text || ''}" reason=${msg.reason || ''}`);
      else if (msg.type === 'asr_log') logLine(`[ASR] ${msg.message}`);
      else if (msg.type === 'process_log') logLine(`[${msg.process}] ${msg.message}`, msg.level || '');
      else logLine(JSON.stringify(msg));
    };
    fetch('/api/state').then(r => r.json()).then(data => {
      if (data.latest_status) updateStatus(data.latest_status);
      (data.logs || []).forEach(msg => logLine(JSON.stringify(msg)));
    });
  </script>
</body>
</html>
"""


class MonitorState:
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self.latest_status: dict[str, Any] | None = None
        self.logs: deque[dict[str, Any]] = deque(maxlen=1000)
        self.clients: list[queue.Queue[dict[str, Any]]] = []
        self.asr_process: subprocess.Popen[str] | None = None
        self.scrcpy_process: subprocess.Popen[str] | None = None

    def publish(self, message: dict[str, Any]) -> None:
        message.setdefault("received_at", time.time())
        if not message.get("type"):
            message["type"] = "voice_intent" if message.get("intent") else "event"

        with self.lock:
            if message.get("type") == "status":
                self.latest_status = message
            else:
                self.logs.append(message)

            for client in list(self.clients):
                try:
                    client.put_nowait(message)
                except queue.Full:
                    pass

    def add_client(self) -> queue.Queue[dict[str, Any]]:
        client: queue.Queue[dict[str, Any]] = queue.Queue(maxsize=200)
        with self.lock:
            self.clients.append(client)
        return client

    def remove_client(self, client: queue.Queue[dict[str, Any]]) -> None:
        with self.lock:
            if client in self.clients:
                self.clients.remove(client)


STATE = MonitorState()


def publish_process_log(process: str, message: str, level: str = "") -> None:
    STATE.publish({"type": "process_log", "process": process, "message": message.rstrip(), "level": level})


def udp_listener(host: str, port: int) -> None:
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((host, port))
    publish_process_log("monitor", f"UDP listener ready on {host or '0.0.0.0'}:{port}", "ok")

    while True:
        data, addr = sock.recvfrom(65535)
        try:
            message = json.loads(data.decode("utf-8"))
            message.setdefault("remote_addr", f"{addr[0]}:{addr[1]}")
            STATE.publish(message)
        except Exception as exc:
            publish_process_log("monitor", f"bad UDP packet from {addr}: {exc}", "warn")


def read_json(handler: BaseHTTPRequestHandler) -> dict[str, Any]:
    length = int(handler.headers.get("content-length", "0") or "0")
    if length <= 0:
        return {}
    return json.loads(handler.rfile.read(length).decode("utf-8"))


def json_response(handler: BaseHTTPRequestHandler, payload: dict[str, Any], status: int = 200) -> None:
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    handler.send_response(status)
    handler.send_header("content-type", "application/json; charset=utf-8")
    handler.send_header("content-length", str(len(body)))
    handler.end_headers()
    handler.wfile.write(body)


def stream_process_output(name: str, proc: subprocess.Popen[str]) -> None:
    assert proc.stdout is not None
    for line in proc.stdout:
        publish_process_log(name, line)
    code = proc.wait()
    publish_process_log(name, f"exited with code {code}", "warn" if code else "ok")
    with STATE.lock:
        if name == "asr" and STATE.asr_process is proc:
            STATE.asr_process = None
        if name == "scrcpy" and STATE.scrcpy_process is proc:
            STATE.scrcpy_process = None


def start_asr(payload: dict[str, Any]) -> dict[str, Any]:
    with STATE.lock:
        if STATE.asr_process is not None and STATE.asr_process.poll() is None:
            return {"ok": True, "message": "ASR already running"}

    unity_host = str(payload.get("unity_host") or "127.0.0.1")
    unity_port = str(int(payload.get("unity_port") or 5055))
    python_bin = str(DEFAULT_ASR_PYTHON if DEFAULT_ASR_PYTHON.exists() else sys.executable)
    args = [
        python_bin,
        "stream_test.py",
        "--unity-host",
        unity_host,
        "--unity-port",
        unity_port,
    ]

    proc = subprocess.Popen(
        args,
        cwd=str(ASR_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        env={**os.environ, "PYTHONUNBUFFERED": "1"},
    )
    with STATE.lock:
        STATE.asr_process = proc
    threading.Thread(target=stream_process_output, args=("asr", proc), daemon=True).start()
    publish_process_log("monitor", "started ASR: " + " ".join(args), "ok")
    return {"ok": True, "pid": proc.pid}


def stop_process(name: str) -> dict[str, Any]:
    attr = "asr_process" if name == "asr" else "scrcpy_process"
    with STATE.lock:
        proc = getattr(STATE, attr)
    if proc is None or proc.poll() is not None:
        return {"ok": True, "message": f"{name} is not running"}
    proc.terminate()
    publish_process_log("monitor", f"terminating {name} pid={proc.pid}", "warn")
    return {"ok": True}


def adb_devices() -> dict[str, Any]:
    adb = shutil.which("adb")
    if not adb:
        return {"ok": False, "output": "adb not found. Install Android platform tools or Meta Quest Developer Hub."}
    result = subprocess.run([adb, "devices"], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    return {"ok": result.returncode == 0, "output": result.stdout}


def start_scrcpy(payload: dict[str, Any] | None = None) -> dict[str, Any]:
    with STATE.lock:
        if STATE.scrcpy_process is not None and STATE.scrcpy_process.poll() is None:
            return {"ok": True, "message": "scrcpy already running"}

    scrcpy = shutil.which("scrcpy")
    if not scrcpy:
        message = "scrcpy not found. On macOS: brew install scrcpy android-platform-tools"
        publish_process_log("scrcpy", message, "warn")
        return {"ok": False, "message": message}

    payload = payload or {}
    view_mode = str(payload.get("view_mode") or "left").lower()
    args = [
        scrcpy,
        "--max-size",
        "1280",
        "--video-bit-rate",
        "8M",
        "--stay-awake",
        "--audio-source=playback",
        "--audio-dup",
    ]
    if view_mode == "left":
        args.append("--crop=2064:2208:0:0")
    elif view_mode == "right":
        args.append("--crop=2064:2208:2064:0")

    proc = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
    with STATE.lock:
        STATE.scrcpy_process = proc
    threading.Thread(target=stream_process_output, args=("scrcpy", proc), daemon=True).start()
    publish_process_log("monitor", "started scrcpy: " + " ".join(args), "ok")
    return {"ok": True, "pid": proc.pid}


class Handler(BaseHTTPRequestHandler):
    server_version = "DTTMonitor/0.1"

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == "/":
            body = INDEX_HTML.encode("utf-8")
            self.send_response(200)
            self.send_header("content-type", "text/html; charset=utf-8")
            self.send_header("content-length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return

        if parsed.path == "/events":
            self.send_response(200)
            self.send_header("content-type", "text/event-stream")
            self.send_header("cache-control", "no-cache")
            self.send_header("connection", "keep-alive")
            self.end_headers()
            client = STATE.add_client()
            try:
                while True:
                    message = client.get()
                    data = json.dumps(message, ensure_ascii=False)
                    self.wfile.write(f"data: {data}\n\n".encode("utf-8"))
                    self.wfile.flush()
            except (BrokenPipeError, ConnectionResetError):
                pass
            finally:
                STATE.remove_client(client)
            return

        if parsed.path == "/api/state":
            with STATE.lock:
                payload = {
                    "latest_status": STATE.latest_status,
                    "logs": list(STATE.logs)[-200:],
                    "asr_running": STATE.asr_process is not None and STATE.asr_process.poll() is None,
                    "scrcpy_running": STATE.scrcpy_process is not None and STATE.scrcpy_process.poll() is None,
                }
            json_response(self, payload)
            return

        if parsed.path == "/api/quest/devices":
            json_response(self, adb_devices())
            return

        json_response(self, {"ok": False, "error": "not found"}, 404)

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        query = parse_qs(parsed.query)
        try:
            payload = read_json(self)
            if query:
                payload.update({key: value[-1] for key, value in query.items()})

            if parsed.path == "/api/asr/start":
                json_response(self, start_asr(payload))
                return
            if parsed.path == "/api/asr/stop":
                json_response(self, stop_process("asr"))
                return
            if parsed.path == "/api/quest/scrcpy/start":
                json_response(self, start_scrcpy(payload))
                return
            if parsed.path == "/api/quest/scrcpy/stop":
                json_response(self, stop_process("scrcpy"))
                return

            json_response(self, {"ok": False, "error": "not found"}, 404)
        except Exception as exc:
            publish_process_log("monitor", f"request failed: {exc}", "error")
            json_response(self, {"ok": False, "error": str(exc)}, 500)

    def log_message(self, fmt: str, *args: Any) -> None:
        publish_process_log("http", fmt % args)


def main() -> None:
    parser = argparse.ArgumentParser(description="DTT desktop monitor server")
    parser.add_argument("--http-host", default="127.0.0.1")
    parser.add_argument("--http-port", type=int, default=8088)
    parser.add_argument("--udp-host", default="")
    parser.add_argument("--udp-port", type=int, default=5060)
    args = parser.parse_args()

    threading.Thread(target=udp_listener, args=(args.udp_host, args.udp_port), daemon=True).start()
    server = ThreadingHTTPServer((args.http_host, args.http_port), Handler)
    print(f"DTT monitor dashboard: http://{args.http_host}:{args.http_port}")
    print(f"Listening for Unity monitor UDP on port {args.udp_port}")
    server.serve_forever()


if __name__ == "__main__":
    main()
