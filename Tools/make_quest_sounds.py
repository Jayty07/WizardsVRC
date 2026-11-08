"""Quest and exploration sounds (Audio/UI), all original synthesis, 44.1 kHz mono 16-bit. Magical in feel: glassy
celesta and bell tones with a gentle chorus, rising shimmer, falling sparkles, a soft choir pad and a long reverb.

    QuestProgress.wav   one objective counted: two glassy celesta notes and a puff of sparkle (about 1.3 s)
    QuestObjective.wav  a step of the quest done: a quick rising celesta arpeggio on a sparkle glissando (1.9 s)
    QuestComplete.wav   quest complete: a shimmer swell, then a bright bell chord over a choir pad, sparkles falling (3.4 s)
    Discovery.wav       a new place found: a harp run up a Lydian scale over a shimmering pad and wind chimes (2.9 s)

    python3 Tools/make_quest_sounds.py Assets/WizardsVRC/Audio/UI      (needs numpy and scipy)
"""
import os
import sys
import wave

import numpy as np
from scipy.signal import lfilter, butter

SR = 44100
rng = np.random.default_rng(23)


def timeline(seconds):
    return np.arange(int(SR * seconds)) / SR


def note(f):
    """Note name like 'G5' or 'F#6' to Hz."""
    names = {'C': -9, 'D': -7, 'E': -5, 'F': -4, 'G': -2, 'A': 0, 'B': 2}
    semis = names[f[0]] + (1 if '#' in f else 0) - (1 if f[1] == 'b' else 0)
    octave = int(f[-1])
    return 440.0 * 2 ** ((semis + 12 * (octave - 4)) / 12)


def window(t, start, dur):
    m = (t >= start) & (t < start + dur)
    return m, t[m] - start


def celesta(t, f0, start, dur, amp, decay=2.6):
    """Glassy and bell-like: a pure fundamental, a quick metallic shimmer on top, two slightly detuned voices."""
    s = np.zeros_like(t)
    m, tt = window(t, start, dur)
    env = np.exp(-tt * decay) * np.minimum(1, tt / 0.003)
    tone = np.zeros_like(tt)
    for detune in (1.0, 1.0035):
        f = f0 * detune
        tone += (np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * f * 4.0 * tt) * np.exp(-tt * 9)
                 + 0.18 * np.sin(2 * np.pi * f * 6.27 * tt) * np.exp(-tt * 16))
    s[m] = tone * env * amp * 0.5
    return s


def bell(t, f0, start, dur, amp, decay=1.6):
    """A deeper struck bell with inharmonic partials (the warm floor under a chord)."""
    s = np.zeros_like(t)
    m, tt = window(t, start, dur)
    env = np.exp(-tt * decay) * np.minimum(1, tt / 0.004)
    tone = np.zeros_like(tt)
    for ratio, a, d in ((1.0, 1.0, 1.0), (2.0, 0.6, 1.4), (2.76, 0.4, 2.2), (5.4, 0.25, 3.5), (8.93, 0.12, 5.0)):
        tone += a * np.sin(2 * np.pi * f0 * ratio * tt) * np.exp(-tt * decay * (d - 1))
    s[m] = tone * env * amp
    return s


def harp(t, f0, start, dur, amp):
    s = np.zeros_like(t)
    m, tt = window(t, start, dur)
    env = np.minimum(1, tt / 0.002)
    tone = np.zeros_like(tt)
    for h, a in ((1, 1.0), (2, 0.55), (3, 0.3), (4, 0.16), (5, 0.08), (6, 0.04)):
        tone += a * np.sin(2 * np.pi * f0 * h * tt) * np.exp(-tt * (1.8 + h * 1.3))
    s[m] = tone * env * amp
    return s


def choir(t, freqs, start, dur, amp, swell=0.5):
    """A soft 'aah' pad: several detuned voices per note with vibrato, harmonics shaped like an open vowel."""
    s = np.zeros_like(t)
    m, tt = window(t, start, dur)
    env = np.minimum(1, tt / swell) * np.clip((dur - tt) / 0.8, 0, 1)
    formant = {1: 1.0, 2: 0.7, 3: 0.55, 4: 0.25, 5: 0.12, 6: 0.18, 7: 0.1}
    tone = np.zeros_like(tt)
    for f0 in freqs:
        for detune, ph in ((0.997, 0.0), (1.0, 1.3), (1.004, 2.1)):
            vib = 1 + 0.005 * np.sin(2 * np.pi * (5.0 + detune) * tt + ph)
            for h, a in formant.items():
                tone += a * np.sin(2 * np.pi * f0 * detune * h * vib * tt + ph * h)
    s[m] = tone * env * amp / (len(freqs) * 3)
    return s


def sparkles(t, start, span, n, amp, lo=2400, hi=6000, rise=0.0):
    """Tiny glassy pings scattered in time; rise > 0 makes later ones higher (a shimmer climbing), < 0 falling."""
    s = np.zeros_like(t)
    for _ in range(n):
        u = rng.random()
        f = lo + (hi - lo) * np.clip(rng.random() * 0.6 + (u * rise if rise >= 0 else (1 - u) * -rise) * 0.6, 0, 1)
        s += celesta(t, f, start + u * span, 0.5, amp * (0.6 + 0.4 * rng.random()), decay=10)
    return s


def shimmer_swell(t, start, dur, amp, f_lo=1500, f_hi=7000):
    """Airy noise swept upward through a band-pass, swelling in: the 'magic gathering' whoosh."""
    s = np.zeros_like(t)
    m, tt = window(t, start, dur)
    noise = rng.standard_normal(len(tt))
    out = np.zeros_like(tt)
    steps = 24
    edges = np.linspace(0, len(tt), steps + 1).astype(int)
    for k in range(steps):
        fc = f_lo * (f_hi / f_lo) ** (k / (steps - 1))
        b, a = butter(2, [fc * 0.8 / (SR / 2), min(0.99, fc * 1.25 / (SR / 2))], btype='band')
        seg = lfilter(b, a, noise)[edges[k]:edges[k + 1]]
        out[edges[k]:edges[k + 1]] = seg
    env = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 1.5
    s[m] = out * env * amp
    return s


def reverb(x, wet=0.35, size=1.0):
    """A small Schroeder reverb: parallel combs into series all-passes, with a gentle high cut on the tail."""
    combs = [(int(1557 * size), 0.84), (int(1617 * size), 0.83), (int(1491 * size), 0.85), (int(1422 * size), 0.86),
             (int(1277 * size), 0.87), (int(1356 * size), 0.85)]
    tail = np.zeros_like(x)
    for d, g in combs:
        a = np.zeros(d + 1)
        a[0] = 1
        a[d] = -g
        tail += lfilter([1.0], a, x)
    tail /= len(combs)
    for d, g in ((225, 0.7), (556, 0.7), (441, 0.7), (341, 0.7)):
        b = np.zeros(d + 1)
        b[0] = -g
        b[d] = 1
        a = np.zeros(d + 1)
        a[0] = 1
        a[d] = -g
        tail = lfilter(b, a, tail)
    bl, al = butter(1, 6500 / (SR / 2))
    tail = lfilter(bl, al, tail)
    return x * (1 - wet * 0.5) + tail * wet * 2.2


def write(path, out, seconds, wet=0.35, size=1.0, gain=1.0):
    t = timeline(seconds)
    out = reverb(out, wet, size)
    hp_b, hp_a = butter(2, 120 / (SR / 2), btype='high')
    out = lfilter(hp_b, hp_a, out)
    out = out / (np.max(np.abs(out)) * 1.1) * gain
    out *= np.clip((seconds - t) / 0.35, 0, 1)
    data = (out * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print('wrote', path)


def main(folder):
    os.makedirs(folder, exist_ok=True)

    # Objective counted: E6 then B6, glassy, with a little puff of sparkle
    T = 1.4
    t = timeline(T)
    out = celesta(t, note('E6'), 0.0, 1.2, 0.55) + celesta(t, note('B6'), 0.075, 1.2, 0.5)
    out += sparkles(t, 0.06, 0.25, 7, 0.08, lo=3500, hi=7000, rise=1.0)
    out += shimmer_swell(t, 0.0, 0.35, 0.05, 3000, 8000)
    write(os.path.join(folder, 'QuestProgress.wav'), out, T, wet=0.3, size=0.8, gain=0.7)   # it plays often: softer

    # Step done: G5 B5 D6 G6 B6 rising quickly on a climbing glissando of sparkles
    T = 2.0
    t = timeline(T)
    out = np.zeros_like(t)
    for i, n in enumerate(('G5', 'B5', 'D6', 'G6', 'B6')):
        out += celesta(t, note(n), i * 0.06, 1.6, 0.42)
    out += bell(t, note('G4'), 0.0, 1.6, 0.14, decay=2.2)
    out += sparkles(t, 0.05, 0.6, 16, 0.06, lo=2500, hi=8000, rise=1.0)
    out += shimmer_swell(t, 0.0, 0.5, 0.07, 2000, 8000)
    write(os.path.join(folder, 'QuestObjective.wav'), out, T, wet=0.35)

    # Quest complete: magic gathers (shimmer swell and a rising run), then a bright chord blooms over a choir and a
    # deep bell, and sparkles fall away into the reverb. In D, with a lifted fourth (G#) for a touch of wonder.
    T = 3.6
    t = timeline(T)
    out = shimmer_swell(t, 0.0, 0.75, 0.12, 1200, 8000)
    for i, n in enumerate(('D5', 'F#5', 'A5', 'C#6', 'E6', 'G#6')):
        out += celesta(t, note(n), 0.05 + i * 0.07, 0.9, 0.22, decay=4)
    hit = 0.55
    for i, n in enumerate(('D6', 'F#6', 'A6', 'D7')):
        out += celesta(t, note(n), hit + i * 0.03, 2.8, 0.36, decay=1.4)
    out += bell(t, note('D4'), hit, 3.0, 0.32, decay=0.9) + bell(t, note('A4'), hit + 0.02, 2.6, 0.16, decay=1.2)
    out += choir(t, [note('D4'), note('F#4'), note('A4'), note('D5')], hit - 0.15, 2.9, 0.42, swell=0.45)
    out += sparkles(t, hit, 1.8, 34, 0.05, lo=2200, hi=8000, rise=-1.0)
    write(os.path.join(folder, 'QuestComplete.wav'), out, T, wet=0.42, size=1.15)

    # Discovery: a harp run up C Lydian (the raised fourth, F#, sounds like wonder) over a soft shimmering pad and chimes
    T = 3.0
    t = timeline(T)
    run = ('C5', 'D5', 'E5', 'F#5', 'G5', 'A5', 'B5', 'C6', 'D6', 'E6', 'F#6', 'G6')
    out = np.zeros_like(t)
    for i, n in enumerate(run):
        out += harp(t, note(n), i * 0.05, 1.8, 0.3)
        out += celesta(t, note(n) * 2, i * 0.05 + 0.01, 0.8, 0.06, decay=5)
    end = len(run) * 0.05
    out += choir(t, [note('C5'), note('E5'), note('G5'), note('B5')], 0.15, 2.6, 0.22, swell=0.7)
    for n in ('E6', 'G6', 'B6', 'D7'):
        out += celesta(t, note(n), end, 2.0, 0.14, decay=1.8)
    out += sparkles(t, 0.3, 1.8, 22, 0.045, lo=3000, hi=8000, rise=0.3)
    out += shimmer_swell(t, 0.0, 1.0, 0.07, 1500, 7000)
    write(os.path.join(folder, 'Discovery.wav'), out, T, wet=0.42, size=1.1)


if __name__ == '__main__':
    main(sys.argv[1])
