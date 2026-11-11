"""Sunfire Wyrm, simulated: the whole cast as the film camera shoots it, drawn here without Unity.

    python3 Tools/preview_sunfire_wyrm.py gif OUT.gif [one|four|two]     the whole spell (the default: four enemies);
                                                                         OUT.mp4 makes a video instead
    python3 Tools/preview_sunfire_wyrm.py sheet OUT.png [case [EVERY START END]]   a contact sheet of stills
    python3 Tools/preview_sunfire_wyrm.py frame T OUT.png [case]         one frame, T seconds into the cast
    python3 Tools/preview_sunfire_wyrm.py card OUT.png                   the spell's card: its picture as the builder
                                                                         paints it (WizardsVRCSceneBuilder.CardArt.cs),
                                                                         on the Fire card frame

What is the game's own, and what is only this preview's:
  - the dragon's flight, its timing and its poses come from Tools/sunfire_wyrm_flight.py, which Scripts/SunfireWyrm.cs
    follows line for line; the film shots (Shots below) are SpellCinematic's (ChargeShot and the wyrm's own);
  - the dragon is the game's meshes and skin (Tools/make_sunfire_wyrm.py), the duel ring, the rune and the spark,
    flame and glow sprites are the game's textures, and the cast's times are the staging formula's;
  - the charge round the caster, the bursts and rings, the dark and the particles follow SpellCinematic's numbers but
    are redrawn by a small software renderer, so they are close, not exact; the wizards, the monsters and the
    scenery round the ring are stand-ins (the game has avatars, its own monsters and whichever street the ring is in).
"""
import math
import os
import shutil
import subprocess
import sys
import tempfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sunfire_wyrm_flight as flight      # noqa: E402
import make_sunfire_wyrm as model         # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Textures')
W, H, FPS = 480, 270, 15
GIF_FPS = 12.5
FOV = 50.0                                  # the film camera's (WizardsVRCSceneBuilder: FilmCamera)
FOCAL = (H * 0.5) / math.tan(math.radians(FOV * 0.5))
NEAR = 0.08
UP = np.array([0.0, 1.0, 0.0])
FIRE = np.array([1.0, 0.42, 0.1])           # GlyphColors[Fire]
FIRE_EDGE = np.array([1.0, 0.92, 0.55])     # GlyphEdgeColors[Fire]
RNG = np.random.default_rng(7)

norm, ease = flight.norm, flight.ease


def load(path, flip=True):
    """A texture as floats (h, w, 4), row 0 at the bottom (v = 0)."""
    a = np.asarray(Image.open(os.path.join(TEX, path)).convert('RGBA'), dtype=np.float32) / 255.0
    return a[::-1].copy() if flip else a


def roty(v, deg):
    """Quaternion.Euler(0, deg, 0) * v."""
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return np.array([v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c])


def euler(x, y, z):
    """Quaternion.Euler(x, y, z) as a matrix (columns are the turned axes): Unity turns about Z, then X, then Y."""
    x, y, z = (math.radians(a) for a in (x, y, z))
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return ry @ rx @ rz


def look_rotation(fwd, up=UP):
    """Quaternion.LookRotation as a matrix whose columns are right, up, forward."""
    f = norm(np.array(fwd, dtype=float))
    r = np.cross(up, f)
    if np.linalg.norm(r) < 1e-5:
        r = np.cross(np.array([0.0, 0.0, 1.0]), f)
    r = norm(r)
    return np.stack([r, np.cross(f, r), f], axis=1)


# ---------------------------------------------------------------------- the renderer

class Cam:
    def set(self, pos, look):
        f = norm(look - pos)
        r = norm(np.cross(UP, f))
        self.pos, self.M, self.fwd = np.array(pos, dtype=float), np.stack([r, np.cross(f, r), f]), f

    def to_cam(self, p):
        return (np.asarray(p, dtype=float) - self.pos) @ self.M.T


class FB:
    def __init__(self):
        self.col = np.zeros((H, W, 3), dtype=np.float32)
        self.z = np.full((H, W), 1e9, dtype=np.float32)


SUN = norm(-(euler(50.0, -30.0, 0.0) @ np.array([0.0, 0.0, 1.0])))     # the scene's directional light
SUN_COL = np.array([1.0, 0.95, 0.84]) * 0.9


def shade(normals, centres, lights):
    """PSX-style light on each face: sun, sky and ground, a cool bounce on the shadowed side, and point lights."""
    ndl = normals @ SUN
    sky = np.array([0.5, 0.56, 0.7])
    ground = np.array([0.3, 0.28, 0.32])
    amb = ground + (sky - ground) * (0.5 + 0.5 * normals[:, 1:2])
    light = amb + SUN_COL * np.maximum(0.0, ndl)[:, None]
    light += np.array([0.32, 0.24, 0.62]) * 0.35 * np.clip(0.5 - 0.5 * ndl, 0.0, 1.0)[:, None]
    for pos, col, rng in lights:
        d = pos - centres
        dist = np.linalg.norm(d, axis=1)
        att = np.clip(1.0 - (dist / rng) ** 2, 0.0, 1.0) ** 2
        light += 0.8 * col * (att * np.maximum(0.0, np.sum(d * normals, axis=1) / np.maximum(dist, 1e-4)))[:, None]
    return np.clip(light, 0.0, 2.0)


def draw_tris(fb, cam, V, UV=None, tex=None, color=(1, 1, 1), lights=(), lit=True, emission=(0, 0, 0), mode='solid',
              cull=True, cutout=False, gain=1.0, paint=0.0):
    """Triangles V (n, 3, 3) in the world. 'solid' writes colour and depth; 'add' adds light where nothing is nearer.
    With a texture: UV (n, 3, 2); 'cutout' drops its clear pixels; 'gain' brightens an additive one. 'color' may be
    one colour or one per triangle. 'paint' varies each face a little, as the PSX shader's vertex paint does."""
    if len(V) == 0:
        return
    V = np.asarray(V, dtype=float)
    C = cam.to_cam(V)
    n_w = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
    ln = np.linalg.norm(n_w, axis=1)
    ok = ln > 1e-12
    n_w = n_w / np.maximum(ln, 1e-12)[:, None]
    n_c = np.cross(C[:, 1] - C[:, 0], C[:, 2] - C[:, 0])
    facing = np.sum(n_c * C[:, 0], axis=1) < 0
    ok &= (C[:, :, 2].max(axis=1) > NEAR)
    if cull:
        ok &= facing
    col = np.broadcast_to(np.asarray(color, dtype=float), (len(V), 3)).copy()
    if lit:
        nn = np.where(facing[:, None], n_w, -n_w)
        col *= shade(nn, V.mean(axis=1), lights)
    if paint > 0.0:
        h = np.abs(np.sin(V[:, 0].sum(axis=1) * 12.9898 + V[:, 1].sum(axis=1) * 78.233)) * 43758.5453
        col *= (1.0 + (h - np.floor(h) - 0.5) * paint)[:, None]
    em = np.asarray(emission, dtype=float)
    for i in np.nonzero(ok)[0]:
        c = C[i]
        uv = UV[i] if UV is not None else None
        if c[:, 2].min() > NEAR:
            _fill(fb, c, uv, col[i], em, tex, mode, cutout, gain)
            continue
        # cut off at the near plane
        poly, puv = [], []
        for a in range(3):
            b = (a + 1) % 3
            za, zb = c[a, 2], c[b, 2]
            if za > NEAR:
                poly.append(c[a])
                puv.append(uv[a] if uv is not None else None)
            if (za > NEAR) != (zb > NEAR):
                f = (NEAR - za) / (zb - za)
                poly.append(c[a] + (c[b] - c[a]) * f)
                puv.append(uv[a] + (uv[b] - uv[a]) * f if uv is not None else None)
        for k in range(1, len(poly) - 1):
            cc = np.array([poly[0], poly[k], poly[k + 1]])
            cu = np.array([puv[0], puv[k], puv[k + 1]]) if uv is not None else None
            _fill(fb, cc, cu, col[i], em, tex, mode, cutout, gain)


def _fill(fb, c, uv, col, em, tex, mode, cutout, gain):
    z = c[:, 2]
    x = c[:, 0] / z * FOCAL + W * 0.5
    y = H * 0.5 - c[:, 1] / z * FOCAL
    x0, x1 = max(int(math.floor(x.min())), 0), min(int(math.ceil(x.max())), W - 1)
    y0, y1 = max(int(math.floor(y.min())), 0), min(int(math.ceil(y.max())), H - 1)
    if x0 > x1 or y0 > y1:
        return
    area = (x[1] - x[0]) * (y[2] - y[0]) - (x[2] - x[0]) * (y[1] - y[0])
    if abs(area) < 1e-9:
        return
    px = np.arange(x0, x1 + 1, dtype=np.float32) + 0.5
    py = (np.arange(y0, y1 + 1, dtype=np.float32) + 0.5)[:, None]
    w0 = ((x[1] - px) * (y[2] - py) - (x[2] - px) * (y[1] - py)) / area
    w1 = ((x[2] - px) * (y[0] - py) - (x[0] - px) * (y[2] - py)) / area
    w2 = 1.0 - w0 - w1
    m = (w0 >= -1e-5) & (w1 >= -1e-5) & (w2 >= -1e-5)
    if not m.any():
        return
    iz = w0 / z[0] + w1 / z[1] + w2 / z[2]
    zz = 1.0 / np.maximum(iz, 1e-9)
    sl = (slice(y0, y1 + 1), slice(x0, x1 + 1))
    m &= zz < fb.z[sl]
    if not m.any():
        return
    if tex is not None:
        u = (w0 * (uv[0, 0] / z[0]) + w1 * (uv[1, 0] / z[1]) + w2 * (uv[2, 0] / z[2])) * zz
        v = (w0 * (uv[0, 1] / z[0]) + w1 * (uv[1, 1] / z[1]) + w2 * (uv[2, 1] / z[2])) * zz
        th, tw = tex.shape[:2]
        if tw <= 32:
            # (the small tiling ones repeat)
            tx = np.floor(np.clip(u, -1e4, 1e4) * tw).astype(np.int32) % tw
            ty = np.floor(np.clip(v, -1e4, 1e4) * th).astype(np.int32) % th
        else:
            tx = np.clip(u * tw, 0, tw - 1).astype(np.int32)
            ty = np.clip(v * th, 0, th - 1).astype(np.int32)
        texel = tex[ty, tx]
        if cutout or mode == 'add':
            m &= texel[..., 3] > (0.5 if cutout else 0.004)
            if not m.any():
                return
        rgb = texel[..., :3]
    else:
        rgb = None
    if mode == 'solid':
        out = (rgb[m] * col + em) if rgb is not None else (col + em)
        fb.col[sl][m] = out
        fb.z[sl][m] = zz[m]
    else:
        if rgb is not None:
            add = rgb[m] * texel[..., 3][m][:, None] * (col * gain)
        else:
            add = col * gain
        fb.col[sl][m] += add


def sprite(fb, pc, half_w, half_h, col, axis=(0.0, 1.0), flame=False, res=7.0):
    """One additive sprite at camera-space point 'pc', half_w by half_h metres, its long axis along 'axis' on the
    screen (x right, y up): a soft blob, or a teardrop for a flame; chunky, like the game's small point-filtered ones."""
    z = pc[2]
    if z <= NEAR * 2:
        return
    sx, sy = pc[0] / z * FOCAL + W * 0.5, H * 0.5 - pc[1] / z * FOCAL
    a, b = half_w * FOCAL / z, half_h * FOCAL / z
    if a < 0.35 and b < 0.35:
        ix, iy = int(sx), int(sy)
        if 0 <= ix < W and 0 <= iy < H and z < fb.z[iy, ix] + 0.15:
            fb.col[iy, ix] += np.asarray(col) * min(1.0, (a + b) * 1.6)
        return
    r = max(a, b)
    x0, x1 = max(int(sx - r), 0), min(int(sx + r) + 1, W - 1)
    y0, y1 = max(int(sy - r), 0), min(int(sy + r) + 1, H - 1)
    if x0 > x1 or y0 > y1:
        return
    px = np.arange(x0, x1 + 1, dtype=np.float32) + 0.5 - sx
    py = sy - (np.arange(y0, y1 + 1, dtype=np.float32) + 0.5)[:, None]
    ax, ay = axis
    u = (px * ay - py * ax) / max(a, 0.3)          # across
    v = (px * ax + py * ay) / max(b, 0.3)          # along
    if res > 0:
        u = (np.floor(u * res) + 0.5) / res
        v = (np.floor(v * res) + 0.5) / res
    if flame:
        # a teardrop: fat at its root (v = -1), drawn out to a point (v = 1)
        wide = np.clip(0.95 - 0.5 * (v + 1.0), 0.08, 1.0)
        d = (u / wide) ** 2 + np.where(v < -0.6, ((v + 0.6) / 0.4) ** 2, 0.0)
        al = np.clip(1.0 - d, 0.0, 1.0) * np.clip(1.15 - 0.55 * (v + 1.0), 0.0, 1.0) * (np.abs(v) <= 1.0)
    else:
        al = np.clip(1.0 - (u * u + v * v), 0.0, 1.0) ** 1.4
    sl = (slice(y0, y1 + 1), slice(x0, x1 + 1))
    al = al * (fb.z[sl] + 0.15 > z)
    fb.col[sl] += al[..., None] * np.asarray(col, dtype=np.float32)


def quad_xz(centre, size, yaw=0.0):
    """A flat quad lying on the floor, 'size' across, turned 'yaw' degrees: (2, 3, 3) triangles and UVs."""
    h = size * 0.5
    pts = [roty(np.array(p), yaw) + centre for p in ((-h, 0, -h), (h, 0, -h), (h, 0, h), (-h, 0, h))]
    V = np.array([[pts[0], pts[2], pts[1]], [pts[0], pts[3], pts[2]]])
    UV = np.array([[(0, 0), (1, 1), (1, 0)], [(0, 0), (0, 1), (1, 1)]], dtype=float)
    return V, UV


def quad_facing(centre, rot, size):
    """A quad in the plane of rot's X and Z axes (FlatQuad under a turned pivot)."""
    h = size * 0.5
    pts = [rot @ np.array(p) + centre for p in ((-h, 0, -h), (h, 0, -h), (h, 0, h), (-h, 0, h))]
    V = np.array([[pts[0], pts[2], pts[1]], [pts[0], pts[3], pts[2]]])
    UV = np.array([[(0, 0), (1, 1), (1, 0)], [(0, 0), (0, 1), (1, 1)]], dtype=float)
    return V, UV


def place(v, pos, rot, scale):
    """Mesh vertices (n, 3, 3) scaled, turned by 'rot' (columns: right, up, forward) and moved to 'pos'."""
    return (v * np.asarray(scale, dtype=float)) @ rot.T + pos


# ---------------------------------------------------------------------- stand-in figures and scenery (preview only)

def lathe(profile, sides=7):
    tris = []
    for i in range(len(profile) - 1):
        (r0, y0), (r1, y1) = profile[i], profile[i + 1]
        for s in range(sides):
            a0, a1 = 2 * math.pi * s / sides, 2 * math.pi * (s + 1) / sides
            p = [np.array([math.cos(a) * r, y, math.sin(a) * r]) for r, y in ((r0, y0), (r1, y1)) for a in (a0, a1)]
            mid = np.array([math.cos((a0 + a1) / 2), 0.0, math.sin((a0 + a1) / 2)])
            want = mid * (y1 - y0) + UP * (r0 - r1)
            for tri in ((p[0], p[1], p[3]), (p[0], p[3], p[2])):
                if np.linalg.norm(np.cross(tri[1] - tri[0], tri[2] - tri[0])) < 1e-9:
                    continue
                tris.append(tri if np.dot(np.cross(tri[1] - tri[0], tri[2] - tri[0]), want) > 0 else (tri[0], tri[2], tri[1]))
    return np.array(tris)


def box(lo, hi):
    lo, hi = np.array(lo, dtype=float), np.array(hi, dtype=float)
    c = [np.array([x, y, z]) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    mid = (lo + hi) / 2
    tris = []
    for f in faces:
        for tri in ((c[f[0]], c[f[1]], c[f[2]]), (c[f[0]], c[f[2]], c[f[3]])):
            want = (tri[0] + tri[1] + tri[2]) / 3 - mid
            tris.append(tri if np.dot(np.cross(tri[1] - tri[0], tri[2] - tri[0]), want) > 0 else (tri[0], tri[2], tri[1]))
    return np.array(tris)


def wizard(robe, hat):
    """A robed figure 1.75 m tall facing +Z: (triangles, a colour for each)."""
    parts = [(lathe([(0.0, 0.0), (0.34, 0.0), (0.3, 0.5), (0.2, 1.05), (0.24, 1.3), (0.0, 1.36)]), robe),
             (lathe([(0.0, 1.3), (0.13, 1.36), (0.15, 1.5), (0.11, 1.62), (0.0, 1.66)]) , (0.93, 0.74, 0.6)),
             (lathe([(0.0, 1.56), (0.3, 1.58), (0.16, 1.66), (0.05, 1.98), (0.0, 2.02)]), hat)]
    for sx in (-1, 1):
        arm = box((0.2 * sx - 0.06, 1.0, -0.06), (0.2 * sx + 0.06, 1.3, 0.34))
        parts.append((arm, robe))
    return np.concatenate([p for p, _ in parts]), np.concatenate([np.tile(np.array(c, dtype=float), (len(p), 1)) for p, c in parts])


def monster(hue):
    """A squat horned brute 1.9 m tall facing +Z."""
    skin, dark, horn, eye = hue, tuple(x * 0.55 for x in hue), (0.9, 0.85, 0.7), (1.0, 0.9, 0.3)
    parts = [(lathe([(0.0, 0.0), (0.5, 0.05), (0.62, 0.6), (0.55, 1.15), (0.3, 1.45), (0.0, 1.5)], 8), skin),
             (lathe([(0.0, 1.32), (0.26, 1.4), (0.3, 1.62), (0.2, 1.82), (0.0, 1.88)], 7), skin),
             (box((-0.2, 1.5, 0.22), (-0.08, 1.6, 0.32)), eye), (box((0.08, 1.5, 0.22), (0.2, 1.6, 0.32)), eye)]
    for sx in (-1, 1):
        parts.append((lathe([(0.0, 0.0), (0.07, 0.0), (0.0, 0.34)], 4) + np.array([0.2 * sx, 1.78, 0.0]), horn))
        parts.append((box((0.56 * sx - 0.13, 0.5, -0.13), (0.56 * sx + 0.13, 1.25, 0.2)), dark))
    return np.concatenate([p for p, _ in parts]), np.concatenate([np.tile(np.array(c, dtype=float), (len(p), 1)) for p, c in parts])


def scenery():
    """A ring of plain buildings and trees well back from the duel circle, for something to move against."""
    tris, cols = [], []
    rng = np.random.default_rng(3)
    for k in range(22):
        a = k / 22.0 * 2 * math.pi + rng.uniform(-0.08, 0.08)
        r = rng.uniform(21.0, 30.0)
        c = np.array([math.sin(a) * r, 0.0, math.cos(a) * r])
        if k % 3 == 2:
            trunk = lathe([(0.0, 0.0), (0.35, 0.0), (0.3, 2.2), (0.0, 2.2)], 5) + c
            crown = lathe([(0.0, 1.8), (2.2, 2.4), (1.6, 4.4), (0.0, 6.0)], 6) + c
            tris += [trunk, crown]
            cols += [np.tile([0.36, 0.26, 0.2], (len(trunk), 1)), np.tile([0.22, 0.5, 0.26] if k % 2 else [0.3, 0.56, 0.24], (len(crown), 1))]
            continue
        w, d, h = rng.uniform(3.5, 6.0), rng.uniform(3.5, 5.5), rng.uniform(4.0, 7.5)
        wall = box((-w, 0, -d), (w, h, d))
        roof = lathe([(0.0, h + 3.0), (max(w, d) * 1.5, h - 0.2), (0.0, h - 0.2)], 4)
        rot = euler(0, math.degrees(a) + 45, 0)
        tris += [wall @ euler(0, math.degrees(a), 0).T + c, roof @ rot.T + c]
        tone = rng.uniform(0.75, 1.0)
        cols += [np.tile(np.array([0.8, 0.74, 0.62]) * tone, (len(wall), 1)), np.tile(np.array([0.5, 0.3, 0.52]) * tone, (len(roof), 1))]
    return np.concatenate(tris), np.concatenate(cols)


def cobbles():
    """A small tiling cobble texture for the ground round the ring."""
    rng = np.random.default_rng(11)
    t = np.zeros((32, 32, 4), dtype=np.float32)
    t[..., 3] = 1.0
    for y in range(32):
        for x in range(32):
            row = y // 8
            xx = (x + (4 if row % 2 else 0)) % 8
            edge = y % 8 == 0 or xx == 0
            t[y, x, :3] = (0.36, 0.35, 0.38) if edge else (0.52 + 0.06 * ((x // 8 + row) % 3), 0.5, 0.5)
    t[..., :3] *= 1.0 + (rng.random((32, 32, 1), dtype=np.float32) - 0.5) * 0.12
    return t


# ---------------------------------------------------------------------- particles

class PS:
    """One of the rig's emit-on-demand particle systems (WizardsVRCSceneBuilder.CinePS): what Emit does, and how its
    particles live. Sizes and speeds scale with the emitter's scale, as the game's do (Hierarchy scaling)."""

    def __init__(self, life, speed, size, gravity=0.0, shape='sphere', radius=0.1, angle=0.0, thick=1.0, rise=(0.0, 0.0),
                 orbit=0.0, inward=0.0, stretch=0.0, flame=False, grow=(1.0, 0.2), colors=None, power=1.0, tint=FIRE):
        self.life, self.speed, self.size, self.gravity = life, speed, size, gravity
        self.shape, self.radius, self.angle, self.thick = shape, radius, angle, thick
        self.rise, self.orbit, self.inward, self.stretch, self.flame, self.grow = rise, orbit, inward, stretch, flame, grow
        self.colors, self.power, self.tint = colors, power, np.asarray(tint, dtype=float)
        self.p = np.zeros((0, 3))
        self.v = np.zeros((0, 3))
        self.age = np.zeros(0)
        self.ttl = np.zeros(0)
        self.sz = np.zeros(0)
        self.up = np.zeros(0)         # each particle's own steady climb (velocity over lifetime)
        self.c = np.zeros((0, 3))     # the centre it orbits

    def emit(self, pos, n, scale=1.0, rot=None):
        if n <= 0:
            return
        rot = np.eye(3) if rot is None else rot
        u = RNG.random((n, 3))
        if self.shape == 'sphere':
            d = RNG.normal(size=(n, 3))
            d /= np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-6)
            off = d * (self.radius * u[:, :1] ** (1 / 3.0))
        elif self.shape == 'cone':
            # from a disc, along the emitter's forward within 'angle'
            a = u[:, 0] * 2 * math.pi
            r = self.radius * np.sqrt(u[:, 1])
            off = np.stack([np.cos(a) * r, np.sin(a) * r, np.zeros(n)], axis=1)
            spread = math.tan(math.radians(self.angle)) * (r / max(self.radius, 1e-6))
            d = np.stack([np.cos(a) * spread, np.sin(a) * spread, np.ones(n)], axis=1)
            d /= np.linalg.norm(d, axis=1, keepdims=True)
        else:
            # a circle in the emitter's XY plane, its particles heading outwards
            a = u[:, 0] * 2 * math.pi
            r = self.radius * (1.0 - self.thick * u[:, 1])
            d = np.stack([np.cos(a), np.sin(a), np.zeros(n)], axis=1)
            off = d * r[:, None]
        off = (off @ rot.T) * scale
        d = d @ rot.T
        sp = (self.speed[0] + (self.speed[1] - self.speed[0]) * u[:, 2]) * scale
        self.p = np.concatenate([self.p, pos + off])
        self.v = np.concatenate([self.v, d * sp[:, None]])
        self.age = np.concatenate([self.age, np.zeros(n)])
        self.ttl = np.concatenate([self.ttl, self.life[0] + (self.life[1] - self.life[0]) * RNG.random(n)])
        self.sz = np.concatenate([self.sz, (self.size[0] + (self.size[1] - self.size[0]) * RNG.random(n)) * scale])
        self.up = np.concatenate([self.up, (self.rise[0] + (self.rise[1] - self.rise[0]) * RNG.random(n)) * scale])
        self.c = np.concatenate([self.c, np.tile(np.asarray(pos, dtype=float), (n, 1))])

    def step(self, dt):
        if len(self.age) == 0:
            return
        self.age += dt
        keep = self.age < self.ttl
        if not keep.all():
            self.p, self.v, self.age, self.ttl, self.sz, self.up, self.c = (a[keep] for a in (self.p, self.v, self.age, self.ttl, self.sz, self.up, self.c))
        self.v[:, 1] -= 9.81 * self.gravity * dt
        self.p += self.v * dt
        self.p[:, 1] += self.up * dt
        if self.orbit or self.inward:
            rel = self.p - self.c
            ang = self.orbit * dt
            c, s = math.cos(ang), math.sin(ang)
            x, z = rel[:, 0] * c + rel[:, 2] * s, -rel[:, 0] * s + rel[:, 2] * c
            flat = np.sqrt(x * x + z * z)
            k = np.maximum(0.0, flat + self.inward * dt) / np.maximum(flat, 1e-6)
            self.p[:, 0], self.p[:, 2] = self.c[:, 0] + x * k, self.c[:, 2] + z * k

    def draw(self, fb, cam):
        if len(self.age) == 0:
            return
        pc = cam.to_cam(self.p)
        f = np.clip(self.age / self.ttl, 0.0, 1.0)
        size = self.sz * (self.grow[0] + (self.grow[1] - self.grow[0]) * f)
        alpha = np.where(f < 0.6, 1.0 - f / 3.0, 0.8 * (1.0 - (f - 0.6) / 0.4))
        vel = self.v.copy()
        vel[:, 1] += self.up
        vc = vel @ cam.M.T
        for i in np.nonzero(pc[:, 2] > NEAR * 2)[0]:
            if self.colors is not None:
                col = self.colors(f[i])
            else:
                col = self.tint
            hw = size[i] * 0.5
            hh = hw
            axis = (0.0, 1.0)
            if self.stretch > 0.0 or self.flame:
                sx, sy = vc[i, 0], vc[i, 1]
                sp = math.hypot(sx, sy)
                if sp > 1e-3:
                    axis = (sx / sp, sy / sp)
                hh = hw * max(1.0, self.stretch)
            sprite(fb, pc[i], hw, hh, np.asarray(col) * (alpha[i] * self.power), axis, self.flame)


def fire_ramp(f):
    """White-hot, gold, orange, red, out: a flame's colours over its life."""
    if f < 0.15:
        return np.array([1.0, 0.95, 0.75])
    if f < 0.4:
        k = (f - 0.15) / 0.25
        return np.array([1.0, 0.95 - 0.35 * k, 0.75 - 0.6 * k])
    if f < 0.75:
        k = (f - 0.4) / 0.35
        return np.array([1.0 - 0.1 * k, 0.6 - 0.35 * k, 0.15 - 0.1 * k])
    k = (f - 0.75) / 0.25
    return np.array([0.9 - 0.5 * k, 0.25 - 0.2 * k, 0.05])


# ---------------------------------------------------------------------- the dragon

MESHES = model.build_all()
SKIN = np.asarray(Image.open(model.SKIN_OUT).convert('RGBA'), dtype=np.float32)[::-1].copy() / 255.0
WYRM_EMISSION = (0.1, 0.035, 0.01)
JAW_OPEN = 38.0                              # degrees, wide open (SunfireWyrm.jawOpenDegrees)


def wyrm_parts(pose, size):
    """The dragon's meshes in the world for a pose from the flight model: (triangles, UVs)."""
    V, UV = [], []
    P, D, U, hs = pose['head']
    if hs > 0.01:
        rot = look_rotation(D, U)
        V.append(place(MESHES['Head'][0], P, rot, size * hs))
        UV.append(MESHES['Head'][1])
        hinge = P + rot @ (np.array(model.JAW_HINGE) * size * hs)
        V.append(place(MESHES['Jaw'][0], hinge, rot @ euler(pose['jaw'] * JAW_OPEN, 0.0, 0.0), size * hs))
        UV.append(MESHES['Jaw'][1])
    for i, sg in enumerate(pose['segs']):
        if sg is None or sg[3] <= 0.01:
            continue
        p, d, u, k = sg
        name = 'Tail' if i == model.SEGMENTS - 1 else 'SegmentLegs' if i in model.LEG_SEGMENTS else 'Segment'
        across, along = model.segment_scale(i)
        V.append(place(MESHES[name][0], p, look_rotation(d, u), np.array([across, across, along]) * (size * k)))
        UV.append(MESHES[name][1])
    if not V:
        return np.zeros((0, 3, 3)), np.zeros((0, 3, 2))
    return np.concatenate(V), np.concatenate(UV)


def draw_wyrm(fb, cam, pose, size, lights):
    V, UV = wyrm_parts(pose, size)
    draw_tris(fb, cam, V, UV, SKIN, lights=lights, emission=WYRM_EMISSION, paint=0.15)


# ---------------------------------------------------------------------- the cast (SpellCinematic, for this spell)

GLYPH = load('CastDraw/GlyphDrawFire.png')
RING_TEX = load('SpellRing.png')
RING_LAYERS = [load('DuelRingBase.png'), load('DuelRingSigil.png'), load('DuelRingRunes.png')]
COBBLE = cobbles()
SCENERY = scenery()
SPARK = FIRE + (1.0 - FIRE) * 0.25
RING_COL = FIRE + (1.0 - FIRE) * 0.2
AURA_PROFILE = [(0.0, 0.0), (0.3, 0.02), (0.46, 0.16), (0.5, 0.34), (0.44, 0.52), (0.32, 0.68), (0.2, 0.82), (0.09, 0.93), (0.0, 1.0)]


def _finer(profile, n):
    """The profile with n steps to each of its own (the game lights the shell per pixel; this draws it per face)."""
    out = []
    for i in range(len(profile) - 1):
        (r0, y0), (r1, y1) = profile[i], profile[i + 1]
        for k in range(n):
            out.append((r0 + (r1 - r0) * k / n, y0 + (y1 - y0) * k / n))
    return out + [profile[-1]]


AURA_MESH = lathe(_finer(AURA_PROFILE, 3), 30)
UPWARD = euler(-90.0, 0.0, 0.0)             # an emitter turned to face up (its circle lies flat)
DOWNWARD = euler(90.0, 0.0, 0.0)


def glyph_image(reveal, flare, fade, time):
    """The drawn rune (Shaders/GlyphDraw.shader, the fire style) as an additive texture."""
    order, glow, ink = GLYPH[..., 0], GLYPH[..., 1], GLYPH[..., 3]
    on = (ink > 0.5) & (order <= reveal)
    tip = np.clip(1.0 - (reveal - order) / 0.06, 0.0, 1.0)
    b = glow * (1.0 + 1.6 * tip) * (0.85 + 0.3 * np.sin(time * 23.0 + order * 31.0))
    col = FIRE[None, None, :] + (FIRE_EDGE - FIRE)[None, None, :] * tip[..., None]
    out = np.zeros(GLYPH.shape, dtype=np.float32)
    out[..., :3] = col * (b * 1.3 * fade * (1.0 + flare))[..., None]
    out[..., 3] = on.astype(np.float32)
    return out


class Stage:
    """The spell, beat by beat, as SpellCinematic stages it for a wizard casting Sunfire Wyrm (Fire, 7 pips, an area
    spell; its recipe: rings turning round the caster as they charge, a nova when it lands, blown apart as it leaves)."""

    def __init__(self, case='four'):
        w = flight.case(case)
        self.w = w
        T = w.times
        self.tCast, self.tSound, self.tUnl, self.tDisp, self.tExp, self.tBar = T['cast'], T['sound'], T['unl'], T['disp'], T['exp'], T['bar']
        self.tStrike, self.tExit, self.tAfter, self.tEnd = T['strike'], T['exit'], T['after'], T['end']
        self.pips, self.vis, self.tier, self.body, self.flip = 7, 13, 4, w.body, w.flip
        self.scale = (0.85 + 0.17 * self.vis) * max(1.0, self.body * 0.8)
        self.O, self.fwd = w.O, w.F
        self.right = np.cross(UP, self.fwd)
        self.aim = w.G.copy()
        self.targets = w.targets_sorted
        self.aoe = len(self.targets) > 1
        self.color = FIRE
        self.t = 0.0
        # the rig's particle systems (WizardsVRCSceneBuilder.SpellCast.cs: CinePS(...))
        S = dict(tint=SPARK)
        self.ps = {
            'chargeAura': PS((0.45, 0.55), (-4.6, -3.8), (0.08, 0.2), radius=2.2, stretch=2.0, **S),
            'auraFlames': PS((0.45, 0.75), (0.0, 0.0), (0.5, 0.9), shape='circle', radius=0.42, thick=0.5, rise=(2.6, 3.9), flame=True, stretch=1.35,
                             tint=FIRE + (1 - FIRE) * 0.15, power=0.3),
            'auraStreaks': PS((0.22, 0.4), (0.0, 0.0), (0.04, 0.09), shape='circle', radius=1.5, thick=0.6, rise=(8.0, 13.0), stretch=6.0, **S),
            'portalBurst': PS((0.6, 1.0), (1.5, 4.5), (0.08, 0.22), gravity=-0.2, radius=0.5, **S),
            'debris': PS((1.6, 2.6), (0.3, 1.1), (0.05, 0.13), gravity=-0.12, shape='cone', radius=2.6, angle=4.0, **S),
            'rimSparks': PS((0.35, 0.6), (0.2, 0.9), (0.07, 0.16), gravity=-0.15, radius=0.1, **S),
            'arcs': PS((0.12, 0.24), (9.0, 17.0), (0.05, 0.1), radius=0.25, stretch=5.0, **S),
            'vortex': PS((1.4, 2.2), (0.0, 0.0), (0.06, 0.15), shape='circle', radius=1.9, thick=0.15, rise=(1.9, 1.9), orbit=2.6, inward=-0.35, **S),
            'skyfall': PS((0.7, 1.0), (8.0, 12.0), (0.07, 0.16), gravity=0.4, shape='cone', radius=3.2, angle=2.0, stretch=4.0, **S),
            'shockwave': PS((0.6, 0.9), (7.0, 10.0), (0.12, 0.3), shape='circle', radius=0.4, thick=0.0, **S),
            'impactBurst': PS((0.4, 0.8), (2.0, 6.0), (0.1, 0.3), gravity=0.2, radius=0.4, **S),
            'shardBurst': PS((0.8, 1.4), (4.0, 8.0), (0.06, 0.14), gravity=1.2, radius=0.3, stretch=2.5, **S),
            'embers': PS((1.0, 1.8), (0.4, 1.4), (0.05, 0.13), gravity=-0.3, radius=0.8, **S),
            # the wyrm's own (WizardsVRCSceneBuilder.Wyrm.cs)
            'wyrmFlames': PS((0.4, 0.7), (0.2, 0.6), (0.5, 0.85), radius=0.12, rise=(1.2, 2.0), flame=True, stretch=1.7, colors=fire_ramp, power=0.6),
            'wyrmEmbers': PS((0.7, 1.3), (0.3, 1.2), (0.05, 0.12), gravity=-0.25, radius=0.15, stretch=2.0, tint=FIRE + (FIRE_EDGE - FIRE) * 0.5),
            'breath': PS((0.42, 0.56), (12.0, 16.0), (0.35, 0.6), shape='cone', radius=0.1, angle=9.0, flame=True, stretch=1.6, grow=(0.7, 2.8),
                         colors=fire_ramp, power=0.5),
            'breathCore': PS((0.25, 0.36), (18.0, 22.0), (0.2, 0.34), shape='cone', radius=0.05, angle=4.0, flame=True, stretch=2.2, grow=(0.8, 1.8),
                             colors=lambda f: np.array([1.0, 0.97 - 0.3 * f, 0.8 - 0.6 * f]), power=0.7),
        }
        self.acc = {}
        self.pulse = [None, None, None]     # the three shock rings: (at, when, size, len)
        self.pulseNext = 0
        self.runeAngle = self.rimAngle = 0.0
        self.nextBeat = self.tCast + 0.5
        self.nextArc = self.tCast + 0.4
        self.flareAt = self.arcFlash = self.kickAt = self.auraKick = self.showKick = -100.0
        self.auraLit = False
        self.cue = 3                         # (the film opens as the charge begins)
        self.afterDone = 0
        self.exitCue = 0
        self.shake = 0.0
        # the wyrm's cues
        self.wLaps = self.wPassed = 0
        self.wOut = self.wRested = self.wRoared = self.wLaunched = self.wBreathing = False
        self.wEmit = 0
        self.wLit = [False] * len(self.targets)
        self.wBurn = model.SEGMENTS - 1
        self.flinchAt = -100.0
        self.hitAt = -100.0
        # stand-ins
        self.caster = wizard((0.72, 0.16, 0.12), (0.45, 0.1, 0.1))
        self.ally = wizard((0.2, 0.36, 0.7), (0.14, 0.22, 0.5))
        hues = [(0.5, 0.36, 0.62), (0.36, 0.52, 0.42), (0.56, 0.42, 0.34), (0.42, 0.42, 0.62)]
        self.monsters = [monster(hues[k % 4]) for k in range(len(self.targets))]
        self.cam = Cam()

    # ---- helpers (SpellCinematic's)
    def emit(self, name, at, n, scale=1.0, rot=None):
        self.ps[name].emit(np.asarray(at, dtype=float), int(n), scale, rot)

    def rate(self, key, per_second, dt):
        """Whole particles due this step at 'per_second' (the game's '_xAcc += dt * rate; n = (int)acc')."""
        a = self.acc.get(key, 0.0) + dt * per_second
        n = int(a)
        self.acc[key] = a - n
        return n

    def Pulse(self, at, size, length):
        i = self.pulseNext % 3
        self.pulseNext += 1
        self.pulse[i] = (np.asarray(at, dtype=float), self.t, size, max(0.15, length))

    def Shock(self, at, count, scale):
        self.emit('shockwave', at, count, max(0.2, scale), UPWARD)

    def build(self, t):
        return min(1.0, max(0.0, (t - self.tCast) / max(0.1, self.tUnl - self.tCast)))

    def hit_for(self, k):
        t = self.targets[k % len(self.targets)]
        if not self.aoe:
            return self.aim - self.fwd * 0.35 + UP
        toward = norm(flight.flat(t - self.O))
        return t - toward * 0.35 + UP

    # ---- one step of the cast
    def step(self, t, dt):
        self.t = t
        w, O, up = self.w, self.O, UP
        scale, vis, tier, body, flip = self.scale, self.vis, self.tier, self.body, self.flip
        chest = O + up * (1.2 * body)
        build = self.build(t)
        winding = self.tCast <= t < self.tUnl
        # --- the rune turns
        spin = (40.0 + 8.0 * vis) * (0.5 + 1.5 * build if t < self.tUnl else 1.0)
        self.runeAngle += dt * spin * flip
        # --- the battle aura (AnimateAura)
        charge = max(0.1, self.tUnl - self.tCast)
        ignite = self.tCast + min(0.5, charge * 0.18)
        if self.tCast <= t < ignite:
            self.emit('chargeAura', chest, self.rate('aura', 50.0 + 16.0 * vis, dt), scale)
        if not self.auraLit and ignite <= t < self.tUnl:
            self.auraLit = True
            self.auraKick = self.arcFlash = self.kickAt = t
            self.Pulse(O, (3.0 + 0.6 * tier) * scale, 0.5)
            self.Shock(O + up * 0.12, 10 + 3 * vis, 0.5 + 0.2 * tier)
        lit = self.aura_lit(t, ignite)
        if lit > 0.001:
            tall, size, flare = self.aura_size(t)
            fit = tall / 2.57 * size
            rate = (38.0 + 9.0 * vis) * (0.55 + 0.6 * build + 1.2 * flare) if t < self.tUnl else (10.0 + 3.0 * vis) * lit
            self.emit('auraFlames', O + up * 0.05, self.rate('flame', rate, dt), fit, UPWARD)
            if t < self.tUnl:
                self.emit('auraStreaks', O + up * 0.05, self.rate('streak', (10.0 + 5.0 * vis) * (0.3 + build + flare), dt), fit, UPWARD)
        # --- the flourish and the tiers (AnimateCharge, ChargeTiers)
        if winding:
            crush = min(1.0, max(0.0, (t - (self.tUnl - 0.5)) / 0.5))
            self.emit('chargeAura', chest, self.rate('aura', (25.0 + 14.0 * vis) * 0.45 * (0.4 + build) * (1.0 + 3.0 * crush), dt), scale)
            self.emit('embers', O + up * 0.1, self.rate('ember', (30.0 + 10.0 * vis) * (0.5 + build) * 0.4, dt), scale)
            self.emit('debris', O + up * 0.05, self.rate('debris', (8.0 + 5.0 * vis) * (0.3 + build), dt), scale * (0.8 + 0.2 * tier), UPWARD)
            self.rimAngle += dt * (160.0 + 380.0 * build) * flip
            radius = 1.2 * scale * ease((t - self.tCast) / 0.45)
            for _ in range(self.rate('rim', 30.0 + 30.0 * build, dt)):
                for k in range(4):
                    a = math.radians(self.rimAngle + k * 90.0)
                    self.emit('rimSparks', O + (self.right * math.cos(a) + self.fwd * math.sin(a)) * radius + up * 0.12, 1)
            if t >= self.nextBeat:
                self.nextBeat = t + (0.95 + (0.32 - 0.95) * build)
                self.auraKick = t
                self.Pulse(O, (2.4 + 0.5 * tier) * scale * (0.7 + 0.5 * build), 0.6)
                self.Shock(O + up * 0.12, int((8 + 2 * vis) * (0.4 + build)), 0.4 + 0.15 * tier)
            self.emit('vortex', O + up * 0.1, self.rate('vortex', (18.0 + 6.0 * vis) * (0.3 + build), dt), scale, UPWARD)
            if t >= self.nextArc:
                self.nextArc = t + (0.6 + (0.1 - 0.6) * build) * (0.6 + 0.8 * RNG.random())
                self.emit('arcs', chest, 4 + 2 * tier, scale)
                self.arcFlash = t
            self.emit('skyfall', O + up * self.sky_height(), self.rate('sky', 20.0 + 60.0 * build, dt), scale, DOWNWARD)
        # --- the release (Unleash)
        if self.cue == 3 and t >= self.tUnl:
            self.cue = 4
            self.flareAt = self.kickAt = t
            self.Shock(O + up * 0.12, 40 + 26 * tier + 6 * vis, 0.7 + 0.3 * tier)
            self.Shock(O + up * 0.12, 30 + 5 * vis, 1.2 + 0.3 * tier)
            self.Pulse(O, (4.5 + 1.6 * tier) * max(1.0, body * 0.8), 0.75)
            self.emit('portalBurst', chest, 25 + 20 * tier)
            self.emit('embers', O + up * 0.2, 16 * tier, scale)
            self.emit('arcs', chest, 10 + 6 * tier, scale)
        # --- the wyrm (SpellCinematic.AnimateWyrm and SunfireWyrm)
        self.wyrm(t, dt)
        # --- the finisher's hit and what follows (Strike, AnimateImpact)
        if self.cue == 4 and t >= self.tStrike:
            self.cue = 5
            self.kickAt = self.hitAt = t
            big = self.scale if self.aoe else self.impact_size()
            for k in range(len(self.targets)):
                self.emit('impactBurst', self.hit_for(k), int((35 + 22 * vis) * 1.6), big)
            self.impactAt = np.array([self.targets[0][0], self.aim[1], self.targets[0][2]])
            self.Shock(self.impactAt + up * 0.12, 30 + 20 * tier, 0.6 + 0.3 * tier)
            self.emit('arcs', self.hit_for(0), 10 + 5 * tier, big)
            self.emit('skyfall', self.impactAt + up * self.sky_height(), 140, scale * 1.3, DOWNWARD)
        if t >= self.tStrike:
            r = t - self.tStrike
            big = self.impact_size()
            if self.afterDone < 5 and r >= 0.16 * (self.afterDone + 1):
                self.afterDone += 1
                self.kickAt = t
                ang = math.radians(self.afterDone * 137.5 + 40.0 * flip)
                rad = 0.9 + 0.45 * self.afterDone
                at = self.impactAt + (self.right * math.cos(ang) + self.fwd * math.sin(ang)) * rad + up * (0.6 + 0.25 * self.afterDone)
                self.emit('impactBurst', at, 20 + 8 * vis, big * 0.6)
                self.Shock(np.array([at[0], self.aim[1] + 0.12, at[2]]), 12 + 3 * vis, 0.3 + 0.05 * vis)
            if t < self.tAfter:
                fall = 1.0 - min(1.0, r / max(0.5, self.tAfter - self.tStrike))
                self.emit('embers', self.impactAt + up * 0.6, self.rate('ember', (18.0 + 8.0 * vis) * 1.7 * fall, dt), big)
        # --- the exit: blown apart (EXIT_SCATTER)
        if self.exitCue == 0 and t >= self.tExit:
            self.exitCue = 1
            self.Shock(O + up * 0.12, 30 + 6 * vis, 0.6)
            self.Shock(self.impactAt + up * 0.12, 30 + 6 * vis, 0.6)
            self.emit('portalBurst', self.impactAt + up, 30)
        for p in self.ps.values():
            p.step(dt)

    def sky_height(self):
        return 8.0 + 1.5 * self.body

    def impact_size(self):
        return (0.85 + 0.17 * self.vis) * (1.25 if self.aoe else 1.0)

    def aura_lit(self, t, ignite):
        if ignite <= t < self.tUnl:
            return min(1.0, (t - ignite) / 0.1)
        if self.tUnl <= t < self.tStrike + 0.3:
            return (1.0 + (0.3 - 1.0) * min(1.0, (t - self.tUnl) / 0.4)) * (1.0 - min(1.0, max(0.0, (t - self.tStrike) / 0.3)))
        return 0.0

    def aura_size(self, t):
        build = self.build(t)
        tall = 2.1 * self.body + 0.2 + 0.27 * self.vis
        flare = min(1.0, max(0.0, (build - 0.82) / 0.18)) if t < self.tUnl else 1.0
        beat = math.exp(-max(0.0, t - self.auraKick) * 7.0)
        size = (0.82 + 0.3 * build) * (1.0 + 0.5 * flare * flare)
        if t >= self.tUnl:
            after = t - self.tUnl
            size *= 1.0 + 0.6 * (after / 0.3) if after < 0.3 else 1.6 + (0.55 - 1.6) * min(1.0, (after - 0.3) / 0.35)
        return tall, size * (1.0 + 0.16 * beat), flare

    # ---- the wyrm's part of the staging
    def wyrm(self, t, dt):
        w, O, up, scale, vis, tier = self.w, self.O, UP, self.scale, self.vis, self.tier
        if t < self.tUnl or t >= self.tAfter:
            return
        pose = w.pose(t)
        self.pose = pose
        size = w.size
        P, D, U, hs = pose['head']
        rot = look_rotation(D, U)
        mouth = P + rot @ (np.array(model.MOUTH) * size * hs)
        self.headPos = P
        if not self.wOut:
            # It bursts out of the caster: a gout of flame ahead of it.
            self.wOut = True
            self.emit('breath', O + up * (1.25 * self.body), 40, 0.5 * size, look_rotation(self.fwd + up * 0.2))
        # A ring of light under the caster each time it comes round.
        laps = w.laps(t)
        if laps > self.wLaps:
            self.wLaps = laps
            self.showKick = self.auraKick = t
            self.Pulse(O, (1.6 + 0.5 * laps) * scale, 0.55)
            self.Shock(O + up * 0.12, 10 + 3 * vis, 0.3 + 0.1 * laps)
        if not self.wRested and t >= w.t_rest:
            self.wRested = True
            self.kickAt = t
            self.Shock(O + up * 0.12, 30 + 4 * vis, 0.7)
        if not self.wRoared and t >= w.t_roar:
            # The display's peak: it roars.
            self.wRoared = True
            self.showKick = self.kickAt = self.arcFlash = self.auraKick = t
            self.Pulse(O, (3.0 + 0.6 * tier) * scale, 0.7)
            self.Shock(O + up * 0.12, 30 + 10 * vis, 0.8 + 0.2 * tier)
            self.emit('portalBurst', P, 20 + 6 * vis)
        if w.t_roar <= t < self.tExp:
            self.emit('breath', mouth, self.rate('roar', 70.0, dt), 0.32 * size, rot)
        if not self.wLaunched and t >= self.tBar:
            self.wLaunched = True
            self.kickAt = t
            self.Shock(O + up * 0.12, 40, 0.8)
        # Through each of them in turn: a burst on each as its head goes through.
        passed = w.passed(t)
        while self.wPassed < passed:
            k = self.wPassed
            self.wPassed += 1
            self.kickAt = t
            at = self.hit_for(k)
            big = self.impact_size() * 0.7
            self.emit('impactBurst', at, 14 + 6 * vis, big)
            self.emit('shardBurst', at, 8 + 3 * vis, big)
            self.emit('wyrmFlames', at, 10, 1.4 * size)
            self.Pulse(np.array([at[0], self.aim[1], at[2]]), 2.2 + 0.25 * vis, 0.5)
            self.Shock(np.array([at[0], self.aim[1] + 0.12, at[2]]), 14 + 5 * vis, 0.5)
            if k == 0:
                self.flinchAt = t
        # It breathes fire on them.
        if w.t_hover <= t < self.tExit:
            if not self.wBreathing:
                self.wBreathing = True
                self.kickAt = t
            aim = w.breath_aim(t)
            reach = float(np.linalg.norm(aim - mouth))
            brot = look_rotation(aim - mouth)
            self.emit('breath', mouth, self.rate('breath', 200.0, dt), reach / 5.8, brot)
            self.emit('breathCore', mouth, self.rate('core', 80.0, dt), reach / 5.4, brot)
            # Whoever the fire has reached burns until it stops.
            arrived = t - w.t_hover > 0.3
            for k, tg in enumerate(self.targets):
                if arrived and not self.wLit[k] and np.linalg.norm(flight.flat(aim - tg)) < 1.6:
                    self.wLit[k] = True
                if self.wLit[k]:
                    for _ in range(self.rate('burn%d' % k, 34.0, dt)):
                        off = np.array([RNG.uniform(-0.45, 0.45), 0.2 + RNG.random() * 1.5, RNG.uniform(-0.45, 0.45)])
                        self.emit('wyrmFlames', tg + off, 1, 1.25)
        # It is always shedding fire: flames off its back the whole way along, embers with them, more from its mane.
        segs = pose['segs']
        out = sum(1 for sg in segs if sg is not None and sg[3] > 0.3)
        n = self.rate('shed', 150.0 * size * (out + 1) / (model.SEGMENTS + 1.0), dt)
        for _ in range(n):
            self.wEmit = (self.wEmit + 7) % (model.SEGMENTS + 1)
            i = self.wEmit
            if i == model.SEGMENTS:
                if hs > 0.3:
                    self.emit('wyrmFlames', P + rot @ (np.array(model.MANE) * size), 1, size)
                continue
            sg = segs[i]
            if sg is None or sg[3] <= 0.3:
                continue
            r = model.segment_radius(i)
            self.emit('wyrmFlames', sg[0] + sg[2] * (r * size * 0.9), 1, size * (0.55 + 1.7 * r))
            if i % 3 == 0:
                self.emit('wyrmEmbers', sg[0], 1, size)
        if hs > 0.3:
            self.emit('wyrmFlames', P + rot @ (np.array(model.MANE) * size), self.rate('mane', 30.0, dt), size)
        # As it burns away, each length of it goes up in a puff.
        burn = w.burn(t)
        while self.wBurn >= 0 and w.burnt(self.wBurn, burn) < 0.5:
            sg = segs[self.wBurn]
            if sg is not None:
                self.emit('wyrmFlames', sg[0], 5, size * 1.2)
                self.emit('wyrmEmbers', sg[0], 3, size)
            self.wBurn -= 1

    # ---- the film (SpellCinematic.Film: ChargeShot, then the wyrm's shots)
    def cam_k(self):
        return max(1.0, self.body * 0.9) * (0.85 + 0.15 * self.scale / max(1.0, self.body * 0.8))

    def shot(self, t):
        O, up, F, body, tier, flip = self.O, UP, self.fwd, self.body, self.tier, self.flip
        k_body = self.cam_k()
        w = self.w
        if t < self.tUnl:
            b = self.build(t)
            if b < 0.3:
                k = b / 0.3
                pos = O + roty(F * (8.5 * k_body), (-40.0 + 30.0 * k) * flip) + up * (5.2 - 1.2 * k)
                look = O + up * (2.2 * body)
            elif b < 0.5:
                k = (b - 0.3) / 0.2
                pos = O + roty(F * (3.4 * k_body), (150.0 + 25.0 * k) * flip) + up * 0.35
                look = O + up * (3.2 + 2.2 * k)
            else:
                k = min(1.0, (b - 0.5) / 0.5)
                pos = O + roty(F * ((4.2 - 1.4 * k) * k_body), (35.0 + 70.0 * k) * flip) + up * ((0.55 + 1.1 * (1.0 - k)) * body)
                look = O + up * ((1.25 + 0.3 * k) * body)
            self.shake = (0.003 * tier + 0.012 * (tier - 2)) * b * b + 0.02 * (0.4 + 0.2 * tier) * math.exp(-max(0.0, t - self.auraKick) * 8.0)
            return pos, look
        return self.wyrm_shot(t)

    def wyrm_shot(self, t):
        """The wyrm's own shots (SpellCinematic.WyrmShot)."""
        O, up, F, body, tier = self.O, UP, self.fwd, self.body, self.tier
        k_body = self.cam_k()
        w = self.w
        side0 = self.right * self.flip            # the side it first turns towards
        head = self.headPos
        self.shake = 0.0
        if t < self.tDisp:
            # Out of the caster: in front of them, off to the side it swings away from, so it comes out at the lens.
            k = (t - self.tUnl) / max(0.05, self.tDisp - self.tUnl)
            e = 1.0 - (1.0 - k) * (1.0 - k)
            pos = O + F * ((2.4 + 2.0 * e) * k_body) - side0 * (1.5 * k_body) + up * ((1.15 + 0.35 * e) * body)
            look = O + up * (1.3 * body)
            look = look + (head - look) * (0.35 + 0.3 * e)
            self.shake = 0.03 * (1.0 + 0.5 * tier) * math.exp(-(t - self.tUnl) * 5.0)
            return pos, look
        if t < self.tExp:
            k = (t - self.tDisp) / max(0.05, self.tExp - self.tDisp)
            if k < 0.5:
                # The twirls: wide, drifting round against them, so its coils cross the picture as they climb.
                e = k / 0.5
                a = math.radians(-20.0 + 60.0 * e)
                pos = O + (F * math.cos(a) - side0 * math.sin(a)) * ((5.2 - 0.4 * e) * k_body) + up * ((1.2 + 0.7 * e) * body + 0.3 * e)
                look = O + up * (1.45 * body + 0.6 * e)
            elif k < 0.8:
                # Down behind them and up: from in front and to one side, low, the caster before it.
                e = ease((k - 0.5) / 0.3)
                pos = O + F * ((5.2 - 0.8 * e) * k_body) - side0 * ((2.6 - 0.6 * e) * k_body) + up * ((1.3 - 0.3 * e) * body)
                a0 = O + up * (1.7 * body)
                a1 = O + up * (1.5 * body)
                a1 = a1 + (w.rest - a1) * 0.6
                look = a0 + (a1 - a0) * e
            else:
                # At rest over their shoulder, and the roar: from low before the caster, looking up at them both.
                roar = min(1.0, max(0.0, (t - w.t_roar) / 0.3))
                pos = O + F * ((3.4 - 0.5 * roar) * k_body) - side0 * (1.1 * k_body) + up * (0.75 * body)
                look = O + up * (1.5 * body)
                look = look + (w.rest - look) * 0.6
                if t >= w.t_roar:
                    self.shake = 0.012
            self.shake += 0.02 * (0.5 + 0.25 * tier) * math.exp(-max(0.0, t - self.showKick) * 6.0)
            return pos, look
        toward, G = w.toward, w.G
        dist = float(np.linalg.norm(flight.flat(G - O)))
        spread = max(abs(float(np.dot(tg - O, w.side))) for tg in self.targets)
        if spread < 0.5:
            # One target: side on, the caster at one end of the picture and the target at the other, the loop over it in full.
            mid = O + toward * (0.5 * dist)
            wide_pos = mid - w.side * (0.75 * dist + 2.2) - toward * 0.8 + up * 2.6
            wide_look = mid + up * 1.9
        else:
            # Several: from high behind the caster, the whole of the far side in the picture.
            wide_pos = O - toward * (4.6 + 0.43 * spread) + up * (3.2 + 0.5 * spread)
            wide_look = O + toward * (0.5 * dist + 1.1) + up * 0.6
        if t < w.t_hover - 0.1:
            # It goes, through them and round: the wide shot, following it a little.
            follow = 0.2 + 0.25 * ease((t - self.tBar) / max(0.2, w.t_hover - self.tBar))
            self.shake = 0.02 * math.exp(-max(0.0, t - self.kickAt) * 6.0)
            return wide_pos, wide_look + (head - wide_look) * follow
        if t < self.tStrike:
            # The fire: from beyond whoever it burns, looking back past them at it and the caster under it.
            H = w.hover
            aim = w.breath_aim(t)
            if spread < 0.5:
                big = max(1.0, w.chest * 0.8)
                pos = self.targets[0] + (toward * 1.6 - w.side * 2.6) * big + up * 0.6
                look = H + (aim - H) * 0.35
            else:
                pos = O + toward * (0.55 * dist) + w.side * (0.9 * spread + 3.0) + up * 3.4
                look = H + (aim - H) * 0.3
            self.shake = 0.01 + 0.02 * math.exp(-max(0.0, t - self.kickAt) * 6.0)
            return pos, look
        r = t - self.tStrike
        hold = max(0.3, self.tExit - self.tStrike)
        if r < hold:
            # It lands: wide again, every one of them burning, shaken.
            self.shake = 0.035 * (1.0 + 0.6 * self.pips) * math.exp(-r * 6.0) * 1.6 + 0.03 * math.exp(-max(0.0, t - self.kickAt) * 8.0)
            return wide_pos + up * 0.4 - toward * 0.6, wide_look
        # Wide, turning slowly round the ring as it settles.
        k = r - hold
        mid = (O + self.aim) * 0.5
        radius = max(6.0, float(np.linalg.norm(self.aim - O)) * 0.9)
        pos = mid + roty(self.right * (radius * self.flip) - F * 1.5, 14.0 * k * self.flip) + up * 7.0
        return pos, mid + up * 0.8

    # ---- drawing a frame
    def render(self, t):
        fb, cam, O, up = FB(), self.cam, self.O, UP
        pos, look = self.shot(t)
        if self.shake > 1e-4:
            pos = pos + np.array([math.sin(t * 71.0), math.sin(t * 53.0 + 1.3), math.sin(t * 61.0 + 2.1)]) * self.shake
        cam.set(pos, look)
        # the sky
        rows = (H * 0.5 - (np.arange(H) + 0.5)) / FOCAL
        elev = (cam.fwd[1] + rows * cam.M[1][1]) / np.sqrt(1.0 + rows * rows)
        f = np.clip(elev * 1.8 + 0.1, 0.0, 1.0)[:, None]
        fb.col[:] = (np.array([0.78, 0.86, 0.95]) * (1.0 - f) + np.array([0.3, 0.5, 0.86]) * f)[:, None, :]
        # the school's light (AnimateLight): round the caster as they charge, with the wyrm's head, then on the hit
        peak = 1.4 + 0.35 * self.vis
        if t < self.tUnl:
            lp = O + up * (1.6 * self.body)
            li = peak * (ease(self.build(t)) + 0.5 * math.exp(-max(0.0, t - self.arcFlash) * 9.0))
        elif t < self.tStrike:
            lp = self.headPos
            li = peak * (1.0 + 1.2 * math.exp(-max(0.0, t - self.kickAt) * 4.0))
        else:
            lp = self.impactAt + up * 1.4
            li = peak * 4.0 * math.exp(-(t - self.tStrike) * 2.5)
        lights = [(lp, FIRE * min(li, 9.0) * 0.3, 16.0)]
        # the ground, the duel ring, the scenery, everyone in the fight
        g = 45.0
        gv = np.array([[(-g, 0, -g), (g, 0, g), (g, 0, -g)], [(-g, 0, -g), (-g, 0, g), (g, 0, g)]], dtype=float)
        guv = gv[:, :, [0, 2]] / 2.5
        draw_tris(fb, cam, gv, guv, COBBLE, color=(0.82, 0.78, 0.72), lights=lights)
        for i, layer in enumerate(RING_LAYERS):
            V, UV = quad_xz(np.array([0.0, 0.14 + 0.015 * min(i, 1) + 0.002 * i, 0.0]), 18.6)
            draw_tris(fb, cam, V, UV, layer, lights=lights, cutout=True)
        draw_tris(fb, cam, SCENERY[0], color=SCENERY[1], lights=[], paint=0.12)
        seat_y = 0.19
        mesh, cols = self.caster
        draw_tris(fb, cam, place(mesh, O + up * seat_y, look_rotation(self.fwd), 1.0), color=cols, lights=lights, paint=0.1)
        mesh, cols = self.ally
        a = flight.seat(202.5) + up * seat_y
        draw_tris(fb, cam, place(mesh, a, look_rotation(-flight.flat(a)), 1.0), color=cols, lights=lights, paint=0.1)
        for k, tg in enumerate(self.targets):
            mesh, cols = self.monsters[k]
            face = norm(flight.flat(O - tg))
            lean = 16.0 * math.exp(-max(0.0, t - self.flinchAt) * 5.0) * (1.0 if t >= self.flinchAt else 0.0)
            lean += 26.0 * math.exp(-max(0.0, t - self.hitAt) * 3.0) * (1.0 if t >= self.hitAt else 0.0)
            rot = look_rotation(face) @ euler(-lean, 0.0, 0.0)
            flash = 0.9 * math.exp(-max(0.0, t - self.hitAt) * 7.0) if t >= self.hitAt else 0.0
            draw_tris(fb, cam, place(mesh, tg + up * seat_y, rot, 1.0), color=cols, lights=lights, emission=(flash, flash, flash), paint=0.1)
        # the dark (AnimateStage): everything drawn so far dims; the magic after it doesn't
        if self.tCast <= t < self.tAfter:
            a = 0.7 * min(self.build(t) ** 0.7, 1.0 - ease((t - (self.tAfter - 0.8)) / 0.8))
            flash = math.exp(-max(0.0, t - self.kickAt) * 5.0)
            c = self.color * (0.12 + 0.45 * flash)
            a *= 1.0 - 0.4 * flash
            fb.col[:] = fb.col * (1.0 - a) + c * a
        # the wyrm
        if self.tUnl <= t < self.tAfter and hasattr(self, 'pose') and self.pose is not None:
            draw_wyrm(fb, cam, self.pose, self.w.size, lights)
        self.draw_magic(fb, cam, t)
        return self.finish(fb, t)

    def draw_magic(self, fb, cam, t):
        O, up, scale, vis, body, flip, tier = self.O, UP, self.scale, self.vis, self.body, self.flip, self.tier
        build = self.build(t)
        # the rune on the floor and its ring (AnimateRune)
        if self.tCast <= t < self.tAfter:
            opening = ease((t - self.tCast) / 0.45)
            shut = max(0.15, (self.tAfter - self.tExit) * 0.6)
            closing = 1.0 - ease((t - self.tExit) / shut)
            jump = 0.08 * math.exp(-max(0.0, t - self.flareAt) * 6.0)
            s = 2.6 * scale * min(opening, closing) * (1.0 + 0.03 * math.sin(t * 14.0) + jump)
            if s > 0.05:
                draw_n = min(1.0, (t - self.tCast) / max(0.3, (self.tUnl - self.tCast) * 0.6))
                flare = max(0.0, 2.5 - (t - self.flareAt) * 3.0) if t >= self.tUnl else 0.6 * math.exp(-max(0.0, t - self.arcFlash) * 9.0)
                V, UV = quad_xz(O + up * 0.2, s, self.runeAngle)
                draw_tris(fb, cam, V, UV, glyph_image(draw_n if draw_n < 1.0 else 1.05, min(flare, 1.2), 1.0, t), lit=False, mode='add', cull=False, gain=0.7)
                V, UV = quad_xz(O + up * 0.21, s * 1.3, -self.runeAngle * 1.2)
                draw_tris(fb, cam, V, UV, RING_TEX, color=RING_COL, lit=False, mode='add', cull=False, gain=0.7)
            # the rune in the sky (tier 4)
            opening = ease((t - self.tCast) / 0.8)
            closing = min(1.0, max(0.0, 1.0 - ease((t - (self.tStrike + 0.3)) / 0.9)))
            s = 7.5 * scale * min(opening, 0.6 + 0.4 * closing)
            draw_n = min(1.0, (t - self.tCast) / max(0.5, (self.tUnl - self.tCast) * 0.8))
            V, UV = quad_xz(O + up * self.sky_height(), s, -self.runeAngle * 0.5)
            draw_tris(fb, cam, V, UV, glyph_image(draw_n if draw_n < 1.0 else 1.05, max(0.0, 2.2 - max(0.0, t - self.kickAt) * 2.5), closing, t),
                      lit=False, mode='add', cull=False, gain=0.55)
        chest = O + up * (1.2 * body)
        # the rings turning round the caster (CHARGE_GYRO)
        if self.tCast <= t < self.tUnl + 0.3:
            crush = min(1.0, max(0.0, (t - (self.tUnl - 0.5)) / 0.5))
            s = (1.5 + 0.12 * vis) * body * ease((t - self.tCast) / 0.4) * (1.0 - ease((t - self.tUnl) / 0.3)) * (1.0 - 0.25 * crush)
            turn = 0.6 + 1.2 * build
            for rot in (euler(70.0, t * 200.0 * turn * flip, 0.0), euler(-55.0, -t * 160.0 * turn * flip, 35.0)):
                V, UV = quad_facing(chest, rot, s)
                draw_tris(fb, cam, V, UV, RING_TEX, color=RING_COL, lit=False, mode='add', cull=False, gain=0.8)
        # the column of light for the back half of the charge
        t0 = self.tCast + (self.tUnl - self.tCast) * 0.55
        if t0 <= t < self.tUnl + 0.45:
            grow, fade = ease((t - t0) / 0.5), 1.0 - ease((t - self.tUnl) / 0.45)
            wd = (1.1 + 0.12 * vis) * body * fade * (1.0 + 0.08 * math.sin(t * 22.0)) * 0.6
            ht = grow * (4.0 + 0.8 * vis)
            pc = cam.to_cam(np.array([O + up * (ht * (k + 0.5) / 10.0) for k in range(10)]))
            for p in pc:
                sprite(fb, p, wd * 0.5, ht / 10.0 * 0.8, self.color * 0.8 * 0.16, res=0)
        # the battle aura's shells
        ignite = self.tCast + min(0.5, (self.tUnl - self.tCast) * 0.18)
        lit = self.aura_lit(t, ignite)
        if lit > 0.001:
            tall, size, flare = self.aura_size(t)
            wide = tall * 0.6 + 0.3 * body
            beat = math.exp(-max(0.0, t - self.auraKick) * 7.0)
            power = lit * (0.75 + 0.55 * build + (0.9 * flare if t < self.tUnl else 0.0) + 0.5 * beat) * (0.85 + 0.15 * tier)
            shimmer = 1.0 + 0.05 * math.sin(t * 23.0)
            self.shell(fb, cam, t, O - up * 0.05, wide * size * shimmer, tall * size, power, self.color, self.color + (1 - self.color) * 0.8,
                       0.0, 8.0 + 8.0 * build + 6.0 * flare, 0.12 + 0.08 * build + 0.08 * flare, t * 140.0 * flip)
            self.shell(fb, cam, t, O - up * 0.05, wide * size * 1.3, tall * size * 1.18, power * 0.5, self.color + (1 - self.color) * 0.3, self.color,
                       2.6, 6.0 + 6.0 * build, 0.18 + 0.1 * build, -t * 95.0 * flip)
        # rings of light spreading on the floor (Pulse), and the nova's under each target after the hit
        for i, pl in enumerate(self.pulse):
            if pl is None or t >= self.tStrike:
                continue
            at, when, size, length = pl
            k = (t - when) / length
            if k >= 1.0:
                self.pulse[i] = None
                continue
            sz = 0.4 + (size - 0.4) * (1.0 - (1.0 - k) * (1.0 - k))
            V, UV = quad_xz(at + up * (0.22 + 0.02 * i), sz)
            draw_tris(fb, cam, V, UV, RING_TEX, color=RING_COL, lit=False, mode='add', cull=False, gain=0.8)
        if t >= self.tStrike:
            r = t - self.tStrike
            for i in range(3):
                if self.aoe and i >= len(self.targets):
                    continue
                rk = (r - 0.12 * i) / 0.65
                if rk < 0.0 or rk >= 1.0:
                    continue
                tg = self.targets[i % len(self.targets)]
                far = (6.0 + 0.45 * vis) * (1.4 if self.aoe else 1.0)
                sz = 0.6 + (far - 0.6) * (1.0 - (1.0 - rk) * (1.0 - rk))
                V, UV = quad_xz(np.array([tg[0], self.aim[1] + 0.22 + 0.02 * i, tg[2]]), sz)
                draw_tris(fb, cam, V, UV, RING_TEX, color=RING_COL, lit=False, mode='add', cull=False, gain=0.9)
        for p in self.ps.values():
            p.draw(fb, cam)

    def shell(self, fb, cam, t, foot, wide, tall, power, color, core, seed, flicker, wobble, spin):
        """One shell of the aura (Shaders/AuraFlame.shader): its vertices waving, bright at its edge, additive."""
        v = AURA_MESH.reshape(-1, 3).copy()
        h = np.clip(v[:, 1], 0.0, 1.0)
        ang = np.arctan2(v[:, 2], v[:, 0] + 1e-5)
        tt = t * flicker + seed
        lick = np.sin(ang * 3.0 + tt + v[:, 1] * 7.0) * 0.6 + np.sin(ang * 5.0 - tt * 1.7 + v[:, 1] * 11.0) * 0.4
        rise = h * h
        k = 1.0 + lick * wobble * (0.25 + rise)
        nrm = np.stack([v[:, 0], 0.35 * h + 1e-4, v[:, 2]], axis=1)
        v[:, 0] *= k
        v[:, 2] *= k
        v[:, 1] += np.maximum(0.0, lick) * wobble * 1.6 * rise
        rot = euler(0.0, spin, 0.0)
        world = ((v * np.array([wide, tall, wide])) @ rot.T + foot).reshape(-1, 3, 3)
        nrm = (nrm / np.linalg.norm(nrm, axis=1, keepdims=True)) @ rot.T
        nrm = nrm.reshape(-1, 3, 3).mean(axis=1)
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-6)
        mid = world.mean(axis=1)
        view = cam.pos - mid
        view /= np.maximum(np.linalg.norm(view, axis=1, keepdims=True), 1e-6)
        rim = (1.0 - np.abs(np.sum(nrm * view, axis=1))) ** 2.2
        hh = h.reshape(-1, 3).mean(axis=1)
        tip = 1.0 - np.clip((hh - 0.72) / 0.28, 0.0, 1.0) ** 2 * (3.0 - 2.0 * np.clip((hh - 0.72) / 0.28, 0.0, 1.0))
        mix = np.clip(hh / 0.55, 0.0, 1.0)
        col = (core[None, :] * (1.0 - mix[:, None]) + color[None, :] * mix[:, None]) * ((rim + 0.1) * tip * power * 0.3)[:, None]
        draw_tris(fb, cam, world, color=col, lit=False, mode='add', cull=False)

    def finish(self, fb, t):
        col = np.clip(fb.col, 0.0, 1.0)
        # 15-bit colour with the PS1's ordered dither (PSXCommon.cginc)
        col = np.floor(col * 31.0 + BAYER[np.arange(H)[:, None] % 4, np.arange(W)[None, :] % 4, None]) / 31.0
        img = Image.fromarray((np.clip(col, 0.0, 1.0) * 255.0).astype(np.uint8), 'RGB')
        d = ImageDraw.Draw(img)
        d.text((6, H - 13), 'Sunfire Wyrm  -  simulated preview, not a capture from the game', fill=(235, 225, 200))
        return img


BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) + 0.5) / 16.0


# ---------------------------------------------------------------------- its card (WizardsVRCSceneBuilder.CardArt.cs)

ART_W, ART_H = 116, 76
ART_INK = np.array([0.07, 0.05, 0.1])
# Fire's row of ArtPalette: sky top, sky bottom, ground, light (glow and rim), motes
ART_FIRE = [np.array(c) for c in ((0.30, 0.04, 0.05), (0.98, 0.48, 0.12), (0.16, 0.05, 0.05), (1.0, 0.75, 0.3), (1.0, 0.6, 0.2))]
ART_BAYER = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5]
CARD_ID = 111


class NetRandom:
    """System.Random with a seed, as Mono has it: so the card's backdrop, streaks and embers fall where the builder's do."""
    MBIG, MSEED = 2147483647, 161803398

    def __init__(self, seed):
        a = [0] * 56
        mj = self.MSEED - abs(seed)
        a[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            a[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = a[ii]
        for _ in range(4):
            for i in range(1, 56):
                a[i] -= a[1 + (i + 30) % 55]
                if a[i] < 0:
                    a[i] += self.MBIG
        self.a, self.inext, self.inextp = a, 0, 21

    def sample(self):
        self.inext = self.inext + 1 if self.inext + 1 < 56 else 1
        self.inextp = self.inextp + 1 if self.inextp + 1 < 56 else 1
        r = self.a[self.inext] - self.a[self.inextp]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self.a[self.inext] = r
        return r * (1.0 / self.MBIG)

    def next(self, lo, hi):
        return int(self.sample() * (hi - lo)) + lo


def _put(px, x, y, c, a=1.0):
    if 0 <= x < ART_W and 0 <= y < ART_H:
        a = min(1.0, max(0.0, a))
        px[y, x] = px[y, x] + (np.asarray(c, dtype=float) - px[y, x]) * a


def art_backdrop(rng):
    """PaintBackdrop for Fire: a lava glow along the horizon, jagged rocks with glowing seams. (ART_H, ART_W, 3), top down."""
    top, bot, ground = ART_FIRE[0], ART_FIRE[1], ART_FIRE[2]
    g = [rng.sample() for _ in range(64 * 64)]

    def v(x, y):
        x0, y0 = int(math.floor(x)), int(math.floor(y))
        fx, fy = x - x0, y - y0
        fx, fy = fx * fx * (3.0 - 2.0 * fx), fy * fy * (3.0 - 2.0 * fy)
        xa, ya = x0 % 64, y0 % 64
        xb, yb = (xa + 1) % 64, (ya + 1) % 64
        a, b, c, d = g[ya * 64 + xa], g[ya * 64 + xb], g[yb * 64 + xa], g[yb * 64 + xb]
        t, u = a + (b - a) * fx, c + (d - c) * fx
        return t + (u - t) * fy

    def fbm(x, y):
        return v(x, y) * 0.6 + v(x * 2.1, y * 2.1) * 0.3 + v(x * 4.3, y * 4.3) * 0.1

    px = np.zeros((ART_H, ART_W, 3))
    for y in range(ART_H):
        px[y, :] = top + (bot - top) * (y / (ART_H - 1.0)) ** 1.1
    horizon = int(ART_H * 0.72) + rng.next(-3, 4)
    for y in range(ART_H):
        a = min(1.0, max(0.0, 1.0 - abs(y - horizon) / 14.0)) * 0.5
        px[y, :] = px[y, :] + (np.array([1.0, 0.8, 0.35]) - px[y, :]) * a
    for x in range(ART_W):
        h = horizon + int((fbm(x / 9.0, 3.3) - 0.5) * 18.0)
        for y in range(h, ART_H):
            seam = y - h < 10 and abs(v(x / 5.0, y / 3.0) - 0.5) < 0.025
            _put(px, x, y, (1.0, 0.55, 0.15) if seam else ground)
        _put(px, x, h, (0.45, 0.12, 0.06))
    return px


def art_subject():
    """RenderArtSubject for Sunfire Wyrm: its posed dragon, turned to three-quarter view and pitched into its dive, lit
    from the upper left, framed to fill the picture. (ART_H, ART_W, 4) top down; alpha = the dragon."""
    global W, H, FOCAL, SUN, SUN_COL
    pose = model.card_pose()
    V, UV = [], []
    P, D, U, hs = pose['head']
    rot = look_rotation(D, U)
    V.append(place(MESHES['Head'][0], P, rot, hs))
    UV.append(MESHES['Head'][1])
    hinge = P + rot @ (np.array(model.JAW_HINGE) * hs)
    V.append(place(MESHES['Jaw'][0], hinge, rot @ euler(pose['jaw'], 0.0, 0.0), hs))
    UV.append(MESHES['Jaw'][1])
    for i, (q, d, u, k) in enumerate(pose['segs']):
        name = 'Tail' if i == model.SEGMENTS - 1 else 'SegmentLegs' if i in model.LEG_SEGMENTS else 'Segment'
        across, along = model.segment_scale(i)
        V.append(place(MESHES[name][0], q, look_rotation(d, u), np.array([across, across, along]) * k))
        UV.append(MESHES[name][1])
    turn = euler(0.0, 145.0, 0.0) @ euler(20.0, 0.0, 0.0)       # a flyer's: three-quarter view, pitched into its dive
    V = np.concatenate(V) @ turn.T
    UV = np.concatenate(UV)
    saved = (W, H, FOCAL, SUN, SUN_COL)
    fov = 30.0
    W, H = ART_W, ART_H
    FOCAL = (H * 0.5) / math.tan(math.radians(fov * 0.5))
    SUN = norm(-(euler(35.0, 30.0, 0.0) @ np.array([0.0, 0.0, 1.0])))      # the art's light: from the upper left, in front
    SUN_COL = np.array([1.25, 1.25, 1.25])
    try:
        pts = V.reshape(-1, 3)
        lo, hi = pts.min(axis=0), pts.max(axis=0)
        aim = (lo + hi) * 0.5
        radius = max(float(np.linalg.norm((hi - lo) * 0.5)), 0.5)
        dist = radius / math.tan(math.radians(fov * 0.5)) * 1.4
        along = norm(np.array([0.0, -0.12, 1.0]))
        cam = Cam()
        fb, on = None, None
        for again in range(2):
            cam.set(aim - along * dist, aim)
            fb = FB()
            draw_tris(fb, cam, V, UV, SKIN, emission=WYRM_EMISSION, paint=0.15)
            on = fb.z < 1e8
            if again == 1 or not on.any():
                break
            # Re-frame from the first render: centre it, zoom so it fills 0.86 of the picture, sit it a little low.
            ys, xs = np.nonzero(on)
            x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
            scale = 0.86 / max((x1 - x0 + 1) / float(W), (y1 - y0 + 1) / float(H))
            mpp = 2.0 * dist * math.tan(math.radians(fov * 0.5)) / H
            right, up = cam.M[0], cam.M[1]
            aim = aim + right * (((x0 + x1) / 2.0 - W / 2.0) * mpp) - up * (((y0 + y1) / 2.0 - H / 2.0) * mpp)
            aim = aim + up * ((0.54 - 0.5) * H * mpp / scale)
            dist /= scale
        out = np.zeros((H, W, 4))
        out[..., :3] = np.clip(fb.col, 0.0, 1.0)
        out[..., 3] = on
        return out
    finally:
        W, H, FOCAL, SUN, SUN_COL = saved


def art_composite(px, subject, rng):
    """ArtComposite for a flyer: a glow behind it, six speed streaks off its back edge up to the left, a dark outline, a
    rim of the school's light along its upper and right edges."""
    on = subject[..., 3] >= 0.5
    if not on.any():
        return
    ys, xs = np.nonzero(on)
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    light, white = ART_FIRE[3], np.ones(3)
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    r = max(x1 - x0, y1 - y0) * 0.75 + 8.0
    for y in range(ART_H):
        for x in range(ART_W):
            d = math.sqrt(((x - cx) / 1.25) ** 2 + (y - cy) ** 2) / r
            if d < 1.0:
                _put(px, x, y, light, (1.0 - d) ** 1.6 * 0.6)
    for k in range(6):
        sy = y0 + int(round((y1 - y0) * (k + 0.5) / 6.0)) + rng.next(-2, 3)
        if sy < 0 or sy >= ART_H:
            continue
        row = np.nonzero(on[sy])[0]
        if len(row) == 0:
            continue
        sx = int(row[0])
        length = rng.next(10, 27)
        for j in range(2, length):
            f = j / float(length)
            _put(px, int(round(sx - 0.86 * j)), int(round(sy - 0.5 * j)), white + (light - white) * f, (1.0 - f) * 0.7)
    for y in range(ART_H):
        for x in range(ART_W):
            l, rr = x > 0 and on[y, x - 1], x < ART_W - 1 and on[y, x + 1]
            u, d = y > 0 and on[y - 1, x], y < ART_H - 1 and on[y + 1, x]
            if not on[y, x]:
                if l or rr or u or d:
                    px[y, x] = ART_INK
                continue
            c = subject[y, x, :3]
            rim = (y > 0 and not u) or (x < ART_W - 1 and not rr)
            px[y, x] = c + (light - c) * 0.55 if rim else c


def card_art():
    """PaintCardArt for Sunfire Wyrm: (ART_H, ART_W, 3) top down."""
    rng = NetRandom(CARD_ID * 7919 + 17)
    px = art_backdrop(rng)
    art_composite(px, art_subject(), rng)
    for _ in range(14):                                  # embers (ArtMotes)
        x, y = rng.next(0, ART_W), rng.next(0, ART_H)
        _put(px, x, y, ART_FIRE[4], 0.9)
        _put(px, x, y + 1, ART_FIRE[4], 0.45)
    out = np.zeros_like(px)                              # ArtFinish: a vignette, then 15-bit colour, dithered
    for y in range(ART_H):
        for x in range(ART_W):
            vx, vy = (x - ART_W / 2.0) / (ART_W / 2.0), (y - ART_H / 2.0) / (ART_H / 2.0)
            vig = 1.0 - 0.32 * min(1.0, max(0.0, vx * vx + vy * vy - 0.35))
            dither = (ART_BAYER[(y % 4) * 4 + x % 4] / 16.0 - 0.5) * 0.9
            out[y, x] = np.clip(np.rint(np.clip(px[y, x] * vig, 0.0, 1.0) * 31.0 + dither) / 31.0, 0.0, 1.0)
    return out


def make_card(out):
    """The card as it is dealt: the Fire frame, the picture in its window, and its name, cost and numbers lettered on
    (the lettering here is only this preview's; the game's is its own pixel font)."""
    art = Image.fromarray((card_art() * 255.0).astype(np.uint8), 'RGB')
    art.save(os.path.splitext(out)[0] + '_art.png')
    k = 6
    frame = Image.open(os.path.join(TEX, 'Cards', 'CardFrameFire.png')).convert('RGBA').resize((80 * k, 112 * k), Image.NEAREST)
    card = frame.copy()
    # The picture lies over the frame's window: 58 x 38 of the frame's pixels (CardFace: 142 x 95 on a 196 x 280 card), from (11, 22).
    card.paste(art.resize((58 * k, 38 * k), Image.NEAREST), (11 * k, 22 * k))
    d = ImageDraw.Draw(card)

    font_path = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Fonts', 'PsxPixel.ttf')

    def text(s, cx, cy, fill, px, outline=False):
        """'s' centred on (cx, cy) of the frame's pixels, in the game's pixel font, each of its pixels 'px' across."""
        font = ImageFont.truetype(font_path, 10 * px)
        mask = Image.new('1', (len(s) * 10 * px + 8, 14 * px), 0)
        dm = ImageDraw.Draw(mask)
        dm.fontmode = '1'
        dm.text((4, px), s, fill=1, font=font)
        box = mask.getbbox()
        if box is None:
            return
        mask = mask.crop(box).convert('L')
        x, y = int(cx * k - mask.size[0] / 2), int(cy * k - mask.size[1] / 2)
        if outline:
            for ox, oy in ((-px, 0), (px, 0), (0, -px), (0, px)):
                card.paste((30, 22, 36, 255), (x + ox, y + oy), mask)
        card.paste(fill, (x, y), mask)

    ink = (61, 41, 23, 255)
    text('Sunfire Wyrm', 40, 12, (255, 240, 160, 255), 3, True)
    text('7', 9.5, 10.5, (255, 255, 255, 255), 5, True)
    text('75%', 15.5, 64.5, ink, 3)
    text('335-505 fire', 40, 80, ink, 3)
    text('to all enemies', 40, 88, ink, 3)
    text('Fire', 40, 99, (158, 133, 92, 255), 2)
    back = Image.new('RGBA', (card.size[0] + 60, card.size[1] + 60), (34, 30, 44, 255))
    back.alpha_composite(card, (30, 30))
    back.convert('RGB').save(out)
    print('wrote', out, 'and', os.path.splitext(out)[0] + '_art.png')


def frames(case='four', fps=FPS, sim_hz=60):
    """Every frame of the film: from the charge beginning to the unleash's end."""
    st = Stage(case)
    sub = max(1, int(round(sim_hz / fps)))
    dt = 1.0 / (fps * sub)
    t = st.tCast
    n = 0
    while t < st.tAfter - 1e-6:
        for _ in range(sub):
            st.step(t, dt)
            t += dt
        yield n, t, st.render(t - dt)
        n += 1


def make_gif(out, case='four'):
    """The whole film as a GIF (12.5 frames a second: 80 ms a frame exactly, and under 10 MB), or as an .mp4 (30 a second,
    twice the size, sharp pixels) when 'out' ends in .mp4."""
    video = out.lower().endswith('.mp4')
    fps = 30.0 if video else GIF_FPS
    tmp = tempfile.mkdtemp(prefix='wyrm_')
    try:
        count = 0
        for n, t, img in frames(case, fps):
            img.save(os.path.join(tmp, 'f%04d.png' % n))
            count += 1
            if n % 25 == 0:
                print('  %.1f s' % t, flush=True)
        src = ['-framerate', str(fps), '-i', os.path.join(tmp, 'f%04d.png')]
        if video:
            subprocess.run(['ffmpeg', '-y', '-loglevel', 'error'] + src + ['-vf', 'scale=iw*2:ih*2:flags=neighbor', '-c:v', 'libx264', '-crf', '17',
                                                                         '-preset', 'slow', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', out], check=True)
        else:
            pal = os.path.join(tmp, 'pal.png')
            subprocess.run(['ffmpeg', '-y', '-loglevel', 'error'] + src + ['-vf', 'palettegen=max_colors=128:stats_mode=diff', pal], check=True)
            subprocess.run(['ffmpeg', '-y', '-loglevel', 'error'] + src + ['-i', pal, '-lavfi', 'paletteuse=dither=none:diff_mode=rectangle', out], check=True)
        print('%d frames -> %s (%.1f MB)' % (count, out, os.path.getsize(out) / 1e6))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def make_sheet(out, case='four', every=0.6, start=None, end=None):
    shots = []
    st = Stage(case)
    dt = 1.0 / 60.0
    t = st.tCast
    due = st.tCast + 0.3 if start is None else start
    stop = st.tAfter if end is None else min(end, st.tAfter)
    while t < stop - 1e-6:
        st.step(t, dt)
        if t >= due:
            img = st.render(t)
            ImageDraw.Draw(img).text((6, 4), '%.2f s' % t, fill=(255, 255, 255))
            shots.append(img)
            due += every
        t += dt
    cols = 4
    rows = (len(shots) + cols - 1) // cols
    sheet = Image.new('RGB', (W * cols, H * rows))
    for i, im in enumerate(shots):
        sheet.paste(im, ((i % cols) * W, (i // cols) * H))
    sheet.save(out)
    print('%d stills -> %s' % (len(shots), out))


def make_frame(when, out, case='four'):
    st = Stage(case)
    dt = 1.0 / 60.0
    t = st.tCast
    while t < when:
        st.step(t, dt)
        t += dt
    st.render(t - dt).resize((W * 2, H * 2), Image.NEAREST).save(out)


if __name__ == '__main__':
    args = sys.argv[1:]
    if len(args) >= 2 and args[0] == 'gif':
        make_gif(args[1], args[2] if len(args) > 2 else 'four')
    elif len(args) >= 2 and args[0] == 'sheet':
        make_sheet(args[1], args[2] if len(args) > 2 else 'four', *(float(x) for x in args[3:6]))
    elif len(args) >= 3 and args[0] == 'frame':
        make_frame(float(args[1]), args[2], args[3] if len(args) > 3 else 'four')
    elif len(args) >= 2 and args[0] == 'card':
        make_card(args[1])
    else:
        print(__doc__)
