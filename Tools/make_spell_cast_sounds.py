"""Spell casting sounds (Audio/Cast), all original synthesis, 44.1 kHz mono 16-bit, built from the same glassy celesta,
bells, choir and reverb as the quest sounds (make_quest_sounds.py).

    Pattern_Grab.wav     the casting wand taken from your hip: a soft shimmer and a low bell (0.7 s)
    Pattern_Tick.wav     a rune point touched: one glassy ping (CastPattern raises its pitch a step each time) (0.45 s)
    Pattern_Miss.wav     a point missed: a muffled, sagging thunk with a puff of air (0.45 s)
    Pattern_Done.wav     the rune drawn (half or more of it): a rising run into a bright chord (1.4 s)
    Pattern_Fail.wav     the rune mostly missed: two falling notes and a fizz (1.0 s)
    Spell_Whoosh.wav     the caster vanishing and reappearing: a swirling whoosh, up and away (1.0 s)
    Spell_Release.wav    the spell let go: a punch of air sweeping down over a thump, sparks off it (0.9 s)
    Spell_Impact.wav     it hits: a falling boom, a crack, a shimmering tail (1.7 s)
    Spell_ImpactBig.wav  a big spell (4+ pips) hits: a deeper double boom and rumble, a bell chord and a choir, debris (2.9 s)

    python3 Tools/make_spell_cast_sounds.py Assets/WizardsVRC/Audio/Cast      (needs numpy and scipy)
"""
import os
import sys

import numpy as np
from scipy.signal import butter, lfilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_quest_sounds as q  # noqa: E402

SR = q.SR
rng = np.random.default_rng(71)


def sweep_noise(t, start, dur, f0, f1, amp, shape=1.5, q_width=0.35):
    """Noise through a band-pass sliding from f0 to f1 over 'dur', swelling in and out (a whoosh)."""
    s = np.zeros_like(t)
    m, tt = q.window(t, start, dur)
    noise = rng.standard_normal(len(tt))
    out = np.zeros_like(tt)
    steps = 28
    edges = np.linspace(0, len(tt), steps + 1).astype(int)
    for k in range(steps):
        fc = f0 * (f1 / f0) ** (k / (steps - 1))
        lo = max(30, fc * (1 - q_width)) / (SR / 2)
        hi = min(0.99, fc * (1 + q_width) / (SR / 2))
        b, a = butter(2, [lo, hi], btype='band')
        out[edges[k]:edges[k + 1]] = lfilter(b, a, noise)[edges[k]:edges[k + 1]]
    env = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** shape
    s[m] = out * env * amp
    return s


def boom(t, start, dur, f0, f1, amp, decay=3.0):
    """A low sine falling from f0 to f1 with a hard attack (the body of an impact)."""
    s = np.zeros_like(t)
    m, tt = q.window(t, start, dur)
    f = f1 + (f0 - f1) * np.exp(-tt * 9)
    phase = 2 * np.pi * np.cumsum(f) / SR
    env = np.exp(-tt * decay) * np.minimum(1, tt / 0.004)
    s[m] = (np.sin(phase) + 0.3 * np.sin(phase * 2.01) * np.exp(-tt * 8)) * env * amp
    return s


def crack(t, start, amp, dur=0.25, hp=900):
    """A sharp noise crack (the snap at the front of a hit)."""
    s = np.zeros_like(t)
    m, tt = q.window(t, start, dur)
    noise = rng.standard_normal(len(tt))
    b, a = butter(2, hp / (SR / 2), btype='high')
    s[m] = lfilter(b, a, noise) * np.exp(-tt * 22) * amp
    return s


def rumble(t, start, dur, amp):
    s = np.zeros_like(t)
    m, tt = q.window(t, start, dur)
    noise = rng.standard_normal(len(tt))
    b, a = butter(2, 140 / (SR / 2), btype='low')
    env = np.minimum(1, tt / 0.05) * np.exp(-tt * 1.6)
    s[m] = lfilter(b, a, noise) * env * amp * 6
    return s


def main(folder):
    os.makedirs(folder, exist_ok=True)
    n = q.note

    # Wand taken: a quick shimmer up and a soft low bell.
    T = 0.8
    t = q.timeline(T)
    out = q.shimmer_swell(t, 0.0, 0.4, 0.07, 2500, 8000) + q.bell(t, n('D4'), 0.02, 0.7, 0.18, decay=3.5)
    out += q.celesta(t, n('A6'), 0.05, 0.6, 0.12, decay=6)
    q.write(os.path.join(folder, 'Pattern_Grab.wav'), out, T, wet=0.25, size=0.7, gain=0.6)

    # A point touched: one glassy ping (pitched up at runtime as the rune goes on).
    T = 0.5
    t = q.timeline(T)
    out = q.celesta(t, n('E6'), 0.0, 0.45, 0.6, decay=7) + q.sparkles(t, 0.0, 0.08, 3, 0.06, lo=4000, hi=8000, rise=1.0)
    q.write(os.path.join(folder, 'Pattern_Tick.wav'), out, T, wet=0.18, size=0.6, gain=0.65)

    # A point missed: a muffled thunk that sags, and a puff.
    T = 0.5
    t = q.timeline(T)
    out = boom(t, 0.0, 0.4, 260, 120, 0.5, decay=10) + sweep_noise(t, 0.0, 0.25, 1800, 500, 0.08, shape=1.0)
    q.write(os.path.join(folder, 'Pattern_Miss.wav'), out, T, wet=0.12, size=0.5, gain=0.5)

    # The rune drawn: a rising run into a bright chord.
    T = 1.5
    t = q.timeline(T)
    out = np.zeros_like(t)
    for i, nn in enumerate(('A5', 'C#6', 'E6', 'A6')):
        out += q.celesta(t, n(nn), i * 0.05, 1.2, 0.35, decay=3)
    for nn in ('A5', 'E6', 'A6', 'C#7'):
        out += q.celesta(t, n(nn), 0.24, 1.2, 0.2, decay=2.2)
    out += q.bell(t, n('A3'), 0.22, 1.2, 0.18, decay=2.0)
    out += q.sparkles(t, 0.2, 0.6, 14, 0.05, lo=3000, hi=8000, rise=-0.5)
    out += q.shimmer_swell(t, 0.0, 0.35, 0.06, 2000, 8000)
    q.write(os.path.join(folder, 'Pattern_Done.wav'), out, T, wet=0.32, size=0.9, gain=0.75)

    # The rune mostly missed: two falling notes (minor) and a fizz.
    T = 1.0
    t = q.timeline(T)
    out = q.celesta(t, n('E5'), 0.0, 0.8, 0.32, decay=4) + q.celesta(t, n('C5'), 0.14, 0.8, 0.3, decay=4)
    out += sweep_noise(t, 0.1, 0.6, 4000, 900, 0.07, shape=1.2)
    q.write(os.path.join(folder, 'Pattern_Fail.wav'), out, T, wet=0.25, size=0.8, gain=0.6)

    # The caster vanishes / appears: a swirling whoosh up and away, with a glint.
    T = 1.0
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.75, 300, 5000, 0.4, shape=1.3) + sweep_noise(t, 0.1, 0.6, 600, 9000, 0.12, shape=2.0)
    out += q.sparkles(t, 0.3, 0.4, 8, 0.05, lo=3000, hi=8000, rise=1.0) + q.bell(t, n('F#5'), 0.35, 0.6, 0.06, decay=4)
    q.write(os.path.join(folder, 'Spell_Whoosh.wav'), out, T, wet=0.3, size=0.9, gain=0.7)

    # Released: a punch of air sweeping down over a thump, a spray of sparks.
    T = 0.9
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.5, 6000, 400, 0.5, shape=0.8) + boom(t, 0.0, 0.5, 160, 60, 0.6, decay=7)
    out += q.sparkles(t, 0.02, 0.3, 10, 0.05, lo=2500, hi=8000, rise=-0.3)
    q.write(os.path.join(folder, 'Spell_Release.wav'), out, T, wet=0.2, size=0.8, gain=0.8)

    # It hits: a falling boom, a crack, a shimmering tail.
    T = 1.8
    t = q.timeline(T)
    out = boom(t, 0.0, 1.4, 110, 38, 0.9, decay=3.2) + crack(t, 0.0, 0.5) + rumble(t, 0.0, 1.2, 0.25)
    out += q.shimmer_swell(t, 0.05, 1.0, 0.06, 6000, 2000) + q.sparkles(t, 0.05, 0.9, 16, 0.04, lo=2500, hi=8000, rise=-1.0)
    q.write(os.path.join(folder, 'Spell_Impact.wav'), out, T, wet=0.3, size=1.0, gain=0.9)

    # A big spell hits: a double boom and rumble, a bell chord and a choir, debris falling.
    T = 3.0
    t = q.timeline(T)
    out = boom(t, 0.0, 2.2, 95, 30, 1.0, decay=2.0) + boom(t, 0.12, 1.8, 70, 28, 0.7, decay=2.4)
    out += crack(t, 0.0, 0.6, dur=0.35) + crack(t, 0.12, 0.3) + rumble(t, 0.0, 2.6, 0.45)
    out += q.bell(t, n('D3'), 0.05, 2.6, 0.3, decay=1.1) + q.bell(t, n('A3'), 0.07, 2.4, 0.2, decay=1.3)
    out += q.choir(t, [n('D3'), n('A3'), n('D4'), n('F4')], 0.02, 2.4, 0.3, swell=0.12)
    out += q.sparkles(t, 0.15, 2.0, 30, 0.04, lo=2000, hi=8000, rise=-1.0)
    q.write(os.path.join(folder, 'Spell_ImpactBig.wav'), out, T, wet=0.38, size=1.2, gain=0.95)


if __name__ == '__main__':
    main(sys.argv[1])
