"""Ring texture (Textures/SpellRing.png) for the spell choreography (SpellCinematic): shock rings, the rings round the
caster's rune, the turning rings of a gyro wind-up and the rims of portals. A bright thin band near the edge with a soft
glow either side, a fainter inner band, and four small notches of light round it so a turning ring reads as turning.
White, with the falloff in both colour and alpha (it's drawn additively).

    python3 Tools/make_spell_ring.py Assets/WizardsVRC/Textures/SpellRing.png      (needs numpy and Pillow)
"""
import sys

import numpy as np
from PIL import Image

N = 256
y, x = np.mgrid[0:N, 0:N]
u = (x + 0.5) / N * 2 - 1
v = (y + 0.5) / N * 2 - 1
r = np.hypot(u, v)
ang = np.arctan2(v, u)


def band(center, width):
    return np.exp(-((r - center) / width) ** 2)


a = band(0.86, 0.035) + 0.45 * band(0.86, 0.11) + 0.35 * band(0.66, 0.025)
# Four bright notches on the outer band, and a faint shimmer of runes between the bands.
notch = np.clip(np.cos(ang * 4) * 4 - 3, 0, 1)
a += 0.6 * notch * band(0.76, 0.04)
a += 0.12 * (0.5 + 0.5 * np.sin(ang * 24)) * band(0.76, 0.05)
a *= np.clip((0.99 - r) / 0.03, 0, 1)
a = np.clip(a / a.max(), 0, 1)
img = np.zeros((N, N, 4), dtype=np.uint8)
for c in range(4):
    img[..., c] = (a * 255).astype(np.uint8)
Image.fromarray(img, 'RGBA').save(sys.argv[1])
print('wrote', sys.argv[1])
