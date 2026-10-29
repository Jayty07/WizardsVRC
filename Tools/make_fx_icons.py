"""
Builds the 64x64 pixel-art effect icons (Shield / Blade / Trap per school) from the school badge icons.

    python3 Tools/make_fx_icons.py Assets/WizardsVRC/Textures/FxIcons

Reads School<Name>.png from that folder (the hand-drawn badges) and writes Shield<Name>.png, Blade<Name>.png,
Trap<Name>.png and preview.png (delete the preview before committing, or leave it; Unity ignores nothing here).
Colours per school are in RING / FILL below. Rebuild the scene afterwards.
"""
import math
import sys
from PIL import Image

D = sys.argv[1]
NAMES = ['Fire', 'Ice', 'Storm', 'Life', 'Death', 'Myth', 'Balance', 'General']
RING = {'Fire': (235, 90, 30), 'Ice': (110, 180, 240), 'Storm': (150, 90, 220), 'Life': (70, 190, 80), 'Death': (120, 110, 130),
        'Myth': (230, 180, 40), 'Balance': (200, 165, 100), 'General': (190, 190, 205)}
FILL = {'Fire': (69, 24, 20), 'Ice': (24, 42, 79), 'Storm': (42, 24, 70), 'Life': (24, 60, 29), 'Death': (27, 24, 34),
        'Myth': (69, 46, 20), 'Balance': (60, 46, 29), 'General': (42, 42, 56)}
OUT = (30, 24, 36)
GOLD = (255, 205, 70)
GOLD_D = (190, 130, 40)
GOLD_L = (255, 240, 160)
WHITE = (255, 255, 255)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def outline(img, mask):
    """Dark 1px outline round the mask (on transparent / other pixels)."""
    px = img.load()
    w, h = img.size
    add = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in mask and 0 <= n[0] < w and 0 <= n[1] < h:
                add.add(n)
    for (x, y) in add:
        px[x, y] = OUT + (255,)


def badge(name):
    """The school badge with its symbol removed: outline, ring, dark fill and a softer inner disc."""
    img = Image.open('%s/School%s.png' % (D, name)).convert('RGBA')
    px = img.load()
    f, r = FILL[name], RING[name]
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - 31.5, y - 31.5)
            if d <= 26.6:
                px[x, y] = (mix(f, r, 0.28) if d <= 20.5 else f) + (255,)
    return img


def blade(name):
    img = badge(name)
    px = img.load()
    r, f = RING[name], FILL[name]
    # brighter inner disc behind the sword (like the Iceblade card), with a light rim
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - 31.5, y - 31.5)
            if d <= 22.5:
                px[x, y] = (mix(f, r, 0.55) if d > 20.5 else mix(f, r, 0.42)) + (255,)
    light = mix(r, WHITE, 0.65)
    mid = mix(r, WHITE, 0.35)
    mask = {}
    # blade: 9 wide with a pointed tip, y 6..39
    for y in range(6, 40):
        half = 4 if y >= 12 else (y - 6) * 4 // 6
        for x in range(32 - half - 1, 32 + half):
            if x == 32 - half - 1:
                c = WHITE
            elif x == 32 + half - 1:
                c = r
            elif x == 31 and y >= 13:
                c = mid
            else:
                c = light
            mask[(x, y)] = c
    # crossguard with a school gem
    for y in range(40, 45):
        for x in range(17, 47):
            c = GOLD_L if y == 40 else (GOLD if y < 43 else GOLD_D)
            mask[(x, y)] = c
    for (x, y) in ((16, 41), (16, 42), (16, 43), (47, 41), (47, 42), (47, 43)):
        mask[(x, y)] = GOLD_D
    for y in range(40, 45):
        for x in range(29, 35):
            mask[(x, y)] = mix(r, WHITE, 0.55) if (x, y) in ((30, 41), (31, 41)) else r
    # grip and pommel
    for y in range(45, 53):
        for x in range(29, 35):
            mask[(x, y)] = (90, 50, 25) if (y % 2) else (125, 75, 38)
    for y in range(53, 58):
        w = 2 if y in (53, 57) else 3
        for x in range(32 - w, 32 + w):
            mask[(x, y)] = GOLD_L if y == 53 else (GOLD if y < 57 else GOLD_D)
    for (x, y), c in mask.items():
        px[x, y] = c + (255,)
    outline(img, mask)
    return img


def shield(name):
    """Heraldic shield like Wizard101's shield cards: glossy school-coloured face (light at the top), smooth bevelled
    gold rim, the school's symbol in the middle and a glint. Traps use the same outline but dark with gold teeth."""
    img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    px = img.load()
    r, f = RING[name], FILL[name]
    top_c, mid_c, low_c = mix(r, WHITE, 0.42), r, mix(r, f, 0.4)
    mask = set()
    for y in range(64):
        for x in range(64):
            if not in_shield(x, y, 0.5):
                continue
            mask.add((x, y))
            if not in_shield(x, y, 3.5):
                k = (x - 31.5) * 0.6 + (y - 33)
                c = GOLD_L if k < -14 else (GOLD if k < 12 else GOLD_D)
            elif not in_shield(x, y, 4.5):
                c = GOLD_D
            else:
                t = (y - 9) / 50.0
                c = mix(top_c, mid_c, min(1, t * 2)) if t < 0.5 else mix(mid_c, low_c, min(1, (t - 0.5) * 2))
                # darker right half for a rounded, glossy look
                if x > 34 and in_shield(x, y, 4.5):
                    c = mix(c, f, 0.18)
                if not in_shield(x, y, 6.5):
                    c = mix(c, WHITE, 0.25)
            px[x, y] = c + (255,)
    sym = symbol(name, 32)
    sp = sym.load()
    ox, oy = 16, 14
    placed = set()
    for y in range(32):
        for x in range(32):
            if sp[x, y][3] and in_shield(x + ox, y + oy, 6.5):
                px[x + ox, y + oy] = sp[x, y]
                placed.add((x + ox, y + oy))
    # soft dark edge round the symbol so it stands out on light faces
    for (x, y) in placed:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in placed and in_shield(n[0], n[1], 5.5):
                c = px[n]
                px[n] = mix(c[:3], OUT, 0.55) + (255,)
    # glint, top left
    for i in range(6):
        x, y = 13 + i, 16 - i
        if in_shield(x, y, 6.5):
            px[x, y] = WHITE + (255,)
        if in_shield(x, y + 1, 6.5):
            px[x, y + 1] = mix(top_c, WHITE, 0.6) + (255,)
    outline(img, mask)
    return img


def in_shield(x, y, inset=0.0):
    """Heater shield filling the canvas: straight sides down to y 36, then curving to a point at y 61."""
    top, left, right, mid, tip = 5 + inset, 6 + inset, 57 - inset, 36, 61 - inset * 1.4
    cx = 31.5
    if y < top or y > tip:
        return False
    if y <= mid:
        return left <= x <= right
    t = (y - mid) / (tip - mid)
    half = (right - left) / 2 * (1 - t ** 1.7)
    return abs(x - cx) <= half


def symbol(name, size):
    """The school icon's symbol (its inner disc minus the fill), scaled down with a palette-preserving box filter."""
    src = Image.open('%s/School%s.png' % (D, name)).convert('RGBA')
    sp = src.load()
    f = FILL[name]
    sym = Image.new('RGBA', (52, 52), (0, 0, 0, 0))
    q = sym.load()
    pal = set()
    for y in range(52):
        for x in range(52):
            p = sp[x + 6, y + 6]
            if math.hypot(x + 6 - 31.5, y + 6 - 31.5) <= 26.0 and p[3] and p[:3] != f:
                q[x, y] = p
                pal.add(p[:3])
    small = sym.resize((size, size), Image.BOX)
    sp2 = small.load()
    pal = list(pal)
    for y in range(size):
        for x in range(size):
            p = sp2[x, y]
            if p[3] < 110:
                sp2[x, y] = (0, 0, 0, 0)
                continue
            rgb = p[:3]
            # undo premultiplied darkening toward transparent, then snap to the symbol's own palette
            k = 255.0 / max(1, p[3])
            rgb = tuple(min(255, int(c * k)) for c in rgb) if p[3] < 255 else rgb
            best = min(pal, key=lambda c: sum((c[i] - rgb[i]) ** 2 for i in range(3)))
            sp2[x, y] = best + (255,)
    return small


def trap(name):
    img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    px = img.load()
    r, f = RING[name], FILL[name]
    mask = set()
    for y in range(64):
        for x in range(64):
            if not in_shield(x, y):
                continue
            mask.add((x, y))
            if not in_shield(x, y, 3):
                c = mix(r, WHITE, 0.25) if (x < 31 and y < 40) else r
            elif not in_shield(x, y, 4):
                c = GOLD
            else:
                c = f
            px[x, y] = c + (255,)
    # gold spikes round the rim (the "trap" teeth), pointing outward
    spikes = [((14, 3), (0, -1)), ((25, 3), (0, -1)), ((38, 3), (0, -1)), ((49, 3), (0, -1)),
              ((4, 14), (-1, 0)), ((4, 26), (-1, 0)), ((59, 14), (1, 0)), ((59, 26), (1, 0)),
              ((9, 45), (-1, 1)), ((54, 45), (1, 1)), ((19, 56), (-1, 1)), ((44, 56), (1, 1))]
    for (sx, sy), (ox, oy) in spikes:
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                if abs(dx) + abs(dy) > 2:
                    continue
                x, y = sx + dx, sy + dy
                if 0 <= x < 64 and 0 <= y < 64:
                    lit = dx * ox + dy * oy > 0 or (dx + dy) < 0
                    px[x, y] = (GOLD_L if lit else GOLD) + (255,)
                    mask.add((x, y))
    sym = symbol(name, 34)
    sp = sym.load()
    ox, oy = 32 - 17, 30 - 17
    for y in range(34):
        for x in range(34):
            if sp[x, y][3] and in_shield(x + ox, y + oy, 5):
                px[x + ox, y + oy] = sp[x, y]
    outline(img, mask)
    return img


for n in NAMES:
    shield(n).save('%s/Shield%s.png' % (D, n))
    blade(n).save('%s/Blade%s.png' % (D, n))
    trap(n).save('%s/Trap%s.png' % (D, n))
# preview sheet (x4)
sheet = Image.new('RGBA', (8 * 72, 4 * 72), (60, 60, 70, 255))
for i, n in enumerate(NAMES):
    for j, kind in enumerate(('School', 'Shield', 'Blade', 'Trap')):
        ic = Image.open('%s/%s%s.png' % (D, kind, n))
        sheet.alpha_composite(ic, (i * 72 + 4, j * 72 + 4))
sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save('%s/preview.png' % D)
print('ok')
