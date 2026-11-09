"""The school symbols a caster draws in the air with arcane energy (Assets/WizardsVRC/Textures/CastDraw).

Each symbol is a few pen strokes (a flame, a snowflake, a lightning bolt, a flower, a skull, an open book, a pair of
scales, a star for General spells, a microchip for the Tech School), followed by a ring round it like a spell seal. For each one this writes:

    GlyphDraw<School>.png   128x128, read as data (not colour) by the WizardsVRC/GlyphDraw shader:
                              R = when the pen passes this pixel (0 = first, 1 = last), so the shader can reveal the
                                  strokes in drawing order as its _Reveal value runs from 0 to 1
                              G = 255 on the stroke's bright core, lower on its glow
                              A = 255 where there's any ink
    Editor/GlyphPenData.cs  the pen tip's path: SAMPLES points per symbol, evenly spaced in drawing time, in texture
                              coordinates (u right, v up), and where the pen lifts between strokes (BattleClient moves a
                              glowing tip and its school's sparks along it)

    python3 Tools/make_glyph_draw.py [preview.png]      (needs numpy and Pillow)
"""
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "WizardsVRC", "Textures", "CastDraw")
CS_OUT = os.path.join(ROOT, "Assets", "WizardsVRC", "Editor", "GlyphPenData.cs")
SIZE = 128
SAMPLES = 64
CORE = 2.1    # px
HALO = 4.4    # px
LABELS = ["Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance", "General", "Tech"]


# ------------------------------------------------------------------ stroke helpers (u, v in 0..1, v up)

def bez(p0, p1, p2, p3, n=24):
    out = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3
        b = 3 * (1 - t) ** 2 * t
        c = 3 * (1 - t) * t * t
        d = t ** 3
        out.append((a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]))
    return out


def chain(*segs):
    """Joins bezier/line segments into one stroke (dropping the repeated joint points)."""
    out = []
    for s in segs:
        out.extend(s if not out else s[1:])
    return out


def arc(cx, cy, r, a0, a1, n=40):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def line(*pts):
    return list(pts)


def polar(cx, cy, r, deg):
    return (cx + r * math.cos(math.radians(deg)), cy + r * math.sin(math.radians(deg)))


def seal():
    """The ring drawn round every symbol last, starting at the bottom."""
    return arc(0.5, 0.5, 0.46, -90, 270, 72)


# ------------------------------------------------------------------ the symbols

def fire():
    outer = chain(bez((0.5, 0.13), (0.22, 0.13), (0.16, 0.42), (0.30, 0.58)),
                  bez((0.30, 0.58), (0.37, 0.66), (0.30, 0.77), (0.38, 0.88)),
                  bez((0.38, 0.88), (0.45, 0.74), (0.52, 0.78), (0.57, 0.93)),
                  bez((0.57, 0.93), (0.59, 0.78), (0.70, 0.74), (0.69, 0.61)),
                  bez((0.69, 0.61), (0.84, 0.46), (0.78, 0.13), (0.5, 0.13)))
    inner = chain(bez((0.5, 0.22), (0.37, 0.24), (0.37, 0.42), (0.46, 0.52)),
                  bez((0.46, 0.52), (0.50, 0.57), (0.49, 0.62), (0.52, 0.67)),
                  bez((0.52, 0.67), (0.61, 0.56), (0.65, 0.27), (0.5, 0.22)))
    return [outer, inner]


def ice():
    c = (0.5, 0.5)
    strokes = []
    for k in range(3):
        a = 90 + 60 * k
        strokes.append(line(polar(*c, 0.36, a), polar(*c, 0.36, a + 180)))
    for k in range(6):
        a = 90 + 60 * k
        for along, size in ((0.17, 0.08), (0.27, 0.07)):
            p = polar(*c, along, a)
            strokes.append(line(polar(p[0], p[1], size, a + 45), p, polar(p[0], p[1], size, a - 45)))
    return strokes


def storm():
    bolt = line((0.60, 0.92), (0.30, 0.47), (0.48, 0.47), (0.36, 0.08), (0.72, 0.57), (0.53, 0.57), (0.67, 0.92), (0.60, 0.92))
    sparks = [line((0.20, 0.72), (0.27, 0.66)), line((0.80, 0.34), (0.73, 0.40))]
    return [bolt] + sparks


def life():
    c = (0.5, 0.62)
    petals = []
    for k in range(5):
        a = 90 + 72 * k
        petals.append(bez(polar(*c, 0.06, a - 32), polar(*c, 0.25, a - 24), polar(*c, 0.25, a + 24), polar(*c, 0.06, a + 32)))
    centre = arc(c[0], c[1], 0.05, 90, 450, 24)
    stem = bez((0.5, 0.55), (0.47, 0.42), (0.53, 0.28), (0.5, 0.12))
    left = chain(bez((0.5, 0.32), (0.42, 0.40), (0.28, 0.38), (0.24, 0.29)),
                 bez((0.24, 0.29), (0.33, 0.24), (0.44, 0.25), (0.5, 0.32)))
    right = chain(bez((0.5, 0.24), (0.58, 0.32), (0.72, 0.30), (0.76, 0.21)),
                  bez((0.76, 0.21), (0.67, 0.16), (0.56, 0.17), (0.5, 0.24)))
    return petals + [centre, stem, left, right]


def death():
    cranium = chain(arc(0.5, 0.57, 0.27, 215, -35, 48),
                    line(polar(0.5, 0.57, 0.27, -35), (0.66, 0.22), (0.34, 0.22), polar(0.5, 0.57, 0.27, 215)))
    eyes = [arc(0.39, 0.53, 0.075, 90, 450, 28), arc(0.61, 0.53, 0.075, 90, 450, 28)]
    nose = line((0.5, 0.43), (0.455, 0.35), (0.545, 0.35), (0.5, 0.43))
    teeth = [line((x, 0.22), (x, 0.30)) for x in (0.42, 0.5, 0.58)]
    return [cranium] + eyes + [nose] + teeth


def myth():
    left = chain(bez((0.5, 0.64), (0.38, 0.73), (0.25, 0.73), (0.13, 0.69)),
                 line((0.13, 0.69), (0.13, 0.27)),
                 bez((0.13, 0.27), (0.25, 0.31), (0.38, 0.31), (0.5, 0.22)))
    right = chain(bez((0.5, 0.64), (0.62, 0.73), (0.75, 0.73), (0.87, 0.69)),
                  line((0.87, 0.69), (0.87, 0.27)),
                  bez((0.87, 0.27), (0.75, 0.31), (0.62, 0.31), (0.5, 0.22)))
    spine = line((0.5, 0.64), (0.5, 0.22))
    text = []
    for y in (0.56, 0.47, 0.38):
        text.append(line((0.20, y + 0.02), (0.43, y - 0.01)))
        text.append(line((0.57, y - 0.01), (0.80, y + 0.02)))
    star = [line((0.5, 0.92), (0.5, 0.76)), line((0.42, 0.84), (0.58, 0.84))]
    return [left, right, spine] + text + star


def balance():
    post = line((0.5, 0.17), (0.5, 0.80))
    base = line((0.33, 0.15), (0.67, 0.15))
    beam = line((0.18, 0.70), (0.82, 0.70))
    knob = arc(0.5, 0.83, 0.035, -90, 270, 16)
    pans = []
    for cx in (0.18, 0.82):
        pans.append(line((cx - 0.10, 0.44), (cx, 0.70), (cx + 0.10, 0.44)))
        pans.append(arc(cx, 0.44, 0.11, 180, 360, 24))
    return [post, base, beam, knob] + pans


def general():
    star = [polar(0.5, 0.5, 0.37, 90 + 144 * k) for k in range(6)]
    return [star]


def tech():
    """The Tech School (The Dominion's): a microchip, two pins a side, with a charge zig-zagging across its core."""
    body = line((0.30, 0.30), (0.30, 0.70), (0.70, 0.70), (0.70, 0.30), (0.30, 0.30))
    pins = []
    for t in (0.42, 0.58):
        pins.append(line((t, 0.70), (t, 0.84)))
        pins.append(line((0.70, t), (0.84, t)))
        pins.append(line((t, 0.30), (t, 0.16)))
        pins.append(line((0.30, t), (0.16, t)))
    charge = line((0.40, 0.60), (0.56, 0.54), (0.44, 0.46), (0.60, 0.40))
    return [body] + pins + [charge]


SYMBOLS = [fire, ice, storm, life, death, myth, balance, general, tech]


# ------------------------------------------------------------------ rendering

def resample(stroke, step):
    """Points every 'step' (uv units) along a stroke, with their arc length from its start."""
    pts = [np.array(p, dtype=float) for p in stroke]
    out = [(pts[0], 0.0)]
    run = 0.0
    for a, b in zip(pts[:-1], pts[1:]):
        seg = np.linalg.norm(b - a)
        k = max(1, int(math.ceil(seg / step)))
        for i in range(1, k + 1):
            out.append((a + (b - a) * i / k, run + seg * i / k))
        run += seg
    return out, run


def render(strokes):
    step = 0.4 / SIZE
    dense = []      # (uv point, distance along everything, stroke index)
    total = 0.0
    for si, s in enumerate(strokes):
        pts, length = resample(s, step)
        dense.extend((p, total + d, si) for p, d in pts)
        total += length
    order = np.full((SIZE, SIZE), 2.0)
    core = np.zeros((SIZE, SIZE), dtype=bool)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    r = int(math.ceil(HALO)) + 1
    for p, d, _ in dense:
        px, py = p[0] * (SIZE - 1), (1 - p[1]) * (SIZE - 1)
        x0, x1 = max(0, int(px) - r), min(SIZE, int(px) + r + 2)
        y0, y1 = max(0, int(py) - r), min(SIZE, int(py) + r + 2)
        dist = np.hypot(xx[y0:y1, x0:x1] - px, yy[y0:y1, x0:x1] - py)
        t = d / total
        win = order[y0:y1, x0:x1]
        hit = dist <= HALO
        win[hit] = np.minimum(win[hit], t)
        core[y0:y1, x0:x1] |= dist <= CORE
    ink = order <= 1.0
    img = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    img[..., 0] = np.where(ink, np.clip(order, 0, 1) * 255 + 0.5, 0).astype(np.uint8)
    img[..., 1] = np.where(core, 255, np.where(ink, 80, 0)).astype(np.uint8)
    img[..., 3] = np.where(ink, 255, 0).astype(np.uint8)

    # The pen: SAMPLES points evenly spaced in drawing time, and whether it lifts after each one.
    pen_u, pen_v, jump = [], [], []
    di = 0
    stroke_at = []
    for k in range(SAMPLES):
        target = total * k / (SAMPLES - 1)
        while di < len(dense) - 1 and dense[di + 1][1] < target:
            di += 1
        p, _, si = dense[di]
        pen_u.append(float(p[0]))
        pen_v.append(float(p[1]))
        stroke_at.append(si)
    for k in range(SAMPLES):
        jump.append(1 if k < SAMPLES - 1 and stroke_at[k + 1] != stroke_at[k] else 0)
    return img, pen_u, pen_v, jump


def main():
    os.makedirs(OUT, exist_ok=True)
    us, vs, js, imgs = [], [], [], []
    for label, fn in zip(LABELS, SYMBOLS):
        # The symbol sits inside the seal ring with a little room to spare.
        inner = [[(0.5 + (u - 0.5) * 0.84, 0.5 + (v - 0.5) * 0.84) for u, v in st] for st in fn()]
        img, pu, pv, j = render(inner + [seal()])
        Image.fromarray(img, "RGBA").save(os.path.join(OUT, "GlyphDraw%s.png" % label))
        imgs.append(img)
        us += pu
        vs += pv
        js += j

    def floats(a):
        return ", ".join("%.4ff" % x for x in a)

    with open(CS_OUT, "w") as f:
        f.write("// Generated by Tools/make_glyph_draw.py: do not edit by hand.\n")
        f.write("namespace WizardsVRC.EditorTools\n{\n")
        f.write("    /// <summary>The pen tip's path while a caster draws their school's symbol (see Tools/make_glyph_draw.py).</summary>\n")
        f.write("    public static class GlyphPenData\n    {\n")
        f.write("        /// <summary>Order of the symbols: the seven schools, then General, then Tech (school 8).</summary>\n")
        f.write("        public static readonly string[] Labels = { %s };\n" % ", ".join('"%s"' % l for l in LABELS))
        f.write("        public const int Samples = %d;\n" % SAMPLES)
        f.write("        public static readonly float[] U = { %s };\n" % floats(us))
        f.write("        public static readonly float[] V = { %s };\n" % floats(vs))
        f.write("        /// <summary>1 = the pen lifts between this sample and the next (a new stroke starts).</summary>\n")
        f.write("        public static readonly int[] Jump = { %s };\n" % ", ".join(str(x) for x in js))
        f.write("    }\n}\n")

    if len(sys.argv) > 1:
        # Preview: each symbol at 1/3, 2/3 and fully drawn.
        sheet = Image.new("RGB", (len(LABELS) * 136, 3 * 136), (16, 14, 24))
        for i, img in enumerate(imgs):
            for row, reveal in enumerate((0.33, 0.66, 1.0)):
                order = img[..., 0] / 255.0
                show = (img[..., 3] > 0) & (order <= reveal)
                rgb = np.zeros((SIZE, SIZE, 3))
                glow = img[..., 1] / 255.0
                tip = np.clip(1 - (reveal - order) / 0.06, 0, 1)
                for c, (lo, hi) in enumerate(((0.9, 1.0), (0.6, 1.0), (1.0, 0.8))):
                    rgb[..., c] = np.where(show, glow * (lo + (hi - lo) * tip), 0)
                sheet.paste(Image.fromarray((rgb * 255).astype(np.uint8)), (i * 136 + 4, row * 136 + 4))
        sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
        sheet.save(sys.argv[1])
    print("wrote", len(LABELS), "symbols")


main()
