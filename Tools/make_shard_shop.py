"""Pixel art for the Starshard Shop (Assets/WizardsVRC/Textures/Shop): a blue prismatic window of cut crystal.

    ShopBack        the window: deep blue to violet, faint crystal facets, prismatic light bands and stars (392 x 220,
                    drawn 5x on the 1960 x 1100 book canvas)
    GlassPanel      dark glass panel with a bright faceted rim (9-slice, 12 px border)
    Tile / TileSel  an item tile, and the selected one blazing (9-slice, 12 px)
    CatButton / CatButtonSel / BuyButton / GetButton / BackButton    prism buttons (9-slice, 10 px)
    PriceChip       the little price plate (9-slice, 6 px)
    Shard           the Starshard (balance and prices)
    IconFeatured / IconElixir / IconTitle / IconAura / IconSkin       category icons
    ElixirGlass / ElixirLiquid    an elixir bottle; the liquid is white so the game tints it per elixir
    AuraRing / SkinGlyph          white, tinted per aura / spell skin
    TitleScroll     a gold banner for titles
    Sparkle / Sheen / Glow        effects (a twinkle, a diagonal light band, a soft round glow)
    BookGem         the Starshard set in gold, the shop's icon on the book
    AuraFlame / AuraSpark         the power-up auras' flame tongue and spark streak (white, tinted per school)

python3 Tools/make_shard_shop.py   (needs Pillow)
"""
import math
import os
import random

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Textures', 'Shop')
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
INK = (14, 10, 40, 255)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def clamp(v, lo=0, hi=255):
    return max(lo, min(hi, int(v)))


def new(w, h):
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def save(img, name):
    img.save(os.path.join(OUT, name + '.png'))
    print('wrote', name, img.size)


def dither(x, y):
    return BAYER[y % 4][x % 4] / 16.0 - 0.5


# ------------------------------------------------------------------ the window

def shop_back():
    W, H = 392, 220
    img = new(W, H)
    px = img.load()
    rng = random.Random(11)
    top, bot = (16, 22, 70), (52, 22, 96)
    # Crystal facets: a jittered grid of points, each pixel takes a slightly different shade from its nearest point.
    pts = []
    for gy in range(-1, 8):
        for gx in range(-1, 14):
            pts.append((gx * 32 + rng.uniform(-10, 10), gy * 32 + rng.uniform(-10, 10), rng.uniform(-0.07, 0.07)))
    for y in range(H):
        for x in range(W):
            t = y / (H - 1)
            c = mix(top, bot, t)
            best, shade = 1e9, 0.0
            for (qx, qy, s) in pts:
                d = (qx - x) ** 2 + (qy - y) ** 2
                if d < best:
                    best, shade = d, s
            k = 1 + shade + dither(x, y) * 0.06
            c = tuple(clamp(ch * k) for ch in c)
            # Prismatic bands sweeping across: faint rainbow along the diagonal.
            u = (x + y * 1.4) / 90.0
            band = max(0.0, math.sin(u * math.pi)) ** 18
            hue = (x / W + y / H * 0.5) % 1.0
            rr = 0.5 + 0.5 * math.cos(2 * math.pi * hue)
            gg = 0.5 + 0.5 * math.cos(2 * math.pi * (hue - 0.33))
            bb = 0.5 + 0.5 * math.cos(2 * math.pi * (hue - 0.66))
            c = (clamp(c[0] + band * 70 * rr), clamp(c[1] + band * 70 * gg), clamp(c[2] + band * 90 * bb))
            px[x, y] = c + (255,)
    # Facet edges: a thin lighter line where the nearest point changes.
    for (qx, qy, s) in pts:
        pass
    # Stars.
    for k in range(120):
        x, y = rng.randrange(W), rng.randrange(H)
        b = rng.random()
        px[x, y] = mix(px[x, y], (220, 235, 255, 255), 0.4 + 0.6 * b)
        if b > 0.85:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + dx < W and 0 <= y + dy < H:
                    px[x + dx, y + dy] = mix(px[x + dx, y + dy], (150, 200, 255, 255), 0.5)
    # A frame: bright cut-crystal rim round the edge.
    for y in range(H):
        for x in range(W):
            e = min(x, y, W - 1 - x, H - 1 - y)
            if e == 0:
                px[x, y] = INK
            elif e == 1:
                px[x, y] = (120, 210, 255, 255) if (x + y) % 6 < 4 else (190, 150, 255, 255)
            elif e == 2:
                px[x, y] = (60, 110, 200, 255)
    save(img, 'ShopBack')


def faceted_frame(w, h, fill, rim, rim2, border, corner=3, alpha=255, inner_glow=None):
    img = new(w, h)
    px = img.load()
    for y in range(h):
        for x in range(w):
            # Cut corners.
            cx = min(x, w - 1 - x)
            cy = min(y, h - 1 - y)
            if cx + cy < corner:
                continue
            e = min(x, y, w - 1 - x, h - 1 - y)
            if cx + cy == corner or e == 0:
                px[x, y] = INK
                continue
            if e <= 2 or cx + cy <= corner + 2:
                c = rim if (x + y) % 8 < 5 else rim2
                if y < h / 2 and e <= 2 and (y <= 2):
                    c = mix(c, (255, 255, 255, 255), 0.35)
                px[x, y] = c
                continue
            t = y / (h - 1)
            c = mix(fill[0], fill[1], t)
            if inner_glow is not None and e <= border - 2:
                c = mix(c, inner_glow, (border - 2 - e) / (border - 2) * 0.55)
            c = tuple(clamp(ch * (1 + dither(x, y) * 0.08)) for ch in c[:3]) + (alpha,)
            px[x, y] = c
    return img


def panels():
    save(faceted_frame(48, 48, ((22, 30, 74, 255), (16, 16, 52, 255)), (110, 200, 255, 255), (170, 140, 255, 255), 12, 4, 225,
                       (60, 110, 210, 255)), 'GlassPanel')
    save(faceted_frame(48, 48, ((40, 58, 128, 255), (26, 30, 86, 255)), (120, 190, 255, 255), (150, 130, 240, 255), 12, 4, 240,
                       (70, 120, 220, 255)), 'Tile')
    save(faceted_frame(48, 48, ((70, 110, 200, 255), (44, 52, 150, 255)), (240, 255, 255, 255), (170, 240, 255, 255), 12, 4, 255,
                       (160, 220, 255, 255)), 'TileSel')
    save(faceted_frame(64, 24, ((44, 58, 130, 255), (28, 30, 90, 255)), (110, 190, 255, 255), (160, 130, 240, 255), 10, 3), 'CatButton')
    save(faceted_frame(64, 24, ((110, 170, 255, 255), (60, 90, 210, 255)), (245, 255, 255, 255), (190, 240, 255, 255), 10, 3), 'CatButtonSel')
    save(faceted_frame(64, 24, ((80, 230, 255, 255), (30, 140, 220, 255)), (240, 255, 255, 255), (180, 255, 255, 255), 10, 3), 'BuyButton')
    save(faceted_frame(64, 24, ((170, 110, 255, 255), (100, 50, 190, 255)), (240, 220, 255, 255), (200, 170, 255, 255), 10, 3), 'GetButton')
    save(faceted_frame(64, 24, ((56, 60, 110, 255), (34, 34, 76, 255)), (150, 160, 220, 255), (120, 120, 200, 255), 10, 3), 'BackButton')
    save(faceted_frame(32, 16, ((16, 20, 52, 255), (10, 12, 36, 255)), (90, 160, 240, 255), (130, 110, 220, 255), 6, 2), 'PriceChip')


# ------------------------------------------------------------------ icons (32 x 32, drawn big on screen)

def outline(img, c=INK):
    """A 1 px dark outline round everything opaque."""
    w, h = img.size
    px = img.load()
    src = img.copy().load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + dx < w and 0 <= y + dy < h and src[x + dx, y + dy][3] > 128:
                    px[x, y] = c
                    break
    return img


def poly_fill(px, pts, color_fn, w, h):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        for x in range(int(min(xs)), int(max(xs)) + 1):
            if 0 <= x < w and 0 <= y < h and inside(pts, x + 0.5, y + 0.5):
                px[x, y] = color_fn(x, y)


def inside(pts, x, y):
    n, c = len(pts), False
    j = n - 1
    for i in range(n):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if ((yi > y) != (yj > y)) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-9) + xi:
            c = not c
        j = i
    return c


def shard_icon(size=32, mount=False):
    img = new(size, size)
    px = img.load()
    s = size / 32.0
    cx = size / 2
    # A tall cut crystal: left facets violet-blue, right facets cyan-white, a bright centre ridge.
    top, mid_l, mid_r, bot = (cx, 2 * s), (cx - 9 * s, 13 * s), (cx + 9 * s, 13 * s), (cx, 30 * s)
    facets = [
        ([top, mid_l, (cx, 15 * s)], (120, 120, 255)),
        ([top, (cx, 15 * s), mid_r], (200, 245, 255)),
        ([mid_l, (cx, 15 * s), bot], (70, 60, 200)),
        ([(cx, 15 * s), mid_r, bot], (100, 190, 255)),
    ]
    for pts, col in facets:
        poly_fill(px, pts, lambda x, y, col=col: tuple(clamp(c * (1 + dither(x, y) * 0.1)) for c in col) + (255,), size, size)
    # Highlight glints.
    for (x, y) in ((cx - 2 * s, 7 * s), (cx - 3 * s, 9 * s), (cx + 3 * s, 18 * s)):
        if 0 <= int(x) < size and 0 <= int(y) < size:
            px[int(x), int(y)] = (255, 255, 255, 255)
    if mount:
        # A gold setting round its waist.
        for x in range(int(cx - 10 * s), int(cx + 11 * s)):
            for y in range(int(14 * s), int(17 * s)):
                if 0 <= x < size and px[x, y][3] > 0 or abs(x - cx) < 11 * s:
                    px[x, y] = (255, 210, 80, 255) if y == int(14 * s) else (200, 140, 40, 255)
    return outline(img)


def star_icon(points=8, color=(255, 230, 120)):
    img = new(32, 32)
    px = img.load()
    pts = []
    for k in range(points * 2):
        a = k * math.pi / points - math.pi / 2
        r = 14 if k % 2 == 0 else 6
        pts.append((16 + math.cos(a) * r, 16 + math.sin(a) * r))
    poly_fill(px, pts, lambda x, y: mix(color + (255,), (255, 255, 255, 255), max(0, 1 - math.hypot(x - 15, y - 14) / 7)), 32, 32)
    return outline(img)


def elixir(liquid_only=False):
    img = new(32, 32)
    px = img.load()
    for y in range(32):
        for x in range(32):
            # Round flask body (centre 16, 20, radius 10), a neck, a cork.
            d = math.hypot(x - 15.5, y - 20)
            body = d < 10
            neck = 12 <= x <= 19 and 6 <= y <= 11
            cork = 12 <= x <= 19 and 2 <= y <= 5
            if liquid_only:
                if body and y >= 15:
                    px[x, y] = (255, 255, 255, 255) if not (x < 13 and y < 22) else (255, 255, 255, 200)
                continue
            if cork:
                px[x, y] = (150, 95, 50, 255) if (x + y) % 3 else (120, 75, 40, 255)
            elif neck or (body and y < 15):
                px[x, y] = (200, 230, 255, 120)
            elif body:
                px[x, y] = (0, 0, 0, 0)   # the liquid layer shows through
            if body and d > 8.8:
                px[x, y] = (220, 240, 255, 230)
    if not liquid_only:
        for (x, y) in ((10, 17), (11, 16), (10, 18)):
            px[x, y] = (255, 255, 255, 255)
        img = outline(img)
    return img


def title_scroll():
    img = new(32, 32)
    px = img.load()
    for y in range(9, 23):
        for x in range(3, 29):
            wave = int(math.sin(x / 4.0) * 1.5)
            yy = y + wave
            if 9 <= yy < 23:
                c = (255, 214, 90) if y < 12 else (230, 170, 50) if y < 20 else (190, 120, 30)
                px[x, yy] = c + (255,)
    # Ribbon tails.
    for (x0, d) in ((1, 1), (30, -1)):
        for k in range(6):
            for y in range(14 + k // 2, 24 + k // 2):
                x = x0 + d * (k // 3)
                if 0 <= x < 32 and 0 <= y < 32:
                    px[x, y] = (180, 60, 200, 255) if y < 23 else (0, 0, 0, 0)
    for x in range(8, 24, 3):   # little lettering marks
        px[x, 15] = (120, 70, 20, 255)
        px[x + 1, 15] = (120, 70, 20, 255)
    return outline(img)


def aura_ring():
    img = new(32, 32)
    px = img.load()
    for y in range(32):
        for x in range(32):
            d = math.hypot((x - 15.5) / 1.0, (y - 20) / 0.45)
            if 9 < d < 13.5:
                px[x, y] = (255, 255, 255, 255 if d < 12 else 170)
    for (x, y) in ((6, 9), (25, 7), (16, 4), (10, 13), (22, 12)):
        px[x, y] = (255, 255, 255, 255)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            px[x + dx, y + dy] = (255, 255, 255, 140)
    return img


def skin_glyph():
    img = new(32, 32)
    px = img.load()
    # A spiral stroke with a star at its heart.
    for k in range(400):
        t = k / 400 * 3.2 * math.pi
        r = 2 + t * 1.15
        x, y = 16 + math.cos(t) * r, 16 + math.sin(t) * r
        for dx in (0, 1):
            for dy in (0, 1):
                xi, yi = int(x) + dx, int(y) + dy
                if 0 <= xi < 32 and 0 <= yi < 32:
                    px[xi, yi] = (255, 255, 255, 255)
    return img


def sparkle():
    img = new(16, 16)
    px = img.load()
    for y in range(16):
        for x in range(16):
            dx, dy = abs(x - 7.5), abs(y - 7.5)
            v = max(0, 1 - (dx * dy) / 2.5 - (dx + dy) / 11)
            if v > 0.05:
                px[x, y] = (255, 255, 255, clamp(v * 255))
    return img


def sheen():
    img = new(32, 64)
    px = img.load()
    for y in range(64):
        for x in range(32):
            v = max(0, 1 - abs(x - 16) / 16) ** 2
            if v > 0.02 and dither(x, y) + 0.5 < v * 1.2:
                px[x, y] = (255, 255, 255, clamp(90 * v))
    return img


def glow():
    img = new(64, 64)
    px = img.load()
    for y in range(64):
        for x in range(64):
            v = max(0, 1 - math.hypot(x - 31.5, y - 31.5) / 32) ** 1.6
            if v > 0.01:
                px[x, y] = (255, 255, 255, clamp(v * 230))
    return img


def aura_flame():
    """A soft tongue of flame, widest low down, tapering to a point at the top (white; tinted per school)."""
    img = new(32, 64)
    px = img.load()
    for y in range(64):
        for x in range(32):
            t = y / 63.0                      # 0 top .. 1 bottom
            half = 14 * (t ** 0.55) * (1 - max(0, t - 0.82) * 3)
            d = abs(x - 15.5)
            if half <= 0 or d > half:
                continue
            v = (1 - d / half) ** 0.8 * min(1, t * 2.2) * (1 - max(0, t - 0.85) * 4)
            if v > 0.03:
                core = max(0, 1 - d / (half * 0.45))
                px[x, y] = (255, 255, 255, clamp(v * 255 * (0.7 + 0.3 * core)))
    return img


def aura_spark():
    img = new(8, 32)
    px = img.load()
    for y in range(32):
        for x in range(8):
            v = max(0, 1 - abs(x - 3.5) / 3.5) * max(0, 1 - abs(y - 15.5) / 16)
            if v > 0.05:
                px[x, y] = (255, 255, 255, clamp(v * 255))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    shop_back()
    panels()
    save(shard_icon(), 'Shard')
    save(shard_icon(40, True), 'BookGem')
    save(star_icon(), 'IconFeatured')
    ic = elixir()
    liq = elixir(True)
    combo = new(32, 32)
    for y in range(32):
        for x in range(32):
            if liq.getpixel((x, y))[3]:
                combo.putpixel((x, y), (120, 220, 140, 255))
    combo.alpha_composite(ic)
    save(combo, 'IconElixir')
    save(ic, 'ElixirGlass')
    save(liq, 'ElixirLiquid')
    save(title_scroll(), 'TitleScroll')
    save(title_scroll(), 'IconTitle')
    ar = aura_ring()
    tinted = new(32, 32)
    for y in range(32):
        for x in range(32):
            p = ar.getpixel((x, y))
            if p[3]:
                tinted.putpixel((x, y), (140, 200, 255, p[3]))
    save(ar, 'AuraRing')
    save(outline(tinted), 'IconAura')
    sg = skin_glyph()
    tg = new(32, 32)
    for y in range(32):
        for x in range(32):
            p = sg.getpixel((x, y))
            if p[3]:
                tg.putpixel((x, y), (220, 150, 255, 255))
    save(sg, 'SkinGlyph')
    save(outline(tg), 'IconSkin')
    save(sparkle(), 'Sparkle')
    save(sheen(), 'Sheen')
    save(glow(), 'Glow')
    save(aura_flame(), 'AuraFlame')
    save(aura_spark(), 'AuraSpark')


if __name__ == '__main__':
    main()
