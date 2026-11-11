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
    Pattern_Slash.wav    a stroke cut with the Runecutter blade: a fast swish and a bright steel ring (0.6 s)
    Pattern_Shot.wav     a rune-shot from the Dominion sidearm: a crack, a short punch and a brass ping (0.6 s)
    Pattern_Beat.wav     the Conductor's Baton's metronome: one dry wooden click (0.2 s)
    Pattern_Wisp.wav     the Wisp Lantern's wisp setting off: a soft breathy glide up (0.8 s)
    Spell_Charge.wav     a big spell charging: a hum that climbs and swells for 7 s over a quickening pulse, a rising
                         rush of air and a gathering shimmer, cut off at its height (SpellCinematic plays its last few
                         seconds so that it ends on the release)
    Spell_Surge.wav      the release of a big spell: a double boom and a rush of air blown outward, a low bell (1.9 s)
    Charm_Appear.wav     a charm, shield or trap appearing round its owner: a bright chime with a breath of shimmer (0.7 s;
                         BattleClient pitches it: blades high, traps middling, shields low)
    Charm_Shatter.wav    one being used up: a quick rising ring as it flashes white, then the crack and a spray of
                         glass (0.8 s; pitched the same way)
    Spell_Ignite.wav     the battle aura catching alight round a caster as they charge: a rush of air, a thump and
                         its fluttering roar (1.3 s)

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


def write_cut(path, out, gain=1.0):
    """Like q.write, but it ends where it ends: no tail and no fade but a few milliseconds against a click."""
    import wave
    out = q.reverb(out, 0.25, 0.9)
    hp_b, hp_a = butter(2, 60 / (SR / 2), btype='high')
    out = lfilter(hp_b, hp_a, out)
    out = out / (np.max(np.abs(out)) * 1.1) * gain
    fade = int(0.012 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    out[:fade] *= np.linspace(0, 1, fade)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((out * 32767).astype(np.int16).tobytes())
    print('wrote', path)


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

    # (Added later, after every sound above, so they keep their random draws.)
    # A stroke cut: a fast high swish and a bright steel ring.
    T = 0.6
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.18, 1500, 9000, 0.45, shape=0.9) + q.bell(t, n('B6'), 0.08, 0.5, 0.12, decay=6)
    out += q.celesta(t, n('F#7'), 0.08, 0.4, 0.1, decay=8)
    q.write(os.path.join(folder, 'Pattern_Slash.wav'), out, T, wet=0.15, size=0.5, gain=0.7)

    # A rune-shot: a crack, a short punch, a brass ping.
    T = 0.6
    t = q.timeline(T)
    out = crack(t, 0.0, 0.7, dur=0.12, hp=1500) + boom(t, 0.0, 0.3, 220, 70, 0.6, decay=14) + q.bell(t, n('E6'), 0.03, 0.5, 0.08, decay=7)
    q.write(os.path.join(folder, 'Pattern_Shot.wav'), out, T, wet=0.12, size=0.6, gain=0.75)

    # The metronome: one dry wooden click.
    T = 0.2
    t = q.timeline(T)
    out = boom(t, 0.0, 0.08, 1800, 900, 0.5, decay=60) + crack(t, 0.0, 0.25, dur=0.03, hp=2500)
    q.write(os.path.join(folder, 'Pattern_Beat.wav'), out, T, wet=0.05, size=0.3, gain=0.6)

    # The wisp sets off: a breathy glide up with a glint.
    T = 0.8
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.6, 500, 3500, 0.18, shape=1.6) + q.celesta(t, n('A6'), 0.3, 0.45, 0.1, decay=6)
    q.write(os.path.join(folder, 'Pattern_Wisp.wav'), out, T, wet=0.3, size=0.8, gain=0.55)

    # A big spell charging: everything climbs to the last instant, then stops dead (the release sound takes over).
    T = 7.0
    t = q.timeline(T)
    k = t / T                                   # 0 at the start, 1 on the release
    swell = k ** 1.6
    # The hum: a low tone gliding up a fifth, with a fifth and an octave over it, beating slowly.
    f = 73.4 * (1.5 ** k)
    phase = 2 * np.pi * np.cumsum(f) / SR
    hum = np.sin(phase) + 0.5 * np.sin(phase * 1.5) + 0.35 * np.sin(phase * 2.0 + 0.4 * np.sin(2 * np.pi * 0.7 * t))
    hum += 0.25 * np.sin(phase * 3.0) * k + 0.18 * np.sin(phase * 4.0) * k * k
    # The pulse: a heartbeat that quickens from one a second to three (the rings beating out from the caster).
    rate = 1.05 + 2.1 * k
    beat = 0.5 + 0.5 * np.cos(2 * np.pi * np.cumsum(rate) / SR)
    out = hum * swell * (0.55 + 0.45 * beat ** 3) * 0.5
    # A rush of air rising with it, and a second, higher one in the last three seconds.
    out += sweep_noise(t, 0.0, T * 2.0, 180, 5200, 0.34, shape=1.0)[:len(t)] * swell * 1.4
    out += sweep_noise(t, 4.0, 6.0, 900, 9000, 0.22, shape=1.0)[:len(t)] * np.clip((t - 4.0) / 3.0, 0, 1) ** 2 * 1.6
    # The shimmer gathering over the last half, and a choir holding under it.
    out += q.shimmer_swell(t, 3.2, 7.6, 0.2, 2200, 9000) * np.clip((t - 3.2) / 3.8, 0, 1) ** 1.5
    out += q.choir(t, [n('D3'), n('A3'), n('D4')], 1.5, 11.0, 0.24, swell=0.5) * swell
    # The last third of a second: everything is drawn in.
    out += sweep_noise(t, 6.55, 0.9, 700, 9500, 0.45, shape=2.2)[:len(t)]
    write_cut(os.path.join(folder, 'Spell_Charge.wav'), out, gain=0.9)

    # The release of a big spell: a double boom, the air blown outward, a low bell under it.
    T = 2.0
    t = q.timeline(T)
    out = boom(t, 0.0, 1.5, 150, 42, 1.0, decay=2.6) + boom(t, 0.07, 1.3, 96, 34, 0.7, decay=3.0)
    out += crack(t, 0.0, 0.55, dur=0.3) + rumble(t, 0.0, 1.6, 0.4)
    out += sweep_noise(t, 0.0, 1.0, 7500, 260, 0.5, shape=0.7) + sweep_noise(t, 0.05, 1.3, 2400, 180, 0.25, shape=0.9)
    out += q.bell(t, n('D3'), 0.03, 1.7, 0.22, decay=1.6) + q.sparkles(t, 0.05, 0.9, 18, 0.04, lo=2500, hi=8000, rise=-0.8)
    q.write(os.path.join(folder, 'Spell_Surge.wav'), out, T, wet=0.32, size=1.1, gain=0.95)

    # A charm, shield or trap appears: a bright two-note chime and a breath of shimmer.
    T = 0.75
    t = q.timeline(T)
    out = q.celesta(t, n('E6'), 0.0, 0.6, 0.5, decay=5) + q.celesta(t, n('B6'), 0.05, 0.55, 0.35, decay=6)
    out += q.bell(t, n('E5'), 0.0, 0.6, 0.12, decay=4) + q.shimmer_swell(t, 0.0, 0.3, 0.06, 3000, 9000)
    out += q.sparkles(t, 0.03, 0.25, 6, 0.04, lo=4000, hi=9000, rise=0.5)
    q.write(os.path.join(folder, 'Charm_Appear.wav'), out, T, wet=0.22, size=0.7, gain=0.7)

    # One is used up: a ring that climbs as it flashes white, then the crack and the glass flying.
    T = 0.85
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.16, 2500, 9500, 0.25, shape=1.4) + q.celesta(t, n('A6'), 0.0, 0.2, 0.25, decay=10)
    out += crack(t, 0.14, 0.7, dur=0.16, hp=2200) + boom(t, 0.14, 0.25, 420, 160, 0.3, decay=16)
    out += q.sparkles(t, 0.15, 0.45, 22, 0.07, lo=3500, hi=10000, rise=-1.2)
    out += sweep_noise(t, 0.14, 0.4, 9000, 2500, 0.16, shape=0.7)
    q.write(os.path.join(folder, 'Charm_Shatter.wav'), out, T, wet=0.2, size=0.6, gain=0.8)

    # The battle aura catching alight round a caster: air rushing in and up, a thump as it catches, then its roar
    # fluttering as it settles.
    T = 1.3
    t = q.timeline(T)
    out = sweep_noise(t, 0.0, 0.22, 300, 5200, 0.5, shape=1.2)
    out += boom(t, 0.18, 0.8, 130, 48, 0.8, decay=4.0) + crack(t, 0.18, 0.3, dur=0.2, hp=1500)
    out += sweep_noise(t, 0.18, 1.0, 3800, 700, 0.45, shape=0.6) + rumble(t, 0.2, 1.0, 0.3)
    out = out * np.where(t > 0.2, 0.75 + 0.25 * np.sin(2 * np.pi * 23 * t), 1.0)
    q.write(os.path.join(folder, 'Spell_Ignite.wav'), out, T, wet=0.25, size=0.9, gain=0.85)


if __name__ == '__main__':
    main(sys.argv[1])
