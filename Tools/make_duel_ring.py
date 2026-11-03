"""The duel ring's floor art (Assets/WizardsVRC/Textures/DuelRing*.png), hand-shaded pixel art in the game's PS1 style.

    DuelRingBase     the seat band (fixed): carved slate flagstones between two gold inlays, eight sunken seat sockets
                     with gold lips, a glowing channel cut round the band linking every socket, carved stars between them
    DuelRingSigil    the turning star chart in the middle: a deep night disc of stars, a seven-point star joining the seven
                     school emblems in gold bezels (each turned bottom outward, so it reads upright from the edge), a ring
                     of ticks, gold edges (the turn clock covers the very middle)
    DuelRingRunes    the turning outer band: dark stone between gold borders, glowing runes, diamond stops
    DuelRingFlash    the start flash: the whole circle's silhouette in white light
    DuelRingSeat     the ring round each seat (white, tinted per school / side at runtime)
    DuelRingDivider  the gold inlay across the middle that splits the two sides (laid as two strips)
    DuelRingClock    the turn clock's dial in the middle: a night face, gold and pale ticks, a gold star boss; its outer
                     track (0.78-0.93 of the radius) stays dark because the DuelClock shader paints the time there
    DuelRingClockHand  the clock's gold hand (points up; BattleClient turns it)

The circle is 512 px across for the whole duel circle (DuelCircleRadius = 256 px). The seat sockets sit exactly where
the old art had them, 200 px out at 22.5 + 45k degrees (clockwise from the top), because the seats stand there
(DuelSeatRadius = DuelCircleRadius * 200 / 256). Light falls from the top left.

    python3 Tools/make_duel_ring.py       (needs numpy and Pillow; run from the repo root)
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Textures')
ICONS = os.path.join(OUT, 'FxIcons')
N = 512
C = N / 2.0
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.47

yy, xx = np.mgrid[0:N, 0:N]
DX = xx + 0.5 - C
DY = C - (yy + 0.5)                       # up = +
R = np.sqrt(DX * DX + DY * DY)
A = (np.degrees(np.arctan2(DX, DY)) + 360.0) % 360.0   # clockwise from the top, like the seat angles
DITH = BAYER[yy % 4, xx % 4]

# Palette
INK = np.array([16, 12, 30])
SLATE = np.array([86, 92, 122])
SLATE_HI = np.array([132, 140, 172])
SLATE_LO = np.array([52, 54, 80])
MORTAR = np.array([30, 28, 46])
GOLD = np.array([232, 184, 82])
GOLD_HI = np.array([255, 236, 150])
GOLD_LO = np.array([150, 98, 34])
GLOW = np.array([150, 225, 255])
GLOW_SOFT = np.array([70, 120, 190])
NIGHT_IN = np.array([30, 34, 86])
NIGHT_OUT = np.array([12, 14, 44])
STAR_LINE = np.array([196, 222, 255])
STAR_HALO = np.array([78, 104, 186])

SCHOOLS = ['Fire', 'Ice', 'Storm', 'Life', 'Death', 'Myth', 'Balance']

# Geometry (px from the centre)
BAND_IN, BAND_OUT = 167.0, 237.0
SOCKET_R, SEAT_LINE = 200.0, 200.0
SOCKET_ANGLES = [22.5 + 45 * k for k in range(8)]
RUNE_IN, RUNE_OUT = 234.5, 255.5
SIGIL_R = 170.0


def rgba():
    return np.zeros((N, N, 4), np.float32)


def put(img, mask, col, alpha=255):
    img[mask, 0:3] = col
    img[mask, 3] = alpha


def shade(base, amount):
    """base colour lifted (amount > 0) toward white-ish or dropped toward ink, per pixel."""
    a = amount[..., None]
    up = base + (np.array([255, 250, 235]) - base) * np.clip(a, 0, 1)
    down = base + (INK - base) * np.clip(-a, 0, 1)
    return np.where(a >= 0, up, down)


def quant(col, levels=24):
    """Posterise with ordered dither so gradients break into PS1-style steps."""
    step = 255.0 / levels
    return np.clip(np.floor(col / step + 0.5 + DITH[..., None]) * step, 0, 255)


def hash2(a, b):
    v = np.sin(a * 12.9898 + b * 78.233) * 43758.5453
    return v - np.floor(v)


def ang_diff(a, b):
    return np.abs((a - b + 180.0) % 360.0 - 180.0)


def socket_xy(angle_deg, radius=SOCKET_R):
    t = math.radians(angle_deg)
    return radius * math.sin(t), radius * math.cos(t)


def bevel(dist_edge, inward_normal_dx, inward_normal_dy, width):
    """A raised edge: light where the surface faces the top left, shadow where it faces the bottom right."""
    lx, ly = -0.7071, 0.7071
    facing = -(inward_normal_dx * lx + inward_normal_dy * ly)
    k = np.clip(1.0 - dist_edge / width, 0, 1)
    return facing * k


# ================================================================== the seat band


def base_band():
    img = rgba()
    band = (R >= BAND_IN) & (R < BAND_OUT)

    # Flagstones: 32 round the band, each a little different, with joints and bevelled edges.
    seg = 360.0 / 32
    sa = (A + seg / 2) % 360.0                          # joints offset half a stone, so none crosses a socket or star
    stone = np.floor(sa / seg)
    within = (sa - stone * seg) / seg                   # 0..1 across a stone
    tint = (hash2(stone, 3.0) - 0.5) * 0.16
    speck = (hash2(xx * 1.0, yy * 1.0) - 0.5) * 0.22
    mottle = (np.sin(xx * 0.21 + stone) * np.cos(yy * 0.17 - stone * 2) ) * 0.05
    arc = within * seg * math.pi / 180.0 * R           # px from the stone's anticlockwise joint
    arc_to_next = (1 - within) * seg * math.pi / 180.0 * R
    rad_in, rad_out = R - BAND_IN, BAND_OUT - R
    light = tint + speck + mottle
    # Bevels on the four sides of each stone (joints run radially; light from the top left).
    nx_a, ny_a = np.cos(np.radians(A)), -np.sin(np.radians(A))      # pointing clockwise round the circle
    light += 0.42 * bevel(arc, nx_a, ny_a, 3.0)
    light += 0.42 * bevel(arc_to_next, -nx_a, -ny_a, 3.0)
    rx, ry = DX / np.maximum(R, 1), DY / np.maximum(R, 1)
    light += 0.42 * bevel(rad_in - 4, rx, ry, 3.0)
    light += 0.42 * bevel(rad_out - 5, -rx, -ry, 3.0)
    col = shade(SLATE.astype(np.float32), light)
    joint = (arc < 1.0) | (arc_to_next < 0.6)
    col[joint] = MORTAR
    # Hairline cracks on a few stones.
    crack = (hash2(stone, 9.0) > 0.7) & (np.abs(np.sin(within * 9 + R * 0.08 + stone)) < 0.06) & (rad_in > 8) & (rad_out > 9)
    col[crack] = SLATE_LO
    put(img, band, quant(col)[band])

    # Gold inlays at both edges of the band.
    for r0, r1 in ((BAND_IN, BAND_IN + 4.0), (BAND_OUT - 5.0, BAND_OUT)):
        m = (R >= r0) & (R < r1)
        t = (R - r0) / (r1 - r0)
        g = GOLD + (GOLD_HI - GOLD) * np.clip(1 - np.abs(t - 0.3) * 3, 0, 1)[..., None]
        g = shade(g.astype(np.float32), 0.35 * bevel(np.minimum(R - r0, r1 - R), rx, ry, 1.5) - 0.0)
        g = np.where((t > 0.8)[..., None], GOLD_LO, g)
        put(img, m, quant(g)[m])
        put(img, m & ((np.abs(R - r0) < 0.7) | (np.abs(R - r1) < 0.7)), INK)

    # The channel: a groove cut round the band at the seats' radius, a line of light running in it.
    groove = band & (np.abs(R - SEAT_LINE) < 2.6)
    put(img, groove, MORTAR)
    put(img, band & (np.abs(R - SEAT_LINE) < 1.6), GLOW_SOFT)
    put(img, band & (np.abs(R - SEAT_LINE) < 0.7), GLOW)
    put(img, band & (np.abs(R - SEAT_LINE - 2.6) < 0.6), SLATE_HI)        # lit lower lip (the light is above)

    # Between the seats: carved four-point stars, and a gold eight-point star at the top and bottom of the band.
    for k in range(8):
        a = 45.0 * k
        cx, cy = socket_xy(a, SEAT_LINE)
        ddx, ddy = DX - cx, DY - cy
        big = k in (0, 4)
        size = 15.0 if big else 10.0
        # Star by an L-1/2 norm: |x|^0.5 + |y|^0.5 < s^0.5
        t = np.radians(a)
        ux = ddx * math.cos(t) - ddy * math.sin(t)
        uy = ddx * math.sin(t) + ddy * math.cos(t)
        star4 = np.sqrt(np.abs(ux)) + np.sqrt(np.abs(uy)) < math.sqrt(size)
        if big:
            vx, vy = (ux - uy) * 0.7071, (ux + uy) * 0.7071
            star8 = star4 | (np.sqrt(np.abs(vx)) + np.sqrt(np.abs(vy)) < math.sqrt(size * 0.62))
            ring_m = (np.hypot(ddx, ddy) > size + 2) & (np.hypot(ddx, ddy) < size + 4)
            put(img, ring_m, GOLD_LO)
            put(img, ring_m & (np.hypot(ddx, ddy) < size + 3), GOLD)
            shine = shade(GOLD.astype(np.float32), 0.6 * np.clip((-ux * 0.7 + uy * 0.7) / size, -1, 1))
            put(img, star8, quant(shine)[star8])
            edge = star8 & ~(np.sqrt(np.abs(ux * 1.12)) + np.sqrt(np.abs(uy * 1.12)) < math.sqrt(size))
            put(img, edge & ~(np.sqrt(np.abs(vx * 1.12)) + np.sqrt(np.abs(vy * 1.12)) < math.sqrt(size * 0.62)), GOLD_LO)
            put(img, np.hypot(ddx, ddy) < 2.2, GLOW)
        else:
            inner = np.sqrt(np.abs(ux)) + np.sqrt(np.abs(uy)) < math.sqrt(size) - 0.55
            put(img, star4 & ~inner, MORTAR)
            put(img, inner, SLATE_HI)
            put(img, inner & (ux + uy < 0), SLATE_LO)

    # The eight seat sockets: a stone lip, a gold rim and a sunken basin with a faint carved star and a gem.
    for a in SOCKET_ANGLES:
        cx, cy = socket_xy(a)
        ddx, ddy = DX - cx, DY - cy
        d = np.hypot(ddx, ddy)
        nx, ny = ddx / np.maximum(d, 1), ddy / np.maximum(d, 1)
        lip = (d < 34) & (d >= 31)
        lipc = shade(SLATE.astype(np.float32), 0.5 * bevel(34 - d, -nx, -ny, 3) + 0.1)
        put(img, lip, quant(lipc)[lip])
        rim = (d < 31) & (d >= 27)
        rimc = shade(GOLD.astype(np.float32), 0.55 * (-(nx * -0.7071 + ny * 0.7071)) * np.clip(1 - np.abs(d - 29) / 2, 0, 1))
        put(img, rim, quant(rimc)[rim])
        put(img, (d < 34.6) & (d >= 34), INK)
        put(img, (d < 27) & (d >= 26.2), INK)
        basin = d < 26.2
        # Inner shadow on the lit side (it's sunken), light pooling at the far side.
        bas = shade(np.array([40, 42, 70], np.float32), -0.45 * bevel(26.2 - d, nx, ny, 6) + (hash2(xx * 1.0, yy * 1.0) - 0.5) * 0.12)
        put(img, basin, quant(bas)[basin])
        # Carved eight-point star.
        ang = (np.degrees(np.arctan2(ddx, ddy)) + 360) % 360
        spoke = (np.minimum(ang % 45, 45 - ang % 45) < 22.5 * (1 - d / 22)) & (d < 22)
        edge = spoke & ~((np.minimum(ang % 45, 45 - ang % 45) < 22.5 * (1 - d / 22) - 3.0 / np.maximum(d, 1) * 60 / 22.5))
        put(img, basin & edge, np.array([66, 72, 118]))
        put(img, d < 3.4, GOLD_LO)
        put(img, d < 2.6, GLOW)
        put(img, (d < 1.2) & (ddx < 0.5) & (ddy > -0.5), np.array([235, 250, 255]))
    return img


# ================================================================== the turning star chart


def school_icon(name, size=32, turn=0.0):
    """The school's 64 px emblem turned 'turn' degrees clockwise (at full size, so the pixels stay crisp), then brought down
    to 'size' keeping its pixel colours (each 2x2 block takes its commonest colour, darker on a tie so the outlines
    survive)."""
    im = Image.open(os.path.join(ICONS, 'School' + name + '.png')).convert('RGBA')
    if turn % 360:
        # Blown up 4x first, so each source pixel turns as a block and the downscale below picks clean colours.
        im = im.resize((im.width * 4, im.height * 4), Image.NEAREST).rotate(-turn, resample=Image.NEAREST, expand=False)
    src = np.array(im)
    f = src.shape[0] // size
    out = np.zeros((size, size, 4), np.uint8)
    for y in range(size):
        for x in range(size):
            block = src[y * f:(y + 1) * f, x * f:(x + 1) * f].reshape(-1, 4)
            cols = {}
            for p in block:
                key = tuple(int(v) for v in p)
                cols[key] = cols.get(key, 0) + 1
            best = sorted(cols.items(), key=lambda kv: (-kv[1], kv[0][3] < 128, sum(kv[0][:3])))[0][0]
            out[y, x] = best
    return out


def line_dist(ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = np.clip(((DX - ax) * vx + (DY - ay) * vy) / (vx * vx + vy * vy), 0, 1)
    return np.hypot(DX - (ax + t * vx), DY - (ay + t * vy))


def sigil():
    img = rgba()
    disc = R < SIGIL_R
    rng = np.random.RandomState(7)

    # Night sky: lighter toward the middle, faint nebula wisps.
    t = np.clip(R / SIGIL_R, 0, 1)[..., None]
    sky = NIGHT_IN + (NIGHT_OUT - NIGHT_IN) * t
    wisp = (np.sin(DX * 0.045 + np.sin(DY * 0.03) * 2.1) * np.cos(DY * 0.05 - DX * 0.02)) * 0.5 + 0.5
    sky = sky + np.array([26, 10, 40]) * (wisp ** 3)[..., None] + np.array([0, 22, 30]) * ((1 - wisp) ** 4)[..., None]
    put(img, disc, quant(sky, 20)[disc])

    # Stars.
    for _ in range(260):
        r = SIGIL_R * math.sqrt(rng.uniform(0.12, 0.92))
        a = rng.uniform(0, 2 * math.pi)
        x, y = int(C + r * math.sin(a)), int(C - r * math.cos(a))
        b = rng.uniform()
        c = (235, 240, 255) if b > 0.6 else (170, 190, 240) if b > 0.25 else (120, 130, 200)
        img[y, x, :3] = c
        if b > 0.93:
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                img[y + oy, x + ox, :3] = (150, 170, 235)

    # Edge: gold double ring with a bevel, then a thin pale ring and a ring of ticks.
    rx, ry = DX / np.maximum(R, 1), DY / np.maximum(R, 1)
    outer = (R >= SIGIL_R - 5) & (R < SIGIL_R)
    gc = shade(GOLD.astype(np.float32), 0.55 * (-(rx * -0.7071 + ry * 0.7071)) * np.clip(1 - np.abs(R - (SIGIL_R - 2.5)) / 2.5, 0, 1))
    put(img, outer, quant(gc)[outer])
    put(img, (R >= SIGIL_R - 0.8) & (R < SIGIL_R), GOLD_LO)
    put(img, (R >= SIGIL_R - 6) & (R < SIGIL_R - 5), INK)
    put(img, (R >= SIGIL_R - 9) & (R < SIGIL_R - 8), GOLD_LO)
    ticks_band = (R >= SIGIL_R - 20) & (R < SIGIL_R - 10)
    minor = ticks_band & (np.minimum(A % 6, 6 - A % 6) * np.radians(1) * R < 0.75) & (R >= SIGIL_R - 15)
    major = ticks_band & (np.minimum(A % 30, 30 - A % 30) * np.radians(1) * R < 1.3)
    put(img, minor, np.array([118, 146, 214]))
    put(img, major, GOLD)
    put(img, (R >= SIGIL_R - 21) & (R < SIGIL_R - 20), np.array([104, 128, 200]))

    # The seven-point star {7/3} through the emblems, glowing, and the circle they sit on.
    RE = 116.0
    pts = [socket_xy(360.0 / 7 * k, RE) for k in range(7)]
    d = np.full((N, N), 1e9)
    for k in range(7):
        ax, ay = pts[k]
        bx, by = pts[(k + 3) % 7]
        d = np.minimum(d, line_dist(ax, ay, bx, by))
    circ = np.abs(R - RE)
    halo = disc & (d < 3.2) & (d >= 1.4) & (DITH < 0.2)
    put(img, halo, STAR_HALO)
    put(img, disc & (d < 1.4), STAR_LINE)
    put(img, disc & (circ < 0.9), np.array([126, 150, 222]))
    # A second, inner seven-point star {7/2} turned half a step, in gold.
    pts2 = [socket_xy(360.0 / 7 * (k + 0.5), 78.0) for k in range(7)]
    d2 = np.full((N, N), 1e9)
    for k in range(7):
        ax, ay = pts2[k]
        bx, by = pts2[(k + 2) % 7]
        d2 = np.minimum(d2, line_dist(ax, ay, bx, by))
    put(img, disc & (d2 < 1.6) & (R > 54), GOLD)
    put(img, disc & (d2 < 0.7) & (R > 54), GOLD_HI)
    # Inner gold ring and a ring of little constellation dots (the clock covers everything inside 54 px).
    put(img, (R >= 80) & (R < 83), GOLD)
    put(img, (R >= 82.2) & (R < 83), GOLD_LO)
    for k in range(21):
        x, y = socket_xy(360.0 / 21 * k + 4, 67.0)
        m = np.hypot(DX - x, DY - y) < (1.8 if k % 3 == 0 else 1.1)
        put(img, m, GOLD_HI if k % 3 == 0 else np.array([170, 196, 250]))
    put(img, (R >= 52) & (R < 54.5), np.array([104, 128, 200]))

    # The seven school emblems in gold bezels at the star's points.
    for k, name in enumerate(SCHOOLS):
        x, y = pts[k]
        cx, cy = C + x, C - y
        dd = np.hypot(DX - x, DY - y)
        nx, ny = (DX - x) / np.maximum(dd, 1), (DY - y) / np.maximum(dd, 1)
        bez = (dd < 20.5) & (dd >= 16)
        bc = shade(GOLD.astype(np.float32), 0.6 * (-(nx * -0.7071 + ny * 0.7071)) * np.clip(1 - np.abs(dd - 18.2) / 2.2, 0, 1))
        put(img, bez, quant(bc)[bez])
        put(img, (dd < 21.3) & (dd >= 20.5), INK)
        put(img, (dd < 16) & (dd >= 15.2), INK)
        put(img, dd < 15.2, np.array([20, 18, 40]))
        # Bottom outward: each emblem reads upright to the wizards standing nearest it, at the circle's edge.
        icon = school_icon(name, turn=360.0 / 7 * k + 180.0)
        ox, oy = int(round(cx - 16)), int(round(cy - 16))
        region = img[oy:oy + 32, ox:ox + 32]
        a = icon[..., 3:4] / 255.0
        region[..., :3] = region[..., :3] * (1 - a) + icon[..., :3] * a
        region[..., 3] = np.maximum(region[..., 3], icon[..., 3])
        # Four little gold studs round each bezel.
        for s in range(4):
            sx, sy = x + 23 * math.sin(math.radians(45 + 90 * s)), y + 23 * math.cos(math.radians(45 + 90 * s))
            put(img, np.hypot(DX - sx, DY - sy) < 1.6, GOLD_HI)
    return img


# ================================================================== the turning rune band

RUNES = [
    ["..#..", "..#..", "#.#.#", ".###.", "..#..", "..#..", "..#.."],
    ["#...#", ".#.#.", "..#..", "..#..", "..#..", ".#.#.", "#...#"],
    ["###..", "#..#.", "#..#.", "###..", "#.#..", "#..#.", "#...#"],
    ["..#..", ".#.#.", "#...#", ".#.#.", "..#..", "..#..", "..#.."],
    ["#....", "##...", "#.#..", "#..#.", "#.#..", "##...", "#...."],
    ["..#..", "..#..", "#####", "..#..", ".#.#.", "#...#", "....."],
    [".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."],
    ["#...#", "#...#", "#.#.#", "#.#.#", ".#.#.", "..#..", "..#.."],
    ["..#..", ".###.", "#.#.#", "..#..", "#.#.#", ".###.", "..#.."],
    ["#.#.#", "#.#.#", ".###.", "..#..", "..#..", "..#..", ".###."],
    ["....#", "...#.", "..#..", ".###.", "..#..", ".#...", "#...."],
    ["#####", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#####"],
]


def runes():
    img = rgba()
    band = (R >= RUNE_IN) & (R < RUNE_OUT)
    rx, ry = DX / np.maximum(R, 1), DY / np.maximum(R, 1)
    base = shade(np.array([46, 38, 74], np.float32), (hash2(xx * 1.0, yy * 1.0) - 0.5) * 0.18 + 0.25 * bevel(R - RUNE_IN, rx, ry, 2.5))
    put(img, band, quant(base, 20)[band])
    for r0, r1 in ((RUNE_IN, RUNE_IN + 2.4), (RUNE_OUT - 2.6, RUNE_OUT)):
        m = (R >= r0) & (R < r1)
        put(img, m, GOLD)
        put(img, m & (R >= r1 - 0.8), GOLD_LO)
        put(img, m & (R < r0 + 0.8), GOLD_HI)

    # Runes, each standing on the band with its top outward, 1.5 px per rune pixel.
    rm = (RUNE_IN + RUNE_OUT) / 2
    slot = 12.0
    circum = 2 * math.pi * rm
    slots = int(circum // slot)
    slot = circum / slots
    s = np.radians(A) * rm
    idx = np.floor(s / slot).astype(int)
    lx = (s - idx * slot - (slot - 5 * 1.5) / 2) / 1.5          # rune column (0..5)
    ly = (RUNE_OUT - 4.2 - R) / 1.5                              # rune row from the top (outer edge)
    on = np.zeros((N, N), bool)
    stop = (idx % 9) == 8
    gx, gy = np.floor(lx).astype(int), np.floor(ly).astype(int)
    inside = band & (gx >= 0) & (gx < 5) & (gy >= 0) & (gy < 7) & ~stop
    for k, glyph in enumerate(RUNES):
        g = np.array([[c == '#' for c in row] for row in glyph])
        sel = inside & ((idx * 7 + 3) % len(RUNES) == k)
        on[sel] = g[gy[sel], gx[sel]]
    # Diamond stops every ninth slot.
    cx = (s - idx * slot) - slot / 2
    cy = R - rm
    dia = band & stop & (np.abs(cx) + np.abs(cy) < 5.0)
    put(img, dia, GOLD)
    put(img, dia & (np.abs(cx) + np.abs(cy) < 2.2), GLOW)
    # Glow: a soft dither halo round every rune stroke, then the stroke itself.
    halo = np.zeros_like(on)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            halo |= np.roll(np.roll(on, oy, 0), ox, 1)
    put(img, band & halo & ~on & (DITH < 0.15), np.array([150, 104, 60]))
    put(img, on, np.array([255, 206, 104]))
    put(img, on & (np.roll(on, 1, 0) == False), GOLD_HI)       # lit top edge of each stroke
    return img


# ================================================================== flash, seat ring, divider


def composite(*layers):
    out = np.zeros((N, N, 4), np.float32)
    for l in layers:
        a = l[..., 3:4] / 255.0
        out[..., :3] = out[..., :3] * (1 - a) + l[..., :3] * a
        out[..., 3] = np.maximum(out[..., 3], l[..., 3])
    return out


def flash(comp):
    img = comp.copy()
    lum = (comp[..., 0] * 0.3 + comp[..., 1] * 0.55 + comp[..., 2] * 0.15) / 255.0
    v = 205 + 50 * lum
    img[..., 0] = v
    img[..., 1] = v
    img[..., 2] = np.minimum(255, v + 8)
    return img


def seat_ring(n=128):
    yy2, xx2 = np.mgrid[0:n, 0:n]
    c = n / 2.0
    dx, dy = xx2 + 0.5 - c, c - (yy2 + 0.5)
    r = np.hypot(dx, dy)
    a = (np.degrees(np.arctan2(dx, dy)) + 360) % 360
    img = np.zeros((n, n, 4), np.float32)
    rx, ry = dx / np.maximum(r, 1), dy / np.maximum(r, 1)
    lit = -(rx * -0.7071 + ry * 0.7071)

    def p(mask, v):
        img[mask, :3] = v
        img[mask, 3] = 255

    # Outer band, bevelled, with eight teeth biting inward and studs between them.
    band = (r >= 52) & (r < 61)
    v = 200 + 55 * lit * np.clip(1 - np.abs(r - 56.5) / 4.5, 0, 1)
    p(band, np.stack([v, v, v], -1)[band])
    p((r >= 61) & (r < 62.4), (96, 96, 96))
    p((r >= 51) & (r < 52), (110, 110, 110))
    p(band & (np.abs(r - 56.5) < 0.8), (255, 255, 255))
    tooth = (r >= 44) & (r < 52) & (np.minimum(a % 45, 45 - a % 45) * np.radians(1) * r < (r - 43) * 0.55)
    p(tooth, (236, 236, 236))
    stud_m = np.zeros((n, n), bool)
    for k in range(8):
        sx, sy = 47 * math.sin(math.radians(22.5 + 45 * k)), 47 * math.cos(math.radians(22.5 + 45 * k))
        stud_m |= np.hypot(dx - sx, dy - sy) < 2.2
    p(stud_m, (255, 255, 255))
    # Thin inner ring.
    p((r >= 38) & (r < 40.5), (214, 214, 214))
    p((r >= 37.2) & (r < 38), (120, 120, 120))
    return img


def divider(w=256, h=16):
    img = np.zeros((h, w, 4), np.float32)
    yy2, xx2 = np.mgrid[0:h, 0:w]
    cy = (h - 1) / 2.0
    dy = np.abs(yy2 - cy)

    def p(mask, col):
        img[mask, :3] = col
        img[mask, 3] = 255

    ends = (xx2 > 10) & (xx2 < w - 11)
    p(ends & (dy < 2.6), GOLD_LO)
    p(ends & (dy < 1.6), GOLD)
    p(ends & (dy < 0.6), GOLD_HI)
    p(ends & (np.abs(dy - 4.5) < 0.6), GOLD_LO)
    # Diamond studs with a glowing gem, every 32 px, and a little arrowhead pointing out at each end.
    for sx in range(16, w, 32):
        d = np.abs(xx2 - sx) + np.abs(yy2 - cy)
        p(d < 7.2, INK)
        p(d < 6.2, GOLD)
        p((d < 6.2) & (xx2 - sx + (yy2 - cy) < -1.5), GOLD_HI)
        p((d < 6.2) & (xx2 - sx + (yy2 - cy) > 2.5), GOLD_LO)
        p(d < 2.6, GLOW)
    for ex, sgn in ((4, -1), (w - 5, 1)):
        d = sgn * (xx2 - ex) + np.abs(yy2 - cy) * 1.2
        p((d < 4) & (d > -6) & (sgn * (xx2 - ex) > -6), GOLD)
        p((d < 4) & (d > 0.5) & (sgn * (xx2 - ex) > -6), GOLD_LO)
    return img


def clock_dial(n=256):
    yy2, xx2 = np.mgrid[0:n, 0:n]
    px_ = (xx2 + 0.5) / n * 2 - 1
    py_ = 1 - (yy2 + 0.5) / n * 2
    r = np.hypot(px_, py_)
    a = (np.degrees(np.arctan2(px_, py_)) + 360) % 360
    dith = BAYER[yy2 % 4, xx2 % 4]
    img = np.zeros((n, n, 4), np.float32)
    lit = -(px_ / np.maximum(r, 1e-3) * -0.7071 + py_ / np.maximum(r, 1e-3) * 0.7071)

    def p(mask, col):
        img[mask, :3] = col
        img[mask, 3] = 255

    def q(col, levels=20):
        step = 255.0 / levels
        return np.clip(np.floor(col / step + 0.5 + dith[..., None]) * step, 0, 255)

    face = r < 0.99
    sky = NIGHT_IN + (NIGHT_OUT - NIGHT_IN) * np.clip(r / 0.76, 0, 1)[..., None]
    p(face, q(sky)[face])
    rng = np.random.RandomState(3)
    for _ in range(40):
        rr, aa = rng.uniform(0.42, 0.72), rng.uniform(0, 2 * math.pi)
        x, y = int((rr * math.sin(aa) + 1) / 2 * n), int((1 - rr * math.cos(aa)) / 2 * n)
        img[y, x, :3] = (200, 214, 255) if rng.uniform() > 0.5 else (120, 136, 210)
    # Gold bezel (bevelled), the dark track the shader fills, an inner gold ring.
    bez = (r >= 0.945) & (r < 0.99)
    p(bez, q(shade(GOLD.astype(np.float32), 0.6 * lit * np.clip(1 - np.abs(r - 0.967) / 0.022, 0, 1)))[bez])
    p((r >= 0.982) & (r < 0.99), GOLD_LO)
    p((r >= 0.93) & (r < 0.945), INK)
    p((r >= 0.78) & (r < 0.93), np.array([18, 18, 40]))
    inner = (r >= 0.75) & (r < 0.78)
    p(inner, GOLD)
    p((r >= 0.75) & (r < 0.757), GOLD_LO)
    # Ticks: the four quarters as gold wedges with a lit edge, the rest pale blue.
    for k in range(12):
        ang = k * 30
        d = np.abs(((a - ang) + 180) % 360 - 180)
        big = k % 3 == 0
        r0 = 0.5 if big else 0.6
        w = 6.0 if big else 3.0
        m = (r > r0) & (r < 0.72) & (d < w * (1.25 - (r - r0)))
        if big:
            side = ((a - ang + 180) % 360 - 180) < 0
            p(m, GOLD)
            p(m & side, GOLD_HI)
            p((np.hypot(px_ - math.sin(math.radians(ang)) * 0.43, py_ - math.cos(math.radians(ang)) * 0.43) < 0.04), GLOW)
        else:
            p(m, np.array([150, 176, 236]))
    # Inner ring with a ring of small dots, then the star boss.
    p((r >= 0.355) & (r < 0.38), np.array([126, 150, 222]))
    p((r >= 0.2) & (r < 0.355), q((NIGHT_OUT + 6).astype(np.float32)[None, None, :] + np.zeros((n, n, 3)))[(r >= 0.2) & (r < 0.355)])
    for k in range(16):
        x, y = 0.28 * math.sin(math.radians(k * 22.5)), 0.28 * math.cos(math.radians(k * 22.5))
        p(np.hypot(px_ - x, py_ - y) < (0.018 if k % 2 == 0 else 0.011), GOLD_HI if k % 4 == 0 else np.array([170, 196, 250]))
    star = r < (0.12 + 0.075 * np.abs(np.cos(np.radians(a) * 4)) ** 6)
    sc = shade(GOLD.astype(np.float32), 0.55 * np.clip((-px_ + py_) / 0.2, -1, 1))
    p(star, q(sc)[star])
    edge = star & ~(r < (0.105 + 0.07 * np.abs(np.cos(np.radians(a) * 4)) ** 6))
    p(edge, GOLD_LO)
    p(r < 0.045, GLOW)
    p((r < 0.02) & (px_ < 0.005) & (py_ > -0.005), np.array([240, 252, 255]))
    return img


def clock_hand(n=256):
    yy2, xx2 = np.mgrid[0:n, 0:n]
    px_ = (xx2 + 0.5) / n * 2 - 1
    py_ = 1 - (yy2 + 0.5) / n * 2
    r = np.hypot(px_, py_)
    img = np.zeros((n, n, 4), np.float32)

    def p(mask, col):
        img[mask, :3] = col
        img[mask, 3] = 255

    t = py_
    taper = 1 - np.clip((t - 0.1) / 0.62, 0, 1)
    blade = (t > -0.05) & (t < 0.72) & (np.abs(px_) < 0.07 * taper + 0.012)
    outline = (t > -0.08) & (t < 0.75) & (np.abs(px_) < 0.07 * taper + 0.03)
    tail_o = (np.hypot(px_, py_ + 0.17) < 0.12) & (np.hypot(px_, py_ + 0.24) > 0.085)
    tail = (np.hypot(px_, py_ + 0.17) < 0.1) & (np.hypot(px_, py_ + 0.24) > 0.1)
    p(outline | tail_o, INK)
    p(blade, GOLD)
    p(blade & (px_ < -0.004), GOLD_HI)                    # the lit side of the blade
    p(blade & (px_ > 0.03 * taper + 0.004), GOLD_LO)
    p(blade & (np.abs(px_) < 0.006) & (t > 0.12) & (t < 0.6), GLOW)   # a vein of light down the middle
    p(tail, GOLD)
    p(tail & (px_ > 0.02), GOLD_LO)
    p(r < 0.08, INK)
    p(r < 0.062, GOLD)
    p(r < 0.032, GLOW)
    return img


def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '.png'))
    print('wrote', name, arr.shape[1], 'x', arr.shape[0])


def main():
    b, s, r = base_band(), sigil(), runes()
    save(b, 'DuelRingBase')
    save(s, 'DuelRingSigil')
    save(r, 'DuelRingRunes')
    save(flash(composite(b, s, r)), 'DuelRingFlash')
    save(seat_ring(), 'DuelRingSeat')
    save(divider(), 'DuelRingDivider')
    save(clock_dial(), 'DuelRingClock')
    save(clock_hand(), 'DuelRingClockHand')


if __name__ == '__main__':
    main()
