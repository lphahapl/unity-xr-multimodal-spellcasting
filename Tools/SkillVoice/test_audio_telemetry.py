"""Scalar-only input diagnostics; no microphone, model, or production UDP ports."""
import json
import unittest
import numpy as np
from skill_voice import AudioTelemetry


class AudioTelemetryTests(unittest.TestCase):
    def setUp(self):
        self.now = 10.0
        self.meter = AudioTelemetry(lambda: self.now)
        self.meter.configure("Steam microphone", "Windows WDM-KS", 64, 16000, .012)

    def test_no_audio_is_unknown_not_success(self):
        p = self.meter.snapshot()
        self.assertEqual(p["audio_age"], -1)
        self.assertEqual(p["audio_frames"], 0)
        self.assertFalse(p["audio_score_available"])
        self.meter.observe([])
        self.assertEqual(self.meter.snapshot()["audio_age"], -1)

    def test_silent_samples_prove_stream_not_voice(self):
        self.meter.observe(np.zeros(320))
        self.now += .5
        p = self.meter.snapshot()
        self.assertEqual(p["audio_age"], .5)
        self.assertEqual(p["audio_rms"], 0)
        self.assertEqual(p["audio_voiced_frames"], 0)

    def test_signal_and_phrase_are_distinct(self):
        self.meter.observe([.5, -.5])
        p = self.meter.snapshot()
        self.assertEqual(p["audio_rms"], .5)
        self.assertEqual(p["audio_peak"], .5)
        self.assertEqual(p["audio_phrase_count"], 0)
        self.assertEqual(p["audio_voiced_frames"], 1)
        self.meter.phrase("火球术")
        self.assertEqual(self.meter.snapshot()["audio_last_phrase"], "火球术")

    def test_queued_input_retains_capture_age_for_ducking(self):
        self.meter.observe([.25, -.25], captured_at=self.now - 1.5)
        p = self.meter.snapshot()
        self.assertEqual(p["audio_snapshot_at"], self.now)
        self.assertEqual(p["audio_voice_age"], 1.5)
        self.assertEqual(p["audio_age"], 1.5)

    def test_invalid_audio_rejected_and_diagnostics_finite(self):
        for value in [float("nan"), float("inf"), -float("inf")]:
            with self.assertRaises(ValueError):
                self.meter.observe([value])
        self.meter.observe([1e300, -1e300])
        p = self.meter.snapshot()
        self.assertEqual(p["audio_peak"], 1)
        json.dumps(p, allow_nan=False)

    def test_logging_throttled_and_payload_bounded(self):
        self.assertTrue(self.meter.log_due())
        for i in range(1, 250):
            self.now = 10 + i * .02
            self.assertFalse(self.meter.log_due())
        self.now = 15
        self.assertTrue(self.meter.log_due())
        self.meter.configure("麦"*10000, "后端"*10000, 1, 16000, .012)
        self.meter.phrase("火"*10000)
        self.assertLess(len(json.dumps(self.meter.snapshot(), ensure_ascii=False).encode()), 2000)


if __name__ == "__main__":
    unittest.main()
