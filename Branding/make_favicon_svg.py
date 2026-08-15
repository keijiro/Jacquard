"""Write the favicon as an SVG that follows the browser's colour scheme.

Safari backs a favicon it thinks is too dark for the tab bar with a white plate,
which puts a ring around a black tile. So the tile goes: what is served is the
J alone on nothing, inked dark on a light scheme and light on a dark one. The
switch is a media query inside the file, because Safari ignores `media` on the
link element and honours it here.

Same grid and same placement as the PNG beside it, so the two are one mark.
"""
import os
from jacquard_grid import CELL, crop, grid, j_only, runs

CANVAS = 16        # square canvas in cells, as the PNG favicon uses
INK_LIGHT = "#161616"      # the app's own ground, for a light tab bar
INK_DARK = "#f2f2f2"       # and the type it writes on it, for a dark one
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "favicon.svg")

j = crop(j_only(grid))
jh, jw = len(j), len(j[0])
cx0, cy0 = (CANVAS - jw) // 2, (CANVAS - jh) // 2

cells = [[False] * CANVAS for _ in range(CANVAS)]
for y, row in enumerate(j):
    for x, v in enumerate(row):
        cells[cy0 + y][cx0 + x] = v

d = "".join(f"M{cx * CELL} {cy * CELL}h{w * CELL}v{CELL}h{-w * CELL}z"
            for cy, cx, w in runs(cells))

size = CANVAS * CELL
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}">\
<style>path{{fill:{INK_LIGHT}}}\
@media(prefers-color-scheme:dark){{path{{fill:{INK_DARK}}}}}</style>\
<path d="{d}"/></svg>'''

with open(OUT, "w") as f:
    f.write(svg)
print(f"wrote {OUT} ({len(svg)} bytes, {jw}x{jh} cells on a {CANVAS} canvas)")
