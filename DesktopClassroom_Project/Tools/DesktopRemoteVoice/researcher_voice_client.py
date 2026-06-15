#!/usr/bin/env python3
"""
Researcher-side remote voice client for the desktop classroom Unity build.

The script captures a microphone or virtual microphone, sends mono PCM chunks to
Unity over UDP, and can also trigger simple student behaviors.
"""

import argparse
import base64
import json
import socket
import sys
import time

try:
    import numpy as np
    import sounddevice as sd
except ImportError as exc:
    print("Missing Python dependency:", exc)
    print("Install dependencies with:")
    print("  python3 -m pip install sounddevice numpy")
    sys.exit(2)


class ResearcherVoiceClient:
    def __init__(self, host, port, sample_rate, chunk_ms, device, gain):
        self.target = (host, port)
        self.sample_rate = sample_rate
        self.chunk_ms = chunk_ms
        self.device = device
        self.gain = gain
        self.student_index = 1
        self.sequence = 0
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.stream = None
        self.recording = False

    def close(self):
        self.stop_recording()
        self.sock.close()

    def set_student(self, index):
        if index < 1:
            print("Student index must be 1 or greater.")
            return

        was_recording = self.recording
        if was_recording:
            self.stop_recording()

        self.student_index = index
        print(f"Selected student {self.student_index}.")

        if was_recording:
            self.start_recording()

    def start_recording(self):
        if self.recording:
            print("Already recording.")
            return

        self.sequence = 0
        self.send_control("voice_start")
        blocksize = max(80, int(self.sample_rate * self.chunk_ms / 1000))

        self.stream = sd.InputStream(
            samplerate=self.sample_rate,
            blocksize=blocksize,
            device=self.device,
            channels=1,
            dtype="float32",
            latency="low",
            callback=self._on_audio,
        )
        self.stream.start()
        self.recording = True
        print(f"Recording for student {self.student_index}. Type 'r' again to stop.")

    def stop_recording(self):
        if not self.recording and self.stream is None:
            return

        if self.stream is not None:
            try:
                self.stream.stop()
                self.stream.close()
            finally:
                self.stream = None

        self.recording = False
        self.send_control("voice_stop")
        print("Recording stopped.")

    def toggle_recording(self):
        if self.recording:
            self.stop_recording()
        else:
            self.start_recording()

    def send_behavior(self, behavior, duration):
        packet = {
            "type": "behavior",
            "studentIndex": self.student_index,
            "behavior": behavior,
            "duration": float(duration),
            "timestamp": time.time(),
        }
        self._send(packet)
        print(f"Sent behavior '{behavior}' to student {self.student_index}.")

    def send_control(self, packet_type):
        packet = {
            "type": packet_type,
            "studentIndex": self.student_index,
            "sampleRate": self.sample_rate,
            "channels": 1,
            "sequence": self.sequence,
            "gain": self.gain,
            "timestamp": time.time(),
        }
        self._send(packet)

    def _on_audio(self, indata, frames, time_info, status):
        if status:
            print(f"Audio status: {status}", file=sys.stderr)

        if not self.recording:
            return

        mono = np.asarray(indata[:, 0], dtype=np.float32)
        mono = np.clip(mono * self.gain, -1.0, 1.0)
        pcm16 = (mono * 32767.0).astype("<i2", copy=False)
        payload = base64.b64encode(pcm16.tobytes()).decode("ascii")

        packet = {
            "type": "voice_chunk",
            "studentIndex": self.student_index,
            "sampleRate": self.sample_rate,
            "channels": 1,
            "sequence": self.sequence,
            "payloadBase64": payload,
            "gain": 1.0,
            "timestamp": time.time(),
        }
        self.sequence += 1
        self._send(packet)

    def _send(self, packet):
        raw = json.dumps(packet, separators=(",", ":")).encode("utf-8")
        self.sock.sendto(raw, self.target)


def print_devices():
    print(sd.query_devices())


def print_help():
    print("")
    print("Commands:")
    print("  1 / 2 / 3          select student")
    print("  r                  toggle recording for selected student")
    print("  b NAME [SECONDS]   send behavior, e.g. b raise_hand 5")
    print("  devices            print audio input/output devices")
    print("  help               show this help")
    print("  q                  quit")
    print("")
    print("Behavior names:")
    print("  raise_hand, ask_question, distracted, talk, scream, hit_desk")
    print("  lie_down, leave_seat, recover, stop")
    print("")


def parse_args():
    parser = argparse.ArgumentParser(description="Remote voice client for Unity desktop classroom.")
    parser.add_argument("--host", default="127.0.0.1", help="Unity desktop client IP or hostname.")
    parser.add_argument("--port", type=int, default=5066, help="Unity UDP voice receiver port.")
    parser.add_argument("--student", type=int, default=1, help="Initial student index, 1-based.")
    parser.add_argument("--sample-rate", type=int, default=16000, help="Microphone capture sample rate.")
    parser.add_argument("--chunk-ms", type=int, default=20, help="Audio packet duration in milliseconds.")
    parser.add_argument("--device", default=None, help="Input device index/name. Use --list-devices first.")
    parser.add_argument("--gain", type=float, default=1.0, help="Researcher-side microphone gain.")
    parser.add_argument("--list-devices", action="store_true", help="List audio devices and exit.")
    return parser.parse_args()


def main():
    args = parse_args()
    if args.list_devices:
        print_devices()
        return

    client = ResearcherVoiceClient(
        host=args.host,
        port=args.port,
        sample_rate=args.sample_rate,
        chunk_ms=args.chunk_ms,
        device=args.device,
        gain=args.gain,
    )
    client.set_student(args.student)

    print(f"Sending to Unity at {args.host}:{args.port}.")
    print("Use a voice changer by selecting its virtual microphone with --device.")
    print_help()

    try:
        while True:
            command = input("remote> ").strip()
            if not command:
                continue

            lower = command.lower()
            if lower in ("q", "quit", "exit"):
                break
            if lower == "r":
                client.toggle_recording()
                continue
            if lower in ("1", "2", "3", "4", "5", "6", "7", "8", "9"):
                client.set_student(int(lower))
                continue
            if lower == "devices":
                print_devices()
                continue
            if lower == "help":
                print_help()
                continue
            if lower.startswith("b "):
                parts = command.split()
                behavior = parts[1]
                duration = float(parts[2]) if len(parts) >= 3 else 3.0
                client.send_behavior(behavior, duration)
                continue

            print("Unknown command. Type 'help'.")
    except KeyboardInterrupt:
        print("")
    finally:
        client.close()


if __name__ == "__main__":
    main()
