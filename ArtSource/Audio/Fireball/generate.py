"""Render three original mono, 48 kHz variants of each P1 fireball cue."""
from pathlib import Path
import wave

import numpy as np

RATE = 48_000
OUT = Path(__file__).resolve().parents[3] / "Assets/Audio/Fireball"


def noise(rng, count, low, high):
    frequencies = np.fft.rfftfreq(count, 1 / RATE)
    band = (1 - np.exp(-(frequencies / low) ** 4)) * np.exp(-(frequencies / high) ** 4)
    value = np.fft.irfft(np.fft.rfft(rng.normal(size=count)) * band, n=count)
    return value / max(np.std(value), .001)


def tone(frequency):
    return np.sin(2 * np.pi * np.cumsum(frequency) / RATE)


def envelope(t, attack, decay):
    return (1 - np.exp(-np.maximum(t, 0) / attack)) * np.exp(-np.maximum(t, 0) / decay)


def render(kind, variant):
    index = ("Form", "Release", "Impact").index(kind)
    rng = np.random.default_rng(4317 + 101 * index + variant)
    t = np.arange(int((.48, .37, .61)[index] * RATE)) / RATE
    pitch = (.965, 1., 1.043)[variant]
    flame = noise(rng, len(t), 290 * pitch, (2200, 4100, 1850)[index] * pitch)
    sparks = noise(rng, len(t), 2400, 7200)
    if kind == "Form":
        # A short strike opens into rising breath around a small bronze resonator.
        attack = sparks * envelope(t, .0018, .014) * .25
        body = (flame * .29 + tone(pitch * (158 + 113 * (1 - np.exp(-14*t)))) * .24) * envelope(t, .024, .13)
        accent = sum(gain * tone(freq * pitch + 0*t) * envelope(t - .017, .012, decay)
                     for freq, gain, decay in ((760,.12,.13),(1457,.051,.091),(2172,.027,.069)))
    elif kind == "Release":
        # Thrown air drops quickly in pitch; a short whip and ember snap mark departure.
        attack = sparks * envelope(t, .0011, .016) * .32
        body = (flame * .55 + tone(pitch * (130 + 530 * np.exp(-22*t))) * .15) * envelope(t, .006, .066)
        accent = noise(rng, len(t), 730, 2600) * envelope(t - .020 - .002*variant, .004, .029) * .26
    else:
        # A dry fracture, a pitched low impact and several irregular scattering embers.
        attack = sparks * envelope(t, .001, .021) * .66
        body = (flame * .37 + tone(pitch * (94 + 165 * np.exp(-30*t))) * .78) * envelope(t, .003, .095)
        accent = np.zeros_like(t)
        for dt in (.032, .057, .092, .137, .196):
            accent += sparks * envelope(t - dt * rng.uniform(.9,1.12), .0008, .006) * .20 * np.exp(-6*dt)
    dry = attack + body + accent
    tail = np.zeros_like(dry)
    for delay, gain in ((.019,.14),(.037,.09),(.061,.055),(.094,.03)):
        samples = int(RATE * delay * (1 + .023*variant))
        tail[samples:] += dry[:-samples] * gain
    output = dry + tail
    output -= np.mean(output)
    output[:48] *= np.linspace(0,1,48)
    output[-480:] *= np.linspace(1,0,480)
    peak = 10 ** ((-6., -5.8, -5.)[index] / 20)
    return np.rint(output * ((32767 * peak) / np.max(np.abs(output)))).astype("<i2")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for stage in ("Form", "Release", "Impact"):
        for i in range(3):
            audio = render(stage, i)
            with wave.open(str(OUT / f"Fireball_{stage}_{i + 1:02d}.wav"), "wb") as stream:
                stream.setparams((1, 2, RATE, len(audio), "NONE", "PCM"))
                stream.writeframes(audio.tobytes())
