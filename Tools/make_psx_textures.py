"""PSX-style tiling textures for the terrain and water (all seamless, point-filtered in Unity).

Terrain textures are mostly grey so the zone colours (road, grass, building...) still tint them. The water textures
carry two independent patterns in the red and green channels; PSXWater scrolls them against each other.

    python3 Tools/make_psx_textures.py Assets/WizardsVRC/Textures [preview.png]
"""
import math
import random
import sys
from PIL import Image

OUT = sys.argv[1]
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def gray(v):
    v = max(0, min(255, int(round(v * 255))))
    return (v, v, v, 255)


def quant(v, levels, x, y, dither=0.9):
    """Ordered-dither a 0..1 value to a few levels (the PSX look)."""
    t = (BAYER[y % 4][x % 4] + 0.5) / 16.0 - 0.5
    return max(0.0, min(1.0, math.floor(v * (levels - 1) + 0.5 + t * dither) / (levels - 1)))


def cobble(size=32, seed=3):
    rnd = random.Random(seed)
    pts = []
    n = 4
    for j in range(n):
        for i in range(n):
            pts.append(((i + 0.5 + rnd.uniform(-0.28, 0.28)) * size / n, (j + 0.5 + rnd.uniform(-0.28, 0.28)) * size / n, rnd.uniform(0.8, 1.0)))
    img = Image.new('RGBA', (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            ds = []
            for (cx, cy, shade) in pts:
                dx = min(abs(x + 0.5 - cx), size - abs(x + 0.5 - cx))
                dy = min(abs(y + 0.5 - cy), size - abs(y + 0.5 - cy))
                # signed offsets (wrapped) for the highlight direction
                sx = (x + 0.5 - cx + size / 2) % size - size / 2
                sy = (y + 0.5 - cy + size / 2) % size - size / 2
                ds.append((math.hypot(dx, dy), shade, sx, sy))
            ds.sort()
            d1, shade, sx, sy = ds[0]
            gap = ds[1][0] - d1
            if gap < 1.3:
                v = 0.5
            else:
                v = shade - 0.012 * d1 + (0.1 if (sx + sy) < -2.5 else 0.0) - (0.08 if (sx + sy) > 3.5 else 0.0)
            px[x, y] = gray(quant(v, 10, x, y, 0.35))
    return img


def grass(size=32, seed=5):
    rnd = random.Random(seed)
    val = [[0.8 + rnd.uniform(-0.03, 0.03) for _ in range(size)] for _ in range(size)]
    # Soft darker patches, then tufts: a 2-3 pixel blade with a lighter tip.
    for _ in range(6):
        cx, cy, r = rnd.randrange(size), rnd.randrange(size), rnd.uniform(3, 6)
        for y in range(size):
            for x in range(size):
                dx = min(abs(x - cx), size - abs(x - cx)); dy = min(abs(y - cy), size - abs(y - cy))
                if dx * dx + dy * dy < r * r: val[y][x] -= 0.06
    for _ in range(46):
        x, y = rnd.randrange(size), rnd.randrange(size)
        n = rnd.randint(2, 3)
        for k in range(n):
            val[(y - k) % size][x] = 0.7 if k == 0 else (1.0 if k == n - 1 else 0.9)
        if rnd.random() < 0.5: val[(y - n + 1) % size][(x + 1) % size] = 0.95
    img = Image.new('RGBA', (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            px[x, y] = gray(quant(val[y][x], 10, x, y, 0.3))
    return img


def stone_wall(size=32, seed=7):
    rnd = random.Random(seed)
    img = Image.new('RGBA', (size, size))
    px = img.load()
    rows = 4
    rh = size // rows
    shades = {}
    for y in range(size):
        r = y // rh
        off = (r % 2) * (size // 4)
        for x in range(size):
            xx = (x + off) % size
            b = xx // (size // 2)
            key = (r, b)
            if key not in shades: shades[key] = rnd.uniform(0.78, 0.98)
            mortar = (y % rh == rh - 1) or (xx % (size // 2) == size // 2 - 1)
            if mortar:
                v = 0.5
            else:
                v = shades[key] + (0.08 if y % rh == 0 else 0) + rnd.uniform(-0.04, 0.04)
            px[x, y] = gray(quant(v, 10, x, y, 0.35))
    return img


def water(size=64):
    img = Image.new('RGBA', (size, size))
    px = img.load()
    tau = 2 * math.pi / size
    for y in range(size):
        for x in range(size):
            # Two tileable caustic patterns (integer frequencies keep them seamless).
            a = math.sin(x * tau * 2 + math.sin(y * tau * 3) * 1.6) + math.sin(y * tau * 2 + math.sin(x * tau * 2) * 1.4)
            b = math.sin((x + y) * tau * 3 + math.sin((x - y) * tau * 1) * 2.0) + math.sin((x - y) * tau * 2 + 1.3)
            ra = 1.0 - min(1.0, abs(a) * 0.9)
            rb = 1.0 - min(1.0, abs(b) * 0.9)
            ra = quant(ra ** 2.2, 5, x, y)
            rb = quant(rb ** 2.2, 5, x + 2, y + 1)
            px[x, y] = (int(ra * 255), int(rb * 255), 128, 255)
    return img


def waterfall(w=32, h=64, seed=11):
    rnd = random.Random(seed)
    img = Image.new('RGBA', (w, h))
    px = img.load()
    cols = [[0.35 + rnd.uniform(0, 0.15)] * h for _ in range(w)]
    for x in range(w):
        col = [0.32 + rnd.uniform(0, 0.12)] * h
        for _ in range(rnd.randint(2, 4)):
            start, length = rnd.randrange(h), rnd.randint(6, 26)
            bright = rnd.uniform(0.75, 1.0)
            for k in range(length):
                fade = 1.0 - k / float(length)
                col[(start + k) % h] = max(col[(start + k) % h], bright * (0.55 + 0.45 * fade))
        cols[x] = col
    for y in range(h):
        for x in range(w):
            v = cols[x][y]
            # Neighbouring columns blend a little so streaks are 1-2 pixels wide.
            v = 0.7 * v + 0.15 * cols[(x - 1) % w][y] + 0.15 * cols[(x + 1) % w][y]
            px[x, y] = (int(quant(v, 5, x, y) * 255), int(quant(v, 5, x, y) * 255), 255, 255)
    return img


out = {
    'Terrain/Cobble.png': cobble(),
    'Terrain/Grass.png': grass(),
    'Terrain/StoneWall.png': stone_wall(),
    'Water/WaterRipple.png': water(),
    'Water/WaterfallStreaks.png': waterfall(),
}
for name, img in out.items():
    img.save('%s/%s' % (OUT, name))
if len(sys.argv) > 2:
    sheet = Image.new('RGBA', (5 * 136, 136), (40, 40, 48, 255))
    for i, (name, img) in enumerate(out.items()):
        t = Image.new('RGBA', (128, 128))
        for yy in range(0, 128, img.height):
            for xx in range(0, 128, img.width):
                t.paste(img, (xx, yy))
        sheet.paste(t, (i * 136 + 4, 4))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(sys.argv[2])
print('ok')
