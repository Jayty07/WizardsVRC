"""The Tech School's badge (Textures/FxIcons/SchoolTech.png, 64x64 pixel art like the other school badges): a teal ring
round a dark fill, a gear with circuit traces running out of it. The Tech School is The Dominion's (hidden from players
for now); the book's Deck page shows this on its tab once you know a Tech spell.

    python3 Tools/make_tech_icon.py Assets/WizardsVRC/Textures/FxIcons
"""
import math
import sys

from PIL import Image

OUT = (30, 24, 36, 255)
RING = (60, 215, 205, 255)
RING_L = (170, 250, 240, 255)
FILL = (16, 52, 56, 255)
FILL_L = (24, 74, 78, 255)
GEAR = (190, 245, 240, 255)
GEAR_D = (90, 180, 175, 255)
TRACE = (255, 205, 70, 255)


def main(folder):
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    px = img.load()
    cx, cy = 31.5, 31.5
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - cx, y - cy)
            if d <= 29.5:
                px[x, y] = OUT
            if d <= 28.2:
                px[x, y] = RING if (x + y) % 9 else RING_L
            if d <= 24.2:
                px[x, y] = FILL_L if d <= 20 else FILL
    # Circuit traces from the gear to the rim, each ending in a pad.
    for ang in (35, 145, 215, 325):
        a = math.radians(ang)
        for r in range(13, 22):
            x, y = int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r))
            px[x, y] = TRACE
        x, y = int(round(cx + math.cos(a) * 22)), int(round(cy + math.sin(a) * 22))
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                px[x + dx, y + dy] = TRACE
    # The gear: a disc with eight teeth and a hole.
    for y in range(64):
        for x in range(64):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx)
            tooth = (math.cos(a * 8) > 0.35)
            outer = 14 if tooth else 11
            if 4.5 < d <= outer:
                shade = GEAR if dx + dy < 4 else GEAR_D
                px[x, y] = shade
    # Ink edge round the gear.
    edge = []
    for y in range(1, 63):
        for x in range(1, 63):
            if px[x, y] in (GEAR, GEAR_D):
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if px[nx, ny] not in (GEAR, GEAR_D):
                        edge.append((nx, ny))
    for (x, y) in edge:
        px[x, y] = OUT
    img.save(folder + "/SchoolTech.png")
    print("wrote", folder + "/SchoolTech.png")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/WizardsVRC/Textures/FxIcons")
