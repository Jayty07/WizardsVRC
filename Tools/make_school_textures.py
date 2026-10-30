"""PS1-style textures for the school classrooms (Assets/WizardsVRC/Textures/School), one set per school:

    Banner<School>.png   96x192  a swallowtail wall banner in the school's colour: gold trim, folds, the school's symbol
    Carpet<School>.png  128x192  the classroom rug: fringed ends, a dark border, a gold diamond band, a patterned field
                                 and a gold medallion with the school's symbol in the middle (the texture's top lies at
                                 the board end, so it reads the right way up walking in from the door)
    Runner<School>.png   32x64   the aisle runner from the door (tiles lengthways)
    Desk.png             64x64   polished desk wood

The symbols are the cast glyphs (Textures/CastGlyph<School>.png). Small palettes, hard shading and the 4x4 ordered
dither, like the street textures.

    python3 Tools/make_school_textures.py Assets/WizardsVRC/Textures [preview.png]      (needs numpy and Pillow)
"""
import os
import sys

import numpy as np
from PIL import Image

TEX = sys.argv[1]
OUT = os.path.join(TEX, "School")
SCHOOLS = ["Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance"]
# Same as WizardsVRCSceneBuilder.SchoolColors.
COLOURS = [(0.95, 0.35, 0.15), (0.55, 0.85, 1.0), (0.65, 0.4, 0.95), (0.4, 0.9, 0.4), (0.35, 0.3, 0.4), (0.95, 0.8, 0.25), (0.85, 0.6, 0.4)]
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=float)
GOLD_DARK, GOLD, GOLD_LIGHT = np.array((0.55, 0.36, 0.08)), np.array((0.95, 0.72, 0.22)), np.array((1.0, 0.93, 0.6))


def dither(rgb, levels=12):
    """Cut a float RGB image down to a few levels per channel with the PS1's ordered dither."""
    h, w, _ = rgb.shape
    yy, xx = np.mgrid[0:h, 0:w]
    t = ((BAYER[yy % 4, xx % 4] + 0.5) / 16.0 - 0.5)[..., None]
    return np.clip(np.floor(np.clip(rgb, 0, 1) * (levels - 1) + 0.5 + t * 0.9) / (levels - 1), 0, 1)


def shade(colour, k):
    """The colour lightened (k > 0) or darkened (k < 0)."""
    c = np.array(colour, dtype=float)
    return c + (1 - c) * k if k > 0 else c * (1 + k)


def noise(h, w, seed, scale=0.06):
    return np.random.default_rng(seed).standard_normal((h, w))[..., None] * scale


def glyph(school, size):
    g = Image.open(os.path.join(TEX, "CastGlyph%s.png" % school)).convert("RGBA")
    if g.size[0] != size:
        g = g.resize((size, size), Image.NEAREST)
    return np.asarray(g, dtype=float) / 255.0


def paste(rgb, alpha, src, x0, y0):
    """Alpha-composite src (RGBA float) onto rgb at x0, y0."""
    h, w = src.shape[:2]
    a = src[..., 3:4]
    rgb[y0:y0 + h, x0:x0 + w] = rgb[y0:y0 + h, x0:x0 + w] * (1 - a) + src[..., :3] * a
    if alpha is not None:
        alpha[y0:y0 + h, x0:x0 + w] = np.maximum(alpha[y0:y0 + h, x0:x0 + w], src[..., 3])


def diamond(rgb, cx, cy, r, colour, edge=None):
    h, w = rgb.shape[:2]
    for y in range(max(0, cy - r), min(h, cy + r + 1)):
        for x in range(max(0, cx - r), min(w, cx + r + 1)):
            d = abs(x - cx) + abs(y - cy)
            if d <= r:
                rgb[y, x] = edge if (edge is not None and d == r) else colour


def save(arr, alpha, name):
    rgba = np.dstack([dither(arr), alpha if alpha is not None else np.ones(arr.shape[:2])])
    img = Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA")
    img.save(os.path.join(OUT, name))
    return img


# ------------------------------------------------------------------ banner

def banner(i, school):
    W, H = 96, 192
    col = COLOURS[i]
    yy, xx = np.mgrid[0:H, 0:W]
    # Swallowtail: the bottom edge rises to a notch in the middle.
    bottom = 158 + 33 * (np.abs(xx - 47.5) / 47.5)
    inside = (yy <= bottom) & (xx >= 2) & (xx <= W - 3)
    alpha = inside.astype(float)
    fold = np.sin(xx / W * np.pi * 3.0) * 0.12 + np.sin(xx / W * np.pi * 7.0 + 1.0) * 0.04
    base = np.array(shade(col, -0.35))
    rgb = base[None, None, :] * (1 + fold[..., None]) + noise(H, W, 10 + i, 0.025)
    # Gold trim 4 px in from the sides and the forked bottom, and a hem band at the top.
    dist_side = np.minimum(xx - 2, W - 3 - xx)
    dist_bottom = (bottom - yy) * 0.82
    edge = np.minimum(dist_side, dist_bottom)
    trim = inside & (edge >= 3) & (edge < 7)
    rgb[trim] = GOLD
    rgb[inside & (edge >= 3) & (edge < 4)] = GOLD_LIGHT
    rgb[inside & (edge >= 6) & (edge < 7)] = GOLD_DARK
    rgb[yy < 14] = shade(col, -0.6)
    rgb[(yy >= 10) & (yy < 14)] = GOLD
    for x in range(10, W - 8, 16):                       # rod loops
        rgb[0:10, x:x + 6] = shade(col, -0.75)
    # The symbol, with a gold ring behind it, and diamonds above and below.
    cy = 82
    rr = (xx - 47.5) ** 2 + (yy - cy) ** 2
    rgb[(rr <= 36 ** 2) & (rr > 33 ** 2)] = GOLD
    rgb[(rr <= 33 ** 2)] = shade(col, -0.55)
    paste(rgb, None, glyph(school, 64), 16, cy - 32)
    diamond(rgb, 48, 30, 6, GOLD, GOLD_DARK)
    diamond(rgb, 48, 136, 6, GOLD, GOLD_DARK)
    for x in (24, 72):
        diamond(rgb, x, 136, 3, GOLD_LIGHT)
    return save(rgb, alpha, "Banner%s.png" % school)


# ------------------------------------------------------------------ carpet

def carpet(i, school):
    W, H = 128, 192
    col = COLOURS[i]
    yy, xx = np.mgrid[0:H, 0:W]
    field = np.array(shade(col, -0.25))
    rgb = np.tile(field, (H, W, 1)) + noise(H, W, 20 + i, 0.02)
    # A lattice of small diamonds over the field.
    lat = ((xx + yy) % 16 == 0) | ((xx - yy) % 16 == 0)
    rgb[lat] = shade(col, -0.45)
    # Fringe at the two short ends (top and bottom of the texture: the ends of the rug in the room).
    fringe = (yy < 6) | (yy >= H - 6)
    rgb[fringe] = (0.92, 0.88, 0.76)
    rgb[fringe & (xx % 3 == 0)] = (0.62, 0.58, 0.5)
    # Borders: dark outer band, gold diamond band, dark inner line.
    edge = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy - 6, H - 7 - yy))
    rgb[~fringe & (edge < 8)] = shade(col, -0.7)
    band = ~fringe & (edge >= 8) & (edge < 16)
    rgb[band] = shade(col, -0.5)
    for c in range(12, W - 8, 10):
        for r in (17, H - 18):
            diamond(rgb, c, r, 3, GOLD, GOLD_DARK)
    for r in range(22, H - 18, 10):
        for c in (12, W - 13):
            diamond(rgb, c, r, 3, GOLD, GOLD_DARK)
    rgb[~fringe & (edge >= 16) & (edge < 18)] = GOLD
    # Medallion with the symbol in the middle, and corner flourishes.
    cx, cy = 63.5, H / 2 - 0.5
    rr = (xx - cx) ** 2 + (yy - cy) ** 2
    rgb[(rr <= 46 ** 2) & (rr > 42 ** 2)] = GOLD
    rgb[(rr <= 42 ** 2) & (rr > 40 ** 2)] = GOLD_DARK
    rgb[rr <= 40 ** 2] = shade(col, -0.6)
    for a in range(0, 360, 30):
        x = int(cx + np.cos(np.radians(a)) * 44)
        y = int(cy + np.sin(np.radians(a)) * 44)
        diamond(rgb, x, y, 2, GOLD_LIGHT)
    g = glyph(school, 64)                                # the texture's top lies at the board end of the room
    paste(rgb, None, g, int(cx - 31.5), int(cy - 31.5))
    for (x, y) in ((28, 30), (W - 29, 30), (28, H - 31), (W - 29, H - 31)):
        diamond(rgb, x, y, 6, GOLD, GOLD_DARK)
        diamond(rgb, x, y, 2, shade(col, 0.4))
    return save(rgb, None, "Carpet%s.png" % school)


def runner(i, school):
    W, H = 32, 64
    col = COLOURS[i]
    yy, xx = np.mgrid[0:H, 0:W]
    rgb = np.tile(np.array(shade(col, -0.3)), (H, W, 1)) + noise(H, W, 40 + i, 0.02)
    rgb[(xx < 3) | (xx >= W - 3)] = shade(col, -0.7)
    rgb[(xx == 4) | (xx == W - 5)] = GOLD
    for cy in (15, 47):
        diamond(rgb, 16, cy, 8, shade(col, 0.15), GOLD)
        diamond(rgb, 16, cy, 3, GOLD_LIGHT)
    return save(rgb, None, "Runner%s.png" % school)


def desk():
    H = W = 64
    yy, xx = np.mgrid[0:H, 0:W]
    rng = np.random.default_rng(77)
    grain = np.sin((yy + 2.5 * np.sin(xx / 7.0) + rng.standard_normal((H, W)) * 0.4) / 2.2) * 0.5 + 0.5
    v = 0.45 + 0.2 * grain + 0.06 * np.sin(xx / 13.0)
    v = np.where(yy % 16 == 0, 0.2, v)                    # board seams
    v = np.where(yy % 16 == 1, v + 0.12, v)
    knots = ((xx - 41) ** 2 + (yy - 22) ** 2 < 6) | ((xx - 12) ** 2 + (yy - 52) ** 2 < 5)
    v = np.where(knots, 0.25, v)
    base = np.array((0.55, 0.3, 0.18))
    rgb = base[None, None, :] * (v[..., None] * 1.6)
    return save(rgb, None, "Desk.png")


os.makedirs(OUT, exist_ok=True)
made = []
for i, s in enumerate(SCHOOLS):
    made += [banner(i, s), carpet(i, s), runner(i, s)]
made.append(desk())
if len(sys.argv) > 2:
    cols = 7
    sheet = Image.new("RGBA", (cols * 140, 3 * 200 + 80), (40, 40, 48, 255))
    for i in range(7):
        sheet.paste(made[i * 3], (i * 140 + 20, 4), made[i * 3])
        sheet.paste(made[i * 3 + 1], (i * 140 + 4, 204))
        r = made[i * 3 + 2]
        for k in range(3):
            sheet.paste(r, (i * 140 + 50, 404 + k * 64))
    sheet.paste(made[-1].resize((128, 128), Image.NEAREST), (4, 600))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(sys.argv[2])
print("made", len(made), "textures")
