"""PS1-style interface art for WizardsVRC (Textures/UI): ornate panels and buttons (9-slice), HUD bar frames and fills,
speaker portrait frames (ally blue / neutral yellow / hostile red), the quest arrow, the book's notification badge,
seat glows for targeting, wooden street signs, tooltips, sparkles and the level-up burst.

Everything is drawn pixel by pixel with a small palette, hard bevels lit from the top left and gold trim, to match the
card frames and the pixel font. The builder imports them point-filtered and sets the 9-slice borders listed in
BORDERS (keep them in step with WizardsVRCSceneBuilder.UiStyle.cs).

    python3 Tools/make_ui_textures.py Assets/WizardsVRC/Textures/UI [preview.png]      (needs numpy and Pillow)
"""
import math
import os
import sys

import numpy as np
from PIL import Image

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)

GOLD = [(70, 44, 12), (150, 100, 28), (226, 172, 58), (255, 228, 140)]      # dark .. light
VIOLET = [(14, 10, 24), (30, 22, 48), (48, 36, 74), (70, 56, 104)]
WOOD = [(46, 26, 14), (84, 50, 26), (122, 78, 42), (160, 108, 62)]
PARCH = [(120, 92, 56), (196, 166, 116), (228, 206, 160), (246, 232, 196)]
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], float) / 16.0

made = []


def save(name, arr):
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')
    img.save(os.path.join(OUT, name + '.png'))
    made.append((name, img))


def canvas(w, h):
    return np.zeros((h, w, 4), float)


def put(a, x, y, c, alpha=255):
    h, w = a.shape[:2]
    if 0 <= x < w and 0 <= y < h:
        a[y, x] = (c[0], c[1], c[2], alpha)


def rect(a, x0, y0, x1, y1, c, alpha=255):
    a[y0:y1, x0:x1] = (c[0], c[1], c[2], alpha)


def dither_fill(a, x0, y0, x1, y1, ramp, top_light=True):
    """A vertical gradient across the ramp, dithered with the 4x4 Bayer pattern."""
    h = y1 - y0
    for y in range(y0, y1):
        t = 1.0 - (y - y0) / max(1, h - 1) if top_light else (y - y0) / max(1, h - 1)
        for x in range(x0, x1):
            v = t * (len(ramp) - 1) + BAYER[y % 4, x % 4] - 0.5
            i = int(np.clip(round(v), 0, len(ramp) - 1))
            a[y, x] = (*ramp[i], 255)


def bevel_frame(a, x0, y0, x1, y1, ramp, width):
    """A bevelled frame: light on the top and left, dark on the bottom and right, mid in between."""
    for k in range(width):
        light = ramp[3] if k == 0 else ramp[2]
        dark = ramp[0] if k == 0 else ramp[1]
        rect(a, x0 + k, y0 + k, x1 - k, y0 + k + 1, light)
        rect(a, x0 + k, y0 + k, x0 + k + 1, y1 - k, light)
        rect(a, x0 + k, y1 - k - 1, x1 - k, y1 - k, dark)
        rect(a, x1 - k - 1, y0 + k, x1 - k, y1 - k, dark)


def gem(a, cx, cy, r, col):
    light = tuple(min(255, int(c * 1.5 + 40)) for c in col)
    dark = tuple(int(c * 0.45) for c in col)
    for y in range(-r, r + 1):
        for x in range(-r, r + 1):
            if abs(x) + abs(y) <= r:
                c = light if x + y < -r // 2 else (dark if x + y > r // 2 else col)
                put(a, cx + x, cy + y, c)
    for y in range(-r - 1, r + 2):
        for x in range(-r - 1, r + 2):
            if abs(x) + abs(y) == r + 1:
                put(a, cx + x, cy + y, GOLD[0])


# ------------------------------------------------------------------ panels and buttons (9-slice)

def ornate_panel(size=64, border=16, fill=VIOLET, trim=GOLD, gem_col=(90, 160, 255), name='PanelOrnate'):
    a = canvas(size, size)
    dither_fill(a, 0, 0, size, size, fill)
    bevel_frame(a, 0, 0, size, size, trim, 3)
    # an inner gold hairline with a dark gap, like a book cover
    rect(a, 5, 5, size - 5, 6, trim[2]); rect(a, 5, size - 6, size - 5, size - 5, trim[1])
    rect(a, 5, 5, 6, size - 5, trim[2]); rect(a, size - 6, 5, size - 5, size - 5, trim[1])
    # corner pieces: a gold square with a gem in each corner
    for cx, cy in ((7, 7), (size - 8, 7), (7, size - 8), (size - 8, size - 8)):
        for y in range(-5, 6):
            for x in range(-5, 6):
                if max(abs(x), abs(y)) == 5:
                    put(a, cx + x, cy + y, trim[1])
        gem(a, cx, cy, 3, gem_col)
    save(name, a)


ornate_panel()
ornate_panel(fill=PARCH, trim=WOOD, gem_col=(200, 60, 50), name='PanelParchment')
ornate_panel(fill=[(10, 8, 16), (18, 14, 28), (26, 20, 40), (34, 28, 52)], trim=GOLD, gem_col=(255, 200, 80), name='Tooltip')


def button(size=48):
    """Greyscale bevelled button (the builder tints it per button) with a gold rim."""
    grey = [(70, 70, 70), (130, 130, 130), (190, 190, 190), (235, 235, 235)]
    a = canvas(size, size)
    dither_fill(a, 0, 0, size, size, grey)
    bevel_frame(a, 0, 0, size, size, GOLD, 2)
    rect(a, 3, 3, size - 3, 4, (255, 255, 255))
    rect(a, 3, size - 4, size - 3, size - 3, (40, 40, 40))
    save('Button', a)


button()


# ------------------------------------------------------------------ bars

def bar_frame(w=128, h=24):
    """Gold frame round a dark well; the ends are capped with a gem. 9-slice border 10 x 8."""
    a = canvas(w, h)
    rect(a, 0, 0, w, h, GOLD[1])
    bevel_frame(a, 0, 0, w, h, GOLD, 2)
    rect(a, 4, 4, w - 4, h - 4, (10, 6, 14))
    rect(a, 4, 4, w - 4, 5, (4, 2, 6))
    save('BarFrame', a)


def bar_fill(name, ramp, w=64, h=16):
    """A fill with a bright top, a shine line and soft vertical bands every 8 px (reads as 'liquid' in a tube)."""
    a = canvas(w, h)
    dither_fill(a, 0, 0, w, h, ramp)
    rect(a, 0, 2, w, 3, tuple(min(255, c + 60) for c in ramp[3]))
    for x in range(0, w, 8):
        rect(a, x, 0, x + 1, h, ramp[1], 255)
    save(name, a)


bar_frame()
bar_fill('BarFillHealth', [(90, 8, 12), (170, 22, 28), (226, 54, 54), (255, 120, 110)])
bar_fill('BarFillMana', [(10, 24, 90), (24, 70, 180), (60, 130, 240), (140, 200, 255)])
bar_fill('BarFillXp', [(40, 14, 70), (96, 46, 160), (150, 96, 230), (210, 170, 255)])
bar_fill('BarFillGold', [(90, 56, 8), (170, 110, 20), (230, 176, 50), (255, 230, 130)])


# ------------------------------------------------------------------ portraits

def portrait_frame(name, ramp, size=64):
    """A round medallion frame (transparent middle) for the speaker's portrait."""
    a = canvas(size, size)
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c)
            if 26 <= r <= 31.5:
                ang = math.atan2(y - c, x - c)
                lit = -math.cos(ang + math.pi / 4)        # top left bright
                v = 1.5 + lit * 1.4 + (BAYER[y % 4, x % 4] - 0.5)
                i = int(np.clip(round(v), 0, 3))
                if r < 27 or r > 30.5:
                    i = 0
                a[y, x] = (*ramp[i], 255)
    for k in range(8):   # studs round the rim
        ang = k * math.pi / 4
        gem(a, int(round(c + math.cos(ang) * 28.7)), int(round(c + math.sin(ang) * 28.7)), 1, ramp[3])
    save(name, a)


portrait_frame('PortraitAlly', [(10, 30, 80), (30, 90, 190), (80, 160, 255), (190, 230, 255)])
portrait_frame('PortraitNeutral', [(80, 60, 6), (180, 140, 20), (240, 200, 60), (255, 244, 170)])
portrait_frame('PortraitHostile', [(80, 8, 8), (170, 24, 24), (240, 70, 60), (255, 170, 150)])


def portrait_back(size=64):
    """A soft dark disc behind the speaker (the face or school icon goes on top)."""
    a = canvas(size, size)
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c)
            if r <= 27:
                v = 1 - r / 27
                g = int(30 + 40 * v + 8 * (BAYER[y % 4, x % 4] - 0.5))
                a[y, x] = (g, int(g * 0.85), int(g * 1.3), 255)
    save('PortraitBack', a)


portrait_back()


# ------------------------------------------------------------------ quest arrow, badge, sparkle, glows

def quest_arrow(size=64):
    """A gold arrowhead pointing up (+Y), dark outline, glowing core. The guide turns it to point the way."""
    a = canvas(size, size)
    pts = []
    for y in range(size):
        for x in range(size):
            u, v = (x - 31.5) / 32.0, (31.5 - y) / 32.0
            head = v > -0.15 and abs(u) < (0.85 - v) * 0.78
            shaft = -0.85 < v <= -0.15 and abs(u) < 0.24
            if head or shaft:
                pts.append((x, y))
    inside = set(pts)
    for x, y in pts:
        edge = any((x + dx, y + dy) not in inside for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        u = (x - 31.5) / 32.0
        if edge:
            a[y, x] = (*GOLD[0], 255)
        else:
            i = 3 if u < -0.15 else (2 if u < 0.2 else 1)
            a[y, x] = (*GOLD[i], 255)
    # outer glow ring of dark outline for contrast
    for x, y in pts:
        for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2)):
            if (x + dx, y + dy) not in inside and a[y + dy if 0 <= y + dy < size else y, x + dx if 0 <= x + dx < size else x][3] == 0:
                put(a, x + dx, y + dy, (20, 10, 4), 200)
    save('QuestArrow', a)


quest_arrow()


def badge(size=32):
    """The book's notification badge: a red gem disc with a white '!'."""
    a = canvas(size, size)
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c)
            if r <= 14.5:
                lit = (c - x) + (c - y)
                col = (255, 120, 100) if lit > 10 else ((220, 40, 40) if lit > -8 else (140, 16, 20))
                if r > 13:
                    col = GOLD[2] if lit > 0 else GOLD[0]
                a[y, x] = (*col, 255)
    rect(a, 14, 7, 18, 19, (255, 255, 255))
    rect(a, 14, 21, 18, 25, (255, 255, 255))
    save('NotifyBadge', a)


badge()


def sparkle(size=16):
    """A four-pointed star for particles (white, the systems colour it)."""
    a = canvas(size, size)
    c = 7.5
    for y in range(size):
        for x in range(size):
            dx, dy = abs(x - c), abs(y - c)
            if (dx < 1 and dy < 7.5) or (dy < 1 and dx < 7.5) or dx + dy < 3.5:
                v = 255 if dx + dy < 2.5 else 210
                a[y, x] = (v, v, v, 255)
    save('Sparkle', a)


sparkle()


def seat_glow(name, col, size=128):
    """A glowing ring with runes of light round a duel seat (targeting: green ally, red enemy). Additive-friendly."""
    a = canvas(size, size)
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c) / c
            ang = math.atan2(y - c, x - c)
            ring = math.exp(-((r - 0.8) / 0.06) ** 2)
            inner = 0.25 * max(0.0, 1 - r / 0.8)
            ticks = 0.6 * math.exp(-((r - 0.92) / 0.03) ** 2) * (1 if (math.degrees(ang) % 30) < 12 else 0)
            v = min(1.0, ring + inner + ticks)
            v = round(v * 6 + BAYER[y % 4, x % 4] - 0.5) / 6.0
            if v > 0:
                a[y, x] = (col[0], col[1], col[2], 255 * v)
    save(name, a)


seat_glow('SeatGlowAlly', (80, 255, 120))
seat_glow('SeatGlowEnemy', (255, 70, 60))


# ------------------------------------------------------------------ signs

def sign_board(w=128, h=48):
    """A wooden street sign: planks in a carved frame, gold corner brackets and scroll ends. 9-slice border 16."""
    a = canvas(w, h)
    for y in range(h):
        plank = (y // 8) % 2
        for x in range(w):
            grain = 0.5 + 0.5 * math.sin(x * 0.21 + y * 0.9 + plank * 2.0)
            v = 1.2 + plank * 0.4 + grain * 0.8 + (BAYER[y % 4, x % 4] - 0.5)
            a[y, x] = (*WOOD[int(np.clip(round(v), 0, 3))], 255)
        if y % 8 == 0:
            rect(a, 0, y, w, y + 1, WOOD[0])
    bevel_frame(a, 0, 0, w, h, WOOD, 3)
    rect(a, 4, 4, w - 4, 5, GOLD[1]); rect(a, 4, h - 5, w - 4, h - 4, GOLD[0])
    rect(a, 4, 4, 5, h - 4, GOLD[1]); rect(a, w - 5, 4, w - 4, h - 4, GOLD[0])
    for cx, cy, sx, sy in ((6, 6, 1, 1), (w - 7, 6, -1, 1), (6, h - 7, 1, -1), (w - 7, h - 7, -1, -1)):
        for k in range(8):   # L-shaped gold bracket
            put(a, cx + sx * k, cy, GOLD[2]); put(a, cx, cy + sy * k, GOLD[2])
            put(a, cx + sx * k, cy + sy, GOLD[1]); put(a, cx + sx, cy + sy * k, GOLD[1])
        gem(a, cx + sx * 3, cy + sy * 3, 1, (220, 60, 50))
    save('SignBoard', a)


sign_board()


# ------------------------------------------------------------------ preview

if len(sys.argv) > 2:
    W = sum(img.width for _, img in made) + 8 * len(made) + 8
    H = max(img.height for _, img in made) + 16
    sheet = Image.new('RGBA', (W, H), (60, 70, 60, 255))
    x = 8
    for _, img in made:
        sheet.alpha_composite(img, (x, 8))
        x += img.width + 8
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(sys.argv[2])
print('made', len(made), 'UI textures')
