"""Art for the world and zone maps (Textures/Maps), all original: parchment, the world maps' skies, the frame, the label
tag, the title scroll, the you-are-here marker, and a set of marker doodles for the margins (little wizards, ghosts,
rats, bats, a troll, a plant monster, a crab, a fire imp, an owl, a cactus, a lantern ghost...), drawn with wobbly
black outlines and flat colours like a child's sketches on the edge of a map.

The scene builder draws each zone's map from its grid (WizardsVRCSceneBuilder.Maps.cs) onto MapParchment.png, scatters
the doodles that suit the zone round it, and builds each world's map on its sky.

    python3 Tools/make_map_art.py Assets/WizardsVRC/Textures/Maps      (needs Pillow and numpy)
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

INK = (40, 28, 22, 255)
SS = 4   # supersampling for the doodles


def rng_for(seed):
    return np.random.default_rng(seed)


# ---------------------------------------------------------------- doodle kit (drawn at SS x, then shrunk)

class Doodle:
    def __init__(self, w, h, seed):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
        self.r = rng_for(seed)
        self.lw = 3

    def P(self, x, y, j=1.2):
        return (x * SS + self.r.normal(0, j) * SS * 0.35, y * SS + self.r.normal(0, j) * SS * 0.35)

    def wob(self, pts, n=6):
        """A polyline with a few jittered points between each pair (a hand-drawn wobble)."""
        out = []
        for i in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[i], pts[i + 1]
            for k in range(n):
                t = k / n
                out.append(self.P(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 0.8))
        out.append(self.P(*pts[-1], 0.8))
        return out

    def line(self, pts, w=None, col=INK):
        w = (w or self.lw) * SS
        q = self.wob(pts)
        self.d.line(q, fill=col, width=int(w), joint="curve")
        for p in (q[0], q[-1]):
            self.d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=col)

    def poly(self, pts, fill, w=None):
        q = self.wob(pts + [pts[0]])
        self.d.polygon(q, fill=fill)
        self.d.line(q, fill=INK, width=int((w or self.lw) * SS), joint="curve")

    def ellipse_pts(self, cx, cy, rx, ry, n=28, a0=0, a1=360):
        return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

    def ellipse(self, cx, cy, rx, ry, fill, w=None):
        pts = self.ellipse_pts(cx, cy, rx, ry)[:-1]
        self.poly(pts, fill, w)

    def dot(self, x, y, r=1.6, col=INK):
        x, y = x * SS, y * SS
        self.d.ellipse([x - r * SS, y - r * SS, x + r * SS, y + r * SS], fill=col)

    def star(self, cx, cy, r, fill=(255, 220, 70, 255)):
        pts = []
        for i in range(10):
            a = math.radians(-90 + i * 36)
            rr = r if i % 2 == 0 else r * 0.45
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        self.poly(pts, fill, 2)

    def done(self):
        im = self.img.resize((self.w, self.h), Image.LANCZOS)
        return im


SKIN = (250, 214, 170, 255)
WHITE = (250, 248, 240, 255)


def wizard(robe, pose, seed, hat=None):
    d = Doodle(140, 160, seed)
    hat = hat or robe
    lean = {"cast": 0, "run": 10, "scared": 0, "wave": 0, "read": 0}[pose]
    cx = 66
    # legs
    if pose == "run":
        d.line([(cx - 6, 128), (cx - 22, 146)]); d.line([(cx + 8, 128), (cx + 22, 144)])
        d.ellipse(cx - 25, 148, 7, 4, (90, 60, 40, 255)); d.ellipse(cx + 26, 146, 7, 4, (90, 60, 40, 255))
    else:
        d.line([(cx - 8, 128), (cx - 10, 146)]); d.line([(cx + 8, 128), (cx + 10, 146)])
        d.ellipse(cx - 13, 148, 7, 4, (90, 60, 40, 255)); d.ellipse(cx + 13, 148, 7, 4, (90, 60, 40, 255))
    # robe (a bell)
    d.poly([(cx - 12 + lean, 78), (cx + 12 + lean, 78), (cx + 26, 132), (cx - 26, 132)], robe)
    d.line([(cx - 20, 118), (cx + 20, 118)], 2)
    # arms
    if pose == "cast":
        d.line([(cx + 10, 88), (cx + 30, 70), (cx + 40, 52)])
        d.line([(cx + 40, 54), (cx + 52, 30)], 3, (120, 80, 50, 255))
        d.star(cx + 55, 24, 9)
        for a in (20, 60, 110, 160):
            x0, y0 = cx + 55 + 12 * math.cos(math.radians(a)), 24 - 12 * math.sin(math.radians(a))
            d.line([(x0, y0), (x0 + 6 * math.cos(math.radians(a)), y0 - 6 * math.sin(math.radians(a)))], 2)
        d.line([(cx - 10, 88), (cx - 28, 104)])
    elif pose == "scared":
        d.line([(cx + 10, 88), (cx + 26, 66)]); d.line([(cx - 10, 88), (cx - 26, 66)])
    elif pose == "wave":
        d.line([(cx + 10, 88), (cx + 24, 70), (cx + 30, 56)]); d.line([(cx - 10, 88), (cx - 24, 106)])
        d.line([(cx + 30, 56), (cx + 26, 48)], 2); d.line([(cx + 30, 56), (cx + 34, 48)], 2)
    elif pose == "read":
        d.line([(cx + 10, 90), (cx + 18, 100)]); d.line([(cx - 10, 90), (cx - 18, 100)])
        d.poly([(cx - 22, 96), (cx, 102), (cx + 22, 96), (cx + 22, 112), (cx, 118), (cx - 22, 112)], (200, 60, 50, 255))
        d.line([(cx, 102), (cx, 118)], 2)
    else:   # run
        d.line([(cx + 12, 88), (cx + 32, 96)]); d.line([(cx - 8, 88), (cx - 24, 76)])
        d.line([(cx + 32, 96), (cx + 44, 86)], 3, (120, 80, 50, 255)); d.star(cx + 48, 82, 6)
    # head
    hx = cx + lean
    d.ellipse(hx, 64, 15, 15, SKIN)
    eyes = (2.4 if pose == "scared" else 1.8)
    d.dot(hx - 5, 62, eyes); d.dot(hx + 5, 62, eyes)
    if pose == "scared":
        d.ellipse(hx, 72, 3, 4, (120, 30, 30, 255), 2)
    else:
        d.line([(hx - 5, 70), (hx, 73), (hx + 5, 70)], 2)
    # hat: a cone with a floppy tip and a brim
    d.poly([(hx - 20, 52), (hx + 20, 52), (hx + 6, 14), (hx + 18, 4), (hx - 2, 10)], hat)
    d.poly([(hx - 24, 50), (hx + 24, 50), (hx + 22, 56), (hx - 22, 56)], hat)
    if pose == "scared":
        for (x, y) in ((hx - 34, 30), (hx + 34, 22)):
            d.line([(x - 5, y - 6), (x, y - 10), (x + 5, y - 6), (x, y - 1), (x, y + 3)], 2)
            d.dot(x, y + 8, 1.6)
    return d.done()


def ghost(col, seed):
    d = Doodle(120, 140, seed)
    pts = d.ellipse_pts(60, 52, 34, 38, 16, 180, 360)
    bottom = [(94, 110), (84, 122), (74, 110), (62, 124), (50, 110), (38, 122), (26, 110)]
    d.poly(pts[:-1] + [(94, 52)] + bottom + [(26, 52)], col)
    d.ellipse(48, 52, 6, 9, INK, 1); d.ellipse(72, 52, 6, 9, INK, 1)
    d.dot(46, 49, 1.6, WHITE); d.dot(70, 49, 1.6, WHITE)
    d.ellipse(60, 76, 6, 7, (60, 20, 30, 255), 2)
    d.line([(26, 70), (12, 84)]); d.line([(94, 70), (108, 80)])
    return d.done()


def rat(seed):
    d = Doodle(140, 90, seed)
    grey = (150, 145, 150, 255)
    d.line([(30, 60), (14, 54), (8, 40), (18, 30)], 2)
    d.ellipse(62, 58, 34, 18, grey)
    d.poly([(90, 46), (122, 58), (90, 70)], grey)
    d.ellipse(84, 40, 8, 8, (230, 170, 170, 255))
    d.dot(102, 54, 2); d.dot(122, 58, 2.5, (220, 120, 130, 255))
    d.line([(110, 60), (126, 52)], 1); d.line([(110, 62), (128, 64)], 1)
    for x in (44, 72):
        d.line([(x, 74), (x - 4, 84)], 2); d.line([(x + 8, 74), (x + 10, 84)], 2)
    return d.done()


def bat(seed):
    d = Doodle(150, 90, seed)
    dark = (70, 60, 90, 255)
    d.poly([(75, 40), (40, 18), (30, 30), (18, 26), (14, 44), (6, 46), (30, 56), (60, 56)], dark)
    d.poly([(75, 40), (110, 18), (120, 30), (132, 26), (136, 44), (144, 46), (120, 56), (90, 56)], dark)
    d.ellipse(75, 48, 14, 16, dark)
    d.poly([(66, 36), (68, 24), (73, 34)], dark); d.poly([(77, 34), (82, 24), (84, 36)], dark)
    d.dot(70, 44, 2, (255, 230, 90, 255)); d.dot(80, 44, 2, (255, 230, 90, 255))
    d.line([(70, 54), (73, 58), (75, 54), (77, 58), (80, 54)], 1)
    return d.done()


def troll(seed):
    d = Doodle(150, 170, seed)
    hide = (150, 170, 110, 255)
    d.line([(52, 136), (48, 160)], 6); d.line([(96, 136), (100, 160)], 6)
    d.ellipse(74, 104, 46, 42, hide)
    d.poly([(40, 120), (108, 120), (100, 140), (48, 140)], (140, 90, 60, 255))
    d.ellipse(74, 52, 20, 18, hide)
    d.dot(67, 50, 2.2); d.dot(81, 50, 2.2)
    d.line([(64, 60), (74, 64), (84, 60)], 2)
    d.poly([(66, 62), (69, 70), (72, 63)], WHITE, 1)
    d.line([(116, 96), (132, 70)], 5)
    d.poly([(126, 76), (124, 30), (140, 22), (146, 70)], (150, 105, 70, 255))
    d.line([(32, 96), (16, 120)], 5)
    return d.done()


def plant(seed):
    d = Doodle(130, 150, seed)
    green = (90, 180, 80, 255)
    d.line([(65, 146), (65, 104)], 5, (70, 130, 60, 255))
    d.poly([(65, 128), (36, 112), (24, 120), (46, 136)], green)
    d.poly([(65, 120), (98, 100), (110, 110), (86, 128)], green)
    d.ellipse(65, 70, 36, 34, (220, 80, 110, 255))
    d.poly([(36, 74), (94, 74), (86, 92), (44, 92)], (90, 20, 40, 255))
    for x in range(42, 92, 9):
        d.poly([(x, 74), (x + 4, 82), (x + 8, 74)], WHITE, 1)
    d.dot(52, 58, 2.4); d.dot(78, 58, 2.4)
    for a in range(0, 360, 60):
        x, y = 65 + 40 * math.cos(math.radians(a)), 70 + 38 * math.sin(math.radians(a))
        d.ellipse(x, y, 7, 7, (250, 200, 80, 255), 2)
    return d.done()


def crab(seed):
    d = Doodle(150, 110, seed)
    red = (230, 90, 60, 255)
    for s in (-1, 1):
        for k in range(3):
            x = 75 + s * (24 + k * 9)
            d.line([(x, 70), (x + s * 12, 92)], 3)
        d.line([(75 + s * 30, 58), (75 + s * 50, 40)], 3)
        cx = 75 + s * 56
        d.poly([(cx - 12, 40), (cx, 22), (cx + 12, 40), (cx, 50)], red)
        d.line([(cx, 22), (cx, 36)], 2)
    d.ellipse(75, 64, 32, 20, red)
    d.line([(66, 46), (64, 32)], 2); d.line([(84, 46), (86, 32)], 2)
    d.ellipse(64, 30, 5, 5, WHITE, 2); d.ellipse(86, 30, 5, 5, WHITE, 2)
    d.dot(64, 30, 1.6); d.dot(86, 30, 1.6)
    d.line([(66, 70), (75, 74), (84, 70)], 2)
    return d.done()


def imp(seed):
    d = Doodle(120, 150, seed)
    d.poly([(60, 10), (74, 40), (88, 26), (94, 70), (100, 60), (98, 104), (80, 130), (40, 130), (22, 104), (20, 60), (28, 70), (34, 30), (48, 46)],
           (250, 140, 40, 255))
    d.poly([(60, 50), (78, 80), (74, 112), (46, 112), (42, 80)], (255, 220, 90, 255), 2)
    d.dot(50, 84, 2.6); d.dot(70, 84, 2.6)
    d.line([(48, 98), (60, 104), (72, 98)], 2)
    d.line([(30, 120), (22, 140)], 3); d.line([(90, 120), (98, 140)], 3)
    return d.done()


def owl(seed):
    d = Doodle(120, 150, seed)
    brown = (150, 100, 60, 255)
    d.line([(20, 136), (100, 136)], 5, (110, 80, 50, 255))
    d.ellipse(60, 86, 34, 44, brown)
    d.poly([(30, 50), (34, 30), (46, 46)], brown); d.poly([(74, 46), (86, 30), (90, 50)], brown)
    d.ellipse(46, 66, 13, 13, WHITE); d.ellipse(74, 66, 13, 13, WHITE)
    d.dot(48, 66, 4); d.dot(72, 66, 4)
    d.poly([(56, 78), (64, 78), (60, 88)], (250, 190, 60, 255), 2)
    d.ellipse(60, 108, 18, 16, (220, 190, 140, 255), 2)
    for x in (50, 70):
        d.line([(x, 128), (x - 3, 136)], 2); d.line([(x, 128), (x + 3, 136)], 2)
    return d.done()


def skeleton(seed):
    d = Doodle(120, 170, seed)
    bone = (240, 236, 220, 255)
    d.ellipse(58, 34, 20, 20, bone)
    d.ellipse(51, 33, 5, 6, INK, 1); d.ellipse(66, 33, 5, 6, INK, 1)
    d.poly([(48, 48), (68, 48), (66, 58), (50, 58)], bone)
    for x in (52, 57, 62):
        d.line([(x, 49), (x, 57)], 1)
    d.line([(58, 58), (58, 110)], 4)
    for y in (70, 80, 90):
        d.line([(44, y), (72, y)], 3)
    d.line([(58, 110), (44, 150)], 4); d.line([(58, 110), (72, 150)], 4)
    d.line([(46, 66), (30, 94)], 3); d.line([(70, 66), (88, 84)], 3)
    d.line([(88, 84), (108, 40)], 4, (170, 180, 200, 255)); d.line([(80, 76), (96, 90)], 4)
    return d.done()


def cactus(seed):
    d = Doodle(120, 160, seed)
    g = (110, 170, 90, 255)
    d.poly([(48, 150), (48, 30), (60, 18), (72, 30), (72, 150)], g)
    d.poly([(48, 90), (24, 90), (20, 54), (32, 50), (34, 76), (48, 76)], g)
    d.poly([(72, 100), (96, 100), (100, 70), (88, 66), (86, 86), (72, 86)], g)
    d.dot(55, 50, 2.2); d.dot(66, 50, 2.2)
    d.line([(54, 60), (60, 64), (66, 60)], 2)
    d.ellipse(60, 16, 6, 5, (250, 120, 160, 255), 2)
    d.line([(30, 152), (90, 152)], 3, (180, 140, 90, 255))
    return d.done()


def tumbleweed(seed):
    d = Doodle(110, 110, seed)
    r = d.r
    d.ellipse(55, 55, 38, 36, (210, 170, 110, 255), 2)
    for _ in range(14):
        a0, a1 = r.uniform(0, 6.28), r.uniform(0, 6.28)
        d.line([(55 + 34 * math.cos(a0), 55 + 32 * math.sin(a0)), (55 + 12 * math.cos(a1), 55 + 12 * math.sin(a1)),
                (55 + 34 * math.cos(a1 + 1), 55 + 32 * math.sin(a1 + 1))], 2)
    for y in (96, 104):
        d.line([(8, y), (30, y)], 2)
    return d.done()


def lantern_ghost(seed):
    d = Doodle(110, 160, seed)
    d.line([(55, 8), (55, 24)], 3)
    d.poly([(40, 24), (70, 24), (64, 34), (46, 34)], (90, 70, 60, 255))
    d.poly([(36, 34), (74, 34), (78, 96), (32, 96)], (255, 214, 110, 255))
    d.poly([(48, 52), (55, 40), (62, 52), (60, 70), (50, 70)], (250, 130, 40, 255), 2)
    d.poly([(32, 96), (78, 96), (72, 108), (38, 108)], (90, 70, 60, 255))
    d.dot(47, 80, 2.4); d.dot(63, 80, 2.4)
    d.line([(50, 88), (55, 91), (60, 88)], 2)
    for k, x in enumerate((40, 55, 70)):
        d.line([(x, 108), (x + (k - 1) * 4, 136), (x + (k - 1) * 8, 150)], 2, (120, 120, 160, 255))
    return d.done()


def cowboy(seed):
    im = wizard((200, 120, 60, 255), "wave", seed, hat=(150, 100, 60, 255))
    return im


def notes(seed):
    d = Doodle(100, 80, seed)
    for (x, y) in ((22, 50), (58, 38)):
        d.ellipse(x, y + 18, 7, 5, INK, 1)
        d.line([(x + 6, y + 16), (x + 6, y - 14)], 2)
    d.line([(28, 22), (64, 10)], 3)
    d.ellipse(86, 58, 6, 4, INK, 1); d.line([(91, 57), (91, 34), (98, 40)], 2)
    return d.done()


def question(seed):
    d = Doodle(70, 90, seed)
    for (x, y, s) in ((24, 30, 1.0), (50, 22, 0.8)):
        d.line([(x - 8 * s, y - 6 * s), (x, y - 14 * s), (x + 8 * s, y - 6 * s), (x, y + 2 * s), (x, y + 10 * s)], 3)
        d.dot(x, y + 20 * s, 2.2)
    return d.done()


def bubble(seed):
    d = Doodle(130, 90, seed)
    d.ellipse(66, 38, 54, 28, WHITE)
    d.poly([(40, 60), (30, 84), (56, 64)], WHITE, 2)
    for x in (40, 58, 76, 94):
        d.ellipse(x, 38, 6, 7, WHITE, 2)
    return d.done()


def sparkle(seed):
    d = Doodle(90, 90, seed)
    d.star(45, 45, 22, (255, 230, 120, 255))
    d.star(76, 18, 8, (255, 255, 200, 255))
    d.star(14, 74, 7, (255, 255, 200, 255))
    return d.done()


SCHOOL = {
    "Fire": (240, 100, 50, 255), "Ice": (120, 190, 250, 255), "Storm": (160, 110, 230, 255), "Life": (100, 200, 90, 255),
    "Death": (110, 100, 130, 255), "Myth": (240, 200, 70, 255), "Balance": (210, 150, 100, 255),
}


def doodles():
    out = {}
    poses = ["cast", "run", "scared", "wave", "read", "cast", "run"]
    for i, (name, col) in enumerate(SCHOOL.items()):
        out["Wizard" + name] = wizard(col, poses[i], 100 + i)
    out["GhostPink"] = ghost((250, 170, 200, 255), 11)
    out["GhostBlue"] = ghost((190, 220, 250, 255), 12)
    out["Rat"] = rat(13)
    out["Bat"] = bat(14)
    out["Troll"] = troll(15)
    out["Plant"] = plant(16)
    out["Crab"] = crab(17)
    out["Imp"] = imp(18)
    out["Owl"] = owl(19)
    out["Skeleton"] = skeleton(20)
    out["Cactus"] = cactus(21)
    out["Tumbleweed"] = tumbleweed(22)
    out["LanternGhost"] = lantern_ghost(23)
    out["Cowboy"] = cowboy(24)
    out["Notes"] = notes(25)
    out["Question"] = question(26)
    out["Bubble"] = bubble(27)
    out["Sparkle"] = sparkle(28)
    return out


# ---------------------------------------------------------------- parchment, skies, frame, tag, scroll, marker

def value_noise(w, h, cell, seed):
    r = rng_for(seed)
    gw, gh = w // cell + 2, h // cell + 2
    g = r.random((gh, gw))
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((gw * cell, gh * cell), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32)[:h, :w] / 255.0


def parchment(w, h, seed=5):
    n = value_noise(w, h, 90, seed) * 0.5 + value_noise(w, h, 30, seed + 1) * 0.3 + value_noise(w, h, 8, seed + 2) * 0.2
    base = np.array([222, 200, 152], np.float32)
    dark = np.array([176, 140, 92], np.float32)
    t = np.clip((n - 0.35) * 1.4, 0, 1)[..., None]
    col = base * (1 - t * 0.45) + dark * (t * 0.45)
    # edges darker (old paper), a couple of stains
    yy, xx = np.mgrid[0:h, 0:w]
    ex = np.minimum(xx, w - 1 - xx) / (w * 0.5)
    ey = np.minimum(yy, h - 1 - yy) / (h * 0.5)
    edge = np.clip(1 - np.minimum(ex, ey) * 6, 0, 1) ** 2
    col = col * (1 - edge[..., None] * 0.28)
    r = rng_for(seed + 3)
    for _ in range(5):
        cx, cy, rad = r.uniform(0, w), r.uniform(0, h), r.uniform(40, 120)
        dd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / rad
        ring = np.exp(-((dd - 1) * 6) ** 2) * 0.045 + np.clip(1 - dd, 0, 1) * 0.025
        col = col * (1 - ring[..., None])
    # fibres
    fib = value_noise(w, h, 3, seed + 9)
    col = col * (0.97 + fib[..., None] * 0.05)
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img)
    # a double border line inside the edge: dark brown, then a thin red one
    m = 14
    d.rectangle([m, m, w - 1 - m, h - 1 - m], outline=(92, 52, 30, 255), width=4)
    d.rectangle([m + 9, m + 9, w - 1 - m - 9, h - 1 - m - 9], outline=(150, 46, 38, 255), width=2)
    return img


def sky(w, h, top, bottom, seed, stars=True, clouds=(255, 255, 255)):
    yy = np.linspace(0, 1, h)[:, None, None]
    col = np.array(top, np.float32) * (1 - yy) + np.array(bottom, np.float32) * yy
    col = np.repeat(col, w, axis=1)
    n = value_noise(w, h, 140, seed) * 0.6 + value_noise(w, h, 45, seed + 1) * 0.4
    cl = np.clip((n - 0.55) * 3.2, 0, 1)[..., None] * 0.35
    col = col * (1 - cl) + np.array(clouds, np.float32) * cl
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img)
    r = rng_for(seed + 5)
    if stars:
        for _ in range(140):
            x, y = r.uniform(0, w), r.uniform(0, h * 0.6)
            s = r.uniform(0.6, 2.2)
            d.ellipse([x - s, y - s, x + s, y + s], fill=(255, 250, 230, int(r.uniform(120, 255))))
        for _ in range(10):
            x, y, s = r.uniform(0, w), r.uniform(0, h * 0.5), r.uniform(4, 7)
            for a in (0, 90):
                dx, dy = s * math.cos(math.radians(a)), s * math.sin(math.radians(a))
                d.line([x - dx, y - dy, x + dx, y + dy], fill=(255, 255, 240, 230), width=2)
    # soft vignette and the same inner border as the parchment
    yy2, xx2 = np.mgrid[0:h, 0:w]
    ex = np.minimum(xx2, w - 1 - xx2) / (w * 0.5)
    ey = np.minimum(yy2, h - 1 - yy2) / (h * 0.5)
    edge = np.clip(1 - np.minimum(ex, ey) * 5, 0, 1) ** 2
    a = np.asarray(img, np.float32)
    a[..., :3] *= (1 - edge[..., None] * 0.35)
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    m = 14
    d.rectangle([m, m, w - 1 - m, h - 1 - m], outline=(92, 52, 30, 255), width=4)
    d.rectangle([m + 9, m + 9, w - 1 - m - 9, h - 1 - m - 9], outline=(150, 46, 38, 255), width=2)
    return img


def frame(size=96, border=34):
    """A 9-slice wooden frame: dark carved wood, a gold bead and a red inner lip (the map sits inside)."""
    s = size * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    b = border * 4
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=36, fill=(96, 58, 34, 255))
    n = value_noise(s, s, 6, 3)
    a = np.asarray(img, np.float32)
    a[..., :3] *= (0.8 + n[..., None] * 0.35)
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=36, outline=(52, 30, 18, 255), width=10)
    d.rounded_rectangle([40, 40, s - 41, s - 41], radius=24, outline=(236, 190, 90, 255), width=8)
    d.rectangle([b - 22, b - 22, s - b + 21, s - b + 21], fill=(150, 46, 38, 255))
    d.rectangle([b - 8, b - 8, s - b + 7, s - b + 7], fill=(52, 30, 18, 255))
    d.rectangle([b, b, s - b - 1, s - b - 1], fill=(0, 0, 0, 0))
    return img.resize((size, size), Image.LANCZOS)


def tag(w=64, h=40):
    """A 9-slice parchment label with a dark outline (map place names)."""
    s = 4
    img = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([6, 10, w * s - 7, h * s - 5], fill=(60, 40, 25, 110))
    d.rectangle([2, 2, w * s - 11, h * s - 13], fill=(244, 228, 186, 255), outline=(70, 46, 28, 255), width=10)
    d.line([(20, h * s - 30), (w * s - 30, h * s - 30)], fill=(214, 190, 140, 255), width=6)
    return img.resize((w, h), Image.LANCZOS)


def title_scroll(w=560, h=96):
    s = 2
    img = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    W, H = w * s, h * s
    paper, edge, shade = (240, 222, 176, 255), (80, 52, 30, 255), (200, 172, 120, 255)
    # rolled ends
    for x0 in (0, W - 70):
        d.rounded_rectangle([x0, 16, x0 + 70, H - 16], radius=30, fill=shade, outline=edge, width=6)
        d.ellipse([x0 + 18, 30, x0 + 52, 64], fill=(230, 205, 150, 255), outline=edge, width=5)
    d.polygon([(54, 26), (W - 54, 26), (W - 40, H // 2), (W - 54, H - 26), (54, H - 26), (40, H // 2)], fill=paper, outline=edge)
    d.line([(54, 26), (W - 54, 26)], fill=edge, width=6)
    d.line([(54, H - 26), (W - 54, H - 26)], fill=edge, width=6)
    d.line([(80, H - 44), (W - 80, H - 44)], fill=shade, width=5)
    return img.resize((w, h), Image.LANCZOS)


def here_marker(size=64):
    """You are here: a red arrowhead with a gold rim (points up; the map turns it to your heading)."""
    s = 4
    img = Image.new("RGBA", (size * s, size * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size * s / 2
    pts = [(c, 12), (size * s - 40, size * s - 30), (c, size * s - 70), (40, size * s - 30)]
    d.polygon([(x + 8, y + 10) for x, y in pts], fill=(30, 20, 10, 120))
    d.polygon(pts, fill=(230, 50, 40, 255), outline=(255, 214, 90, 255))
    d.line(pts + [pts[0]], fill=(255, 214, 90, 255), width=14, joint="curve")
    d.line(pts + [pts[0]], fill=(60, 20, 10, 255), width=4, joint="curve")
    return img.resize((size, size), Image.LANCZOS)


def main(out):
    os.makedirs(out, exist_ok=True)
    os.makedirs(os.path.join(out, "Doodles"), exist_ok=True)
    W, H = 1200, 800
    parchment(W, H).save(os.path.join(out, "MapParchment.png"))
    sky(W, H, (52, 40, 110), (150, 120, 200), 41).save(os.path.join(out, "MapSky1.png"))
    sky(W, H, (60, 50, 120), (245, 170, 110), 42, stars=True, clouds=(255, 220, 190)).save(os.path.join(out, "MapSky2.png"))
    frame().save(os.path.join(out, "MapFrame.png"))
    tag().save(os.path.join(out, "MapTag.png"))
    title_scroll().save(os.path.join(out, "MapTitle.png"))
    here_marker().save(os.path.join(out, "MapHere.png"))
    for name, im in doodles().items():
        im.save(os.path.join(out, "Doodles", "Doodle" + name + ".png"))
    print("wrote map art to", out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/WizardsVRC/Textures/Maps")
