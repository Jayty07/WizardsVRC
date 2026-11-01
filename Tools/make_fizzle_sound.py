"""Generates the fizzle sound (Assets/WizardsVRC/Audio/Cast/FX_Fizzle.wav): played when a spell fizzles, cutting off the
cast sound two seconds into the wind-up.

About 1.1 s: a sputtering crackle as the gathered energy breaks up, a little 'pew' that sags in pitch like a dud, and a
hiss of smoke trailing away.

    python3 Tools/make_fizzle_sound.py      (needs numpy and scipy)
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
LENGTH = 1.15
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "WizardsVRC", "Audio", "Cast", "FX_Fizzle.wav")
rng = np.random.default_rng(7)
n = int(LENGTH * SR)
t = np.arange(n) / SR


def band(x, lo, hi):
    b, a = signal.butter(2, [lo / (SR / 2), hi / (SR / 2)], btype="band")
    return signal.lfilter(b, a, x)


def high(x, f):
    b, a = signal.butter(2, f / (SR / 2), btype="high")
    return signal.lfilter(b, a, x)


out = np.zeros(n)

# 1. Sputtering crackle: short pops, thinning out over the first 0.5 s.
pops = np.zeros(n)
when = 0.0
while when < 0.55:
    k = int(when * SR)
    length = int(rng.uniform(0.004, 0.018) * SR)
    burst = rng.standard_normal(length) * np.exp(-np.linspace(0, 6, length))
    if k + length < n:
        pops[k:k + length] += burst * (1.0 - when / 0.6) * rng.uniform(0.5, 1.0)
    when += rng.uniform(0.012, 0.05) * (1 + when * 3)
out += band(pops, 1200, 6000) * 0.9

# 2. The dud 'pew': a wobbling tone sagging from ~900 Hz to ~160 Hz.
f = 900 * np.exp(-t * 3.2) + 160
f *= 1 + 0.04 * np.sin(2 * np.pi * 11 * t)
phase = 2 * np.pi * np.cumsum(f) / SR
pew = (np.sin(phase) + 0.35 * np.sign(np.sin(phase))) * np.exp(-t * 4.0) * np.clip(t / 0.01, 0, 1)
out += pew * 0.32

# 3. A hiss of smoke trailing away.
hiss = high(rng.standard_normal(n), 3500) * np.clip(t / 0.08, 0, 1) * np.exp(-t * 3.0)
out += hiss * 0.22

# Soft fade at the very end; normalise to -3 dB.
out *= np.clip((LENGTH - t) / 0.1, 0, 1)
out *= 0.7 / np.max(np.abs(out))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with wave.open(OUT, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((out * 32767).astype(np.int16).tobytes())
print("wrote", OUT)
