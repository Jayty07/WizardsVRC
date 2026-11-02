"""Monster voices (Audio/Creatures/<Voice>_<Cast|Hit|Die>.wav): what a monster sounds like when it casts, gets hit and
is defeated, for each kind of body (see WizardsVRCSceneBuilder.Rigs.cs):

    Biped      a gruff voice: a "hah!" as it casts, an "oof" when hit, a falling groan when beaten
    Beast      a snarl, a yelp, a whimpering howl
    Spirit     a rising wail with a chime, a hollow gasp, a fading moan
    Plant      a creaking rustle, a snap of twigs, a long wilting creak
    Construct  a grinding rumble, a stone clank, a crumble of rubble
    Machine    a steam whistle, a metal clang, a hissing groan winding down
    Deep       a low roar, a gurgling grunt, a sinking bellow

22.05 kHz mono 16-bit.    python3 Tools/make_creature_sounds.py Assets/WizardsVRC/Audio/Creatures   (needs numpy)
"""
import os
import sys
import wave

import numpy as np

SR = 22050
rng = np.random.default_rng(31)


def t_of(dur):
    return np.arange(int(dur * SR)) / SR


def envelope(n, attack, release_at=None, release=0.1):
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(attack, 1e-4))
    if release_at is not None:
        e *= np.clip(1 - (t - release_at) / release, 0, 1)
    return e


def band(x, lo, hi):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    f[0] = 1e-3
    g = 1 / (1 + (lo / f) ** 4) / (1 + (f / hi) ** 4)
    return np.fft.irfft(spec * g, len(x))


def voiced(pitch, formants, breath=0.15):
    """A glottal buzz (sawtooth-ish) at the pitch contour, shaped by vowel formants (frequency, width, gain)."""
    ph = 2 * np.pi * np.cumsum(pitch) / SR
    buzz = sum(np.sin(k * ph) / k for k in range(1, 30))
    buzz += rng.standard_normal(len(pitch)) * breath
    out = np.zeros(len(pitch))
    for f, w, g in formants:
        out += band(buzz, f - w, f + w) * g
    return out


def vowel(name):
    return {
        'ah': [(750, 120, 1.0), (1200, 150, 0.6), (2500, 200, 0.25)],
        'oh': [(500, 100, 1.0), (900, 120, 0.5), (2400, 200, 0.15)],
        'uh': [(600, 110, 1.0), (1000, 150, 0.5), (2300, 200, 0.2)],
        'ee': [(300, 80, 1.0), (2200, 200, 0.6), (3000, 250, 0.3)],
    }[name]


def glide(a, b, n, curve=1.0):
    return a + (b - a) * np.linspace(0, 1, n) ** curve


def noise_burst(dur, lo, hi, decay):
    n = int(dur * SR)
    return band(rng.standard_normal(n), lo, hi) * np.exp(-np.arange(n) / SR * decay)


def clank(f0, dur=0.8, decay=6.0):
    t = t_of(dur)
    s = sum(a * np.sin(2 * np.pi * f0 * r * t) * np.exp(-t * decay * d) for r, a, d in ((1, 1, 1), (2.7, 0.6, 1.5), (5.2, 0.35, 2.2), (8.9, 0.2, 3)))
    return s + rng.standard_normal(len(t)) * np.exp(-t * 80) * 0.5


def pad(*parts):
    return np.concatenate(parts)


def gruff(dur, p0, p1, v, rough=0.3):
    n = int(dur * SR)
    pitch = glide(p0, p1, n) * (1 + rough * 0.08 * np.sin(2 * np.pi * 31 * t_of(dur)))
    return voiced(pitch, vowel(v), breath=rough) * envelope(n, 0.02, dur * 0.7, dur * 0.3)


def make():
    S = {}
    # Biped
    S['Biped_Cast'] = gruff(0.45, 150, 190, 'ah', 0.4)
    S['Biped_Hit'] = gruff(0.3, 170, 120, 'uh', 0.5)
    S['Biped_Die'] = gruff(1.1, 160, 70, 'oh', 0.6)
    # Beast
    S['Beast_Cast'] = gruff(0.55, 110, 140, 'ah', 1.4) + noise_burst(0.55, 200, 1500, 3) * 0.4
    S['Beast_Hit'] = gruff(0.22, 600, 420, 'ee', 0.4)
    S['Beast_Die'] = gruff(1.2, 520, 220, 'oh', 0.5)
    # Spirit
    n = int(0.9 * SR)
    wail = np.sin(2 * np.pi * np.cumsum(glide(400, 900, n, 0.7) * (1 + 0.02 * np.sin(2 * np.pi * 6 * t_of(0.9)))) / SR)
    chime = sum(np.sin(2 * np.pi * f * t_of(0.9)) * np.exp(-t_of(0.9) * 4) for f in (1760, 2637))
    S['Spirit_Cast'] = (wail * 0.6 + chime * 0.25 + band(rng.standard_normal(n), 800, 4000) * 0.15) * envelope(n, 0.1, 0.6, 0.3)
    S['Spirit_Hit'] = noise_burst(0.35, 500, 3000, 9) + np.sin(2 * np.pi * np.cumsum(glide(700, 350, int(0.35 * SR))) / SR) * 0.4 * np.exp(-t_of(0.35) * 6)
    n = int(1.4 * SR)
    S['Spirit_Die'] = np.sin(2 * np.pi * np.cumsum(glide(600, 150, n, 0.6)) / SR) * envelope(n, 0.05, 0.8, 0.6) * 0.7 + band(rng.standard_normal(n), 300, 2000) * 0.2 * envelope(n, 0.3, 1.0, 0.4)
    # Plant
    def creak(dur, f0, f1):
        n = int(dur * SR)
        clicks = np.zeros(n)
        f = glide(f0, f1, n)
        ph = np.cumsum(f) / SR
        clicks[np.where(np.diff(np.floor(ph)) > 0)[0]] = 1
        return band(clicks, 200, 3000) * envelope(n, 0.02, dur * 0.8, dur * 0.2) * 3
    S['Plant_Cast'] = creak(0.6, 30, 80) + noise_burst(0.6, 2000, 7000, 3) * 0.3
    S['Plant_Hit'] = pad(noise_burst(0.05, 1000, 6000, 40), noise_burst(0.05, 1000, 6000, 40) * 0.7, noise_burst(0.15, 800, 5000, 25) * 0.5)
    S['Plant_Die'] = creak(1.3, 60, 12) + noise_burst(1.3, 1500, 6000, 2) * 0.2
    # Construct
    grind = band(rng.standard_normal(int(0.7 * SR)), 60, 400) * envelope(int(0.7 * SR), 0.1, 0.5, 0.2)
    S['Construct_Cast'] = grind + clank(140, 0.7, 3) * 0.3
    S['Construct_Hit'] = clank(260, 0.5, 8)
    rubble = np.zeros(int(1.4 * SR))
    for k in range(14):
        b = noise_burst(0.12, 100, 1500, 30) * rng.uniform(0.3, 1)
        s = int(rng.uniform(0, 1.2) * SR)
        rubble[s:s + len(b)] += b[:len(rubble) - s]
    S['Construct_Die'] = rubble + band(rng.standard_normal(len(rubble)), 40, 200) * envelope(len(rubble), 0.05, 0.9, 0.5) * 0.8
    # Machine
    n = int(0.8 * SR)
    whistle = sum(np.sin(2 * np.pi * f * t_of(0.8)) for f in (740, 932, 1108)) * envelope(n, 0.05, 0.6, 0.2) + band(rng.standard_normal(n), 2000, 6000) * 0.2 * envelope(n, 0.05, 0.6, 0.2)
    S['Machine_Cast'] = whistle
    S['Machine_Hit'] = clank(330, 0.6, 5)
    n = int(1.5 * SR)
    S['Machine_Die'] = band(rng.standard_normal(n), 1500, 7000) * envelope(n, 0.02, 0.6, 0.9) * 0.5 + np.sin(2 * np.pi * np.cumsum(glide(220, 50, n)) / SR) * envelope(n, 0.05, 1.0, 0.5) * 0.4
    # Deep
    S['Deep_Cast'] = gruff(1.0, 55, 75, 'oh', 1.2) + band(rng.standard_normal(int(1.0 * SR)), 40, 300) * 0.5 * envelope(int(1.0 * SR), 0.2, 0.7, 0.3)
    S['Deep_Hit'] = gruff(0.4, 80, 60, 'uh', 0.8) + noise_burst(0.4, 100, 800, 5) * 0.3
    S['Deep_Die'] = gruff(1.6, 70, 30, 'oh', 1.0)
    return S


def write(path, x):
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.85
    fade = np.minimum(1, (len(x) - np.arange(len(x))) / (0.02 * SR))
    data = (x * fade * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else 'Assets/WizardsVRC/Audio/Creatures'
    os.makedirs(out, exist_ok=True)
    for name, x in make().items():
        write(os.path.join(out, name + '.wav'), x)
        print('wrote', name, '%.2f s' % (len(x) / SR))
