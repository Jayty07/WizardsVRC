"""Builds Assets/WizardsVRC/Fonts/PsxPixel.ttf from the pixel font in Assets/WizardsVRC/Fonts/PsxNameFont.txt.

The same glyphs as the NPC name tags, as a normal TrueType font so Unity's UI text (combat labels, cards, the hand) can
use it at any size. Each font pixel is 100 units (em = 1000 = 10 pixels: 7 cap height, 2 descender, 1 above). Glyph
outlines are the merged pixel squares (shared edges removed, so there are no seams). Needs fontTools.
Run from the repo root:  python3 Tools/make_pixel_font.py
"""
import os
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Fonts', 'PsxNameFont.txt')
OUT = os.path.join(ROOT, 'Assets', 'WizardsVRC', 'Fonts', 'PsxPixel.ttf')
PX = 100


def read_glyphs():
    glyphs, cur, rows = {}, None, []
    for raw in open(SRC, encoding='utf-8').read().splitlines():
        line = raw.rstrip('\r \t')
        if cur is None and line.startswith('#'):
            continue
        if len(line) == 2 and line[0] == ':':
            if cur is not None:
                glyphs[cur] = rows
            cur, rows = line[1], []
            continue
        if cur is not None and line:
            rows.append(line)
    if cur is not None:
        glyphs[cur] = rows
    return {c: r for c, r in glyphs.items() if len(r) in (7, 9) and all(len(x) == len(r[0]) for x in r)}


def outline(rows):
    """Closed loops (clockwise = ink, TrueType style) round the glyph's ink pixels, in font units."""
    ink = set()
    for i, row in enumerate(rows):
        y = 6 - i  # row 0 is the top of the caps; row 6 sits on the baseline; 7 and 8 hang below
        for x, ch in enumerate(row):
            if ch == '#':
                ink.add((x, y))
    edges = {}
    for (x, y) in ink:
        # Clockwise square, y up: left side up, top side right, right side down, bottom side left.
        for a, b in (((x, y), (x, y + 1)), ((x, y + 1), (x + 1, y + 1)), ((x + 1, y + 1), (x + 1, y)), ((x + 1, y), (x, y))):
            if (b, a) in edges:
                del edges[(b, a)]
            else:
                edges[(a, b)] = True
    out = {}
    for a, b in edges:
        out.setdefault(a, []).append(b)
    loops = []
    while out:
        start = next(iter(out))
        loop, cur, prev = [start], start, None
        while True:
            nxt = out[cur]
            if len(nxt) > 1 and prev is not None:
                # Where two corners touch, turn right (keeps the pixels apart).
                dx, dy = cur[0] - prev[0], cur[1] - prev[1]
                right = (cur[0] + dy, cur[1] - dx)
                b = right if right in nxt else nxt[0]
            else:
                b = nxt[0]
            nxt.remove(b)
            if not nxt:
                del out[cur]
            prev, cur = cur, b
            if cur == start:
                break
            loop.append(cur)
        # Drop points in the middle of straight runs.
        simple = []
        n = len(loop)
        for i in range(n):
            p0, p1, p2 = loop[i - 1], loop[i], loop[(i + 1) % n]
            if (p1[0] - p0[0]) * (p2[1] - p1[1]) - (p1[1] - p0[1]) * (p2[0] - p1[0]) != 0:
                simple.append(p1)
        loops.append(simple)
    return loops


def main():
    glyphs = read_glyphs()
    names = {c: ('uni%04X' % ord(c)) for c in glyphs}
    order = ['.notdef', 'space'] + [names[c] for c in glyphs]
    fb = FontBuilder(1000, isTTF=True)
    fb.setupGlyphOrder(order)
    cmap = {32: 'space', 160: 'space'}
    for c in glyphs:
        cmap[ord(c)] = names[c]
    fb.setupCharacterMap(cmap)
    pens, metrics = {}, {}

    def draw(rows):
        pen = TTGlyphPen(None)
        for loop in outline(rows):
            pen.moveTo((loop[0][0] * PX, loop[0][1] * PX))
            for p in loop[1:]:
                pen.lineTo((p[0] * PX, p[1] * PX))
            pen.closePath()
        return pen.glyph()

    q = glyphs.get('?')
    pens['.notdef'] = draw(q) if q else TTGlyphPen(None).glyph()
    metrics['.notdef'] = ((len(q[0]) + 1) * PX if q else 500, 0)
    pens['space'] = TTGlyphPen(None).glyph()
    metrics['space'] = (4 * PX, 0)
    for c, rows in glyphs.items():
        pens[names[c]] = draw(rows)
        metrics[names[c]] = ((len(rows[0]) + 1) * PX, 0)
    fb.setupGlyf(pens)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable({'familyName': 'WizardsVRC PSX Pixel', 'styleName': 'Regular'})
    fb.setupOS2(sTypoAscender=800, sTypoDescender=-200, sTypoLineGap=100, usWinAscent=800, usWinDescent=200,
                sCapHeight=700, sxHeight=500)
    fb.setupPost()
    fb.save(OUT)
    print('wrote', OUT, len(glyphs), 'glyphs')


if __name__ == '__main__':
    main()
