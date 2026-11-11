"""Generates the cast sound for the Tech school (Assets/WizardsVRC/Audio/Cast/FX_CastingSound_Tech.mp3).

The Dominion's school is machinery, not magic, so this is the one cast sound with no chimes in it. It keeps the shape of
the others (about two seconds of wind-up, the release at 2.0 s where the particle burst fires:
BattleManager.techCastPeakSeconds, then a tail):
  - wind-up: a transformer hum that thickens, a capacitor whine climbing five octaves, a ratchet of servo clicks getting
    quicker, and bursts of data chirps (square-wave bleeps, like a machine thinking aloud);
  - release: a hard relay clack, an arc discharge tearing downward through static, a low thump;
  - tail: the hum winding down, struck metal ringing out, a few last sparks.

    python3 Tools/make_tech_cast_sound.py      (needs numpy and scipy; ffmpeg for the mp3, otherwise it writes a .wav)
"""
import os
import shutil
import subprocess
import wave

import numpy as np
from scipy import signal

SR = 44100
LENGTH = 3.0
PEAK = 2.0
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "WizardsVRC", "Audio", "Cast")
rng = np.random.default_rng(808)
n = int(LENGTH * SR)
t = np.arange(n) / SR
L = np.zeros(n)
R = np.zeros(n)


def add(sig, pan=0.0, gain=1.0):
    global L, R
    L += sig * gain * np.sqrt(0.5 * (1 - pan))
    R += sig * gain * np.sqrt(0.5 * (1 + pan))


def lowpass(x, hz, order=2):
    return signal.sosfilt(signal.butter(order, hz, 'low', fs=SR, output='sos'), x)


def highpass(x, hz, order=2):
    return signal.sosfilt(signal.butter(order, hz, 'high', fs=SR, output='sos'), x)


def bandpass(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], 'band', fs=SR, output='sos'), x)


def place(sig, start):
    """A full-length buffer with sig starting at 'start' seconds (cut off at the end)."""
    out = np.zeros(n)
    i0 = int(start * SR)
    m = min(len(sig), n - i0)
    if m > 0:
        out[i0:i0 + m] = sig[:m]
    return out


x = np.clip(t / PEAK, 0, 1)          # 0..1 through the wind-up
before = (t < PEAK).astype(float)

# ---- transformer hum: 55 Hz and its odd harmonics, buzzier as it charges, with a slow throb
hum = np.zeros(n)
for k, g in ((1, 1.0), (2, 0.35), (3, 0.6), (5, 0.4), (7, 0.28), (9, 0.2), (11, 0.14)):
    hum += g * np.sin(2 * np.pi * 55.0 * k * t + k)
hum = np.tanh(hum * (0.6 + 2.2 * x))                    # drive it harder as it builds: more buzz
hum *= (0.25 + 0.75 * x ** 1.3) * (1 - 0.25 * np.sin(2 * np.pi * (3 + 9 * x) * t) ** 2)
add(lowpass(hum, 2600) * before, 0.0, 0.5)

# ---- capacitor whine: a thin tone climbing from 140 Hz to 4.5 kHz, with a faint fifth above it
f = 140.0 * (4500.0 / 140.0) ** (x ** 1.6)
ph = 2 * np.pi * np.cumsum(f) / SR
whine = (np.sin(ph) + 0.3 * np.sin(ph * 1.5) + 0.18 * signal.sawtooth(ph * 2)) * x ** 1.4 * before
add(lowpass(whine, 9000), -0.25, 0.22)
add(lowpass(np.roll(whine, 180), 9000), 0.25, 0.22)

# ---- servo ratchet: clicks through a small metal resonance, closing up as the release nears
tc = 0.12
while tc < PEAK - 0.03:
    xx = tc / PEAK
    m = int(0.03 * SR)
    tt = np.arange(m) / SR
    click = rng.standard_normal(m) * np.exp(-tt * 260)
    click = bandpass(click, 1800 + 2500 * xx, 5200 + 5000 * xx) * 2.5
    click += 0.5 * np.sin(2 * np.pi * (900 + 1400 * xx) * tt) * np.exp(-tt * 180)
    add(place(click, tc), rng.uniform(-0.5, 0.5), 0.16 + 0.14 * xx)
    tc += 0.19 * (1 - xx) ** 1.5 + 0.028

# ---- data chirps: bursts of square-wave bleeps, a machine counting itself in
notes = [523.25, 659.25, 783.99, 987.77, 1174.66, 1567.98, 2093.0]
for burst_at in (0.35, 0.82, 1.22, 1.52, 1.76):
    pan = rng.uniform(-0.7, 0.7)
    count = rng.integers(3, 7)
    for k in range(count):
        ln = 0.035
        m = int(ln * SR)
        tt = np.arange(m) / SR
        fq = notes[rng.integers(0, len(notes))] * (2 if burst_at > 1.4 else 1)
        bleep = signal.square(2 * np.pi * fq * tt) * np.minimum(1, tt * 2000) * np.minimum(1, (ln - tt) * 2000)
        add(place(lowpass(bleep, 6500), burst_at + k * 0.045), pan, 0.055 + 0.03 * burst_at / PEAK)

# ---- a hiss of static rising under it all (pressure, steam, an open channel)
hiss = highpass(rng.standard_normal(n), 3500) * x ** 2.5 * before
add(hiss, 0.0, 0.07)

# ---- the release at PEAK
i0 = int(PEAK * SR)
m = n - i0
tt = np.arange(m) / SR
# relay clack: a very short hard knock with two metal resonances
clack = rng.standard_normal(m) * np.exp(-tt * 420)
clack = bandpass(clack, 700, 9000) * 3 + np.sin(2 * np.pi * 1340 * tt) * np.exp(-tt * 70) * 0.8 + np.sin(2 * np.pi * 2210 * tt) * np.exp(-tt * 90) * 0.5
# arc discharge: a saw tearing down from 3.4 kHz to 90 Hz, chopped by random gating, in a burst of static
fz = 90.0 + 3300.0 * np.exp(-tt * 9.0)
zp = 2 * np.pi * np.cumsum(fz) / SR
gate = (lowpass(rng.standard_normal(m), 320) > -0.02).astype(float)
gate = lowpass(gate, 1200)
arc = (signal.sawtooth(zp) * 0.7 + signal.square(zp * 0.5) * 0.4) * gate * np.exp(-tt * 5.5)
static = bandpass(rng.standard_normal(m), 2000, 14000) * np.exp(-tt * 11) * (0.5 + 0.5 * gate)
# thump: the bank emptying
thump = np.sin(2 * np.pi * np.cumsum(42 + 110 * np.exp(-tt * 34)) / SR) * np.exp(-tt * 7.5)
# power-down: the hum falling away in pitch
fd = 55.0 * (0.35 + 0.65 * np.exp(-tt * 2.6))
pd = 2 * np.pi * np.cumsum(fd) / SR
down = np.tanh((np.sin(pd) + 0.6 * np.sin(pd * 3) + 0.4 * np.sin(pd * 5)) * 1.6) * np.exp(-tt * 2.4)
# struck metal: inharmonic partials ringing out (a plate, not a bell)
plate = np.zeros(m)
for fq, g, d in ((412.0, 1.0, 3.2), (683.0, 0.7, 3.8), (1127.0, 0.55, 4.6), (1756.0, 0.4, 5.5), (2489.0, 0.3, 6.5), (3391.0, 0.2, 8.0)):
    plate += g * np.sin(2 * np.pi * fq * tt + rng.uniform(0, 6)) * np.exp(-tt * d)
plate *= np.minimum(1, tt * 500)
# last sparks
sparks = np.zeros(m)
for _ in range(14):
    s0 = int(rng.uniform(0.12, 0.8) * SR)
    ln = int(rng.uniform(0.004, 0.012) * SR)
    if s0 + ln < m:
        sparks[s0:s0 + ln] += rng.standard_normal(ln) * rng.uniform(0.4, 1.0)
sparks = highpass(sparks, 2500) * np.exp(-tt * 2.5)
for sig, gain, pan in ((clack, 0.55, 0.0), (arc, 0.42, -0.15), (arc, 0.42, 0.15), (static, 0.3, 0.0), (thump, 0.7, 0.0),
                       (lowpass(down, 1800), 0.5, 0.0), (plate, 0.2, -0.3), (plate, 0.2, 0.3), (sparks, 0.3, 0.2)):
    full = np.zeros(n)
    full[i0:] = sig
    add(full, pan, gain)


# ---- a short, hard room (a machine hall, not a cathedral), then fade the very end
def reverb(sig, seconds=0.55, mix=0.18):
    k = int(seconds * SR)
    ir = rng.standard_normal(k) * np.exp(-np.linspace(0, 7, k))
    ir = bandpass(ir, 250, 7000)
    wet = signal.fftconvolve(sig, ir)[:len(sig)]
    wet *= np.max(np.abs(sig)) / (np.max(np.abs(wet)) or 1)
    return sig * (1 - mix) + wet * mix


L, R = reverb(L), reverb(R)
end = np.clip((LENGTH - t) / 0.25, 0, 1)
L *= end
R *= end
# The release is a hard transient: squash it a little so the wind-up and the tail sit as loud as the other schools'.
peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
L, R = np.tanh(L / peak * 1.7), np.tanh(R / peak * 1.7)
peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
L, R = L / peak * 0.55, R / peak * 0.55

os.makedirs(OUT_DIR, exist_ok=True)
wav = os.path.join(OUT_DIR, "FX_CastingSound_Tech.wav")
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
