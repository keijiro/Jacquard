"""The word "Jacquard" as a grid of cells on the font's own pixel pitch.

Jacquard 12 is drawn on a 60 unit grid (1260 upem, so 21 cells to the em) and
every outline is axis aligned, so the type can be reduced to a boolean grid
without losing anything.  Working in cells is what keeps the glitch locked to
the same dots as the letterforms.
"""
import os
import random
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen

TEXT = "Jacquard"
CELL = 60          # font's pixel pitch in font units
SEED = 11

FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "Jacquard12-Regular.ttf")
if not os.path.exists(FONT):
    sys.exit(f"{FONT} is missing -- fetch it from\n"
             "https://github.com/google/fonts/tree/main/ofl/jacquard12")


def _contours():
    """Glyph outlines laid out along the baseline, y flipped for SVG."""
    font = TTFont(FONT)
    cmap = font.getBestCmap()
    gs = font.getGlyphSet()
    out = []
    x = 0
    for ch in TEXT:
        gname = cmap[ord(ch)]
        pen = RecordingPen()
        gs[gname].draw(pen)
        cur = []
        for op, args in pen.value:
            if op == "moveTo":
                cur = [(args[0][0] + x, -args[0][1])]
            elif op == "lineTo":
                cur.append((args[0][0] + x, -args[0][1]))
            elif op == "closePath":
                if len(cur) > 2:
                    out.append(cur)
                cur = []
        x += font["hmtx"][gname][0]
    return out


contours = _contours()
xs = [p[0] for c in contours for p in c]
ys = [p[1] for c in contours for p in c]
X0, Y0 = min(xs), min(ys)
COLS = (max(xs) - X0) // CELL
ROWS = (max(ys) - Y0) // CELL


def _inside(px, py):
    """Nonzero winding test."""
    w = 0
    for c in contours:
        for i in range(len(c)):
            ax, ay = c[i]
            bx, by = c[(i + 1) % len(c)]
            if ay <= py < by or by <= py < ay:
                t = (py - ay) / (by - ay)
                if ax + t * (bx - ax) > px:
                    w += 1 if by > ay else -1
    return w != 0


base = [[_inside(X0 + (cx + 0.5) * CELL, Y0 + (cy + 0.5) * CELL)
         for cx in range(COLS)] for cy in range(ROWS)]

# --- glitch: slabs of rows torn sideways by whole cells ----------------------
# (top row, height, shift, first col, last col).  Rows 0-3 hold only the J's
# head and the d's ascender, 4-11 the x-height body, 12-14 the descenders --
# so the tears sit where they read as damage without eating the letterforms.
SLABS = [
    (0, 3, 2, 0, COLS),        # cap line and ascenders
    (7, 1, 1, 30, 44),         # a short tear through the middle
    (13, 2, -2, 0, COLS),      # descender line
]

rnd = random.Random(SEED)
main = [row[:] for row in base]     # the word, intact outside the torn slabs
tear = [[False] * COLS for _ in range(ROWS)]    # stray cells at the seams

for _top, _height, _dx, _ca, _cb in SLABS:
    for _cy in range(_top, min(_top + _height, ROWS)):
        src = base[_cy]
        row = src[:]
        for _cx in range(_ca, min(_cb, COLS)):
            row[_cx] = src[_cx - _dx] if 0 <= _cx - _dx < COLS else False
        main[_cy] = row
    for _cx in range(_ca, min(_cb, COLS)):
        for _cy in (max(_top - 1, 0), min(_top + _height, ROWS - 1)):
            if not main[_cy][_cx] and rnd.random() < 0.02:
                tear[_cy][_cx] = True

# Only the J and the d reach above the x-height.  A stray cell over any of the
# letters between them reads as part of that letter -- a tittle, or a piece of
# an ascender that isn't there -- rather than as damage, so keep that air clear.
for _cy in range(0, 4):
    for _cx in range(12, 65):
        tear[_cy][_cx] = False

grid = [[main[cy][cx] or tear[cy][cx] for cx in range(COLS)]
        for cy in range(ROWS)]

def j_only(cells):
    """Just the J's cells.  Its shifted head reaches column 12, but below the
    x-height line the a's bottom serif comes back to column 12 itself, so the
    two can only be told apart row by row."""
    return [[v and cx < (13 if cy < 4 else 11) for cx, v in enumerate(row)]
            for cy, row in enumerate(cells)]


def runs(cells):
    """Each row's lit cells merged into (row, first col, width) runs."""
    for cy, row in enumerate(cells):
        cx = 0
        while cx < len(row):
            if row[cx]:
                end = cx
                while end < len(row) and row[end]:
                    end += 1
                yield cy, cx, end - cx
                cx = end
            else:
                cx += 1


def crop(cells):
    """The grid trimmed to the ink it contains."""
    out = [row[:] for row in cells]
    lit = [(cy, cx) for cy, row in enumerate(out)
           for cx, v in enumerate(row) if v]
    y0 = min(cy for cy, _ in lit)
    y1 = max(cy for cy, _ in lit)
    x0 = min(cx for _, cx in lit)
    x1 = max(cx for _, cx in lit)
    return [row[x0:x1 + 1] for row in out[y0:y1 + 1]]
