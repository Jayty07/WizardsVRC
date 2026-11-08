"""Quest and exploration sounds (Audio/UI), all original synthesis, 44.1 kHz mono 16-bit:

    QuestProgress.wav   one objective counted: a soft, warm two-tone ding (about 0.7 s)
    QuestObjective.wav  a step of the quest done: a bright rising chime, root and fifth, with a little sparkle (1.2 s)
    QuestComplete.wav   quest complete: a short, warm horn fanfare over a struck bell and a soft drum (2.4 s)
    Discovery.wav       a new place found: a harp-like rising run with a shimmer (2 s)

    python3 Tools/make_quest_sounds.py Assets/WizardsVRC/Audio/UI      (needs numpy)
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(11)


def timeline(seconds):
    return np.arange(int(SR * seconds)) / SR


def bell(t, f0, start, dur, amp, decay=3.0, bright=0.45):
    s = np.zeros_like(t)
    m = (t >= start) & (t < start + dur)
    tt = t[m] - start
    env = np.exp(-tt * decay) * np.minimum(1, tt / 0.004)
    tone = (np.sin(2 * np.pi * f0 * tt) + bright * np.sin(2 * np.pi * f0 * 2.76 * tt) * np.exp(-tt * 6)
            + 0.2 * np.sin(2 * np.pi * f0 * 5.4 * tt) * np.exp(-tt * 11))
    s[m] = tone * env * amp
    return s


def pluck(t, f0, start, dur, amp):
    """Harp-ish: a few harmonics that die away fast, the top ones fastest."""
    s = np.zeros_like(t)
    m = (t >= start) & (t < start + dur)
    tt = t[m] - start
    env = np.minimum(1, tt / 0.003)
    tone = np.zeros_like(tt)
    for h, a in ((1, 1.0), (2, 0.5), (3, 0.28), (4, 0.15), (5, 0.08)):
        tone += a * np.sin(2 * np.pi * f0 * h * tt) * np.exp(-tt * (2.2 + h * 1.6))
    s[m] = tone * env * amp
    return s


def horn(t, f0, start, dur, amp):
    """A warm brass-like note: a soft attack, a gentle swell and a sum of harmonics rolled off at the top."""
    s = np.zeros_like(t)
    m = (t >= start) & (t < start + dur)
    tt = t[m] - start
    att = np.minimum(1, tt / 0.05)
    rel = np.clip((dur - tt) / 0.25, 0, 1)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * tt) * np.minimum(1, tt / 0.3)
    tone = np.zeros_like(tt)
    for h in range(1, 9):
        tone += np.sin(2 * np.pi * f0 * h * vib * tt) / (h ** 1.6)
    s[m] = tone * att * rel * amp
    return s


def drum(t, start, amp):
    s = np.zeros_like(t)
    m = (t >= start) & (t < start + 0.6)
    tt = t[m] - start
    f = 95 * np.exp(-tt * 4) + 55
    s[m] = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 7) * amp
    return s


def sparkle(t, start, span, n, amp):
    s = np.zeros_like(t)
    for _ in range(n):
        s += bell(t, 2600 + rng.random() * 2600, start + rng.random() * span, 0.3, amp, decay=9, bright=0.2)
    return s


def lowpass(x, n):
    k = np.ones(n) / n
    return np.convolve(x, k, mode='same')


def write(path, out, seconds):
    t = timeline(seconds)
    out = out / (np.max(np.abs(out)) * 1.12)
    out *= np.clip((seconds - t) / 0.15, 0, 1)
    data = (out * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print('wrote', path)


def main(folder):
    os.makedirs(folder, exist_ok=True)
    # E5 then A5, soft and round
    T = 0.8
    t = timeline(T)
    out = bell(t, 659.25, 0.0, 0.8, 0.5, decay=5, bright=0.25) + bell(t, 880.0, 0.07, 0.75, 0.42, decay=5, bright=0.25)
    write(os.path.join(folder, 'QuestProgress.wav'), lowpass(out, 3), T)

    # G5 - D6 - G6, quick and bright
    T = 1.3
    t = timeline(T)
    out = bell(t, 783.99, 0.0, 1.2, 0.45) + bell(t, 1174.66, 0.08, 1.2, 0.42) + bell(t, 1567.98, 0.16, 1.1, 0.38)
    out += sparkle(t, 0.15, 0.5, 10, 0.05)
    write(os.path.join(folder, 'QuestObjective.wav'), out, T)

    # Fanfare in D: a pickup, then a rising triad that lands on a held chord
    T = 2.5
    t = timeline(T)
    out = horn(t, 293.66, 0.00, 0.16, 0.30) + horn(t, 293.66, 0.17, 0.16, 0.30)
    out += horn(t, 369.99, 0.34, 0.20, 0.32) + horn(t, 440.0, 0.55, 0.22, 0.32)
    for f in (293.66, 369.99, 440.0, 587.33):
        out += horn(t, f, 0.80, 1.6, 0.24)
    out += bell(t, 1174.66, 0.80, 1.6, 0.22, decay=2.2) + bell(t, 1760.0, 0.84, 1.5, 0.12, decay=2.6)
    out += drum(t, 0.80, 0.45) + drum(t, 0.0, 0.18)
    out += sparkle(t, 0.85, 0.9, 16, 0.035)
    write(os.path.join(folder, 'QuestComplete.wav'), lowpass(out, 2), T)

    # A rising pentatonic run, then a soft shimmer chord
    T = 2.1
    t = timeline(T)
    run = [392.0, 440.0, 523.25, 587.33, 659.25, 783.99, 880.0, 1046.5, 1174.66]
    out = np.zeros_like(t)
    for i, f in enumerate(run):
        out += pluck(t, f, i * 0.055, 1.6, 0.34)
    for f in (523.25, 659.25, 783.99, 1046.5):
        out += bell(t, f, 0.55, 1.5, 0.12, decay=2.0, bright=0.15)
    out += sparkle(t, 0.5, 1.0, 14, 0.03)
    noise = lowpass(rng.standard_normal(len(t)), 60)
    out += noise * np.clip(t / 0.6, 0, 1) * np.exp(-np.clip(t - 0.6, 0, None) * 3) * 0.25
    write(os.path.join(folder, 'Discovery.wav'), out, T)


if __name__ == '__main__':
    main(sys.argv[1])
