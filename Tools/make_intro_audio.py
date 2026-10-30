"""Generates the placeholder sounds for the awakening intro (Assets/WizardsVRC/Audio/Intro).

    python3 Tools/make_intro_audio.py      (needs numpy and scipy)

DreamDrone.wav   low, slowly breathing drone (loops)
Whispers.wav     breathy, syllable-like whispering with no words (loops; played from several spots round you)
WarDrums.wav     a slow war-drum cadence (loops while the legion vision is shown)
VisionFlash.wav  a rising rush into a bright shimmer (each vision's white flash)
Heartbeat.wav    two heartbeats (the start, in the dark)
Replace any of them with real recordings of the same name; the builder picks them up on the next rebuild.
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "WizardsVRC", "Audio", "Intro")
rng = np.random.default_rng(7)


def write(name, data, stereo=False):
    os.makedirs(OUT, exist_ok=True)
    data = np.asarray(data, dtype=np.float64)
    peak = np.max(np.abs(data)) or 1.0
    data = data / peak * 0.89
    pcm = (data * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.T.tobytes() if stereo else pcm.tobytes())
    print("wrote", name, f"{data.shape[-1] / SR:.1f}s")


def band(x, lo, hi, order=4):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, hi, order=4):
    sos = signal.butter(order, hi, btype="low", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def loop_fade(x, seconds=0.5):
    """Crossfades the tail into the head so the clip loops without a click."""
    n = int(seconds * SR)
    head, tail = x[..., :n].copy(), x[..., -n:]
    ramp = np.linspace(0, 1, n)
    x = x[..., :-n].copy()
    x[..., :n] = head * ramp + tail * (1 - ramp)
    return x


def reverb(x, seconds=1.6, mix=0.35):
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, 5000)
    wet = signal.fftconvolve(x, ir)[: len(x)]
    wet /= np.max(np.abs(wet)) or 1
    return x * (1 - mix) + wet * mix * np.max(np.abs(x))


# ---- drone: two detuned low tones and a whisper of filtered noise, swelling like slow breathing
t = np.arange(int(20.5 * SR)) / SR
breath = 0.65 + 0.35 * np.sin(2 * np.pi * t / 10.0) ** 2
drone = (np.sin(2 * np.pi * 55 * t) + 0.6 * np.sin(2 * np.pi * 55.4 * t + 1.0) + 0.35 * np.sin(2 * np.pi * 82.6 * t)
         + 0.2 * np.sin(2 * np.pi * 110.3 * t + 0.4))
air = lowpass(rng.standard_normal(len(t)), 400) * 3
write("DreamDrone.wav", loop_fade(reverb((drone + air) * breath, 2.5, 0.4)))

# ---- whispers: bursts of breathy noise shaped like syllables, a phrase every second or two, panned about
dur = 16.5
n = int(dur * SR)
left, right = np.zeros(n), np.zeros(n)
pos = 0.3
while pos < dur - 1.5:
    syllables = rng.integers(3, 8)
    pan = rng.uniform(0.15, 0.85)
    for _ in range(syllables):
        length = rng.uniform(0.09, 0.22)
        m = int(length * SR)
        start = int(pos * SR)
        if start + m >= n:
            break
        centre = rng.uniform(1800, 4200)
        burst = band(rng.standard_normal(m + 2000), centre * 0.55, min(centre * 1.9, 9000))[2000:]
        env = np.sin(np.linspace(0, np.pi, m)) ** 1.5 * rng.uniform(0.4, 1.0)
        # a sibilant now and then
        if rng.random() < 0.3:
            burst = band(rng.standard_normal(m + 2000), 5000, 9500)[2000:]
        left[start:start + m] += burst * env * (1 - pan)
        right[start:start + m] += burst * env * pan
        pos += length + rng.uniform(0.02, 0.09)
    pos += rng.uniform(0.6, 1.8)
st = np.vstack([reverb(left, 1.2, 0.45), reverb(right, 1.2, 0.45)])
write("Whispers.wav", loop_fade(st), stereo=True)


# ---- war drums: a deep tom with a skin slap, BOOM . boom-boom . BOOM . (rest)
def drum(strength=1.0, pitch=1.0):
    m = int(1.1 * SR)
    tt = np.arange(m) / SR
    f = (48 + 70 * np.exp(-tt * 18)) * pitch
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 4.2)
    slap = band(rng.standard_normal(m), 150, 2200) * np.exp(-tt * 45) * 0.5
    return (body + slap) * strength


bar = 3.2
n = int(bar * 2 * SR)
drums = np.zeros(n + SR)
for bar_i in range(2):
    base = bar_i * bar
    for at, s, p in ((0.0, 1.0, 1.0), (0.8, 0.6, 1.08), (1.07, 0.65, 1.08), (1.6, 1.0, 0.95), (2.4, 0.5, 1.15)):
        k = int((base + at) * SR)
        d = drum(s, p)
        drums[k:k + len(d)] += d
write("WarDrums.wav", loop_fade(reverb(drums, 1.8, 0.3)[: n + int(0.5 * SR)]))

# ---- vision flash: noise swelling up a filter sweep into a high shimmer
m = int(1.6 * SR)
tt = np.arange(m) / SR
rush = rng.standard_normal(m)
sweep = np.zeros(m)
for i in range(0, m, 2048):
    hi = 300 + 9000 * min(1, (i / m) * 1.6)
    seg = rush[i:i + 2048]
    sweep[i:i + len(seg)] = lowpass(seg, hi, 2)
swell = np.clip(tt / 0.55, 0, 1) ** 2 * np.exp(-np.clip(tt - 0.55, 0, None) * 4)
shimmer = sum(np.sin(2 * np.pi * f * tt) for f in (1568, 2093, 2637, 3136)) * np.exp(-np.clip(tt - 0.5, 0, None) * 3) * (tt > 0.5) * 0.15
write("VisionFlash.wav", reverb(sweep * swell + shimmer, 1.4, 0.4))


# ---- heartbeat: lub-dub, lub-dub
def thump(strength):
    m = int(0.35 * SR)
    tt = np.arange(m) / SR
    return np.sin(2 * np.pi * (38 + 25 * np.exp(-tt * 30)) * tt) * np.exp(-tt * 14) * strength


beat = np.zeros(int(2.6 * SR))
for at, s in ((0.1, 1.0), (0.38, 0.7), (1.25, 1.0), (1.53, 0.7)):
    k = int(at * SR)
    d = thump(s)
    beat[k:k + len(d)] += d
write("Heartbeat.wav", lowpass(beat, 180))
