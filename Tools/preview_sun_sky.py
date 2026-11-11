"""A numpy model of World 1's sun and solar winds (Shaders/PSXSkyline.shader with PSX_SUN): the same maths, statement
for statement, so a change can be looked at without Unity. Keep the two in step. It writes four pictures:

    view_sun.png     looking at the sun, about what a headset shows (90 degrees across)
    view_close.png   the sun close up, to check the pixel art
    view_away.png    looking the other way, up at the far sky (the winds)
    fisheye.png      the whole sky, zenith in the middle
    areas.png        the sun in each World 1 area's own colours (the builder's SetSkySun, modelled)

    python3 Tools/preview_sun_sky.py [out folder] [time in seconds]      (needs numpy and Pillow)
"""
import os
import sys

import numpy as np
from PIL import Image

PI = np.pi
BAYER = np.array([0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5], dtype=np.float64)


def frac(x): return x - np.floor(x)
def sat(x): return np.clip(x, 0.0, 1.0)
def lerp(a, b, t): return a + (b - a) * t
def step(e, x): return (x >= e).astype(np.float64)
def smooth(a, b, x):
    t = sat((x - a) / (b - a))
    return t * t * (3 - 2 * t)
def hash21(x, y):
    px = frac(x * 123.34); py = frac(y * 456.21)
    dt = px * (px + 45.32) + py * (py + 45.32)
    px = px + dt; py = py + dt
    return frac(px * py)
def norm(v): return v / np.linalg.norm(v, axis=-1, keepdims=True)


class P:
    """The material's properties (the hub's values)."""
    top = np.array([0.28, 0.33, 0.90]); horizon = np.array([1.0, 0.68, 0.52]); bottom = np.array([0.06, 0.2, 0.52])
    sharp = 1.6
    sun_dir = None           # set by set_sun
    ppd = 5.69               # sky pixels per degree (the skyline strip's)
    fps = 12.0               # the sky's animation steps this many times a second (0 = smooth)
    sun_size = 1.5
    sun_power = 1.0
    wind = 1.0
    core = np.array([1.0, 0.98, 0.84]); gold = np.array([1.0, 0.82, 0.34]); deep = np.array([1.0, 0.52, 0.24])
    wind_a = np.array([1.0, 0.88, 0.52]); wind_b = np.array([1.0, 0.62, 0.78]); wind_c = np.array([0.62, 0.93, 1.0])


def set_sun(bearing_deg, elev_deg):
    b, e = np.radians(bearing_deg), np.radians(elev_deg)
    P.sun_dir = np.array([np.sin(b) * np.cos(e), np.sin(e), np.cos(b) * np.cos(e)])


def sky_pixel(d, res):
    """The middle of the sky pixel a direction falls in (a cube of equal-angle squares, 'res' pixels to the radian),
    and that pixel's whole-number place on its face."""
    a = np.abs(d)
    x, y, z = d[..., 0], d[..., 1], d[..., 2]
    fy = (a[..., 1] >= a[..., 0]) & (a[..., 1] >= a[..., 2])
    fx = (~fy) & (a[..., 0] >= a[..., 2])
    # two coordinates across the face, as angles
    m = np.where(fy, a[..., 1], np.where(fx, a[..., 0], a[..., 2]))
    u = np.arctan(np.where(fy, x, np.where(fx, y, x)) / m)
    v = np.arctan(np.where(fy, z, np.where(fx, z, y)) / m)
    iu, iv = np.floor(u * res), np.floor(v * res)
    cu, cv = np.tan((iu + 0.5) / res), np.tan((iv + 0.5) / res)
    sgn = np.sign(np.where(fy, y, np.where(fx, x, z)))
    q = np.stack([np.where(fy, cu, np.where(fx, sgn, cu)),
                  np.where(fy, sgn, np.where(fx, cu, cv)),
                  np.where(fy, cv, np.where(fx, cv, sgn))], axis=-1)
    return norm(q), iu, iv


def bayer(ix, iy):
    i = (np.mod(ix, 4) + 4 * np.mod(iy, 4)).astype(int)
    return (BAYER[i] + 0.5) / 16.0


def sky(d, t):
    d = norm(d)
    tq = np.floor(t * P.fps) / P.fps if P.fps > 0 else t
    S = P.sun_dir
    R = norm(np.cross(np.array([0.0, 1.0, 0.0]), S)); U = np.cross(S, R)

    # ---- the gradient
    up = sat(d[..., 1]) ** (1.0 / P.sharp); down = sat(-d[..., 1]) ** (1.0 / P.sharp)
    col = lerp(P.horizon, P.top, up[..., None]); col = lerp(col, P.bottom, down[..., None])

    # ---- the solar winds: on the sky's own pixels
    res = P.ppd * 57.29578
    q, iu, iv = sky_pixel(d, res)
    by = bayer(iu, iv)
    cs = np.clip(q @ S, -1, 1)
    rho = np.arccos(cs)                                   # angle from the sun
    th = np.arctan2(q @ U, q @ R)                         # angle round it
    rn = rho / PI
    # Ribbons: nine great streams leaving the sun, wandering as they cross the sky.
    a = th + 0.38 * np.sin(rho * 2.3 + tq * 0.05) + 0.16 * np.sin(rho * 5.1 - tq * 0.08 + 1.7)
    lane = a * 9.0 / (2 * PI)
    li = np.floor(lane); lf = frac(lane)
    li = li - 9.0 * np.floor(li / 9.0)
    lh = hash21(li, li * 0.37 + 3.1)
    width = lerp(0.16, 0.42, smooth(0.03, 0.5, rn)) * (0.7 + 0.6 * lh)
    ribbon = sat(1.0 - np.abs(lf - 0.5) / width)
    gust = 0.5 + 0.5 * np.sin(rho * 7.0 - tq * 0.9 + lh * 6.2832)
    gust2 = 0.5 + 0.5 * np.sin(rho * 17.0 - tq * 1.7 + lh * 17.0)
    stream = ribbon * (0.35 + 0.45 * gust + 0.2 * gust2)
    # Finer streaks between them, quicker.
    lane2 = (th + 0.2 * np.sin(rho * 3.7 - tq * 0.06 + 0.9)) * 23.0 / (2 * PI)
    l2i = np.floor(lane2); l2f = frac(lane2)
    l2i = l2i - 23.0 * np.floor(l2i / 23.0)
    l2h = hash21(l2i + 11.0, l2i * 0.61)
    streak = sat(1.0 - np.abs(l2f - 0.5) / 0.16) * smooth(0.55, 1.0, 0.5 + 0.5 * np.sin(rho * 11.0 - tq * 1.5 + l2h * 6.2832)) * step(0.35, l2h)
    reach = smooth(0.012, 0.07, rn) * lerp(1.0, 0.7, smooth(0.1, 0.9, rn)) * smooth(-0.04, 0.06, q[..., 1])
    wind = (stream * 0.95 + streak * 0.5) * reach * P.wind
    # Filaments: thin bright lines along each ribbon, in dashes that run outward and taper at both ends.
    fl = frac(lf * 3.0 + lh * 3.0)
    dash = frac(rho * 2.6 - tq * 0.24 + lh * 7.0 + np.floor(lf * 3.0 + lh * 3.0) * 0.37)
    taper = np.sin(sat(dash / 0.62) * PI)
    thin = (0.5 / P.ppd + 0.03) / np.maximum(np.sin(rho) * (2 * PI / 27.0) * 57.29578, 0.05)   # one sky pixel wide, whatever the ribbon's width there
    filament = step(np.abs(fl - 0.5), thin) * step(dash, 0.62) * ribbon * reach * P.wind
    tone = np.floor(sat(wind) * 3.0 + by)                 # 0 none, then three steps, dithered between
    hue = np.floor(sat(rn * 1.35) * 4.0 + by)             # gold by the sun, rose, then aqua across the sky
    wcol = np.where((hue < 1)[..., None], P.wind_a, np.where((hue < 2)[..., None], lerp(P.wind_a, P.wind_b, 0.6),
                    np.where((hue < 3)[..., None], P.wind_b, np.where((hue < 4)[..., None], lerp(P.wind_b, P.wind_c, 0.6), P.wind_c))))
    walpha = np.where(tone >= 3, 0.66, np.where(tone >= 2, 0.44, np.where(tone >= 1, 0.22, 0.0)))
    col = lerp(col, wcol, walpha[..., None])
    ftone = np.floor(sat(filament * taper * 1.6) * 2.0 + by * 0.999)
    col = lerp(col, lerp(wcol, np.ones(3), 0.55), np.where(ftone >= 2, 0.85, np.where(ftone >= 1, 0.5, 0.0))[..., None])
    # Motes riding the streaks outward: a bright head and a tail behind it.
    mu = rn * 4.0 - tq * 0.11 + l2h * 5.0
    ms = frac(mu)
    across = np.abs(l2f - 0.5) * (2 * PI / 23.0) * np.sin(rho) * 57.29578      # degrees off the streak's middle
    mote = step(across, 0.5 / P.ppd + 0.02) * step(0.5, l2h) * smooth(0.02, 0.08, rn) * smooth(-0.02, 0.04, q[..., 1])
    tail = sat((ms - 0.84) / 0.16)
    mtone = np.floor(tail * mote * 2.0 + by * 0.999)
    head = mote * step(0.975, ms)
    col = lerp(col, lerp(wcol, np.ones(3), 0.5), (0.55 * step(1.0, mtone))[..., None])
    col = lerp(col, np.ones(3), head[..., None] * 0.95)

    # ---- the sun: on pixels of its own, square to it
    front = d @ S
    near = front > 0.8
    sx = np.where(near, (d @ R) / np.maximum(front, 0.1), 9.0) * 57.29578
    sy = np.where(near, (d @ U) / np.maximum(front, 0.1), 9.0) * 57.29578
    ix, iy = np.floor(sx * P.ppd), np.floor(sy * P.ppd)
    px, py = (ix + 0.5) / P.ppd / P.sun_size, (iy + 0.5) / P.ppd / P.sun_size   # degrees, at the pixel's middle
    r = np.sqrt(px * px + py * py)
    ang = np.arctan2(py, px)
    sb = bayer(ix, iy)
    # Its glow: three dithered steps of light, breathing.
    breathe = 1.0 + 0.08 * np.sin(tq * 0.9)
    glow = 1.5 * np.exp(-np.maximum(r - 2.6, 0.0) / (3.0 * breathe)) * step(r, 30.0)
    # A ring of light leaves it every few seconds and thins as it spreads.
    wave = frac(tq / 5.0)
    wr = 4.0 + wave * 15.0
    glow = glow + step(np.abs(r - wr), 0.24 + 0.5 * wave) * (1.0 - wave) * 0.75
    gt = np.floor(sat(glow) * 3.0 + sb)
    galpha = np.where(gt >= 3, 0.78, np.where(gt >= 2, 0.5, np.where(gt >= 1, 0.24, 0.0))) * P.sun_power
    gcol = np.where((gt >= 3)[..., None], P.core, np.where((gt >= 2)[..., None], P.gold, lerp(P.gold, P.deep, 0.6)))
    col = lerp(col, gcol, galpha[..., None])
    # Needles of light between the rays, far longer, flickering one after another.
    nsec = (ang + tq * 0.035) * 16.0 / (2 * PI) + 0.5
    ni = np.floor(nsec)
    ni = ni - 16.0 * np.floor(ni / 16.0)
    nlen = 4.0 + 4.5 * (0.5 + 0.5 * np.sin(tq * 2.3 + ni * 2.4))
    off = np.abs(frac(nsec) - 0.5) * (2 * PI / 16.0) * r * P.ppd * P.sun_size        # sky pixels off the needle's line
    needle = step(4.3, r) * step(r, 4.3 + nlen) * step(off, 0.5)
    col = np.where((needle > 0.5)[..., None], lerp(col, P.core, 0.85 * P.sun_power), col)
    # Rays: sixteen, long and short by turns, turning slowly, their lengths rippling round.
    turn = tq * 0.035
    sec = (ang + turn) * 16.0 / (2 * PI)
    si = np.floor(sec); sf = frac(sec)
    longr = step(0.5, frac(si * 0.5))
    rlen = lerp(2.3, 4.3, longr) * (1.0 + 0.14 * np.sin(tq * 1.6 + si * 0.7853982))
    along = (r - 4.05) / rlen
    ray = step(0.0, along) * step(along, 1.0) * step(np.abs(sf - 0.5), 0.36 * (1.0 - along))
    rcol = np.where((along < 0.34)[..., None], lerp(P.core, P.gold, 0.5), np.where((along < 0.7)[..., None], P.gold, P.deep))
    col = np.where((ray > 0.5)[..., None], lerp(col, rcol, P.sun_power), col)
    # Two rings round it: a plain one, and a band of rune marks turning the other way.
    ring1 = step(3.0, r) * step(r, 3.24)
    marks = (ang - tq * 0.06) * 28.0 / (2 * PI)
    mi = np.floor(marks)
    mwide = lerp(0.3, 0.62, step(0.5, frac(mi * 0.5)))          # a dot, a dash, a dot, a dash
    ring2 = step(3.48, r) * step(r, 3.86) * step(frac(marks), mwide)
    col = np.where(((ring1 + ring2) > 0.5)[..., None], lerp(col, np.where((ring1 > 0.5)[..., None], P.gold, P.deep), P.sun_power), col)
    # The disc: white-hot middle, gold edge, a paler rim, a shaded crescent low on one side.
    disc = step(r, 2.6)
    shade = step(1.55, r) * step(0.35, (px * 0.6 - py * 0.8) / np.maximum(r, 0.001))
    dcol = np.where((r < 1.75)[..., None], P.core, lerp(P.core, P.gold, 0.55))
    dcol = np.where((shade > 0.5)[..., None], lerp(P.gold, P.deep, 0.45), dcol)
    dcol = np.where((r > 2.36)[..., None], lerp(P.core, np.ones(3), 0.5), dcol)
    # A star set in its face: four long points and four short, pulsing.
    pulse = 1.0 + 0.1 * np.sin(tq * 2.2)
    star_a = ang + tq * 0.02
    spike = np.maximum(1.5 * (1.0 - np.abs(np.sin(star_a * 2.0))) ** 1.7, 0.9 * (1.0 - np.abs(np.cos(star_a * 2.0))) ** 1.7)
    star = step(r, (0.4 + spike) * pulse)
    dcol = np.where((star > 0.5)[..., None], np.ones(3), dcol)
    dcol = np.where(((r < 0.42 * pulse) & (star > 0.5))[..., None], P.gold, dcol)
    col = np.where((disc > 0.5)[..., None], lerp(col, dcol, P.sun_power), col)
    # Little diamonds going round it on a dotted path, and stars twinkling out in its light.
    orbit = 9.4
    oa = (ang - tq * 0.05) * 6.0 / (2 * PI)
    oc = (np.floor(oa) + 0.5) * (2 * PI / 6.0) + tq * 0.05
    gx, gy = px - np.cos(oc) * orbit, py - np.sin(oc) * orbit
    gd = (np.abs(gx) + np.abs(gy)) * P.ppd * P.sun_size
    gem = step(gd, 2.6)
    dots = step(np.abs(r - orbit), 0.5 / P.ppd / P.sun_size) * step(frac((ang - tq * 0.05) * 60.0 / (2 * PI)), 0.34)
    col = np.where((dots > 0.5)[..., None], lerp(col, P.gold, 0.7 * P.sun_power), col)
    col = np.where((gem > 0.5)[..., None], lerp(col, np.where((gd < 1.0)[..., None], np.ones(3), np.where((gd < 1.9)[..., None], P.core, P.gold)), P.sun_power), col)
    cell = 2.3
    cx, cy = np.floor(px / cell), np.floor(py / cell)
    ch = hash21(cx + 7.0, cy + 13.0)
    ox = (cx + 0.2 + 0.6 * hash21(cx + 1.3, cy + 9.1)) * cell
    oy = (cy + 0.2 + 0.6 * hash21(cx + 5.7, cy + 2.9)) * cell
    kx = np.abs(np.round((px - ox) * P.ppd * P.sun_size)); ky = np.abs(np.round((py - oy) * P.ppd * P.sun_size))
    frame = np.floor(frac(tq * (0.35 + 0.5 * ch) + ch * 9.0) * 6.0)        # 0..5: off, dot, small, big, small, dot
    arm = np.where(frame == 3, 2.0, np.where((frame == 2) | (frame == 4), 1.0, np.where((frame == 1) | (frame == 5), 0.0, -1.0)))
    orr = np.sqrt(ox * ox + oy * oy)
    twinkle = step(0.62, ch) * step(5.2, orr) * step(orr, 17.0) * step(np.minimum(kx, ky), 0.0) * step(np.maximum(kx, ky), arm)
    col = np.where(((twinkle > 0.5) & near)[..., None], lerp(col, np.where((kx + ky < 0.5)[..., None], np.ones(3), P.core), P.sun_power), col)
    return sat(col)


# ------------------------------------------------------------------ pictures

def view(yaw, pitch, fov, w, h, t):
    """A pinhole camera looking along bearing 'yaw' (degrees clockwise from north), 'pitch' up."""
    f = 0.5 * w / np.tan(np.radians(fov) / 2)
    xs, ys = np.meshgrid(np.arange(w) - w / 2 + 0.5, h / 2 - np.arange(h) - 0.5)
    d = np.stack([xs, ys, np.full_like(xs, f)], -1)
    p, y = np.radians(pitch), np.radians(yaw)
    rx = np.array([[1, 0, 0], [0, np.cos(p), np.sin(p)], [0, -np.sin(p), np.cos(p)]])
    ry = np.array([[np.cos(y), 0, np.sin(y)], [0, 1, 0], [-np.sin(y), 0, np.cos(y)]])
    return sky(d @ rx.T @ ry.T, t)


def fisheye(w, t):
    """The whole sky, looking straight up: the zenith in the middle, the horizon round the rim (north at the top)."""
    xs, ys = np.meshgrid((np.arange(w) + 0.5) / w * 2 - 1, 1 - (np.arange(w) + 0.5) / w * 2)
    r = np.sqrt(xs * xs + ys * ys)
    alt = (1 - np.minimum(r, 1.0)) * np.pi / 2
    az = np.arctan2(xs, ys)
    img = sky(np.stack([np.sin(az) * np.cos(alt), np.sin(alt), np.cos(az) * np.cos(alt)], -1), t)
    img[r > 1] = 0.05
    return img


def save(img, path):
    Image.fromarray((img * 255).astype(np.uint8)).save(path)
    print('wrote', path)


# ------------------------------------------------------------------ each area's light (WizardsVRCSceneBuilder.Sun.cs)

CORE, GOLD, DEEP = P.core.copy(), P.gold.copy(), P.deep.copy()
WIND_A, WIND_B, WIND_C = P.wind_a.copy(), P.wind_b.copy(), P.wind_c.copy()
LUMA = np.array([0.3, 0.59, 0.11])

# name, sky top, sky horizon (WorldDefaults)
AREAS = [
    ('Starfall Castle', (0.28, 0.33, 0.90), (1.0, 0.68, 0.52)),
    ('Sunpetal Gardens', (0.34, 0.55, 0.96), (0.98, 0.86, 0.7)),
    ('Lantern Row', (0.13, 0.11, 0.36), (0.9, 0.48, 0.42)),
    ('Tidewatch Docks', (0.21, 0.25, 0.31), (0.41, 0.42, 0.44)),
    ('Kiln Street', (0.27, 0.12, 0.07), (0.74, 0.39, 0.13)),
    ('Thornhollow Grove', (0.13, 0.24, 0.2), (0.4, 0.46, 0.34)),
    ('The Hollow Keep', (0.12, 0.08, 0.25), (0.5, 0.36, 0.6)),
]


def use_area(top, horizon, amount=1.0):
    """Sets the sky's colours and the sun's for an area, as SetSkySun does."""
    top, horizon = np.array(top), np.array(horizon)
    lum = float(horizon @ LUMA)
    mx, mn = horizon.max(), horizon.min()
    tint = horizon / max(mx, 0.01)
    grey_sky = 1.0 - float(smooth(0.05, 0.3, (mx - mn) / max(mx, 0.01)))      # an overcast sky washes the colour out
    bright = float(smooth(0.35, 0.8, lum))

    def lean(c, k):
        c = lerp(c, c * tint, k)
        g = float(c @ LUMA)
        return lerp(c, np.array([g, g, g]), 0.5 * grey_sky)

    P.top, P.horizon = top, horizon
    P.core, P.gold, P.deep = lean(CORE, 0.3), lean(GOLD, 0.45), lean(DEEP, 0.45)
    P.wind_a, P.wind_b, P.wind_c = lean(WIND_A, 0.35), lean(WIND_B, 0.35), lean(WIND_C, 0.2)
    P.sun_power = lerp(0.78, 1.0, bright) * lerp(0.5, 1.0, amount)
    P.wind = lerp(0.85, 1.0, bright) * amount


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else 'sun_sky_preview'
    t = float(sys.argv[2]) if len(sys.argv) > 2 else 3.0
    os.makedirs(out, exist_ok=True)
    bearing, elev = 150.0, 30.0                 # the scene light's bearing; SunElevation
    set_sun(bearing, elev)
    use_area(*AREAS[0][1:])
    save(view(bearing, elev - 4, 90, 1120, 700, t), os.path.join(out, 'view_sun.png'))
    save(view(bearing, elev, 32, 1100, 700, t), os.path.join(out, 'view_close.png'))
    save(view(bearing + 180, 40, 90, 1120, 700, t), os.path.join(out, 'view_away.png'))
    save(fisheye(900, t), os.path.join(out, 'fisheye.png'))
    tiles = []
    for name, top, horizon in AREAS:
        use_area(top, horizon)
        tiles.append(view(bearing, elev - 4, 80, 560, 400, t))
    tiles.append(np.zeros_like(tiles[0]))
    save(np.concatenate([np.concatenate(tiles[0:4], axis=1), np.concatenate(tiles[4:8], axis=1)], axis=0), os.path.join(out, 'areas.png'))
