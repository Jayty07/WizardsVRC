"""
Spell card frames, design 1 "Classic" (Wizard101 style), one per school, in the game's pixel-art style.

    python3 Tools/make_card_frames.py Assets/WizardsVRC/Textures/FxIcons Assets/WizardsVRC/Textures/Cards

Writes CardFrame<School>.png (80x112, no text: the battle hand draws the name, pips, accuracy and text on top) and
card_preview.png (every school with sample text, for review; not used by the game). The art window is left as a
school-coloured glow until spells have their own art. Colours per school are in CARD below.
"""
import math
import random
import sys
from PIL import Image



OUT = (30, 24, 36)
WHITE = (255, 255, 255)
GOLD = (255, 205, 70)
GOLD_D = (190, 130, 40)
GOLD_L = (255, 240, 160)
PARCH = (236, 222, 186)
PARCH_D = (198, 172, 128)
INK = (62, 40, 24)
RING = {'Fire': (235, 90, 30), 'Ice': (110, 180, 240), 'Storm': (150, 90, 220), 'Life': (70, 190, 80), 'Death': (120, 110, 130),
        'Myth': (230, 180, 40), 'Balance': (200, 165, 100), 'General': (190, 190, 205)}
FILL = {'Fire': (69, 24, 20), 'Ice': (24, 42, 79), 'Storm': (42, 24, 70), 'Life': (24, 60, 29), 'Death': (27, 24, 34),
        'Myth': (69, 46, 20), 'Balance': (60, 46, 29), 'General': (42, 42, 56)}

def _font(rows_by_char, w, h):
    out = {}
    for ch, rows in rows_by_char.items():
        rows = rows.split()
        assert len(rows) == h and all(len(r) == w for r in rows), (ch, rows)
        out[ch] = "".join(rows)
    out[' '] = "." * (w * h)
    return out


F57 = _font({
    'A': ".###. #...# #...# ##### #...# #...# #...#", 'B': "####. #...# #...# ####. #...# #...# ####.",
    'C': ".###. #...# #.... #.... #.... #...# .###.", 'D': "####. #...# #...# #...# #...# #...# ####.",
    'E': "##### #.... #.... ####. #.... #.... #####", 'F': "##### #.... #.... ####. #.... #.... #....",
    'G': ".###. #...# #.... #.### #...# #...# .####", 'H': "#...# #...# #...# ##### #...# #...# #...#",
    'I': ".###. ..#.. ..#.. ..#.. ..#.. ..#.. .###.", 'J': "..### ...#. ...#. ...#. ...#. #..#. .##..",
    'K': "#...# #..#. #.#.. ##... #.#.. #..#. #...#", 'L': "#.... #.... #.... #.... #.... #.... #####",
    'M': "#...# ##.## #.#.# #.#.# #...# #...# #...#", 'N': "#...# #...# ##..# #.#.# #..## #...# #...#",
    'O': ".###. #...# #...# #...# #...# #...# .###.", 'P': "####. #...# #...# ####. #.... #.... #....",
    'Q': ".###. #...# #...# #...# #.#.# #..#. .##.#", 'R': "####. #...# #...# ####. #.#.. #..#. #...#",
    'S': ".#### #.... #.... .###. ....# ....# ####.", 'T': "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
    'U': "#...# #...# #...# #...# #...# #...# .###.", 'V': "#...# #...# #...# #...# #...# .#.#. ..#..",
    'W': "#...# #...# #...# #.#.# #.#.# #.#.# .#.#.", 'X': "#...# #...# .#.#. ..#.. .#.#. #...# #...#",
    'Y': "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#..", 'Z': "##### ....# ...#. ..#.. .#... #.... #####",
    '0': ".###. #...# #..## #.#.# ##..# #...# .###.", '1': "..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.",
    '2': ".###. #...# ....# ...#. ..#.. .#... #####", '3': "####. ....# ....# .###. ....# ....# ####.",
    '4': "...#. ..##. .#.#. #..#. ##### ...#. ...#.", '5': "##### #.... ####. ....# ....# #...# .###.",
    '6': ".###. #.... #.... ####. #...# #...# .###.", '7': "##### ....# ...#. ..#.. .#... .#... .#...",
    '8': ".###. #...# #...# .###. #...# #...# .###.", '9': ".###. #...# #...# .#### ....# ....# .###.",
    '%': "##..# ##..# ...#. ..#.. .#... #..## #..##", '-': "..... ..... ..... ##### ..... ..... .....",
    '+': "..... ..#.. ..#.. ##### ..#.. ..#.. .....", '.': "..... ..... ..... ..... ..... .##.. .##..",
    "'": "..#.. ..#.. .#... ..... ..... ..... .....", '!': "..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#..",
    '/': "....# ....# ...#. ..#.. .#... #.... #....", ':': "..... .##.. .##.. ..... .##.. .##.. .....",
}, 5, 7)
F35 = _font({
    'A': ".#. #.# ### #.# #.#", 'B': "##. #.# ##. #.# ##.", 'C': ".## #.. #.. #.. .##", 'D': "##. #.# #.# #.# ##.",
    'E': "### #.. ##. #.. ###", 'F': "### #.. ##. #.. #..", 'G': ".## #.. #.# #.# .##", 'H': "#.# #.# ### #.# #.#",
    'I': "### .#. .#. .#. ###", 'J': "..# ..# ..# #.# .#.", 'K': "#.# #.# ##. #.# #.#", 'L': "#.. #.. #.. #.. ###",
    'M': "#.# ### #.# #.# #.#", 'N': "##. #.# #.# #.# #.#", 'O': ".#. #.# #.# #.# .#.", 'P': "##. #.# ##. #.. #..",
    'Q': ".#. #.# #.# ##. .##", 'R': "##. #.# ##. #.# #.#", 'S': ".## #.. .#. ..# ##.", 'T': "### .#. .#. .#. .#.",
    'U': "#.# #.# #.# #.# ###", 'V': "#.# #.# #.# #.# .#.", 'W': "#.# #.# #.# ### #.#", 'X': "#.# #.# .#. #.# #.#",
    'Y': "#.# #.# .#. .#. .#.", 'Z': "### ..# .#. #.. ###", '0': "### #.# #.# #.# ###", '1': ".#. ##. .#. .#. ###",
    '2': "##. ..# .#. #.. ###", '3': "##. ..# .#. ..# ##.", '4': "#.# #.# ### ..# ..#", '5': "### #.. ##. ..# ##.",
    '6': ".## #.. ### #.# ###", '7': "### ..# .#. .#. .#.", '8': "### #.# ### #.# ###", '9': "### #.# ### ..# ##.",
    '%': "#.# ..# .#. #.. #.#", '-': "... ... ### ... ...", '+': "... .#. ### .#. ...", '.': "... ... ... ... .#.",
    "'": ".#. .#. ... ... ...", '!': ".#. .#. .#. ... .#.", '/': "..# ..# .#. #.. #..", ':': "... .#. ... .#. ...",
    ',': "... ... ... .#. #..",
}, 3, 5)
# Runic marks for the Runestone edges (not letters).
RUNES = [_font({'r': r}, 3, 5)['r'] for r in (
    "#.. ##. #.# #.. #..", ".#. ### .#. #.# .#.", "#.# .#. .#. .#. #.#", "##. #.. ### ..# .##",
    ".#. #.# .#. .#. ###", "#.# #.# .#. #.. #..", "### .#. #.# .#. ...", "..# .## #.# .#. #..")]
def font_info(f):
    return (5, 7) if f is F57 else (3, 5)


def text_w(s, f):
    w, _ = font_info(f)
    return max(0, len(s) * (w + 1) - 1)


def draw_text(px, x, y, s, f, c, shadow=None):
    w, h = font_info(f)
    for ch in s.upper():
        g = f.get(ch, f[' '])
        for j in range(h):
            for i in range(w):
                if g[j * w + i] == '#':
                    if shadow is not None:
                        px[x + i + 1, y + j + 1] = shadow + (255,)
                    px[x + i, y + j] = c + (255,)
        x += w + 1


def draw_text_c(px, cx, y, s, f, c, shadow=None):
    draw_text(px, cx - text_w(s, f) // 2, y, s, f, c, shadow)


def wrap(s, f, maxw):
    words, lines, cur = s.upper().split(), [], ""
    for wd in words:
        t = (cur + " " + wd).strip()
        if text_w(t, f) <= maxw: cur = t
        else: lines.append(cur); cur = wd
    if cur: lines.append(cur)
    return lines


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def grad(px, x0, y0, x1, y1, ca, cb, inside=None, steps=4, horizontal=False):
    """PSX-style ordered-dither gradient between two colours."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside and not inside(x, y): continue
            t = (x - x0) / max(1, x1 - x0) if horizontal else (y - y0) / max(1, y1 - y0)
            lv = t * steps
            base = int(lv)
            frac = lv - base
            if frac * 16 > BAYER[y % 4][x % 4]: base += 1
            px[x, y] = mix(ca, cb, min(1, base / steps)) + (255,)


def rect(px, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1): px[x, y] = c + (255,)


def frame(px, x0, y0, x1, y1, c):
    for x in range(x0, x1 + 1): px[x, y0] = c + (255,); px[x, y1] = c + (255,)
    for y in range(y0, y1 + 1): px[x0, y] = c + (255,); px[x1, y] = c + (255,)


def in_round(x, y, x0, y0, x1, y1, r):
    if x < x0 or x > x1 or y < y0 or y > y1: return False
    cx = x0 + r if x < x0 + r else (x1 - r if x > x1 - r else x)
    cy = y0 + r if y < y0 + r else (y1 - r if y > y1 - r else y)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r + r * 0.8


def icon(name, size, symbol_only=False):
    """An icon from the FxIcons folder scaled down with a palette-preserving box filter."""
    src = Image.open('%s/%s.png' % (ICONS, name)).convert('RGBA')
    if symbol_only:
        school = name[len('School'):]
        sp = src.load()
        crop = Image.new('RGBA', (52, 52), (0, 0, 0, 0))
        q = crop.load()
        for y in range(52):
            for x in range(52):
                p = sp[x + 6, y + 6]
                if math.hypot(x + 6 - 31.5, y + 6 - 31.5) <= 26.0 and p[3] and p[:3] != FILL[school]: q[x, y] = p
        src = crop
    pal = sorted({p[:3] for p in list(src.getdata()) if p[3] > 0})
    small = src.resize((size, size), Image.BOX)
    sp = small.load()
    for y in range(size):
        for x in range(size):
            p = sp[x, y]
            if p[3] < 110: sp[x, y] = (0, 0, 0, 0); continue
            k = 255.0 / p[3]
            rgb = tuple(min(255, int(c * k)) for c in p[:3])
            sp[x, y] = min(pal, key=lambda c: sum((c[i] - rgb[i]) ** 2 for i in range(3))) + (255,)
    return small


def paste(img, ic, x, y, clip=None):
    sp, px = ic.load(), img.load()
    for j in range(ic.height):
        for i in range(ic.width):
            if sp[i, j][3] and (clip is None or clip(x + i, y + j)): px[x + i, y + j] = sp[i, j]


def outline_alpha(img, c=OUT):
    """1px dark outline round every opaque pixel."""
    px = img.load()
    add = []
    for y in range(img.height):
        for x in range(img.width):
            if px[x, y][3]: continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < img.width and 0 <= ny < img.height and px[nx, ny][3]: add.append((x, y)); break
    for x, y in add: px[x, y] = c + (255,)



# Vibrant card colours per school (frame); the badge icons keep their own softer palette.
CARD = {'Fire': (255, 78, 18), 'Ice': (36, 166, 255), 'Storm': (170, 60, 255), 'Life': (30, 212, 70), 'Death': (46, 42, 54),
        'Myth': (255, 194, 0), 'Balance': (240, 138, 40), 'General': (178, 186, 216)}
# How much the bevel / art glow lighten toward white (dark schools keep it low so they stay dark).
HIGHLIGHT = {'Death': 0.16}

SCHOOLS = ['Fire', 'Ice', 'Storm', 'Life', 'Death', 'Myth', 'Balance', 'General']
W, H = 80, 112


def frame_card(school, icons):
    """The blank Classic card for a school (everything but the text)."""
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()
    r = CARD[school]
    hl = HIGHLIGHT.get(school, 0.5)
    L, D = mix(r, WHITE, hl), mix(r, (0, 0, 0), 0.42)
    banner = mix(r, (0, 0, 0), 0.25)
    inside = lambda x, y: in_round(x, y, 0, 0, W - 1, H - 1, 4)
    for y in range(H):
        for x in range(W):
            if not inside(x, y): continue
            edge = min(x, y, W - 1 - x, H - 1 - y)
            c = r
            if edge < 5:
                if (x < 5 or y < 5) and (x + y) < (W - 1 - x + H - 1 - y): c = L
                elif W - 1 - x < 5 or H - 1 - y < 5: c = D
            # a bright inner rim line
            if edge == 4: c = mix(r, WHITE, hl * 0.5)
            px[x, y] = c + (255,)
    for y in range(H):
        for x in range(W):
            if inside(x, y) and not in_round(x, y, 1, 1, W - 2, H - 2, 3): px[x, y] = OUT + (255,)
    rect(px, 5, 5, W - 6, H - 6, PARCH)
    frame(px, 5, 5, W - 6, H - 6, PARCH_D)
    # name banner
    rect(px, 15, 7, W - 16, 17, banner); frame(px, 15, 7, W - 16, 17, OUT)
    for x in range(16, W - 16): px[x, 8] = mix(r, WHITE, hl * 0.7) + (255,)
    # art window: a school-coloured glow (no picture until spells have art)
    rect(px, 10, 20, W - 11, 60, OUT)
    grad(px, 11, 21, W - 12, 59, mix(r, WHITE, hl * 1.2), mix(r, (0, 0, 0), 0.15))
    cx, cy = W / 2, 40
    for y in range(21, 60):
        for x in range(11, W - 11):
            d = math.hypot((x - cx) / 1.2, y - cy)
            if d < 13 and BAYER[y % 4][x % 4] < (13 - d) * 1.4: px[x, y] = mix(px[x, y][:3], WHITE, hl * 0.9) + (255,)
    frame(px, 10, 20, W - 11, 60, GOLD_D)
    for x in range(11, W - 11): px[x, 21] = GOLD_L + (255,)
    # pip gem
    for y in range(-7, 8):
        for x in range(-7, 8):
            k = abs(x) + abs(y)
            if k <= 7: px[9 + x, 10 + y] = (OUT if k == 7 else (GOLD if k >= 5 else mix(r, (0, 0, 0), 0.35))) + (255,)
    # accuracy seal
    for y in range(-6, 7):
        for x in range(-11, 12):
            e = (x / 11.5) ** 2 + (y / 6.5) ** 2
            if e <= 1: px[15 + x, 64 + y] = (OUT if e > 0.78 else (GOLD_L if y < -2 else GOLD)) + (255,)
    # school badge
    paste(img, icon_from(icons, 'School' + school, 14), W - 17, 3)
    return img


def icon_from(icons, name, size):
    global ICONS
    ICONS = icons
    return icon(name, size)


# Cards whose effect has an icon (shields, blades, traps) show it in the art window; other spells stay blank for now.
ART = {"Tower Ward": "ShieldGeneral", "Rime Edge": "BladeIce"}

SAMPLES = [("Ember Imp", "Fire", "1", "75%", "Deals 65-95 Fire damage"), ("Frost Mite", "Ice", "1", "80%", "Deals 50-80 Ice damage"),
           ("Spark Sprite", "Storm", "1", "70%", "Deals 90-130 Storm damage"), ("Thornling", "Life", "1", "90%", "Deals 55-85 Life damage"),
           ("Shade Wisp", "Death", "1", "85%", "Drains 55-85 health, heals you half"), ("Mire Imp", "Myth", "1", "80%", "Deals 70-100 Myth damage"),
           ("Prism Spark", "Balance", "1", "85%", "Deals 65-95 Balance damage"), ("Tower Ward", "General", "0", "100%", "Shield: blocks 50% of the next spell"),
           ("Rime Edge", "Ice", "0", "100%", "Blade: +35% to the next Ice spell")]


def with_text(card, name, school, pips, acc, text, icons=None):
    img = card.copy(); px = img.load()
    if icons and name in ART: paste(img, icon_from(icons, ART[name], 34), W // 2 - 17, 23)
    if text_w(name, F57) <= W - 34: draw_text_c(px, W // 2, 9, name, F57, GOLD_L, OUT)
    else: draw_text_c(px, W // 2, 10, name, F35, GOLD_L, OUT)
    draw_text_c(px, 10, 7, pips, F57, WHITE)
    draw_text_c(px, 15, 62, acc, F35, INK)
    for i, line in enumerate(wrap(text, F35, W - 22)):
        draw_text_c(px, W // 2, 72 + i * 7, line, F35, INK)
    draw_text_c(px, W // 2, 96, school, F35, PARCH_D)
    return img


if __name__ == "__main__":
    icons, out = sys.argv[1], sys.argv[2]
    import os
    os.makedirs(out, exist_ok=True)
    frames = {}
    for s in SCHOOLS:
        frames[s] = frame_card(s, icons)
        frames[s].save(os.path.join(out, "CardFrame%s.png" % s))
    sheet = Image.new('RGBA', (len(SAMPLES) * (W + 6) + 6, H + 12), (40, 36, 50, 255))
    for i, smp in enumerate(SAMPLES):
        sheet.alpha_composite(with_text(frames[smp[1]], *smp, icons=icons), (6 + i * (W + 6), 6))
    sheet = sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST)
    sheet.save(sys.argv[3] if len(sys.argv) > 3 else os.path.join(out, "card_preview.png"))
    print("ok", sheet.size)
