"""Light shaft texture (Textures/LightShaft.png) for the tavern's sunbeams and the Starfall Crystal's sky beam: soft at
both sides, bright where it starts (v = 0, the top of the texture) and fading away along its length.

    python3 Tools/make_light_shaft.py Assets/WizardsVRC/Textures/LightShaft.png      (needs numpy and Pillow)
"""
import sys

import numpy as np
from PIL import Image

W, H = 64, 256
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
edge = np.sin(np.pi * u) ** 2                              # soft both sides
start = np.clip(v / 0.06, 0, 1) ** 0.7                     # a soft start
fade = (1 - v) ** 1.6                                      # dies away along its length
a = np.outer(start * fade, edge)
a = a / a.max()
img = np.zeros((H, W, 4), dtype=np.uint8)
img[..., 0] = 255
img[..., 1] = 255
img[..., 2] = 255
img[..., 3] = (a * 255).astype(np.uint8)
# Additive particles multiply colour by alpha too: put the falloff in the colour as well.
for c in range(3):
    img[..., c] = (a * 255).astype(np.uint8)
Image.fromarray(img, 'RGBA').save(sys.argv[1])
print('wrote', sys.argv[1])
