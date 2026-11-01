"""Level-up fanfare (Audio/UI/LevelUp.wav): a rising major arpeggio of bell tones, a shimmer of high sparkles and a
soft whoosh under it, about 2.2 s, 44.1 kHz mono 16-bit.

    python3 Tools/make_levelup_sound.py Assets/WizardsVRC/Audio/UI/LevelUp.wav      (needs numpy)
"""
import sys
import wave

import numpy as np

SR = 44100
T = 2.4
t = np.arange(int(SR * T)) / SR
out = np.zeros_like(t)
rng = np.random.default_rng(7)


def bell(f0, start, dur, amp):
    s = np.zeros_like(t)
    m = (t >= start) & (t < start + dur)
    tt = t[m] - start
    env = np.exp(-tt * 3.2) * np.minimum(1, tt / 0.006)
    tone = (np.sin(2 * np.pi * f0 * tt) + 0.45 * np.sin(2 * np.pi * f0 * 2.01 * tt) * np.exp(-tt * 5)
            + 0.25 * np.sin(2 * np.pi * f0 * 3.0 * tt) * np.exp(-tt * 8))
    s[m] = tone * env * amp
    return s


# C major arpeggio up two octaves, then a held chord
notes = [523.25, 659.25, 783.99, 1046.5, 1318.5, 1567.98]
for i, f in enumerate(notes):
    out += bell(f, i * 0.085, 1.6, 0.32)
for f in (1046.5, 1318.5, 1567.98, 2093.0):
    out += bell(f, 0.6, 1.8, 0.18)
# sparkles
for k in range(26):
    st = 0.45 + rng.random() * 1.4
    out += bell(2600 + rng.random() * 2400, st, 0.25, 0.05)
# whoosh: filtered noise swelling up
noise = rng.standard_normal(len(t))
kernel = np.ones(40) / 40
whoosh = np.convolve(noise, kernel, mode='same')
env = np.clip(t / 0.5, 0, 1) * np.exp(-np.clip(t - 0.5, 0, None) * 2.5)
out += whoosh * env * 0.5
out /= np.max(np.abs(out)) * 1.15
fade = np.clip((T - t) / 0.2, 0, 1)
out *= fade
data = (out * 32767).astype(np.int16)
with wave.open(sys.argv[1], 'wb') as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(data.tobytes())
print('wrote', sys.argv[1])
