"""Write the Jacquard wordmark: white on black, glitched on the font's grid."""
import os
from jacquard_grid import CELL, COLS, ROWS, TEXT, X0, Y0, grid, runs

PAD = 5            # cells of margin around the ink
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "jacquard-logo.svg")

d = "".join(f"M{X0 + cx * CELL} {Y0 + cy * CELL}"
            f"h{w * CELL}v{CELL}h{-w * CELL}z"
            for cy, cx, w in runs(grid))

vx = X0 - PAD * CELL
vy = Y0 - PAD * CELL
vw = (COLS + PAD * 2) * CELL
vh = (ROWS + PAD * 2) * CELL

with open(OUT, "w") as f:
    f.write(f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="{vx} {vy} {vw} {vh}" width="{vw}" height="{vh}" role="img" aria-label="{TEXT}">
  <title>{TEXT}</title>
  <rect x="{vx}" y="{vy}" width="{vw}" height="{vh}" fill="#000000"/>
  <path fill="#ffffff" d="{d}"/>
</svg>
''')
print(f"wrote {OUT} ({vw}x{vh} units, {COLS}x{ROWS} cells of ink)")
