"""The duel clock's countdown ticks (Audio/UI), original synthesis, 44.1 kHz mono 16-bit.

    Clock_Tick.wav      one second of the last ten ticking away: a wooden click with a short, hollow knock (0.35 s)
    Clock_TickLast.wav  the last three seconds: a brighter click over a little bell, so the end is heard coming (0.6 s)

    python3 Tools/make_clock_tick.py Assets/WizardsVRC/Audio/UI      (needs numpy)
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(1110)


def env(t, attack, decay):
    return np.minimum(1.0, t / max(attack, 1e-5)) * np.exp(-t / decay)


def click(t, amp, bright):
    """A short burst of noise, high-passed by differencing (brighter with more passes)."""
    n = rng.standard_normal(len(t))
    for _ in range(bright):
        n = np.diff(n, prepend=0.0)
    n /= np.max(np.abs(n)) + 1e-9
    return amp * n * env(t, 0.0004, 0.004)


def knock(t, freqs, amps, decay):
    """A hollow wooden knock: a few inharmonic partials dying away fast, each a little faster than the last."""
    s = np.zeros_like(t)
    for k, (f, a) in enumerate(zip(freqs, amps)):
        s += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * env(t, 0.0015, decay / (1 + 0.6 * k))
    return s


def bell(t, f, amp, decay):
    s = np.zeros_like(t)
    for ratio, a in ((1.0, 1.0), (2.76, 0.45), (5.4, 0.2), (8.9, 0.08)):
        s += a * np.sin(2 * np.pi * f * ratio * t) * env(t, 0.002, decay / (1 + ratio * 0.25))
    return amp * s


def write(path, s):
    s = s / (np.max(np.abs(s)) + 1e-9) * 0.85
    fade = int(0.01 * SR)
    s[-fade:] *= np.linspace(1, 0, fade)
    data = (s * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", path)


def main(out):
    os.makedirs(out, exist_ok=True)
    t = np.arange(int(0.35 * SR)) / SR
    tick = click(t, 0.9, 2) + knock(t, [1450, 2310, 3620], [0.8, 0.45, 0.25], 0.03) + knock(t, [610], [0.35], 0.05)
    write(os.path.join(out, "Clock_Tick.wav"), tick)

    t = np.arange(int(0.6 * SR)) / SR
    last = click(t, 1.0, 3) + knock(t, [1900, 2950, 4300], [0.7, 0.4, 0.2], 0.03) + bell(t, 1318.5, 0.55, 0.22)
    write(os.path.join(out, "Clock_TickLast.wav"), last)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/WizardsVRC/Audio/UI")
