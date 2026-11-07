"""Render two comparable 32-second chamber sketches for 春水玉律."""

from pathlib import Path

import numpy as np
from scipy import signal
from scipy.io import wavfile

from compose_major_soothing_bgm import SR, synth_guqin_note


DEST = Path(__file__).resolve().parents[1] / "docs" / "audio"
DURATION = 32


def frequency(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def insert(track, start, note):
    index = round(start * SR)
    length = min(len(note), len(track) - index)
    track[index:index + length] += note[:length]


def room(track):
    stereo = np.column_stack((track, track))
    for channel, taps in enumerate(((.027, .064, .123, .219), (.036, .081, .142, .237))):
        for delay, gain in zip(taps, (.14, .086, .051, .025)):
            shift = round(delay * SR)
            stereo[shift:, channel] += gain * track[:-shift]
    return stereo


def backing():
    track = np.zeros(DURATION * SR)
    # Each line spans two bars. The spaces after the rising figures belong to the winds.
    phrases = [
        [(0.12, 60), (1.58, 64), (2.34, 67), (3.06, 69), (5.37, 67), (6.5, 64), (7.28, 62)],
        [(8.08, 60), (9.73, 62), (10.42, 64), (11.12, 67), (13.53, 64), (14.88, 62)],
        [(16.11, 60), (17.48, 64), (18.22, 67), (18.95, 69), (20.14, 72), (21.60, 76)],
        [(24.13, 72), (25.26, 69), (26.43, 67), (27.85, 64), (28.62, 62), (30.04, 60)],
    ]
    bass = [(0.03, 48), (4.65, 55), (8.03, 52), (12.62, 55),
            (16.04, 48), (20.06, 52), (24.04, 53), (29.65, 55)]
    for line in [bass, *phrases]:
        for position, pitch in line:
            low = line is bass
            duration = 2.7 if low else 2.25
            amp = .065 if low else .13 + .012 * np.cos(position)
            note = synth_guqin_note(frequency(pitch), duration, amp, is_harmonic=pitch >= 72)
            t = np.arange(len(note)) / SR
            note *= np.exp(-t / (1.2 if low else 1.55)) * np.minimum(1, (duration - t) / .14)
            insert(track, position, note)
    # A single soft bronze strike under the sustained E in the upper voice.
    t = np.arange(3 * SR) / SR
    bell = sum(gain * np.sin(2*np.pi*frequency(60)*ratio*t) * np.exp(-t/decay)
               for ratio, gain, decay in [(1, 1, .76), (2.37, .32, .52), (2.99, .18, .4), (4.23, .10, .22)])
    insert(track, 21.84, .036 * bell * (1 - np.exp(-t/.007)))
    return room(track)


def wind_note(pitch, duration, flute, rng):
    t = np.arange(round(duration * SR)) / SR
    breath = np.sin(np.pi * t / duration) ** .63
    speed = rng.uniform(4.4, 5.1)
    depth = (.0024 if flute else .0006) * np.minimum(1, t / .48)
    tuning = 1 - (.011 if flute else .002) * np.exp(-t/.043) + depth * np.sin(2*np.pi*speed*t)
    phase = 2*np.pi*frequency(pitch)*np.cumsum(tuning)/SR
    if flute:
        noise = rng.normal(size=len(t))
        air = signal.sosfilt(signal.butter(2, [1350, 4900], fs=SR, btype="bandpass", output="sos"), noise)
        tone = np.sin(phase) + .32*np.sin(2*phase) + .18*np.sin(3*phase) + .08*np.sin(5*phase)
        tone += air * (.054 + .16*np.exp(-t/.075))
    else:
        tone = sum(weight * np.sin(h*phase) for h, weight in enumerate(
            [0, .74, .43, .51, .18, .31, .10, .105, .08], start=0) if h)
    return tone * breath * (1 + .028*np.sin(2*np.pi*speed*t))


def render():
    np.random.seed(133)
    base = backing()
    # Notes are written at 60 BPM. Breath breaks and the two differing cadences are intentional.
    dizi = [(3.87, 74, .55), (4.46, 72, .87), (11.91, 69, .63), (12.58, 67, .85),
            (22.47, 74, .63), (23.12, 72, .83), (27.03, 69, .47), (27.55, 67, .78)]
    sheng = [(3.87, 60, 2.13), (3.96, 67, 1.93), (11.91, 62, 2.26), (12.06, 67, 2.03),
             (20.38, 64, 3.28), (20.53, 67, 3.01), (26.94, 60, 2.50), (27.09, 67, 2.16)]
    mixes = {}
    for name, score in [('dizi', dizi), ('sheng', sheng)]:
        rng = np.random.default_rng(331)
        wind = np.zeros(DURATION * SR)
        for position, pitch, duration in score:
            insert(wind, position, wind_note(pitch, duration, name == 'dizi', rng))
        # Match the overall energy of the added layers for an equitable A/B.
        wind *= .016 / np.sqrt(np.mean(wind**2))
        mixes[name] = base + room(wind) * np.array([.93, 1.03])
    gain = .50 / max(np.max(np.abs(mix)) for mix in mixes.values())
    for name, mix in mixes.items():
        mix[-SR//4:] *= np.linspace(1, 0, SR//4)[:, None]
        wavfile.write(DEST / f"kaogong_guqin_{name}_preview.wav", SR, np.rint(32767*gain*mix).astype(np.int16))


if __name__ == "__main__":
    render()
