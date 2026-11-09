"""The title screen's art (Assets/WizardsVRC/Textures/Title), original pixel art in the game's style:

    TitleLogo.png      the WIZARDS VRC wordmark: the game's own pixel font (Fonts/PsxNameFont.txt) blown up into chunky
                       bevelled gold letters with a deep violet extrude and outline, a wizard's hat tipped on the I, the
                       letters VRC on a violet ribbon underneath, and twinkling sparkles round it (transparent background)
    TitleSkyline.png   the panorama strip for the title sky (the PSXSkyline shader): a sea of moonlit clouds below the
                       horizon, floating islands with spires, falls and lit windows on it, the moon, and aurora ribbons
                       overhead. u = bearing round you (0 = straight ahead), v = elevation (-12 to +40 degrees).

    python3 Tools/make_title_art.py [preview.png]
"""
import math
import os
import random
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT_DIR = os.path.join(ROOT, "Assets", "WizardsVRC", "Textures", "Title")
FONT = os.path.join(ROOT, "Assets", "WizardsVRC", "Fonts", "PsxNameFont.txt")

OUT = (24, 14, 40)
EXTRUDE = (70, 34, 120)
EXTRUDE_D = (44, 20, 82)
GOLD_L = (255, 244, 170)
GOLD = (255, 205, 70)
GOLD_M = (236, 160, 40)
GOLD_D = (178, 104, 30)
WHITE = (255, 255, 255)
VIOLET = (120, 60, 200)
VIOLET_L = (170, 110, 240)
VIOLET_D = (70, 30, 130)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def read_font():
    glyphs, cur, rows = {}, None, []
    for raw in open(FONT, encoding="utf-8").read().splitlines():
        line = raw.rstrip("\r \t")
        if line.startswith("#") and (cur is None or " " in line):
            continue
        if len(line) == 2 and line[0] == ":":
            if cur is not None:
                glyphs[cur] = rows
            cur, rows = line[1], []
            continue
        if cur is not None and line:
            rows.append(line)
    if cur is not None:
        glyphs[cur] = rows
    return glyphs


# ------------------------------------------------------------------ the logo

def word_mask(glyphs, text, scale, gap):
    """The ink pixels of a word in the pixel font, each font pixel scale x scale. Returns (set of (x, y), width, height)."""
    ink = set()
    x0 = 0
    for ch in text:
        rows = glyphs[ch]
        w = len(rows[0])
        for gy, row in enumerate(rows):
            for gx, c in enumerate(row):
                if c != "#":
                    continue
                for dy in range(scale):
                    for dx in range(scale):
                        ink.add((x0 + gx * scale + dx, gy * scale + dy))
        x0 += w * scale + gap
    return ink, x0 - gap, len(glyphs[text[0]]) * scale


def sparkle(px, cx, cy, r, col, w, h):
    for k in range(-r, r + 1):
        a = 1 - abs(k) / (r + 1)
        for (x, y) in ((cx + k, cy), (cx, cy + k)):
            if 0 <= x < w and 0 <= y < h:
                base = px[x, y]
                c = mix(base[:3], col, a) if base[3] else col
                px[x, y] = c + (255 if base[3] else int(255 * a),)
    if 0 <= cx < w and 0 <= cy < h:
        px[cx, cy] = WHITE + (255,)


def logo(glyphs):
    W, H = 256, 128
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    scale, gap = 4, 4
    ink, ww, wh = word_mask(glyphs, "WIZARDS", scale, gap)
    # Bolder: every stroke a pixel wider and taller (the 4 px gaps between strokes keep the letters apart).
    ink = ink | {(x + 1, y) for (x, y) in ink} | {(x, y + 1) for (x, y) in ink}
    ww += 1
    wh += 1
    ox, oy = (W - ww) // 2 - 2, 22
    ink = {(x + ox, y + oy) for (x, y) in ink}
    depth = 5
    # The extrude: the letters stacked down and right, darker the deeper.
    for d in range(depth, 0, -1):
        col = mix(EXTRUDE, EXTRUDE_D, d / depth)
        for (x, y) in ink:
            px[x + d, y + d] = col + (255,)
    # Outline round the letters and their extrude.
    solid = set()
    for (x, y) in ink:
        for d in range(depth + 1):
            solid.add((x + d, y + d))
    for (x, y) in solid:
        for dx in (-2, -1, 0, 1, 2):
            for dy in (-2, -1, 0, 1, 2):
                if abs(dx) + abs(dy) > 3:
                    continue
                n = (x + dx, y + dy)
                if n not in solid and 0 <= n[0] < W and 0 <= n[1] < H and px[n][3] == 0:
                    px[n] = OUT + (255,)
    # The gold face: a top-to-bottom gradient with a bevel (light top and left edges, dark bottom and right).
    for (x, y) in ink:
        t = (y - oy) / max(1, wh - 1)
        c = mix(GOLD_L, GOLD, t * 2) if t < 0.5 else mix(GOLD, GOLD_D, (t - 0.5) * 2)
        if (x, y - 1) not in ink:
            c = GOLD_L if t < 0.6 else GOLD
        elif (x - 1, y) not in ink:
            c = mix(c, WHITE, 0.25)
        elif (x + 1, y) not in ink or (x, y + 1) not in ink:
            c = mix(c, GOLD_D, 0.55)
        # a shine band running across the upper third
        if 0.18 < t < 0.3 and (x + y) % 7 < 3:
            c = mix(c, WHITE, 0.35)
        px[x, y] = c + (255,)

    # A wizard's hat tipped on the I (the second letter), its point flopping to the right.
    i_x = ox + 5 * scale + gap + 1 * scale + scale // 2   # the I's stem
    hat_base = oy - 1
    for row in range(0, 15):
        y = hat_base - row
        half = max(0, 8 - row * 8 // 13)
        lean = int((row / 14) ** 2 * 7)
        for x in range(i_x - half + lean, i_x + half + lean + 1):
            if 0 <= x < W and 0 <= y < H:
                edge = x in (i_x - half + lean, i_x + half + lean)
                px[x, y] = (OUT if edge else (VIOLET_L if x < i_x + lean else VIOLET)) + (255,)
    for x in range(i_x - 11, i_x + 12):
        for y in (hat_base, hat_base + 1, hat_base + 2):
            edge = y == hat_base + 2 or x in (i_x - 11, i_x + 11)
            px[x, y] = (OUT if edge else VIOLET_D if y == hat_base + 1 else VIOLET) + (255,)
    for x in range(i_x - 8, i_x + 9):
        px[x, hat_base - 2] = GOLD + (255,)
        px[x, hat_base - 3] = GOLD_M + (255,)
    tip = (i_x + 7, hat_base - 15)
    sparkle(px, tip[0] + 2, tip[1] - 1, 3, GOLD_L, W, H)

    # The ribbon with VRC on it.
    vink, vw, vh = word_mask(glyphs, "VRC", 3, 3)
    rw = vw + 34
    rx0, ry0 = (W - rw) // 2, oy + wh + depth + 8
    rh = vh + 10
    for x in range(rx0 - 10, rx0 + rw + 10):
        for y in range(ry0, ry0 + rh):
            inner = rx0 <= x < rx0 + rw
            if not inner:
                # forked tails, a little lower than the band
                d = x - rx0 if x < rx0 else (rx0 + rw - 1) - x
                notch = abs(y - (ry0 + rh // 2 + 3)) < (-d) // 2
                if y < ry0 + 3 or notch:
                    continue
            edge = y in (ry0, ry0 + rh - 1)
            c = OUT if edge else (VIOLET_L if y < ry0 + 3 else (VIOLET if inner else VIOLET_D))
            px[x, y + (3 if not inner else 0)] = c + (255,)
    vx, vy = (W - vw) // 2, ry0 + 5
    for (x, y) in vink:
        px[vx + x + 1, vy + y + 1] = VIOLET_D + (255,)
    for (x, y) in vink:
        px[vx + x, vy + y] = (WHITE if y < vh // 2 else (235, 225, 255)) + (255,)

    # Sparkles round the word.
    for (sx, sy, r) in ((ox - 10, oy + 4, 4), (ox + ww + 12, oy + wh - 2, 5), (ox + ww - 30, oy - 9, 3), (ox + 24, oy + wh + 12, 3),
                        (ox + ww + 4, ry0 + 8, 3), (ox - 4, ry0 + 6, 2)):
        sparkle(px, sx, sy, r, GOLD_L, W, H)
    return img


# ------------------------------------------------------------------ the sky strip

LOW, HIGH = -12.0, 40.0


def row_of(elev, h):
    """Pixel row (0 = top) for an elevation in degrees."""
    return int(round((HIGH - elev) / (HIGH - LOW) * (h - 1)))


def skyline():
    W, H = 2048, 256
    rng = random.Random(7)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()

    def put(x, y, c, a=1.0):
        x %= W
        if not (0 <= y < H):
            return
        old = px[x, y]
        oa = old[3] / 255.0
        na = a + oa * (1 - a)
        if na <= 0:
            return
        col = tuple(int((c[i] * a + old[i] * oa * (1 - a)) / na) for i in range(3))
        px[x, y] = col + (int(na * 255),)

    # Aurora: two wavy ribbons high overhead, teal fading to violet at the top.
    for band, (base, amp, col_lo, col_hi, ph) in enumerate(((27, 4, (60, 230, 180), (150, 90, 230), 0.0),
                                                            (33, 3, (90, 200, 240), (200, 110, 220), 2.0))):
        for x in range(W):
            u = x / W * math.tau
            centre = base + amp * math.sin(u * 3 + ph) + 1.5 * math.sin(u * 7 + ph * 2)
            curtain = 0.6 + 0.4 * math.sin(u * 17 + ph) * math.sin(u * 4)
            # every row from the ribbon's foot (3 degrees under its middle) to its top (6 over)
            y_top, y_foot = row_of(centre + 6, H), row_of(centre - 3, H)
            for y in range(y_top, y_foot + 1):
                t = (y_foot - y) / max(1, y_foot - y_top)
                a = (1 - abs(t - 0.3) / 0.7) * 0.5 * curtain
                if a > 0:
                    put(x, y, mix(col_lo, col_hi, t), a)

    # The moon, ahead and to the left, with a soft halo.
    mx, my, mr = int(W * 0.9), row_of(23, H), 13
    for y in range(my - 40, my + 41):
        for x in range(mx - 40, mx + 41):
            d = math.hypot(x - mx, (y - my) * 1.0)
            if d <= mr:
                crater = (x - mx + 4) ** 2 + (y - my - 3) ** 2 < 14 or (x - mx - 5) ** 2 + (y - my + 4) ** 2 < 9
                put(x, y, (225, 220, 255) if crater else (250, 248, 255))
            elif d < 40:
                put(x, y, (200, 190, 255), (1 - (d - mr) / (40 - mr)) ** 2 * 0.35)

    # Floating islands, far (dim) then near (lighter), each with a skirt of rock tapering beneath it.
    def island(cx, elev, width, depth, col, rim, spires, falls, windows):
        top = row_of(elev, H)
        for x in range(cx - width, cx + width + 1):
            t = (x - cx) / width
            if abs(t) > 1:
                continue
            surf = top - int((1 - t * t) * 3 + rng.random())
            bottom = top + int(depth * (1 - abs(t)) ** 1.6) + 1
            for y in range(surf, bottom):
                put(x, y, rim if y == surf else col)
        for (sx, sh, sw) in spires:
            for y in range(top - sh, top):
                for x in range(cx + sx - sw, cx + sx + sw + 1):
                    put(x, y, col)
            for k in range(sw * 2 + 3):   # pointed roof
                for x in range(cx + sx - sw - 1 + k // 2, cx + sx + sw + 2 - k // 2):
                    put(x, top - sh - k, col)
            if windows:
                for y in range(top - sh + 3, top - 1, 4):
                    put(cx + sx, y, (255, 220, 140))
        for fx in falls:
            for y in range(top, top + depth + 14):
                a = 0.75 * (1 - (y - top) / (depth + 14))
                put(cx + fx, y, (190, 220, 255), a)
                put(cx + fx + 1, y, (150, 190, 255), a * 0.7)

    far = (60, 46, 110)
    for k in range(10):
        cx = int((k + 0.3 + rng.random() * 0.4) / 10 * W)
        island(cx, 4 + rng.random() * 9, rng.randint(26, 60), rng.randint(16, 30), far, (96, 78, 150),
               [(rng.randint(-10, 10), rng.randint(9, 18), 2)] if rng.random() < 0.6 else [], [rng.randint(-12, 12)] if rng.random() < 0.4 else [], False)
    near = (36, 28, 72)
    # Behind you: the far shore of Starfall Isle, the castle on it.
    island(int(W * 0.5), 3, 170, 48, near, (96, 140, 120),
           [(-62, 22, 4), (-26, 38, 6), (0, 56, 8), (28, 34, 6), (64, 24, 4)], [-100, 92], True)
    island(int(W * 0.25), 9, 70, 30, near, (96, 140, 120), [(8, 20, 4)], [-20], True)
    island(int(W * 0.74), 12, 60, 26, near, (96, 140, 120), [(-10, 16, 3), (12, 26, 4)], [24], True)

    # The cloud sea below the horizon: rolling billows, lit lavender on top, deeper violet beneath.
    horizon = row_of(0, H)
    for x in range(W):
        u = x / W * math.tau
        crest = horizon + 4 + int(2.5 * math.sin(u * 11) + 1.5 * math.sin(u * 29 + 1) + 2 * math.sin(u * 5 + 2))
        for y in range(crest, H):
            depth = (y - crest) / max(1, H - crest)
            lump = 0.5 + 0.5 * math.sin(x / 9.0 + y / 4.0) * math.sin(x / 23.0 - y / 7.0)
            c = mix((214, 196, 245), (92, 70, 150), min(1.0, depth * 1.6 + lump * 0.15))
            if y == crest:
                c = (240, 232, 255)
            put(x, y, c)
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    glyphs = read_font()
    lg = logo(glyphs)
    lg.save(os.path.join(OUT_DIR, "TitleLogo.png"))
    sk = skyline()
    sk.save(os.path.join(OUT_DIR, "TitleSkyline.png"))
    if len(sys.argv) > 1:
        prev = Image.new("RGBA", (1024, 640), (20, 16, 44, 255))
        prev.alpha_composite(lg.resize((768, 384), Image.NEAREST), (128, 0))
        prev.alpha_composite(sk.resize((1024, 128), Image.NEAREST), (0, 450))
        prev.save(sys.argv[1])
    print("wrote TitleLogo.png and TitleSkyline.png to", OUT_DIR)


main()
