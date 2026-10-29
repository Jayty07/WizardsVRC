"""Builds the seat health-gauge mask (DuelSeatGauge.png) used by the WizardsVRC/PSXGauge shader.

Red 255 = gauge band (the shader colours it filled / recently lost / empty by angle), red 128 = dark frame
(outline and the gaps between segments). Alpha 0 elsewhere. SEGMENTS must match the builder's SeatGaugeSegments.

    python3 Tools/make_seat_gauge.py Assets/WizardsVRC/Textures/DuelSeatGauge.png [preview.png]
"""
import math
import sys
from PIL import Image

SIZE = 128
SEGMENTS = 24
R_IN, R_OUT = 49.0, 59.0   # band, in pixels from the centre
GAP_PX = 1.0               # width of the gap between segments, in pixels at the band's middle

img = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
px = img.load()
c = SIZE / 2.0
mid = (R_IN + R_OUT) / 2.0
for y in range(SIZE):
    for x in range(SIZE):
        dx, dy = x + 0.5 - c, y + 0.5 - c
        r = math.hypot(dx, dy)
        if r < R_IN - 1.0 or r > R_OUT + 1.0:
            continue
        band = R_IN <= r <= R_OUT
        if band:
            # Each segment boundary is a ray from the centre (0 = top of the image, clockwise). A pixel is a gap pixel
            # when its centre lies in [-0.5, 0.5) of the ray, which draws every boundary exactly 1 pixel wide.
            for k in range(SEGMENTS):
                t = 2 * math.pi * k / SEGMENTS
                perp = dx * math.cos(t) + dy * math.sin(t)
                along = dx * math.sin(t) - dy * math.cos(t)
                if along > 0 and -GAP_PX / 2.0 <= perp < GAP_PX / 2.0:
                    band = False
                    break
        px[x, y] = (255, 255, 255, 255) if band else (128, 128, 128, 255)
img.save(sys.argv[1])
if len(sys.argv) > 2:
    # preview: full health on the left, 40% with a lost chunk on the right
    prev = Image.new('RGBA', (SIZE * 2 + 8, SIZE), (60, 52, 70, 255))
    for k, (fill, trail) in enumerate(((1.0, 1.0), (0.4, 0.58))):
        out = img.copy()
        op = out.load()
        for y in range(SIZE):
            for x in range(SIZE):
                p = op[x, y]
                if not p[3]:
                    continue
                if p[0] < 200:
                    op[x, y] = (14, 10, 20, 255)
                    continue
                a = math.atan2(x + 0.5 - c, -(y + 0.5 - c)) / (2 * math.pi) % 1.0
                f = math.ceil(fill * SEGMENTS - 0.001) / SEGMENTS
                t = math.ceil(trail * SEGMENTS - 0.001) / SEGMENTS
                op[x, y] = (60, 180, 255, 255) if a < f else ((255, 240, 200, 255) if a < t else (34, 30, 44, 255))
        prev.alpha_composite(out, (k * (SIZE + 8), 0))
    prev.resize((prev.width * 3, prev.height * 3), Image.NEAREST).save(sys.argv[2])
print('ok')
