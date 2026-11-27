"""Render an original sleeve brush and passing air; all excitation is seeded noise."""
from pathlib import Path
import wave

import numpy as np
from scipy import signal


RATE = 48_000
TIME = np.arange(round(0.46 * RATE)) / RATE
OUTPUT = Path(__file__).resolve().parents[3] / "Assets/Resources/Audio"


def band_noise(rng, low, high):
    band = signal.butter(3, [low, high], btype="bandpass", fs=RATE, output="sos")
    samples = signal.sosfilt(band, rng.standard_normal(TIME.size))
    return samples / np.std(samples)


def sweep(peak, approach, retreat):
    """A passing turbulent stream has a short leading edge and longer vanishing wake."""
    width = np.where(TIME < peak, approach, retreat)
    return np.exp(-0.5 * ((TIME - peak) / width) ** 2)


def sleeve(rng):
    # Several strands catch and release at slightly different times. Vary the
    # friction rather than pitching short impulses into a percussive click.
    knots = np.arange(0, TIME[-1] + 0.004, 0.0031)
    roughness = np.interp(TIME, knots, rng.uniform(0.42, 1.16, len(knots)))
    threads = (0.68 * band_noise(rng, 590, 1930) +
               0.32 * band_noise(rng, 2150, 4120))
    folds = (0.70 * sweep(0.026, 0.009, 0.012) +
             0.92 * sweep(0.054, 0.008, 0.021) +
             0.37 * sweep(0.085, 0.009, 0.018))
    return roughness * threads * folds


def perfect_dodge():
    rng = np.random.default_rng(20261126)
    cloth = sleeve(rng)
    # The slipstream brightens as it passes and quickly loses its high frequencies
    # with distance. These are noise bands, with no oscillator or impact layer.
    near = (0.37 * band_noise(rng, 460, 1700) * sweep(0.125, 0.032, 0.048) +
            0.48 * band_noise(rng, 1100, 3600) * sweep(0.121, 0.019, 0.041) +
            0.14 * band_noise(rng, 2800, 5300) * sweep(0.119, 0.013, 0.024))
    wake = sweep(0.171, 0.030, 0.076)
    retreat = 0.17 * band_noise(rng, 390, 1600) * wake
    spread = 0.085 * band_noise(rng, 650, 2350) * wake
    center = 0.69 * cloth + near + retreat
    stereo = np.column_stack((center - spread, center + spread))
    stereo *= np.minimum(1, TIME / 0.005)[:, None]
    stereo *= np.minimum(1, (TIME[-1] - TIME) / 0.027)[:, None]
    stereo *= 10 ** (-3.5 / 20) / np.max(np.abs(stereo))
    return np.rint(stereo * 32767).astype("<i2")


def dodge_start():
    # This is the preparatory cuff movement. Keep it dry and substantially
    # quieter than the near miss so it can overlap a successful dodge naturally.
    samples = sleeve(np.random.default_rng(20261127))[:round(0.082 * RATE)].copy()
    samples *= np.minimum(1, np.arange(len(samples)) / (0.003 * RATE))
    samples *= np.minimum(1, np.arange(len(samples))[::-1] / (0.025 * RATE))
    samples *= 10 ** (-15 / 20) / np.max(np.abs(samples))
    return np.rint(samples * 32767).astype("<i2")[:, None]


if __name__ == "__main__":
    for name, samples in (("PerfectDodge", perfect_dodge()), ("DodgeSleeve", dodge_start())):
        with wave.open(str(OUTPUT / (name + ".wav")), "wb") as output:
            output.setparams((samples.shape[1], 2, RATE, len(samples), "NONE", "PCM"))
            output.writeframes(samples.tobytes())
