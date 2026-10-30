"""Generates the cast sound for General spells (Assets/WizardsVRC/Audio/Cast/FX_CastingSound_General.mp3).

It follows the shape of the seven school cast sounds: about two seconds of building wind-up (a shimmering chord, a rising
whoosh and quickening chimes), the release at 2.0 s (where the particle burst fires: BattleManager.generalCastPeakSeconds)
and a bright ringing tail. The school ones each have a colour (fire's roar, ice's crackle, storm's zaps...); this one is
plain arcane sparkle, so it sits beside them without sounding like any one school.

    python3 Tools/make_cast_sound.py      (needs numpy and scipy; ffmpeg for the mp3, otherwise it writes a .wav)
"""
import os
import shutil
import subprocess
import wave

import numpy as np
from scipy import signal

SR = 44100
LENGTH = 2.85
PEAK = 2.0
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "WizardsVRC", "Audio", "Cast")
rng = np.random.default_rng(42)
n = int(LENGTH * SR)
t = np.arange(n) / SR
L = np.zeros(n)
R = np.zeros(n)


def env_ramp(t0, t1, curve=2.0):
    """0 before t0, rising to 1 at t1 (curved), 0 after t1."""
    e = np.clip((t - t0) / (t1 - t0), 0, 1) ** curve
    e[t > t1] = 0
    return e


def fade(t0, t1):
    """1 until t0, fading to 0 at t1."""
    return np.clip((t1 - t) / (t1 - t0), 0, 1)


def add(sig, pan=0.0, gain=1.0):
    global L, R
    L += sig * gain * np.sqrt(0.5 * (1 - pan))
    R += sig * gain * np.sqrt(0.5 * (1 + pan))


def lowpass(x, hz, order=2):
    return signal.sosfilt(signal.butter(order, hz, 'low', fs=SR, output='sos'), x)


def bandpass(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], 'band', fs=SR, output='sos'), x)


# ---- wind-up: a soft detuned chord (A, E, A, C#) that opens up as it builds
build = env_ramp(0.05, PEAK, 0.7) * 0.75 + np.clip(t / 0.25, 0, 1) * 0.25 * (t < PEAK)
pad = np.zeros(n)
for f in (220.0, 329.63, 440.0, 554.37):
    for det in (-0.35, 0.35):
        ph = 2 * np.pi * np.cumsum(f * (1 + det / 100) * (1 + 0.004 * np.sin(2 * np.pi * 5.2 * t))) / SR
        pad += signal.sawtooth(ph) * 0.12
# filter sweep: open the chord as it builds (process in blocks with a rising cutoff)
swept = np.zeros(n)
block = 1024
zi = None
for i in range(0, n, block):
    cut = 350 + 3200 * min(1.0, (i / SR) / PEAK) ** 1.5
    sos = signal.butter(2, cut, 'low', fs=SR, output='sos')
    if zi is None:
        zi = signal.sosfilt_zi(sos) * 0
    seg, zi = signal.sosfilt(sos, pad[i:i + block], zi=zi)
    swept[i:i + block] = seg
add(swept * build, -0.15, 0.9)
add(swept * build, 0.15, 0.9)

# ---- a rising whoosh: band-passed noise sweeping up into the release
noise = rng.standard_normal(n)
whoosh = np.zeros(n)
for i in range(0, n, block):
    tt = i / SR
    centre = 300 * (12 ** min(1.0, tt / PEAK))
    seg = bandpass(noise[max(0, i - 2048):i + block], centre * 0.6, min(centre * 1.6, 16000))[-min(block, n - i):]
    whoosh[i:i + block] = seg
add(whoosh * env_ramp(0.1, PEAK, 1.4) * 0.6, 0.0)

# ---- rising glides (like the bright sweeps in the school sounds)
for k in range(5):
    t0 = 0.3 + k * 0.28
    f0, f1 = 500 + 150 * k, 1800 + 400 * k
    g = (t >= t0) & (t < PEAK)
    x = np.clip((t - t0) / (PEAK - t0), 0, 1)
    f = f0 * (f1 / f0) ** x
    ph = 2 * np.pi * np.cumsum(f * g) / SR
    add(np.sin(ph) * g * x * (1 - x * 0.3) * 0.05, rng.uniform(-0.7, 0.7))


# ---- chimes, quicker and higher as the spell gathers
def chime(start, freq, length=0.6, gain=0.12, pan=0.0):
    i0 = int(start * SR)
    m = min(int(length * SR), n - i0)
    if m <= 0:
        return
    tt = np.arange(m) / SR
    tone = (np.sin(2 * np.pi * freq * tt) + 0.45 * np.sin(2 * np.pi * freq * 2.76 * tt) * np.exp(-tt * 9)
            + 0.2 * np.sin(2 * np.pi * freq * 5.4 * tt) * np.exp(-tt * 18))
    tone *= np.exp(-tt * 6) * np.minimum(1, tt * 400)
    sig = np.zeros(n)
    sig[i0:i0 + m] = tone * gain
    add(sig, pan)


scale = [880.0, 987.77, 1108.73, 1318.51, 1479.98, 1760.0, 1975.53, 2217.46, 2637.02]
tc = 0.35
step = 0.0
while tc < PEAK - 0.05:
    x = tc / PEAK
    idx = min(len(scale) - 1, int(x * (len(scale) - 2) + rng.integers(0, 3)))
    chime(tc, scale[idx], 0.5, 0.08 + 0.06 * x, rng.uniform(-0.8, 0.8))
    tc += 0.22 * (1 - x) + 0.05

# ---- the release at PEAK: a soft thump, a bright burst of air and a ringing chord
i0 = int(PEAK * SR)
m = n - i0
tt = np.arange(m) / SR
thump = np.sin(2 * np.pi * np.cumsum(55 + 75 * np.exp(-tt * 28)) / SR) * np.exp(-tt * 9)
burst = bandpass(rng.standard_normal(m), 1200, 12000) * np.exp(-tt * 14)
ring = np.zeros(m)
for f, gn in ((1760.0, 1.0), (2217.46, 0.7), (2637.02, 0.6), (3520.0, 0.35), (880.0, 0.5)):
    ring += gn * np.sin(2 * np.pi * f * tt + rng.uniform(0, 6)) * np.exp(-tt * (1.4 + f / 3500))
ring *= np.minimum(1, tt * 300)
sparkle = np.zeros(m)
for _ in range(40):
    s0 = int(rng.uniform(0, 0.6) * SR)
    ln = int(0.05 * SR)
    if s0 + ln < m:
        sparkle[s0:s0 + ln] += np.sin(2 * np.pi * rng.uniform(3000, 7000) * np.arange(ln) / SR) * np.exp(-np.arange(ln) / SR * 60) * rng.uniform(0.3, 1)
for sig, gain, pan in ((thump, 0.55, 0.0), (burst, 0.35, 0.0), (ring, 0.22, -0.2), (ring, 0.22, 0.2), (sparkle * np.exp(-tt * 3), 0.12, 0.0)):
    full = np.zeros(n)
    full[i0:] = sig
    add(full, pan, gain)

# ---- a little room, then fade the very end
def reverb(x, seconds=1.1, mix=0.28):
    k = int(seconds * SR)
    ir = rng.standard_normal(k) * np.exp(-np.linspace(0, 6, k))
    ir = lowpass(ir, 6000)
    wet = signal.fftconvolve(x, ir)[:len(x)]
    wet *= np.max(np.abs(x)) / (np.max(np.abs(wet)) or 1)
    return x * (1 - mix) + wet * mix


L, R = reverb(L), reverb(R)
L *= fade(LENGTH - 0.25, LENGTH)
R *= fade(LENGTH - 0.25, LENGTH)
peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
L, R = L / peak * 0.55, R / peak * 0.55

os.makedirs(OUT_DIR, exist_ok=True)
wav = os.path.join(OUT_DIR, "FX_CastingSound_General.wav")
pcm = (np.stack([L, R], 1) * 32767).astype(np.int16)
with wave.open(wav, "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(pcm.tobytes())
if shutil.which("ffmpeg"):
    mp3 = wav[:-4] + ".mp3"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-codec:a", "libmp3lame", "-b:a", "160k", mp3], check=True)
    os.remove(wav)
    print("wrote", mp3)
else:
    print("wrote", wav)
