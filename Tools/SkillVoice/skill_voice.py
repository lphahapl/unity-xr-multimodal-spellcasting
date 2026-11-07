"""Local Chinese KWS. Audio stays in this process; loopback UDP carries events only."""
from __future__ import annotations

import argparse
import json
import math
import queue
import socket
import sys
import threading
import time
import uuid
import wave
from pathlib import Path
from configuration import load_configuration, materialize_keywords, normalize_phrase, read_keyword_file

ROOT = Path(__file__).resolve().parent
MODEL = ROOT / "models/sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20"
# Historical fixtures may import this constant; live vocabulary comes from configuration.
PHRASES = ("护体咒", "火球术", "取消施法")


def log(kind, **data):
    print(json.dumps({"kind": kind, "monotonic": time.perf_counter(), **data}, ensure_ascii=False), flush=True)


class AudioTelemetry:
    """Bounded scalar diagnostics only. Never stores audio; clock is injectable for tests."""

    def __init__(self, clock=time.perf_counter):
        self.clock = clock
        self.lock = threading.Lock()
        self.device = {}
        self.frames = self.voiced_frames = 0
        self.last_frame = self.last_voice = None
        self.rms = self.peak = 0.0
        self.last_log = None
        self.last_phrase = ""
        self.phrase_count = 0

    def configure(self, name, backend, index, sample_rate, threshold):
        with self.lock:
            self.device = dict(audio_device=str(name)[:160], audio_backend=str(backend)[:80],
                               audio_device_index=int(index), audio_sample_rate=int(sample_rate),
                               audio_energy_threshold=float(threshold))

    def observe(self, samples, captured_at=None):
        import numpy as np
        values = np.asarray(samples, dtype=np.float64)
        if not values.size:
            return
        if not np.isfinite(values).all():
            raise ValueError("麦克风样本含非有限数值；停止识别以避免无效输入")
        # Float PCM should be in [-1,1]; bound diagnostic arithmetic without changing decoder input.
        values = np.clip(values, -1.0, 1.0)
        now = self.clock()
        # Retain the original ADC/callback clock even if decoding has accumulated a backlog.
        frame_end = now if captured_at is None else min(now, captured_at)
        if captured_at is not None and not math.isfinite(captured_at):
            raise ValueError("麦克风样本时间无效")
        with self.lock:
            self.frames += 1
            self.last_frame = frame_end
            self.rms = float(np.sqrt(np.mean(values * values)))
            self.peak = float(np.max(np.abs(values)))
            if self.rms >= self.device.get("audio_energy_threshold", .012):
                self.voiced_frames += 1
                self.last_voice = frame_end

    def phrase(self, phrase):
        with self.lock:
            self.last_phrase = str(phrase)[:80]
            self.phrase_count += 1

    def snapshot(self):
        now = self.clock()
        with self.lock:
            return dict(audio_telemetry_version=1, audio_snapshot_at=now, **self.device,
                        audio_frames=self.frames, audio_voiced_frames=self.voiced_frames,
                        audio_age=-1.0 if self.last_frame is None else max(0.0, now-self.last_frame),
                        audio_voice_age=-1.0 if self.last_voice is None else max(0.0, now-self.last_voice),
                        audio_rms=self.rms, audio_peak=self.peak,
                        audio_last_phrase=self.last_phrase, audio_phrase_count=self.phrase_count,
                        audio_score_available=False)

    def log_due(self, interval=5.0):
        now = self.clock()
        with self.lock:
            if self.last_log is not None and now-self.last_log < interval:
                return False
            self.last_log = now
            return True


class Bridge:
    """NTP-style four-timestamp handshake. No wall-clock synchronization required."""

    def __init__(self, port=47832, unity_port=47831):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        # Exclusive bind by default on Windows; prevents accidental second services.
        if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        self.socket.bind(("127.0.0.1", port))
        self.socket.settimeout(0.1)
        self.target = ("127.0.0.1", unity_port)
        self.session = uuid.uuid4().hex
        self.ready = False
        self.status = "正在加载模型"
        self.mode = "starting"
        self.seq = 0
        self.telemetry = AudioTelemetry()
        self.closed = threading.Event()
        self.thread = threading.Thread(target=self.run, name="SkillVoiceLoopback", daemon=True)
        self.thread.start()

    def send(self, kind, destination=None, **data):
        diagnostics = self.telemetry.snapshot() if kind in ("heartbeat", "pong") and self.mode == "microphone" else {}
        payload = {"v": 1, "kind": kind, "session": self.session, "ready": self.ready,
                   "status": self.status, "mode": self.mode, **diagnostics, **data}
        try:
            self.socket.sendto(json.dumps(payload, ensure_ascii=False).encode("utf-8"), destination or self.target)
        except OSError:
            pass

    def run(self):
        last_beat = 0.0
        while not self.closed.is_set():
            try:
                packet, address = self.socket.recvfrom(4096)
                received = time.perf_counter()
                if address[0] != "127.0.0.1":
                    continue
                data = json.loads(packet)
                sent = float(data.get("client_send", 0))
                nonce = int(data.get("nonce", -1))
                if data.get("v") == 1 and data.get("kind") == "ping" and math.isfinite(sent) and nonce >= 0:
                    self.send("pong", destination=address, nonce=nonce, client_send=sent,
                              server_recv=received, server_send=time.perf_counter())
            except (socket.timeout, ConnectionResetError, UnicodeError, ValueError, TypeError, AttributeError):
                pass
            except OSError:
                if self.closed.is_set():
                    break
            if time.perf_counter() - last_beat > (0.1 if self.mode == "microphone" else 0.5):
                last_beat = time.perf_counter()
                self.send("heartbeat")

    def phrase(self, phrase, ended_at, detected_at, basis, **data):
        self.seq += 1
        self.telemetry.phrase(phrase)
        now = time.perf_counter()
        record = dict(phrase=phrase, seq=self.seq, ended_at=ended_at, detected_at=detected_at,
                      sent_at=now, capture_basis=basis,
                      end_method="synthetic-test" if self.mode == "protocol-test" else "rms-frame-end-estimate",
                      processing_latency_ms=(now - ended_at) * 1000, **data)
        self.send("phrase", **record)
        log("phrase", mode=self.mode, **record)

    def close(self):
        self.ready = False
        self.status = "语音服务已停止"
        self.send("heartbeat")
        self.closed.set()
        self.thread.join(timeout=0.5)
        self.socket.close()


def make_spotter(args):
    import sherpa_onnx
    stem = "epoch-13-avg-2-chunk-8-left-64"
    files = {
        "encoder": args.model / f"encoder-{stem}.int8.onnx",
        "decoder": args.model / f"decoder-{stem}.onnx",
        "joiner": args.model / f"joiner-{stem}.int8.onnx",
        "tokens": args.model / "tokens.txt",
        "keywords_file": args.keywords,
    }
    for path in files.values():
        if not path.is_file():
            raise FileNotFoundError(f"缺少模型/词表 {path}；先运行 Setup.ps1")
    tokens = {line.split()[0] for line in files["tokens"].read_text(encoding="utf-8").splitlines() if line.strip()}
    for keyword in read_keyword_file(args.keywords):
        for token in keyword.tokens.split():
            if token not in tokens:
                raise ValueError(f"词表中有模型不支持的拼音 token: {token}")
    return sherpa_onnx.KeywordSpotter(**{k: str(v) for k, v in files.items()}, num_threads=2,
                                    keywords_threshold=args.threshold, keywords_score=args.score,
                                    num_trailing_blanks=1, provider="cpu")


class Decoder:
    def __init__(self, spotter, args, bridge=None):
        self.kws, self.args, self.bridge = spotter, args, bridge
        self.stream = spotter.create_stream()
        self.origin = None
        self.last_voiced_end = None
        self.pending = None
        self.hits = []
        self.last_emitted = {}

    def emit(self, frame_end, basis, force=False):
        if self.pending is None:
            return
        phrase, detection, audio_detection, token_time = self.pending
        voiced_end = self.last_voiced_end if self.last_voiced_end is not None else audio_detection
        if not force and frame_end - voiced_end < self.args.end_silence and frame_end - audio_detection < 0.8:
            return
        self.pending = None
        if frame_end - self.last_emitted.get(phrase, -1e20) < self.args.debounce:
            return
        self.last_emitted[phrase] = frame_end
        # Energy segmentation is only an estimate; all packets declare the method.
        ended = min(frame_end, voiced_end)
        self.hits.append(phrase)
        if self.bridge:
            self.bridge.phrase(phrase, ended, detection, basis, token_audio_seconds=token_time)
        else:
            log("offline_phrase", phrase=phrase, audio_end_seconds=ended,
                token_audio_seconds=token_time, end_method="rms-frame-end-estimate")

    def accept(self, samples, sample_rate, first_sample_time, basis):
        import numpy as np
        if self.origin is None:
            self.origin = first_sample_time
        frame_end = first_sample_time + len(samples) / sample_rate
        rms = float(np.sqrt(np.mean(samples * samples))) if len(samples) else 0
        if rms >= self.args.energy_threshold:
            self.last_voiced_end = frame_end
        self.stream.accept_waveform(sample_rate, samples)
        while self.kws.is_ready(self.stream):
            self.kws.decode_stream(self.stream)
            # Fetch once: the native result is consumed, so a second timestamps()
            # call after get_result() can return an empty result in sherpa 2.x.
            result = self.kws.keyword_spotter.get_result(self.stream)
            phrase = normalize_phrase(result.keyword)
            if phrase:
                timestamps = result.timestamps
                token_time = timestamps[-1] if timestamps else -1
                self.emit(frame_end, basis, force=True)
                self.pending = (phrase, time.perf_counter(), frame_end, token_time)
                # Preserve acoustic context and buffered audio as the official example does.
                self.kws.reset_stream(self.stream)
        self.emit(frame_end, basis)


def read_wav(path):
    import numpy as np
    with wave.open(str(path), "rb") as wav:
        if wav.getsampwidth() != 2 or wav.getcomptype() != "NONE":
            raise ValueError("--wav 需要 PCM 16-bit WAV（可单/双声道）")
        rate = wav.getframerate()
        channels = wav.getnchannels()
        values = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype("float32") / 32768
        if channels > 1:
            values = values.reshape(-1, channels).mean(axis=1)
    return values, rate


def run_wav(args, kws):
    import numpy as np
    audio, rate = read_wav(args.wav)
    audio = np.concatenate((audio, np.zeros(rate, dtype="float32")))
    bridge = Bridge(args.port, args.unity_port) if args.realtime else None
    try:
        if bridge:
            bridge.ready, bridge.mode, bridge.status = True, "wav-replay", "WAV 回放测试（不是实时麦克风）"
            time.sleep(1.2)  # Unity has time to perform its clock handshake.
        decoder = Decoder(kws, args, bridge)
        origin = time.perf_counter() if bridge else 0.0
        block = int(rate * 0.02)
        for i in range(0, len(audio), block):
            samples = audio[i:i + block]
            if bridge:
                deadline = origin + (i + len(samples)) / rate
                time.sleep(max(0.0, deadline - time.perf_counter()))
            decoder.accept(samples, rate, origin + i / rate, "wav-replay-clock" if bridge else "wav-sample-offset")
        decoder.emit(origin + len(audio) / rate, "wav-replay-clock" if bridge else "wav-sample-offset", force=True)
        log("wav_complete", file=str(args.wav), realtime=args.realtime, detected=decoder.hits,
            note="录音/合成音的离线可重放测试；不能代表真人麦克风准确率")
        if args.expect and decoder.hits != args.expect:
            raise RuntimeError(f"Expected {args.expect}, got {decoder.hits}")
    finally:
        if bridge:
            bridge.close()


def run_microphone(args, kws):
    import sounddevice as sd
    frames = queue.Queue(maxsize=100)
    dropped = threading.Event()

    def callback(indata, frame_count, timing, status):
        now = time.perf_counter()
        # PortAudio ADC timestamp and currentTime share a clock; only their difference is used.
        adc_age = timing.currentTime - timing.inputBufferAdcTime
        if timing.inputBufferAdcTime > 0 and 0 <= adc_age < 2:
            start = now - adc_age
            basis = "portaudio-adc-to-monotonic"
        else:
            start = now - frame_count / args.sample_rate
            basis = "callback-time-estimate-no-valid-adc"
        if status:
            dropped.set()
        try:
            frames.put_nowait((indata[:, 0].copy(), start, basis))
        except queue.Full:
            dropped.set()

    bridge = Bridge(args.port, args.unity_port)
    try:
        decoder = Decoder(kws, args, bridge)
        with sd.InputStream(device=args.device, channels=1, dtype="float32", samplerate=args.sample_rate,
                            blocksize=int(args.sample_rate * 0.02), callback=callback) as input_stream:
            device = sd.query_devices(input_stream.device, "input")
            backend = sd.query_hostapis(device["hostapi"])["name"]
            bridge.telemetry.configure(device["name"], backend, input_stream.device,
                                       input_stream.samplerate, args.energy_threshold)
            bridge.ready, bridge.mode, bridge.status = True, "microphone", "中文口令识别中（本机）"
            log("ready", model=str(args.model), device=str(sd.query_devices(args.device, "input")),
                threshold=args.threshold, energy_threshold=args.energy_threshold,
                phrases=list(args.phrases), udp="127.0.0.1", audio_recorded=False)
            started = time.perf_counter()
            while not args.duration or time.perf_counter() - started < args.duration:
                try:
                    samples, start, basis = frames.get(timeout=1)
                except queue.Empty:
                    raise RuntimeError("麦克风 1 秒内没有送达音频；检查 Steam Streaming Microphone / Windows 输入权限")
                if dropped.is_set():
                    # A discontinuity invalidates timestamps and acoustic context. Fail visibly.
                    raise RuntimeError("麦克风音频溢出/丢帧，已停止；关闭高负载应用或换输入设备后重启")
                bridge.telemetry.observe(samples, captured_at=start + len(samples) / args.sample_rate)
                if bridge.telemetry.log_due():
                    log("audio_input", **bridge.telemetry.snapshot(), audio_recorded=False)
                decoder.accept(samples, args.sample_rate, start, basis)
    finally:
        bridge.close()


def self_test(args):
    import numpy as np
    kws = make_spotter(args)
    decoder = Decoder(kws, args)
    for i in range(100):
        decoder.accept(np.zeros(320, dtype="float32"), 16000, i * .02, "test-silence")
    assert decoder.hits == [], "Silence must not produce a keyword"
    protocol_self_test(model_checked=True, phrase=args.phrases[0])


def protocol_self_test(model_checked=False, phrase="协议测试口令"):
    # Uses ephemeral loopback ports. Never injects simulated phrases into Unity.
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as client:
        client.bind(("127.0.0.1", 0))
        client.settimeout(2)
        bridge = Bridge(port=0, unity_port=client.getsockname()[1])
        try:
            bridge.mode = "protocol-test"
            bridge.status = "独立临时端口协议自测（未运行语音识别）"
            sent = time.perf_counter()
            client.sendto(json.dumps(dict(v=1, kind="ping", nonce=7, client_send=sent)).encode(), bridge.socket.getsockname())
            while True:
                reply = json.loads(client.recv(4096))
                received = time.perf_counter()
                if reply["kind"] == "pong":
                    break
            rtt = (received - sent) - (reply["server_send"] - reply["server_recv"])
            offset = ((sent + received) - (reply["server_recv"] + reply["server_send"])) / 2
            assert reply["nonce"] == 7 and abs(offset) < .1 and 0 <= rtt < .5
            bridge.phrase(phrase, time.perf_counter() - .2, time.perf_counter() - .1, "self-test-only")
            while True:
                packet = json.loads(client.recv(4096))
                if packet["kind"] == "phrase":
                    break
            assert packet["phrase"] == phrase and packet["processing_latency_ms"] >= 190
            log("self_test_pass" if model_checked else "protocol_self_test_pass",
                model_load=model_checked, keyword_tokens=model_checked, silence_no_false_hit=model_checked,
                loopback_utf8=True, handshake_rtt_ms=rtt * 1000, offset_ms=offset * 1000,
                phrase_latency_ms=packet["processing_latency_ms"], human_accuracy_tested=False)
        finally:
            bridge.close()


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, default=MODEL)
    parser.add_argument("--config", type=Path, help="Unity 导出的 JSON；默认读取本目录 skill-voice-config.json")
    parser.add_argument("--keywords", type=Path, help="显式词表覆盖 JSON 口令，用于离线实验")
    parser.add_argument("--check-config", action="store_true", help="仅校验并显示实际词表/端口，不打开麦克风或网络")
    parser.add_argument("--list-devices", action="store_true")
    parser.add_argument("--device", type=int, help="sounddevice --list-devices input ID")
    parser.add_argument("--sample-rate", type=int, default=16000)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--protocol-self-test", action="store_true", help="Only tests loopback UTF-8/handshake/timing, no model or microphone")
    parser.add_argument("--test-event", help="配置中的口令或 all；显式模拟 UDP 接线事件，不运行语音识别")
    parser.add_argument("--wav", type=Path)
    parser.add_argument("--realtime", action="store_true", help="Replay WAV on a real-time clock and send events to Unity")
    parser.add_argument("--expect", nargs="+", help="Expected ordered phrases for repeatable WAV verification")
    parser.add_argument("--duration", type=float, default=0, help="Microphone run seconds, 0 means continuous")
    parser.add_argument("--threshold", type=float, default=.25)
    parser.add_argument("--score", type=float, default=1.0)
    parser.add_argument("--energy-threshold", type=float, default=.012)
    parser.add_argument("--end-silence", type=float, default=.12)
    parser.add_argument("--debounce", type=float, default=.6)
    parser.add_argument("--port", type=int, help="覆盖配置中的语音服务端口")
    parser.add_argument("--unity-port", type=int, help="覆盖配置中的 Unity 接收端口")
    args = parser.parse_args()
    if args.list_devices:
        import sounddevice as sd
        print(sd.query_devices())
        return
    if args.protocol_self_test:
        protocol_self_test()
        return
    config = load_configuration(ROOT, config_path=args.config, keywords_path=args.keywords,
                                unity_port=args.unity_port, service_port=args.port)
    args.port, args.unity_port, args.phrases = config.service_port, config.unity_port, config.phrases
    log("configuration", source=config.source, phrases=list(config.phrases), cancel_phrase=config.cancel_phrase,
        service_port=config.service_port, unity_port=config.unity_port)
    if args.test_event and args.test_event != "all":
        args.test_event = normalize_phrase(args.test_event)
        if args.test_event not in args.phrases:
            raise ValueError("模拟口令不在本次有效词表中：" + args.test_event)
    if args.check_config:
        return
    with materialize_keywords(config) as keywords:
        args.keywords = keywords
        execute(args)


def execute(args):
    if args.self_test:
        self_test(args)
        return
    if args.test_event:
        bridge = Bridge(args.port, args.unity_port)
        try:
            bridge.ready, bridge.mode, bridge.status = True, "protocol-test", "模拟接线测试（未运行语音识别）"
            time.sleep(1.5)
            for phrase in args.phrases if args.test_event == "all" else [args.test_event]:
                now = time.perf_counter()
                bridge.phrase(phrase, now - .2, now - .1, "synthetic-test-clock")
                time.sleep(1)
            time.sleep(1)
        finally:
            bridge.close()
        return
    kws = make_spotter(args)
    if args.wav:
        run_wav(args, kws)
    else:
        run_microphone(args, kws)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log("stopped", reason="用户终止")
    except Exception as exc:
        log("error", error=str(exc), ready=False)
        sys.exit(1)
