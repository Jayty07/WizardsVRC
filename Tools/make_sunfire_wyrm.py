"""The Sunfire Wyrm's dragon: an eastern dragon in fire colours (long, wingless, antlered, whiskered, a mane and a crest
of flame down its back, four short legs, a tuft of flame for a tail). Original art; made for this world.

    python3 Tools/make_sunfire_wyrm.py

writes
    Assets/WizardsVRC/Textures/Spell/WyrmSkin.png    its skin: one 64 x 64 sheet of pixel art (point-filtered in game)
    Assets/WizardsVRC/Editor/WyrmMeshData.cs         its meshes (positions and UVs, three per triangle), which the
                                                     scene builder turns into mesh assets (WizardsVRCSceneBuilder.Wyrm.cs)

The meshes (metres, +Z forward, +Y up; a dragon of size 1):
    Head      pivot at the back of the skull; snout, brow, eyes, antlers, whiskers, teeth and a mane of flame
    Jaw       the lower jaw, pivot at its hinge (the game swings it open)
    Segment   one length of body: radius 0.5, 1 long, with a fin of flame on its back (the game scales it to each
              segment's girth)
    SegmentLegs  the same with a pair of legs
    Tail      the last length: it tapers to a point in a tuft of flame

Tools/preview_sunfire_wyrm.py draws the same meshes with the same skin, so what it shows is the model the game gets.
"""
import math
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIN_OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Textures', 'Spell', 'WyrmSkin.png')
CS_OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Editor', 'WyrmMeshData.cs')

SKIN = 64

# Where each kind of surface is on the sheet (pixels: x0, y0, x1, y1, from the top left).
REGIONS = {
    'body':  (0, 0, 48, 16),     # scales: six sides of the body, 8 px each, 16 px along it
    'belly': (48, 0, 64, 16),    # belly plates: the two sides underneath
    'head':  (0, 16, 48, 32),    # the head's finer scales
    'mouth': (48, 16, 64, 32),   # inside the mouth: dark at the lips, glowing at the throat
    'flame': (0, 32, 16, 48),    # fins, mane and tail tuft: white-hot at the root, red at the tip
    'horn':  (16, 32, 24, 48),
    'gold':  (24, 32, 32, 40),   # whiskers
    'dark':  (24, 40, 32, 48),
    'eye':   (32, 32, 48, 40),
    'tooth': (32, 40, 40, 48),   # teeth and talons
}

# Body segments: girth along the dragon, and which carry legs.
SEGMENTS = 24
LEG_SEGMENTS = (4, 14)
SEGMENT_LENGTH = 0.59            # each overlaps its neighbours (they are 0.38 apart)
MOUTH = (0.0, -0.03, 0.86)       # where its breath leaves, in the head
MANE = (0.0, 0.3, -0.25)         # where its mane burns
JAW_HINGE = (0.0, -0.085, 0.2)   # the lower jaw's pivot, in the head


TAIL_SCALE = (0.44, 0.59)        # the tail piece's own width and length (its tuft is far wider than the tail)


def segment_scale(i):
    """Segment i's transform scale: (across, along). The body meshes are 1 across and 1 long."""
    if i == SEGMENTS - 1:
        return TAIL_SCALE
    return (2.0 * segment_radius(i), SEGMENT_LENGTH)


def segment_radius(i):
    """Segment i's radius: a slim neck, thickest through the shoulders, tapering to the tail."""
    if i <= 3:
        f = i / 3.0
        return 0.2 + 0.07 * (f * f * (3.0 - 2.0 * f))
    if i <= 9:
        return 0.27
    return 0.27 + (0.085 - 0.27) * ((i - 9) / 13.0) ** 1.1


# ---------------------------------------------------------------------- the skin

def _px(img, x, y, c):
    if 0 <= x < SKIN and 0 <= y < SKIN:
        img[y, x, :3] = c
        img[y, x, 3] = 255


def _mix(a, b, f):
    return tuple(int(round(a[k] + (b[k] - a[k]) * f)) for k in range(3))


def make_skin():
    img = np.zeros((SKIN, SKIN, 4), dtype=np.uint8)
    img[:, :, 3] = 255
    crimson, vermilion, orange = (150, 26, 22), (224, 62, 28), (255, 132, 40)
    gold, cream, maroon, white = (255, 200, 84), (255, 238, 176), (64, 12, 16), (255, 250, 224)
    ember = (255, 226, 110)

    img[:, :, :3] = vermilion                 # (whatever nothing maps to)

    def scales(x0, y0, w, h, cw, ch, bands):
        """Rows of overlapping scales, each 'cw' by 'ch' pixels: a lighter shield with a dark trailing edge. 'bands'
        gives each 8 px column group its two colours (shadow, light)."""
        for y in range(h):
            for x in range(w):
                dark, light = bands[min(len(bands) - 1, x * len(bands) // w)]
                row = y // ch
                xx = (x + (cw // 2 if row % 2 else 0)) % cw
                yy = y % ch
                # a scale: rounded at its trailing (lower) end
                edge = yy == ch - 1 or (xx == 0 and yy >= ch // 2)
                rim = yy == 0
                c = dark if edge else _mix(light, white, 0.25) if rim and xx in (cw // 2 - 1, cw // 2) else light
                if not edge and (x * 7 + y * 13) % 11 == 0:
                    c = _mix(c, dark, 0.35)
                _px(img, x0 + x, y0 + y, c)

    # ---- body: lower flank, flank, back, back, flank, lower flank
    body_bands = [(_mix(vermilion, crimson, 0.5), _mix(orange, vermilion, 0.35)), (crimson, vermilion), (maroon, _mix(crimson, vermilion, 0.35)),
                  (maroon, _mix(crimson, vermilion, 0.35)), (crimson, vermilion), (_mix(vermilion, crimson, 0.5), _mix(orange, vermilion, 0.35))]
    scales(0, 0, 48, 16, 4, 4, body_bands)
    # a sun-mark on each flank: a small glowing diamond
    for cx in (12, 36):
        for dx, dy in ((0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)):
            _px(img, cx + dx, 8 + dy, ember if (dx, dy) != (0, 0) else white)
    # ---- belly: plates, lit along their leading edge
    for y in range(16):
        for x in range(16):
            yy = y % 4
            c = cream if yy == 0 else _mix(gold, cream, 0.35) if yy == 1 else gold if yy == 2 else _mix(gold, crimson, 0.45)
            if x in (0, 15):
                c = _mix(c, vermilion, 0.5)
            _px(img, 48 + x, y, c)
    # ---- head: finer scales, a dark brow down the middle of the back
    head_bands = [(_mix(vermilion, crimson, 0.4), _mix(orange, cream, 0.25)), (crimson, vermilion), (maroon, crimson),
                  (maroon, crimson), (crimson, vermilion), (_mix(vermilion, crimson, 0.4), _mix(orange, cream, 0.25))]
    scales(0, 16, 48, 16, 4, 3, head_bands)
    # ---- mouth: dark at the lips (bottom of the patch), a furnace at the throat (top)
    for y in range(16):
        for x in range(16):
            f = y / 15.0
            c = _mix(white, orange, min(1.0, f * 2.2)) if f < 0.45 else _mix(orange, maroon, (f - 0.45) / 0.55)
            if (x + y) % 2 == 0 and 0.3 < f < 0.8:
                c = _mix(c, maroon, 0.25)
            _px(img, 48 + x, 16 + y, c)
    # ---- flame: tongues, white-hot at the root (bottom) through gold and orange to red tips (top)
    for y in range(16):
        for x in range(16):
            f = 1.0 - y / 15.0                                   # 0 root, 1 tip
            f += 0.16 * math.sin(x * 1.9) + (0.08 if (x + y) % 2 else -0.04)
            c = white if f < 0.16 else ember if f < 0.36 else gold if f < 0.52 else orange if f < 0.74 else vermilion if f < 0.92 else crimson
            _px(img, x, 32 + y, c)
    # ---- horn: ivory at the tip (top), gold at the root, ridged
    for y in range(16):
        for x in range(8):
            f = y / 15.0
            c = _mix(cream, gold, f)
            if y % 4 == 3:
                c = _mix(c, crimson, 0.4)
            if x == 0:
                c = _mix(c, crimson, 0.3)
            _px(img, 16 + x, 32 + y, c)
    # ---- gold (whiskers), dark (nostrils and claws' roots)
    for y in range(8):
        for x in range(8):
            _px(img, 24 + x, 32 + y, cream if (x + y) % 4 == 0 else gold)
            _px(img, 24 + x, 40 + y, _mix(maroon, (20, 6, 10), 0.5 if (x + y) % 2 else 0.0))
    # ---- eye: a blazing iris with a slit, rimmed dark
    for y in range(8):
        for x in range(16):
            d = math.hypot((x - 7.5) / 7.5, (y - 3.5) / 3.5)
            c = maroon if d > 0.98 else _mix(white, ember, min(1.0, d * 1.3)) if d < 0.72 else orange
            if abs(x - 7.5) < 1.0 and abs(y - 3.5) < 3.0:
                c = (36, 8, 10)
            _px(img, 32 + x, 32 + y, c)
    # ---- tooth
    for y in range(8):
        for x in range(8):
            _px(img, 32 + x, 40 + y, _mix(white, gold, 0.25 if x < 2 else 0.0))
    return img


# ---------------------------------------------------------------------- mesh building

class Mesh:
    def __init__(self):
        self.v = []      # three per triangle
        self.uv = []

    def tri(self, a, b, c, ua, ub, uc, want):
        a, b, c, want = (np.array(x, dtype=float) for x in (a, b, c, want))
        if np.dot(np.cross(b - a, c - a), want) < 0:
            b, c, ub, uc = c, b, uc, ub
        self.v += [a, b, c]
        self.uv += [tuple(ua), tuple(ub), tuple(uc)]

    def quad(self, a, b, c, d, ua, ub, uc, ud, want):
        self.tri(a, b, c, ua, ub, uc, want)
        self.tri(a, c, d, ua, uc, ud, want)

    def both(self, a, b, c, ua, ub, uc, n):
        """A triangle seen from both sides (a fin, a blade of flame)."""
        self.tri(a, b, c, ua, ub, uc, n)
        self.tri(a, b, c, ua, ub, uc, -np.array(n, dtype=float))

    def arrays(self):
        return np.array(self.v, dtype=float).reshape(-1, 3, 3), np.array(self.uv, dtype=float).reshape(-1, 3, 2)


def uv(region, s, t):
    """A point in a region of the sheet: s across it (0 left, 1 right), t up it (0 bottom, 1 top)."""
    x0, y0, x1, y1 = REGIONS[region]
    # half a pixel in from the edges, so point filtering never picks the neighbour's colour
    px = x0 + 0.5 + s * (x1 - x0 - 1.0)
    py = y1 - 0.5 - t * (y1 - y0 - 1.0)
    return (px / SKIN, 1.0 - py / SKIN)


SIDES = 8
# Which strip of the 'body' / 'head' regions each of the eight sides wears (side j runs from corner j to j + 1, corners
# 45 degrees apart from the top going round to the right): six scaled sides, and the two underneath (None).
STRIP = {2: 0, 1: 1, 0: 2, 7: 3, 6: 4, 5: 5, 3: None, 4: None}


def ring(z, cy, w, h):
    pts = []
    for j in range(SIDES):
        a = math.radians(45.0 * j)
        pts.append(np.array([w * math.sin(a), cy + h * math.cos(a), z]))
    return pts


def loft(m, rings, skin, under, cap_front=None, cap_back=None, under_from=None, under_alt=None, side_skin=None):
    """Joins rings (z, cy, w, h; back to front) into a tube. Its six upper sides wear 'skin', the two underneath
    'under' (or 'under_alt' in front of z = under_from). 'side_skin' can give a side its own region."""
    z0, z1 = rings[0][0], rings[-1][0]
    pts = [ring(*r) for r in rings]
    mid = [np.array([0.0, r[1], r[0]]) for r in rings]
    for i in range(len(rings) - 1):
        t0 = (rings[i][0] - z0) / (z1 - z0)
        t1 = (rings[i + 1][0] - z0) / (z1 - z0)
        for j in range(SIDES):
            a0, a1 = pts[i][j], pts[i][(j + 1) % SIDES]
            b0, b1 = pts[i + 1][j], pts[i + 1][(j + 1) % SIDES]
            want = (a0 + a1 + b0 + b1) / 4.0 - (mid[i] + mid[i + 1]) / 2.0
            strip = STRIP[j]
            if side_skin is not None and j in side_skin:
                reg, s_hi, s_lo = side_skin[j], 1.0, 0.0
            elif strip is None:
                reg = under_alt if under_from is not None and rings[i][0] >= under_from - 1e-6 else under
                k = 0 if j == 3 else 1
                s_hi, s_lo = k / 2.0, (k + 1) / 2.0
            else:
                reg, s_hi, s_lo = skin, (strip + 1) / 6.0, strip / 6.0
            m.quad(a0, a1, b1, b0, uv(reg, s_hi, t0), uv(reg, s_lo, t0), uv(reg, s_lo, t1), uv(reg, s_hi, t1), want)
    for cap, i, n in ((cap_back, 0, (0, 0, -1)), (cap_front, len(rings) - 1, (0, 0, 1))):
        if cap is None:
            continue
        for j in range(SIDES):
            m.tri(mid[i], pts[i][j], pts[i][(j + 1) % SIDES], uv(cap, 0.5, 0.5), uv(cap, 0.2, 0.2), uv(cap, 0.8, 0.2), n)


def limb(m, a, b, ra, rb, region, s0=0.0, s1=1.0, sides=4, cap=True):
    """A tapered stick from a to b (a leg bone, a horn, a whisker, a tooth), 'region' running along it."""
    a, b = np.array(a, dtype=float), np.array(b, dtype=float)
    d = b - a
    d = d / np.linalg.norm(d)
    ref = np.array([0.0, 1.0, 0.0]) if abs(d[1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    x = np.cross(ref, d)
    x = x / np.linalg.norm(x)
    y = np.cross(d, x)
    ra_pts, rb_pts = [], []
    for k in range(sides):
        ang = 2.0 * math.pi * (k + 0.5) / sides
        o = x * math.cos(ang) + y * math.sin(ang)
        ra_pts.append(a + o * ra)
        rb_pts.append(b + o * rb)
    for k in range(sides):
        k1 = (k + 1) % sides
        want = (ra_pts[k] + ra_pts[k1] + rb_pts[k] + rb_pts[k1]) / 4.0 - (a + b) / 2.0
        if rb > 1e-4:
            m.quad(ra_pts[k], ra_pts[k1], rb_pts[k1], rb_pts[k], uv(region, 0.1, s0), uv(region, 0.9, s0), uv(region, 0.9, s1), uv(region, 0.1, s1), want)
        else:
            m.tri(ra_pts[k], ra_pts[k1], b, uv(region, 0.1, s0), uv(region, 0.9, s0), uv(region, 0.5, s1), want)
    if cap:
        for k in range(1, sides - 1):
            m.tri(ra_pts[0], ra_pts[k], ra_pts[k + 1], uv(region, 0.5, s0), uv(region, 0.2, s0), uv(region, 0.8, s0), -d)


def blade(m, root, tip, across, half, lean=None):
    """A tongue of flame from 'root' to 'tip', 'half' wide either side along 'across' at its widest, seen from both
    sides. Three pieces, so it has a waist and a curl ('lean' pushes its middle sideways)."""
    root, tip, across = (np.array(x, dtype=float) for x in (root, tip, across))
    across = across / np.linalg.norm(across)
    along = tip - root
    mid = root + along * 0.42 + (np.array(lean, dtype=float) if lean is not None else 0.0)
    n = np.cross(along, across)
    r0, r1 = root - across * (half * 0.7), root + across * (half * 0.7)
    m0, m1 = mid - across * half, mid + across * (half * 0.75)
    m.both(r0, r1, m1, uv('flame', 0.15, 0.0), uv('flame', 0.85, 0.0), uv('flame', 0.9, 0.42), n)
    m.both(r0, m1, m0, uv('flame', 0.15, 0.0), uv('flame', 0.9, 0.42), uv('flame', 0.1, 0.42), n)
    m.both(m0, m1, tip, uv('flame', 0.1, 0.42), uv('flame', 0.9, 0.42), uv('flame', 0.5, 1.0), n)


# ---------------------------------------------------------------------- the dragon

def segment(legs=False):
    m = Mesh()
    loft(m, [(-0.5, 0.0, 0.4, 0.38), (-0.2, 0.0, 0.5, 0.47), (0.2, 0.0, 0.5, 0.47), (0.5, 0.0, 0.42, 0.4)], 'body', 'belly', cap_front='belly', cap_back='belly')
    # the fin of flame on its back, swept towards the tail
    blade(m, (0.0, 0.42, -0.02), (0.0, 1.04, -0.66), (0.0, 0.0, 1.0), 0.36, lean=(0.0, 0.1, 0.12))
    if legs:
        for sx in (1.0, -1.0):
            hip, knee, foot = (0.4 * sx, -0.2, 0.1), (0.9 * sx, -0.56, -0.36), (1.0 * sx, -1.12, 0.1)
            limb(m, hip, knee, 0.2, 0.14, 'body', 0.1, 0.9)
            limb(m, knee, foot, 0.14, 0.095, 'body', 0.1, 0.9)
            for tx, tz in ((0.0, 0.44), (0.24, 0.34), (-0.24, 0.34)):
                limb(m, foot, (foot[0] + tx, foot[1] - 0.12, foot[2] + tz), 0.085, 0.0, 'tooth')
            limb(m, foot, (foot[0], foot[1] - 0.05, foot[2] - 0.3), 0.07, 0.0, 'tooth')
            # a lick of flame at the shoulder
            blade(m, (0.46 * sx, 0.0, -0.05), (0.78 * sx, 0.42, -0.6), (0.0, 0.3, 1.0), 0.16)
    return m


def tail():
    m = Mesh()
    loft(m, [(-1.0, 0.0, 0.012, 0.012), (-0.6, 0.0, 0.075, 0.07), (0.0, 0.0, 0.155, 0.145), (0.5, 0.0, 0.21, 0.2)], 'body', 'belly', cap_front='belly')
    blade(m, (0.0, 0.13, 0.1), (0.0, 0.52, -0.5), (0.0, 0.0, 1.0), 0.2, lean=(0.0, 0.06, 0.08))
    # the tuft: three tongues of flame fanned out behind it
    for ang in (90.0, 30.0, 150.0):
        a = math.radians(ang)
        across = (math.cos(a), math.sin(a), 0.0)
        lift = (-math.sin(a) * 0.0, 0.18 if ang == 90.0 else 0.0, 0.0)
        blade(m, (0.0, 0.0, -0.45), (lift[0], lift[1] * 2.0, -2.5 if ang == 90.0 else -2.1), across, 0.42, lean=lift)
    return m


def head():
    m = Mesh()
    # skull and snout; under the snout is the roof of the mouth, under the skull the throat
    # (a heavy brow, a long snout dipping to the lip and rising again to a broad, flared nose)
    loft(m, [(-0.12, 0.0, 0.15, 0.15), (0.02, 0.03, 0.235, 0.215), (0.2, 0.07, 0.265, 0.24), (0.36, 0.075, 0.215, 0.165),
             (0.54, 0.055, 0.165, 0.105), (0.72, 0.055, 0.16, 0.1), (0.84, 0.075, 0.178, 0.118), (0.94, 0.07, 0.135, 0.09), (1.0, 0.062, 0.06, 0.042)],
         'head', 'belly', cap_front='gold', cap_back='belly', under_from=0.36, under_alt='mouth')
    for sx in (1.0, -1.0):
        # eye: a blazing slit under a heavy brow
        e0, e1 = (0.205 * sx, 0.07, 0.4), (0.268 * sx, 0.07, 0.225)
        e2, e3 = (0.268 * sx, 0.185, 0.225), (0.205 * sx, 0.185, 0.4)
        m.quad(e0, e1, e2, e3, uv('eye', 0.0, 0.0), uv('eye', 1.0, 0.0), uv('eye', 1.0, 1.0), uv('eye', 0.0, 1.0), (sx, 0.1, 0.3))
        limb(m, (0.285 * sx, 0.31, 0.08), (0.16 * sx, 0.225, 0.47), 0.06, 0.032, 'head', 0.45, 0.55)
        # antler: a beam swept back with two tines
        p0, p1, p2 = np.array([0.12 * sx, 0.24, 0.08]), np.array([0.24 * sx, 0.54, -0.2]), np.array([0.34 * sx, 0.78, -0.6])
        limb(m, p0, p1, 0.066, 0.046, 'horn', 0.0, 0.5)
        limb(m, p1, p2, 0.046, 0.0, 'horn', 0.5, 1.0, cap=False)
        limb(m, p1, (0.38 * sx, 0.86, -0.04), 0.04, 0.0, 'horn', 0.4, 1.0, cap=False)
        limb(m, (p1 + p2) / 2.0, (0.5 * sx, 0.7, -0.7), 0.032, 0.0, 'horn', 0.6, 1.0, cap=False)
        # whisker: a long barbel from the lip, trailing back in a wave
        chain = [(0.15 * sx, 0.05, 0.88), (0.36 * sx, 0.1, 0.82), (0.54 * sx, 0.02, 0.54), (0.62 * sx, 0.12, 0.16), (0.56 * sx, 0.28, -0.3)]
        radii = [0.028, 0.022, 0.016, 0.01, 0.0]
        for k in range(len(chain) - 1):
            limb(m, chain[k], chain[k + 1], radii[k], radii[k + 1], 'gold', sides=3, cap=k == 0)
        # nostril: a dark pit on top of the nose
        limb(m, (0.075 * sx, 0.165, 0.9), (0.09 * sx, 0.215, 0.82), 0.034, 0.0, 'dark', sides=3)
        # upper teeth: fangs at the front, smaller behind
        for z, x, drop, r in ((0.9, 0.105, 0.16, 0.032), (0.76, 0.135, 0.09, 0.024), (0.62, 0.14, 0.08, 0.022), (0.48, 0.16, 0.075, 0.022)):
            limb(m, (x * sx, -0.02, z), (x * sx, -0.02 - drop, z + 0.01), r, 0.0, 'tooth', sides=3)
        # cheek: a lick of flame behind the jaw
        blade(m, (0.235 * sx, -0.04, 0.1), (0.5 * sx, -0.2, -0.42), (0.0, 1.0, 0.25), 0.11)
    # the mane: tongues of flame streaming back off the skull, each two blades crossed
    for ang, length in ((-105.0, 0.5), (-70.0, 0.62), (-35.0, 0.74), (0.0, 0.82), (35.0, 0.74), (70.0, 0.62), (105.0, 0.5)):
        a = math.radians(ang)
        out = np.array([math.sin(a), math.cos(a), 0.0])
        tang = np.array([math.cos(a), -math.sin(a), 0.0])
        root = np.array([0.2 * math.sin(a), 0.03 + 0.19 * math.cos(a), 0.0])
        tip = root + (np.array([0.0, 0.0, -1.0]) + out * 0.5) * length
        blade(m, root, tip, out, 0.12, lean=out * 0.07)
        blade(m, root, tip, tang, 0.1, lean=out * 0.07)
    return m


def jaw():
    m = Mesh()
    # (pivot at the hinge) its top is the floor of the mouth
    loft(m, [(-0.02, -0.02, 0.165, 0.065), (0.24, -0.035, 0.155, 0.06), (0.5, -0.035, 0.135, 0.052), (0.68, -0.02, 0.1, 0.045), (0.76, -0.005, 0.05, 0.028)],
         'head', 'belly', cap_front='belly', cap_back='belly', side_skin={0: 'mouth', 7: 'mouth', 2: 'belly', 5: 'belly'})
    for sx in (1.0, -1.0):
        for z, x, rise, r in ((0.66, 0.075, 0.12, 0.026), (0.5, 0.11, 0.07, 0.02), (0.32, 0.13, 0.065, 0.02)):
            limb(m, (x * sx, 0.01, z), (x * sx, 0.01 + rise, z + 0.01), r, 0.0, 'tooth', sides=3)
    # a beard of flame under the chin
    blade(m, (0.0, -0.08, 0.42), (0.0, -0.46, 0.02), (0.0, 0.0, 1.0), 0.13, lean=(0.0, -0.02, -0.05))
    return m


MESHES = (('Head', head), ('Jaw', jaw), ('Segment', lambda: segment(False)), ('SegmentLegs', lambda: segment(True)), ('Tail', tail))


# ---------------------------------------------------------------------- the pose on its card

# The dragon as its card shows it (and as a minion's unstaged cast sends it along the spell's path): reared in an S,
# its head lunging out to the right with its jaws open, its tail trailing off below. It is laid out as the card sees
# it: x to the right of the picture, y up it, z away from the eye; the curve its body lies along, tail first; its head
# sits on the last point but one, looking at the last. POSE_TURN is how the builder turns a flying creature for its
# card (WizardsVRCSceneBuilder.RenderArtSubject: three-quarter view, pitched into its dive), so the pose is turned
# back by it to get the dragon itself (metres, +Z the way it faces, +Y up, standing on the origin).
POSE_VIEW = (
    (-3.7, -1.2, 0.5), (-2.7, -1.6, 0.1), (-1.5, -1.5, -0.4), (-0.4, -1.0, -0.6), (0.1, -0.3, -0.3), (-0.6, 0.2, 0.3),
    (-1.7, 0.35, 0.8), (-2.5, 0.8, 0.7), (-2.2, 1.45, 0.3), (-1.1, 1.65, -0.1), (0.1, 1.4, -0.5), (1.0, 1.05, -0.8),
    (2.1, 0.8, -1.2),
)
POSE_TURN = (20.0, 145.0)        # pitch, then yaw (degrees)
POSE_HEAD_SCALE = 1.6            # its head looms: bigger than life, the neck swelling to meet it
POSE_NECK_SWELL = 0.45
POSE_NECK_SEGMENTS = 8
POSE_JAW_DEGREES = 34.0


def pose_way():
    """POSE_VIEW turned back from the card's view into the dragon's own space, and stood on the ground."""
    px, py = math.radians(POSE_TURN[0]), math.radians(POSE_TURN[1])
    rx = np.array([[1, 0, 0], [0, math.cos(px), -math.sin(px)], [0, math.sin(px), math.cos(px)]])
    ry = np.array([[math.cos(py), 0, math.sin(py)], [0, 1, 0], [-math.sin(py), 0, math.cos(py)]])
    turn = ry @ rx
    pts = [turn.T @ np.array(q, dtype=float) for q in POSE_VIEW]
    low = min(q[1] for q in pts)
    return [q + np.array([0.0, 0.7 - low, 0.0]) for q in pts]


def card_pose():
    """{'head': (position, forward, up, scale), 'jaw': degrees open, 'segs': [(position, forward, up, scale)] neck to
    tail}: each segment's scale multiplies its usual one (segment_scale)."""
    import sunfire_wyrm_flight as flight
    w = flight.Wyrm()
    w.way = pose_way()
    w._begin_walk()
    while not w.built:
        w.build_some(8)
    s_head = w.way_s[len(w.way) - 2]
    up = np.array([0.0, 1.0, 0.0])

    def squared(u, d):
        u = u - d * float(np.dot(u, d))
        n = np.linalg.norm(u)
        return u / n if n > 1e-6 else up

    P = w.at(s_head)
    D = flight.norm(w.at(s_head + 0.6) - P)
    head = (P, D, squared(w.up_at(s_head), D), POSE_HEAD_SCALE)
    segs = []
    back = flight.NECK * POSE_HEAD_SCALE
    ahead = P
    last_k = None
    for i in range(SEGMENTS):
        k = 1.0 + POSE_NECK_SWELL * max(0.0, 1.0 - i / float(POSE_NECK_SEGMENTS))
        if last_k is not None:
            back += flight.SPACING * 0.5 * (k + last_k)
        last_k = k
        q = w.at(s_head - back)
        d = flight.norm(ahead - q)
        segs.append((q, d, squared(w.up_at(s_head - back), d), k))
        ahead = q
    return {'head': head, 'jaw': POSE_JAW_DEGREES, 'segs': segs}


def build_all():
    """name -> (vertices (n, 3, 3), uvs (n, 3, 2))"""
    return {name: fn().arrays() for name, fn in MESHES}


# ---------------------------------------------------------------------- output

def floats(a):
    return ', '.join('%.4gf' % x if abs(x) > 5e-5 else '0f' for x in a)


def write_cs(meshes):
    with open(CS_OUT, 'w') as f:
        f.write('// Generated by Tools/make_sunfire_wyrm.py: do not edit by hand.\n')
        f.write('namespace WizardsVRC.EditorTools\n{\n')
        f.write('    /// <summary>\n')
        f.write("    /// The Sunfire Wyrm's dragon (see Tools/make_sunfire_wyrm.py): each mesh's vertices (x, y, z, three per triangle,\n")
        f.write('    /// wound to face outwards) and their UVs on Textures/Spell/WyrmSkin.png, and where its parts go.\n')
        f.write('    /// </summary>\n')
        f.write('    public static class WyrmMeshData\n    {\n')
        f.write('        public const int Segments = %d;\n' % SEGMENTS)
        f.write('        public const float SegmentLength = %.4gf;\n' % SEGMENT_LENGTH)
        f.write('        /// <summary>Each body segment\'s radius, neck to tail (a dragon of size 1).</summary>\n')
        f.write('        public static readonly float[] SegmentRadius = { %s };\n' % floats([segment_radius(i) for i in range(SEGMENTS)]))
        f.write('        /// <summary>Each segment\'s scale across and along (its mesh is 1 across and 1 long; the last is the tail).</summary>\n')
        f.write('        public static readonly float[] SegmentAcross = { %s };\n' % floats([segment_scale(i)[0] for i in range(SEGMENTS)]))
        f.write('        public static readonly float[] SegmentAlong = { %s };\n' % floats([segment_scale(i)[1] for i in range(SEGMENTS)]))
        f.write('        /// <summary>The segments that carry a pair of legs.</summary>\n')
        f.write('        public static readonly int[] LegSegments = { %s };\n' % ', '.join(str(i) for i in LEG_SEGMENTS))
        f.write('        /// <summary>In the head: the lower jaw\'s hinge, where its breath leaves, and where its mane burns.</summary>\n')
        f.write('        public static readonly float[] JawHinge = { %s };\n' % floats(JAW_HINGE))
        f.write('        public static readonly float[] Mouth = { %s };\n' % floats(MOUTH))
        f.write('        public static readonly float[] Mane = { %s };\n' % floats(MANE))
        pose = card_pose()
        f.write('        /// <summary>\n')
        f.write('        /// The dragon posed as on its card: its head (position, forward, up, scale: ten numbers), how far its jaw is\n')
        f.write('        /// open, and each segment the same way, neck to tail (the scale multiplies the segment\'s own).\n')
        f.write('        /// </summary>\n')
        hp, hd, hu, hs = pose['head']
        f.write('        public static readonly float[] PoseHead = { %s };\n' % floats(list(hp) + list(hd) + list(hu) + [hs]))
        f.write('        public const float PoseJawDegrees = %.4gf;\n' % pose['jaw'])
        flat = []
        for q, d, u, k in pose['segs']:
            flat += list(q) + list(d) + list(u) + [k]
        f.write('        public static readonly float[] PoseSegments = { %s };\n' % floats(flat))
        for name, (v, t) in meshes.items():
            f.write('        public static readonly float[] %sV = { %s };\n' % (name, floats(v.reshape(-1))))
            f.write('        public static readonly float[] %sUV = { %s };\n' % (name, floats(t.reshape(-1))))
        f.write('    }\n}\n')


def main():
    os.makedirs(os.path.dirname(SKIN_OUT), exist_ok=True)
    Image.fromarray(make_skin(), 'RGBA').save(SKIN_OUT)
    meshes = build_all()
    write_cs(meshes)
    total = 0
    for name, (v, t) in meshes.items():
        lo, hi = v.reshape(-1, 3).min(axis=0), v.reshape(-1, 3).max(axis=0)
        print('%-12s %4d triangles   x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f' % (name, len(v), lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        total += len(v)
    whole = len(meshes['Head'][0]) + len(meshes['Jaw'][0]) + (SEGMENTS - 1 - len(LEG_SEGMENTS)) * len(meshes['Segment'][0]) + \
        len(LEG_SEGMENTS) * len(meshes['SegmentLegs'][0]) + len(meshes['Tail'][0])
    print('the whole dragon: %d triangles in %d objects' % (whole, SEGMENTS + 2))
    print('wrote', os.path.relpath(SKIN_OUT, ROOT), 'and', os.path.relpath(CS_OUT, ROOT))


if __name__ == '__main__':
    main()
