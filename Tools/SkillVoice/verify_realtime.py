"""Run real WAV inference over actual loopback UDP with a receiver clock epoch offset."""
import json
import os
import socket
import subprocess
import sys
import time

from skill_voice import ROOT


def main():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reservation:
        reservation.bind(("127.0.0.1", 0))
        service_port = reservation.getsockname()[1]
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as receiver:
        receiver.bind(("127.0.0.1", 0))
        receiver.settimeout(.2)
        receiver_port = receiver.getsockname()[1]
        flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        with (ROOT / "logs/realtime-service.jsonl").open("w", encoding="utf-8") as out:
            process = subprocess.Popen([sys.executable, "-X", "utf8", str(ROOT / "skill_voice.py"),
                                        "--wav", str(ROOT / "fixtures/shield.wav"), "--realtime",
                                        "--port", str(service_port), "--unity-port", str(receiver_port)],
                                       stdout=out, stderr=subprocess.STDOUT, creationflags=flags)
            epoch_offset = 123.0
            nonce = 0
            mapping = None
            last_ping = 0
            pings = {}
            result = None
            started = time.perf_counter()
            try:
                while process.poll() is None and time.perf_counter() - started < 15:
                    now = time.perf_counter() + epoch_offset
                    if now - last_ping > .2:
                        nonce += 1
                        pings[nonce] = last_ping = now
                        receiver.sendto(json.dumps(dict(v=1, kind="ping", nonce=nonce, client_send=now)).encode(),
                                        ("127.0.0.1", service_port))
                    try:
                        packet = json.loads(receiver.recv(4096))
                    except (socket.timeout, ConnectionResetError):
                        continue
                    now = time.perf_counter() + epoch_offset
                    if packet["kind"] == "pong":
                        t1 = pings[packet["nonce"]]
                        mapping = ((t1 + now) - (packet["server_recv"] + packet["server_send"])) / 2
                    if packet["kind"] == "phrase":
                        assert mapping is not None and packet["mode"] == "wav-replay"
                        age = now - (packet["ended_at"] + mapping)
                        assert packet["phrase"] == "护体咒" and 0 <= age < 1
                        result = {"passed": True, "actual_kws": True, "source": "Windows TTS WAV",
                                  "phrase": packet["phrase"], "epoch_offset_seconds": epoch_offset,
                                  "measured_offset_seconds": mapping, "mapped_end_to_receive_ms": age * 1000,
                                  "service_processing_latency_ms": packet["processing_latency_ms"],
                                  "human_accuracy_tested": False, "unity_play_test": False}
                code = process.wait(timeout=3)
                assert code == 0 and result is not None, f"No valid live event or exit={code}"
                (ROOT / "logs/realtime-report.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
                print(json.dumps(result, ensure_ascii=False, indent=2))
            finally:
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=3)


if __name__ == "__main__":
    main()
