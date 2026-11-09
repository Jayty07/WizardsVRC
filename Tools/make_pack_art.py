"""Starshard card pack art (Textures/Shop/PackBody.png and PackTrim.png, 48x48 pixel art like the shop's other icons).

PackBody is the foil bag in light greys with a diagonal sheen, so the shop can tint it each pack's colour; PackTrim goes on
top untinted: the dark outline, crimped gold ends and a gold seal with a card on it. Original art.

    python3 Tools/make_pack_art.py Assets/WizardsVRC/Textures/Shop
"""
import os
import sys

from PIL import Image

W = 48
X0, X1 = 11, 36      # the bag, inclusive
Y0, Y1 = 3, 44
OUT = (30, 24, 36, 255)
GOLD = (255, 205, 70, 255)
GOLD_D = (190, 130, 40, 255)
GOLD_L = (255, 240, 160, 255)
PARCH = (236, 222, 186, 255)
RED = (190, 60, 50, 255)


def body():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    px = img.load()
    for y in range(Y0, Y1 + 1):
        for x in range(X0, X1 + 1):
            # A soft bulge (lighter in the middle) and a bright diagonal stripe of foil.
            mid = abs(x - (X0 + X1) / 2) / ((X1 - X0) / 2)
            v = int(235 - 55 * mid * mid)
            d = (x - X0) - (y - Y0) * 0.55
            if 6 <= d <= 9:
                v = 255
            elif 10 <= d <= 11:
                v = min(255, v + 15)
            if (x * 7 + y * 3) % 11 == 0:
                v = max(0, v - 18)   # a little foil grain
            px[x, y] = (v, v, v, 255)
    return img


def trim():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    px = img.load()
    # Outline down the sides.
    for y in range(Y0 + 4, Y1 - 3):
        px[X0 - 1, y] = OUT
        px[X1 + 1, y] = OUT
    # Crimped ends: a gold band four pixels tall with a zigzag edge.
    for (top, sign) in ((Y0, 1), (Y1, -1)):
        for x in range(X0 - 1, X1 + 2):
            for k in range(5):
                y = top + sign * k
                zig = (x % 3 == 0)
                if k == 0:
                    px[x, y] = OUT if not zig else (0, 0, 0, 0)
                elif k == 4:
                    px[x, y] = OUT
                else:
                    px[x, y] = GOLD_L if k == 1 else GOLD if (x + k) % 4 else GOLD_D
            if x % 3 == 0:
                px[x, top + sign * 1] = OUT
        # Ridges across the crimp.
        for x in range(X0, X1 + 1, 2):
            px[x, top + sign * 2] = GOLD_D
    # The seal: a gold disc with a little card on it.
    cx, cy = (X0 + X1) // 2, (Y0 + Y1) // 2 + 1
    for y in range(cy - 7, cy + 8):
        for x in range(cx - 7, cx + 8):
            d2 = (x - cx + 0.5) ** 2 + (y - cy + 0.5) ** 2
            if d2 <= 7.6 ** 2:
                px[x, y] = OUT
            if d2 <= 6.6 ** 2:
                px[x, y] = GOLD if (x + y) % 5 else GOLD_L
            if d2 <= 5.2 ** 2 and d2 > 4.3 ** 2:
                px[x, y] = GOLD_D
    for y in range(cy - 4, cy + 4):
        for x in range(cx - 3, cx + 3):
            edge = x in (cx - 3, cx + 2) or y in (cy - 4, cy + 3)
            px[x, y] = OUT if edge else PARCH
    px[cx - 1, cy - 1] = RED
    px[cx, cy - 1] = RED
    px[cx - 1, cy] = RED
    px[cx, cy] = RED
    return img


def main(folder):
    os.makedirs(folder, exist_ok=True)
    body().save(os.path.join(folder, "PackBody.png"))
    trim().save(os.path.join(folder, "PackTrim.png"))
    print("wrote PackBody.png and PackTrim.png to", folder)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/WizardsVRC/Textures/Shop")
