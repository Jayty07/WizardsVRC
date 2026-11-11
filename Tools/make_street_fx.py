"""Sprites for the streets' drifting particles (Textures/StreetFx): falling leaves and petals, smoke / steam / mist
puffs, butterflies and moths (two frames: wings out, wings up), bubbles and drips. All white (or grey), so each street
tints them with its own colours; chunky pixels, hard alpha steps, to sit with the PS1 look.

    python3 Tools/make_street_fx.py Assets/WizardsVRC/Textures/StreetFx      (needs numpy and Pillow)
"""
import os
import sys

import numpy as np
from PIL import Image


def canvas(w, h):
    return np.zeros((h, w, 4), dtype=np.uint8)


def put(img, x, y, rgb, a=255):
    h, w = img.shape[:2]
    if 0 <= x < w and 0 <= y < h:
        img[y, x] = (rgb[0], rgb[1], rgb[2], a)


def leaf():
    """A pointed oval leaf lying corner to corner, with a darker midrib and a short stalk."""
    img = canvas(16, 16)
    for y in range(16):
        for x in range(16):
            # along the diagonal (u) and across it (v)
            u = (x + (15 - y)) / 2.0
            v = (x - (15 - y)) / 2.0
            t = (u - 2.0) / 11.0
            if t < 0 or t > 1:
                continue
            half = 3.6 * np.sin(np.pi * t) ** 0.8
            if abs(v) <= half:
                shade = 255 if abs(v) > 0.6 else 190
                if v < -0.6:
                    shade = 225          # the underside half sits a little darker
                put(img, x, y, (shade, shade, shade))
    for k in range(3):                    # stalk
        put(img, 1 + k, 14 - k, (170, 170, 170))
    return img


def petal():
    """A small round petal: a teardrop."""
    img = canvas(8, 8)
    for y in range(8):
        for x in range(8):
            dx, dy = x - 3.5, y - 4.2
            r = 3.3 - max(0.0, -dy) * 0.45
            if dx * dx + dy * dy <= r * r:
                shade = 255 if dy < 1 else 220
                put(img, x, y, (shade, shade, shade))
    return img


def puff():
    """A lumpy puff of smoke: a few overlapping blobs, in three hard alpha steps (thick core, thin rim)."""
    n = 32
    img = canvas(n, n)
    blobs = [(15.5, 17.0, 9.5), (9.5, 19.5, 6.5), (22.0, 19.0, 6.8), (13.0, 11.0, 6.2), (20.0, 12.0, 5.6)]
    for y in range(n):
        for x in range(n):
            d = max(1.0 - np.hypot(x - bx, y - by) / br for bx, by, br in blobs)
            if d <= 0:
                continue
            a = 215 if d > 0.5 else 150 if d > 0.22 else 80
            # a checker on the rim breaks the edge up the way a dithered PS1 sprite would
            if d <= 0.22 and (x + y) % 2 == 0:
                a = 40
            shade = 255 if y < 16 else 232
            put(img, x, y, (shade, shade, shade), a)
    return img


def butterfly():
    """Two 16 x 16 frames side by side: wings spread, then wings half folded (played back and forth they flap)."""
    img = canvas(32, 16)
    for frame, spread in enumerate((1.0, 0.42)):
        ox = frame * 16
        for y in range(16):
            for x in range(16):
                dx = (x - 7.5) / spread
                dy = y - 7.5
                upper = (abs(dx) - 3.6) ** 2 / 12.0 + (dy + 2.2) ** 2 / 10.0 <= 1.0
                lower = (abs(dx) - 2.8) ** 2 / 7.0 + (dy - 3.2) ** 2 / 6.5 <= 1.0
                if abs(dx) > 0.6 and (upper or lower):
                    edge = (abs(dx) - 3.6) ** 2 / 12.0 + (dy + 2.2) ** 2 / 10.0 > 0.62 if upper else \
                        (abs(dx) - 2.8) ** 2 / 7.0 + (dy - 3.2) ** 2 / 6.5 > 0.55
                    shade = 205 if edge else 255
                    put(img, ox + x, y, (shade, shade, shade))
        for y in range(4, 12):            # body
            put(img, ox + 7, y, (70, 70, 70))
            put(img, ox + 8, y, (70, 70, 70))
        put(img, ox + 6, 3, (70, 70, 70))  # feelers
        put(img, ox + 9, 3, (70, 70, 70))
    return img


def bubble():
    """A bubble: a bright rim, a faint inside and a highlight."""
    img = canvas(16, 16)
    for y in range(16):
        for x in range(16):
            d = np.hypot(x - 7.5, y - 7.5)
            if d <= 6.8:
                a = 235 if d > 5.4 else 55
                put(img, x, y, (255, 255, 255), a)
    for x, y in ((5, 4), (4, 5), (5, 5), (6, 4)):
        put(img, x, y, (255, 255, 255), 255)
    return img


def drip():
    """A falling drop: a thin streak with a round head at the bottom."""
    img = canvas(8, 16)
    for y in range(16):
        for x in range(8):
            dx = x - 3.5
            if y >= 10:
                if dx * dx + (y - 12.0) ** 2 <= 6.0:
                    put(img, x, y, (255, 255, 255), 235)
            elif abs(dx) <= 0.6:
                put(img, x, y, (255, 255, 255), int(40 + 150 * y / 10.0))
    return img


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else 'Assets/WizardsVRC/Textures/StreetFx'
    os.makedirs(out, exist_ok=True)
    for name, make in (('Leaf', leaf), ('Petal', petal), ('Puff', puff), ('Butterfly', butterfly), ('Bubble', bubble), ('Drip', drip)):
        path = os.path.join(out, name + '.png')
        Image.fromarray(make(), 'RGBA').save(path)
        print('wrote', path)


if __name__ == '__main__':
    main()
