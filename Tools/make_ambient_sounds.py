"""Ambient sound loops (Audio/Ambient/*.wav): one bed per kind of place, plus a few point sounds (fire, falling water).

Every loop is built to repeat without a seam: noise is shaped in the frequency domain over the whole loop (so the
filtering wraps round), slow swells use whole numbers of cycles per loop, and one-off events (a bird, a clank, a drip)
that run past the end wrap round to the start. 22.05 kHz mono 16-bit, 20 s each (the fire and water loops 8 s).

    python3 Tools/make_ambient_sounds.py Assets/WizardsVRC/Audio/Ambient      (needs numpy)
"""
import os
import sys
import wave

import numpy as np

SR = 22050


def loop_len(seconds):
    return int(SR * seconds)


def shaped_noise(n, rng, lo, hi, tilt=0.0):
    """White noise band-passed lo..hi Hz (soft edges), circular. tilt < 0 leans toward the lows (-1 ~ brown)."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = 1e-3
    gain = 1 / (1 + (lo / f) ** 4) / (1 + (f / hi) ** 4)
    gain *= (f / 1000.0) ** tilt
    gain[0] = 0
    out = np.fft.irfft(spec * gain, n)
    return out / (np.std(out) + 1e-9)


def swell(n, cycles, depth, phase=0.0, sharp=1.0):
    """A slow, seamless swell between 1 - depth and 1 (whole cycles per loop)."""
    t = np.arange(n) / n
    s = 0.5 + 0.5 * np.sin(2 * np.pi * cycles * t + phase)
    return 1 - depth + depth * s ** sharp


def wobble(n, rng, parts=5, depth=0.5):
    """A random but seamless slow variation (a few whole-cycle sines)."""
    t = np.arange(n) / n
    w = np.zeros(n)
    for k in range(parts):
        w += rng.uniform(0.3, 1) * np.sin(2 * np.pi * rng.integers(1, 7) * t + rng.uniform(0, 6.28))
    w /= np.max(np.abs(w)) + 1e-9
    return 1 - depth / 2 + depth / 2 * w


def place(buf, ev, start):
    """Adds an event at 'start' (samples), wrapping round the loop's end."""
    n = len(buf)
    idx = (np.arange(len(ev)) + start) % n
    np.add.at(buf, idx, ev)


def env(n_ev, attack, decay):
    t = np.arange(n_ev) / SR
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t * decay)


def chirp(rng, f0, f1, dur, amp, warble=0.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = np.linspace(f0, f1, n) * (1 + warble * np.sin(2 * np.pi * rng.uniform(25, 45) * t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    e = np.sin(np.pi * np.arange(n) / n) ** 1.5
    return np.sin(ph) * e * amp


def bird_song(rng, amp):
    """A little phrase of 2-6 chirps."""
    notes = []
    base = rng.uniform(2200, 4200)
    for k in range(rng.integers(2, 7)):
        f0 = base * rng.uniform(0.85, 1.25)
        notes.append(chirp(rng, f0, f0 * rng.uniform(0.7, 1.4), rng.uniform(0.05, 0.16), amp, warble=rng.uniform(0, 0.08)))
        notes.append(np.zeros(int(rng.uniform(0.02, 0.09) * SR)))
    return np.concatenate(notes)


def metal_clank(rng, amp, f0=None):
    f0 = f0 or rng.uniform(380, 700)
    n = int(1.4 * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for ratio, a, d in ((1, 1, 4), (2.76, 0.6, 6), (5.4, 0.4, 9), (8.9, 0.25, 12)):
        s += a * np.sin(2 * np.pi * f0 * ratio * t) * np.exp(-t * d)
    s += rng.standard_normal(n) * np.exp(-t * 60) * 0.6
    return s * amp


def drip(rng, amp):
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    f = rng.uniform(900, 1700) * (1 + 0.6 * np.exp(-t * 40))
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 18)
    # A cave echo or two.
    out = s.copy()
    for k, g in ((int(0.18 * SR), 0.35), (int(0.37 * SR), 0.15)):
        out[k:] += s[:n - k] * g
    return out * amp


def thunder(rng, n_total, amp):
    n = int(rng.uniform(3.5, 5.5) * SR)
    rum = shaped_noise(n, rng, 25, 220, tilt=-1.2)
    t = np.arange(n) / SR
    e = np.minimum(1, t / 0.4) * np.exp(-t * 0.9) * (1 + 0.6 * np.sin(2 * np.pi * 1.3 * t) ** 2)
    return rum * e * amp


def crackle(n, rng, rate, amp):
    """Fire: a soft roar plus random pops."""
    s = shaped_noise(n, rng, 120, 1800, tilt=-0.6) * 0.25 * wobble(n, rng, 6, 0.6)
    for k in range(int(n / SR * rate)):
        pop_n = int(rng.uniform(0.004, 0.03) * SR)
        pop = rng.standard_normal(pop_n) * env(pop_n, 0.0005, rng.uniform(150, 400))
        place(s, pop * rng.uniform(0.4, 1.4), rng.integers(0, n))
    return s * amp


def scatter(buf, rng, count, make):
    for k in range(count):
        place(buf, make(), int(rng.integers(0, len(buf))))


def normalize(x, rms=0.13, peak=0.9):
    """Every loop at about the same loudness (RMS), never clipping."""
    r = np.sqrt(np.mean(x ** 2)) + 1e-9
    return x * min(rms / r, peak / (np.max(np.abs(x)) + 1e-9))


def write(path, x):
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def make_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    L = loop_len(20)
    sounds = {}

    rng = np.random.default_rng(101)
    breeze = shaped_noise(L, rng, 150, 1400, tilt=-0.5) * wobble(L, rng, 5, 0.7) * 0.2
    scatter(breeze, rng, 9, lambda: bird_song(rng, 0.22))
    sounds['Amb_Breeze'] = breeze

    rng = np.random.default_rng(102)
    leaves = shaped_noise(L, rng, 400, 5000, tilt=-0.2) * wobble(L, rng, 6, 0.85) * 0.16
    low = shaped_noise(L, rng, 60, 400, tilt=-0.8) * 0.1
    forest = leaves + low
    scatter(forest, rng, 14, lambda: bird_song(rng, 0.2))
    for k in range(3):
        # A woodpecker, far off.
        taps = np.concatenate([np.concatenate([metal_clank(rng, 0.08, 1200)[:int(0.03 * SR)], np.zeros(int(0.05 * SR))]) for _ in range(rng.integers(5, 10))])
        place(forest, taps, int(rng.integers(0, L)))
    sounds['Amb_Forest'] = forest

    rng = np.random.default_rng(103)
    surf = shaped_noise(L, rng, 80, 2500, tilt=-0.7)
    sea = surf * swell(L, 3, 0.75, sharp=2.0) * 0.35 + shaped_noise(L, rng, 1500, 6000) * swell(L, 3, 0.9, 0.6, 3.0) * 0.08
    scatter(sea, rng, 4, lambda: chirp(rng, 1800, 1100, 0.45, 0.12, warble=0.05))
    sounds['Amb_Sea'] = sea

    rng = np.random.default_rng(104)
    roar = shaped_noise(L, rng, 40, 500, tilt=-1.0) * wobble(L, rng, 4, 0.4) * 0.3
    scatter(roar, rng, 11, lambda: metal_clank(rng, 0.25))
    sounds['Amb_Forge'] = roar

    rng = np.random.default_rng(105)
    howl = np.zeros(L)
    for k in range(3):
        band = shaped_noise(L, rng, 250 + k * 180, 420 + k * 260, tilt=0)
        howl += band * swell(L, 2 + k, 0.8, rng.uniform(0, 6), 2.5)
    sounds['Amb_ColdWind'] = howl * 0.18 + shaped_noise(L, rng, 60, 300, tilt=-1) * 0.12

    rng = np.random.default_rng(106)
    gust = shaped_noise(L, rng, 200, 3000, tilt=-0.4) * swell(L, 2, 0.8, 0, 2.0) * wobble(L, rng, 5, 0.5) * 0.24
    hiss = shaped_noise(L, rng, 3000, 9000) * swell(L, 2, 0.95, 0.4, 4.0) * 0.05
    desert = gust + hiss
    scatter(desert, rng, 2, lambda: chirp(rng, 2600, 1900, 1.1, 0.1, warble=0.03))   # a hawk, high up
    sounds['Amb_DesertWind'] = desert

    rng = np.random.default_rng(107)
    town = shaped_noise(L, rng, 200, 2500, tilt=-0.4) * wobble(L, rng, 5, 0.6) * 0.16
    for k in range(4):
        # The windmill's creak.
        creak = chirp(rng, 300, 220, 0.6, 0.08, warble=0.2) + chirp(rng, 600, 460, 0.6, 0.04, warble=0.3)
        place(town, creak, int(rng.integers(0, L)))
    scatter(town, rng, 3, lambda: metal_clank(rng, 0.05, 1700))   # a far-off bell or spur
    sounds['Amb_Boomtown'] = town

    rng = np.random.default_rng(108)
    mine = shaped_noise(L, rng, 30, 180, tilt=-1.2) * wobble(L, rng, 3, 0.4) * 0.3
    scatter(mine, rng, 16, lambda: drip(rng, 0.22))
    scatter(mine, rng, 4, lambda: metal_clank(rng, 0.06, rng.uniform(1300, 1800)))
    sounds['Amb_Mine'] = mine

    rng = np.random.default_rng(109)
    night = shaped_noise(L, rng, 150, 1200, tilt=-0.5) * wobble(L, rng, 4, 0.6) * 0.12
    t = np.arange(L) / SR
    for k in range(5):
        # Crickets: trains of short 4-5 kHz pulses, each cricket at its own rate, fading in and out.
        f = rng.uniform(4200, 5200)
        rate = rng.integers(10, 18)
        pulse = (np.sin(2 * np.pi * rate * t) > 0.6).astype(float)
        pulse = np.fft.irfft(np.fft.rfft(pulse) / (1 + (np.fft.rfftfreq(L, 1 / SR) / 200) ** 2), L)
        night += np.sin(2 * np.pi * f * t) * np.clip(pulse, 0, None) * swell(L, rng.integers(1, 4), 0.9, rng.uniform(0, 6)) * 0.05
    for k in range(3):
        hoot = np.concatenate([chirp(rng, 420, 380, 0.35, 0.12), np.zeros(int(0.25 * SR)), chirp(rng, 420, 370, 0.6, 0.1)])
        place(night, hoot, int(rng.integers(0, L)))
    sounds['Amb_NightDusk'] = night

    rng = np.random.default_rng(110)
    storm = shaped_noise(L, rng, 150, 2500, tilt=-0.5) * swell(L, 3, 0.7, 0, 2) * 0.22
    scatter(storm, rng, 3, lambda: thunder(rng, L, 0.6))
    sounds['Amb_MesaStorm'] = storm

    rng = np.random.default_rng(111)
    yard = shaped_noise(L, rng, 200, 2500, tilt=-0.5) * wobble(L, rng, 4, 0.5) * 0.14
    scatter(yard, rng, 6, lambda: metal_clank(rng, 0.12, rng.uniform(250, 450)))
    for k in range(4):
        steam_n = int(rng.uniform(1.2, 2.4) * SR)
        steam = shaped_noise(steam_n, rng, 2000, 8000) * env(steam_n, 0.05, 1.6) * 0.18
        place(yard, steam, int(rng.integers(0, L)))
    sounds['Amb_Railyard'] = yard

    rng = np.random.default_rng(112)
    tower = shaped_noise(L, rng, 60, 260, tilt=-1) * 0.12 + crackle(L, rng, 9, 0.5)
    sounds['Amb_TowerTorches'] = tower

    rng = np.random.default_rng(113)
    under = shaped_noise(L, rng, 30, 300, tilt=-1.1) * wobble(L, rng, 3, 0.5) * 0.3
    for k in range(30):
        b_n = int(0.12 * SR)
        tt = np.arange(b_n) / SR
        f = rng.uniform(300, 900) * (1 + 3 * tt)
        place(under, np.sin(2 * np.pi * np.cumsum(f) / SR) * env(b_n, 0.003, 25) * 0.15, int(rng.integers(0, L)))
    sounds['Amb_Underwater'] = under

    rng = np.random.default_rng(114)
    train = shaped_noise(L, rng, 40, 400, tilt=-1) * 0.25 + shaped_noise(L, rng, 300, 1500) * 0.06
    clack_period = L // 26   # 26 clack-clacks per loop: seamless
    for k in range(26):
        for off in (0, int(0.16 * SR)):
            c = metal_clank(rng, 0.18, rng.uniform(150, 220))[:int(0.25 * SR)]
            place(train, c, k * clack_period + off)
    ghost = np.zeros(L)
    for k in range(2):
        ghost += shaped_noise(L, rng, 500 + k * 300, 700 + k * 400) * swell(L, 2 + k, 0.9, rng.uniform(0, 6), 3)
    sounds['Amb_GhostTrain'] = train + ghost * 0.1

    # Point sounds (3D, short loops)
    S = loop_len(8)
    rng = np.random.default_rng(115)
    sounds['Amb_Fire'] = crackle(S, rng, 14, 1.0) + shaped_noise(S, rng, 50, 200, tilt=-1) * 0.1
    rng = np.random.default_rng(116)
    sounds['Amb_Water'] = shaped_noise(S, rng, 200, 6000, tilt=-0.3) * wobble(S, rng, 6, 0.3) * 0.5

    for name, x in sounds.items():
        write(os.path.join(out_dir, name + '.wav'), normalize(x))
        print('wrote', name, '%.1f s' % (len(x) / SR))


if __name__ == '__main__':
    make_all(sys.argv[1] if len(sys.argv) > 1 else 'Assets/WizardsVRC/Audio/Ambient')
