"""The duel circle's turn clock: Assets/WizardsVRC/Textures/DuelClock.png (the dial) and DuelClockHand.png (its hand).

Drawn in the same style as the circle's layers (DuelCircle*.png): slate blue, lavender lines, gold trim. The dial sits
in the middle of the sigil. Its outer track (radius 0.78-0.93 of the texture) is left dark: the DuelClock shader draws
the lit time segments there. Inside: twelve rune ticks (the four quarters larger), an inner ring and a star boss in the
middle. The hand is a gold pointer from the middle to the ticks with a crescent tail.

    python3 Tools/make_duel_clock.py            (needs numpy and Pillow; run from the repo root)
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Textures')
N = 256

SLATE = (74, 86, 128)
SLATE_DARK = (40, 42, 70)
LAVENDER = (170, 178, 230)
GOLD = (240, 196, 80)
GOLD_DARK = (150, 104, 30)
INK = (28, 22, 40)

yy, xx = np.mgrid[0:N, 0:N]
px = (xx + 0.5) / N * 2 - 1
py = 1 - (yy + 0.5) / N * 2           # texture up = +1
R = np.sqrt(px * px + py * py)
A = (np.degrees(np.arctan2(px, py)) + 360) % 360   # clockwise from the top


def ring(img, r0, r1, col):
    img[(R >= r0) & (R < r1)] = col + (255,)


def dial():
    img = np.zeros((N, N, 4), np.uint8)
    ring(img, 0.0, 0.99, SLATE)
    ring(img, 0.955, 0.99, GOLD)
    ring(img, 0.93, 0.955, INK)
    ring(img, 0.78, 0.93, SLATE_DARK)          # the shader paints the time track here
    ring(img, 0.755, 0.78, GOLD)
    # Rune ticks: twelve wedges, the quarters longer and gold, the rest lavender.
    for k in range(12):
        ang = k * 30
        d = np.abs(((A - ang) + 180) % 360 - 180)
        big = k % 3 == 0
        r0 = 0.52 if big else 0.6
        w = 5.5 if big else 3.2
        mask = (R > r0) & (R < 0.73) & (d < w * (1.2 - (R - r0)))
        img[mask] = (GOLD if big else LAVENDER) + (255,)
        if big:
            dot = (np.hypot(px - math.sin(math.radians(ang)) * 0.45, py - math.cos(math.radians(ang)) * 0.45) < 0.035)
            img[dot] = GOLD + (255,)
    ring(img, 0.36, 0.385, LAVENDER)
    ring(img, 0.2, 0.36, SLATE_DARK)
    # Star boss: an eight-point star in gold over the middle.
    star = R < (0.12 + 0.07 * np.abs(np.cos(np.radians(A) * 4)) ** 6)
    img[star] = GOLD + (255,)
    img[R < 0.05] = GOLD_DARK + (255,)
    # A dark rim pixel inside each coloured ring edge, PS1 style.
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, 'DuelClock.png'))


def hand():
    img = np.zeros((N, N, 4), np.uint8)
    # Pointer: a long diamond up to the ticks.
    t = py.copy()
    blade = (t > -0.05) & (t < 0.72) & (np.abs(px) < 0.07 * (1 - np.clip((t - 0.1) / 0.62, 0, 1)) + 0.012)
    outline = (t > -0.08) & (t < 0.75) & (np.abs(px) < 0.07 * (1 - np.clip((t - 0.1) / 0.62, 0, 1)) + 0.03)
    tail = (np.hypot(px, py + 0.16) < 0.11) & (np.hypot(px, py + 0.22) > 0.09)
    boss = R < 0.075
    img[outline | (tail & True)] = INK + (255,)
    img[blade] = GOLD + (255,)
    img[blade & (px > 0.01)] = GOLD_DARK + (255,)       # the shaded side of the blade
    img[tail & (np.hypot(px, py + 0.16) < 0.095)] = GOLD + (255,)
    img[boss] = INK + (255,)
    img[R < 0.055] = GOLD + (255,)
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, 'DuelClockHand.png'))


dial()
hand()
print('wrote DuelClock.png and DuelClockHand.png')
