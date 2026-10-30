"""PS1-style tiling textures for the streets, the castle grounds and their buildings (64x64, seamless, point-filtered).

Each texture is drawn the way PS1 artists painted theirs: a small palette (8 shades per texture), hard bevels lit from
the top left, dark grout and seams, and the 4x4 ordered dither between shades. They are near-grey with a little warm and
cool in them, so each zone's colours (road, grass, building, roof...) still tint them.

    python3 Tools/make_street_textures.py Assets/WizardsVRC/Textures [preview.png]      (needs numpy and Pillow)

Writes Terrain/*.png (ground, walls, kerbs) and Street/*.png (buildings, roofs, trees, hedges).
"""
import os
import sys

import numpy as np
from PIL import Image

OUT = sys.argv[1]
N = 64
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=float)
YY, XX = np.mgrid[0:N, 0:N]


# ------------------------------------------------------------------ building blocks

def rng(seed):
    return np.random.default_rng(seed)


def value_noise(freq, seed, n=N):
    """Tileable smooth noise: a freq x freq random grid, wrapped, cosine-interpolated."""
    g = rng(seed).random((freq, freq))
    t = np.arange(n) * freq / n
    i0 = np.floor(t).astype(int)
    f = t - i0
    f = (1 - np.cos(f * np.pi)) / 2
    i1 = (i0 + 1) % freq
    i0 %= freq
    a = g[np.ix_(i0, i0)] * (1 - f)[None, :] + g[np.ix_(i0, i1)] * f[None, :]
    b = g[np.ix_(i1, i0)] * (1 - f)[None, :] + g[np.ix_(i1, i1)] * f[None, :]
    return a * (1 - f)[:, None] + b * f[:, None]


def fbm(seed, octaves=((4, 0.55), (8, 0.3), (16, 0.15))):
    return sum(w * value_noise(f, seed + i) for i, (f, w) in enumerate(octaves))


def voronoi(cells, jitter, seed):
    """F1, F2 distances (pixels, wrapped) and the nearest cell's id, for a jittered cells x cells grid."""
    r = rng(seed)
    step = N / cells
    pts = []
    for j in range(cells):
        for i in range(cells):
            pts.append(((i + 0.5 + r.uniform(-jitter, jitter)) * step, (j + 0.5 + r.uniform(-jitter, jitter)) * step))
    pts = np.array(pts)
    d = np.full((len(pts), N, N), 1e9)
    for k, (px, py) in enumerate(pts):
        dx = np.abs(XX - px)
        dy = np.abs(YY - py)
        dx = np.minimum(dx, N - dx)
        dy = np.minimum(dy, N - dy)
        d[k] = np.sqrt(dx * dx + dy * dy)
    order = np.argsort(d, axis=0)
    f1 = np.take_along_axis(d, order[0:1], 0)[0]
    f2 = np.take_along_axis(d, order[1:2], 0)[0]
    return f1, f2, order[0]


def bevel(height, strength=1.0):
    """Light from the top left: brighter where the height rises toward the light, darker where it falls away."""
    gx = np.roll(height, 1, axis=1) - np.roll(height, -1, axis=1)
    gy = np.roll(height, 1, axis=0) - np.roll(height, -1, axis=0)
    return np.clip(1.0 + strength * (-(gx + gy)) * 0.5, 0.0, 2.0) - 1.0


def blocks(rows, widths, seed, offset=0.5, mortar=1, jitter_w=0):
    """A coursed block layout: per-pixel block id, distance to the nearest joint (pixels) and per-block random 0..1."""
    r = rng(seed)
    ids = np.zeros((N, N), int)
    edge = np.zeros((N, N))
    rnd = r.random(4096)
    y0 = 0
    bid = 0
    heights = rows if isinstance(rows, list) else [N // rows] * rows
    for ri, h in enumerate(heights):
        # widths for this course, summing to N
        ws = []
        total = 0
        while total < N:
            w = widths if np.isscalar(widths) else int(r.choice(widths))
            if jitter_w:
                w += int(r.integers(-jitter_w, jitter_w + 1))
            w = max(4, min(w, N - total)) if N - total >= 8 else N - total
            ws.append(w)
            total += w
        shift = int((ri % 2) * offset * ws[0]) if np.isscalar(widths) else int(r.integers(0, N))
        x0 = 0
        for w in ws:
            for y in range(y0, y0 + h):
                for x in range(x0, x0 + w):
                    xx = (x + shift) % N
                    ids[y, xx] = bid
                    edge[y, xx] = min(y - y0, y0 + h - 1 - y, x - x0, x0 + w - 1 - x)
            x0 += w
            bid += 1
        y0 += h
    return ids, edge, rnd[ids]


def finish(v, ramp, levels=8, dither=0.85, alpha=None):
    """Value 0..1 -> dithered to a few shades -> coloured by a ramp of (position, (r, g, b)) keys."""
    v = np.clip(v, 0, 1)
    t = (BAYER[YY % 4, XX % 4] + 0.5) / 16.0 - 0.5
    q = np.clip(np.floor(v * (levels - 1) + 0.5 + t * dither), 0, levels - 1) / (levels - 1)
    keys = np.array([k for k, _ in ramp])
    cols = np.array([c for _, c in ramp])
    rgb = np.stack([np.interp(q, keys, cols[:, c]) for c in range(3)], -1)
    a = np.ones((N, N)) if alpha is None else alpha
    img = np.dstack([rgb, a])
    return Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')


# Ramps: dark (cool) to light (warm), so a zone's tint colours them.
STONE = [(0.0, (0.20, 0.20, 0.26)), (0.45, (0.58, 0.57, 0.58)), (0.8, (0.86, 0.84, 0.80)), (1.0, (1.00, 0.97, 0.90))]
EARTH = [(0.0, (0.22, 0.18, 0.18)), (0.5, (0.62, 0.56, 0.50)), (1.0, (0.98, 0.94, 0.84))]
WOOD = [(0.0, (0.18, 0.13, 0.12)), (0.5, (0.58, 0.48, 0.40)), (1.0, (0.96, 0.88, 0.74))]
LEAF = [(0.0, (0.10, 0.14, 0.14)), (0.5, (0.55, 0.62, 0.52)), (1.0, (0.98, 1.00, 0.86))]
PLASTER = [(0.0, (0.16, 0.12, 0.11)), (0.3, (0.40, 0.33, 0.29)), (0.6, (0.84, 0.82, 0.78)), (1.0, (1.00, 0.98, 0.92))]


# ------------------------------------------------------------------ ground

def cobble():
    f1, f2, ids = voronoi(6, 0.32, 3)
    edge = f2 - f1
    stone = np.clip(edge / 4.0, 0, 1)                      # 0 in the grout, 1 well inside a stone
    dome = np.sqrt(stone)
    per = rng(4).random(64)[ids]
    v = 0.5 + 0.22 * per + 0.12 * (fbm(5) - 0.5)
    v += 0.45 * bevel(dome * 6, 0.6)
    v = np.where(edge < 1.4, 0.1 + 0.08 * fbm(6), v)        # grout
    return finish(v, STONE)


def flagstone():
    """Big irregular paving slabs, flush (no bevel to speak of), with cracks and worn patches."""
    ids, edge, per = blocks([22, 14, 18, 10], [14, 22, 30, 18, 26], 21)
    v = 0.56 + 0.22 * per + 0.16 * (fbm(22) - 0.5)
    v += 0.12 * bevel(np.clip(edge / 2.0, 0, 1) * 3, 0.6)
    worn = value_noise(8, 24) > 0.7
    v = np.where(worn & (edge > 2), v + 0.08, v)
    cracks = (np.abs(value_noise(8, 23) - 0.5) < 0.014) & (edge > 2)
    v = np.where(cracks, v - 0.28, v)
    v = np.where(edge < 1, 0.14, v)
    return finish(v, STONE)


def grass():
    r = rng(31)
    base = 0.42 + 0.25 * fbm(32) + 0.1 * (value_noise(16, 33) - 0.5)
    v = base.copy()
    for _ in range(420):                                     # short blades, a pixel wide, leaning a little
        x, y = r.integers(0, N, 2)
        length = r.integers(2, 5)
        lean = r.choice([-1, 0, 0, 1])
        shade = r.uniform(0.15, 0.35) * r.choice([-1, 1, 1])
        for k in range(length):
            v[(y - k) % N, (x + (lean if k == length - 1 else 0)) % N] += shade * (1 - k / (length + 1))
    for _ in range(14):                                      # tiny flowers
        x, y = r.integers(0, N, 2)
        v[y, x] = 1.0
        v[(y + 1) % N, x] = 0.85
    return finish(v, LEAF)


def forest_floor():
    r = rng(41)
    v = 0.3 + 0.22 * fbm(42)
    for _ in range(170):                                     # fallen leaves
        x, y = r.integers(0, N, 2)
        s = r.uniform(0.35, 0.75)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (-1, 0), (1, 1)):
            v[(y + dy) % N, (x + dx) % N] = s + (0.15 if dx + dy < 0 else 0)
        v[(y + 2) % N, (x + 1) % N] = s - 0.25               # shadow under it
    for _ in range(10):                                      # twigs
        x, y = r.integers(0, N, 2)
        for k in range(r.integers(5, 10)):
            v[(y + k // 3) % N, (x + k) % N] = 0.12
    return finish(v, EARTH)


def dirt():
    f1, f2, ids = voronoi(10, 0.45, 51)
    v = 0.5 + 0.3 * (fbm(52) - 0.5)
    pebble = (f1 < 1.6) & (rng(53).random(100)[ids] > 0.55)
    v = np.where(pebble, 0.85, v)
    v = np.where(np.roll(pebble, (1, 1), (0, 1)) & ~pebble, v - 0.25, v)
    ruts = np.abs(np.sin((XX + 3 * value_noise(4, 54)) / N * np.pi * 4)) < 0.12
    v = np.where(ruts, v - 0.12, v)
    return finish(v, EARTH)


# ------------------------------------------------------------------ walls

def stone_wall():
    ids, edge, per = blocks([14, 18, 14, 18], [20, 28, 24, 32], 61)
    h = np.clip(edge / 3.5, 0, 1)
    v = 0.5 + 0.22 * per + 0.16 * (fbm(62) - 0.5)
    v += 0.45 * bevel(np.sqrt(h) * 5, 0.6)
    chips = (value_noise(16, 63) > 0.82) & (edge > 2)
    v = np.where(chips, v - 0.18, v)
    v = np.where(edge < 1, 0.1, v)
    return finish(v, STONE)


def brick():
    ids, edge, per = blocks(8, 16, 71)
    v = 0.55 + 0.25 * per + 0.12 * (value_noise(16, 72) - 0.5)
    v = np.where(per < 0.12, v - 0.2, v)                    # the odd dark (overfired) brick
    v += 0.3 * bevel(np.clip(edge, 0, 1.5), 0.8)
    v = np.where(edge < 1, 0.18 + 0.1 * fbm(73), v)          # mortar
    return finish(v, EARTH)


def plaster():
    """Timber-framed plaster: posts at the tile's edges, a rail across the middle and a brace in one panel."""
    v = 0.78 + 0.14 * (fbm(81) - 0.5)
    stains = value_noise(4, 82)
    v -= np.clip(stains - 0.7, 0, 1) * 0.6
    grain = 0.12 + 0.1 * value_noise(32, 83)
    timber = np.zeros((N, N), bool)
    timber |= (XX < 5) | (YY < 4) | ((YY >= 34) & (YY < 38))
    brace = (np.abs((XX - 5) - (YY - 4) * 27 / 30.0) < 2.4) & (XX >= 5) & (YY >= 4) & (YY < 34) & (XX < 32)
    timber |= brace
    v = np.where(timber, grain + 0.1 * bevel(timber.astype(float), 1.0), v)
    # a shadow line under each beam and beside each post
    v = np.where(~timber & np.roll(timber, 1, 0), v - 0.2, v)
    v = np.where(~timber & np.roll(timber, 1, 1), v - 0.12, v)
    return finish(v, PLASTER)


def boards():
    """Weathered vertical boards (sheds, warehouses)."""
    r = rng(91)
    col = XX // 8
    per = r.random(8)[col]
    grain = value_noise(32, 93)
    streak = np.sin((YY / N * 6 + per * 10) * np.pi + (XX % 8) * 0.9) * 0.08
    v = 0.5 + 0.22 * per + 0.18 * (grain - 0.5) + streak
    v = np.where(XX % 8 == 0, 0.08, v)                       # gaps
    v = np.where(XX % 8 == 1, v + 0.12, v)                   # lit edge
    for c in range(8):                                       # nails top and bottom of each board
        for y in (5, 37):
            v[y, c * 8 + 4] = 0.15
    return finish(v, WOOD)


def planks():
    """Floor planks running across, 8 px wide, one butt joint per plank, with long grain."""
    r = rng(101)
    row = YY // 8
    per = r.random(8)[row]
    joint = (r.integers(0, N, 8))[row]
    grain = value_noise(64, 102)[:, :1].repeat(N, 1) * 0.6 + value_noise(16, 103) * 0.4
    v = 0.5 + 0.22 * per + 0.3 * (grain - 0.5) + 0.06 * np.sin(XX / 5.0 + per * 30 + YY * 1.7)
    knots = (value_noise(16, 104) > 0.86)
    v = np.where(knots, v - 0.18, v)
    v = np.where(YY % 8 == 0, 0.07, v)                       # gaps between planks
    v = np.where(YY % 8 == 1, v + 0.1, v)                    # lit edge
    v = np.where(XX == joint, 0.1, v)                        # butt joints
    return finish(v, WOOD)


# ------------------------------------------------------------------ roofs and plants

def roof_tiles():
    """Rows of round-ended tiles, each row overlapping the one below."""
    v = np.zeros((N, N))
    rowh = 8
    for y in range(N):
        row = y // rowh
        ly = y % rowh
        shift = 4 if row % 2 else 0
        for x in range(N):
            lx = (x + shift) % 8
            dx = (lx - 3.5) / 4.0
            curve = rowh - 1 - 2.2 * dx * dx                 # round bottom edge
            inside = ly < curve
            if inside:
                v[y, x] = 0.42 + 0.4 * (ly / rowh) - 0.25 * abs(dx)
            else:
                v[y, x] = 0.14                               # gap shows the dark row behind
    v += 0.1 * (value_noise(8, 111) - 0.5) + 0.1 * (rng(112).random((N // 8 + 1, N // 8 + 1))[YY // 8, XX // 8] - 0.5)
    return finish(v, EARTH)


def leaves():
    r = rng(121)
    v = 0.18 + 0.1 * fbm(122)
    for _ in range(330):
        x, y = r.integers(0, N, 2)
        s = r.uniform(0.4, 0.9)
        for dx, dy, k in ((0, 0, 0), (1, 0, 0), (-1, 0, 0), (0, -1, 0.15), (0, 1, -0.2), (1, -1, 0.1)):
            v[(y + dy) % N, (x + dx) % N] = max(v[(y + dy) % N, (x + dx) % N], s + k)
    return finish(v, LEAF)


def hedge():
    r = rng(131)
    v = 0.25 + 0.12 * fbm(132)
    for _ in range(600):
        x, y = r.integers(0, N, 2)
        s = r.uniform(0.45, 0.85)
        v[y, x] = s + 0.12
        v[(y + 1) % N, x] = s
        v[y, (x + 1) % N] = s - 0.1
    return finish(v, LEAF)


def bark():
    ridge = value_noise(16, 141)
    col = np.repeat(value_noise(16, 143)[:1], N, 0)          # ridges run up the trunk
    wob = value_noise(4, 144)
    x_shift = (XX + (wob * 6).astype(int)) % N
    ridges = col[YY, x_shift]
    v = 0.3 + 0.45 * ridges + 0.12 * (ridge - 0.5)
    cracks = ridges < 0.28
    v = np.where(cracks, 0.08, v)
    return finish(v, WOOD)


TERRAIN = {
    'Cobble': cobble, 'Flagstone': flagstone, 'Grass': grass, 'ForestFloor': forest_floor, 'Dirt': dirt,
    'StoneWall': stone_wall, 'Brick': brick, 'WoodPlanks': planks,
}
STREET = {
    'Plaster': plaster, 'Boards': boards, 'RoofTiles': roof_tiles, 'Leaves': leaves, 'Hedge': hedge, 'Bark': bark,
}

made = []
for folder, table in (('Terrain', TERRAIN), ('Street', STREET)):
    os.makedirs(os.path.join(OUT, folder), exist_ok=True)
    for name, fn in table.items():
        img = fn()
        img.save(os.path.join(OUT, folder, name + '.png'))
        made.append((name, img))
if len(sys.argv) > 2:
    cell = 2 * N + 8
    cols = 7
    rows = (len(made) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * cell, rows * cell), (40, 40, 48, 255))
    for i, (name, img) in enumerate(made):
        t = Image.new('RGBA', (2 * N, 2 * N))
        for yy in (0, N):
            for xx in (0, N):
                t.paste(img, (xx, yy))
        sheet.paste(t, ((i % cols) * cell + 4, (i // cols) * cell + 4))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(sys.argv[2])
print('made', len(made), 'textures')
