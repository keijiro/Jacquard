"""Write the wordmark as a bitmap for the app's own toolbar.

White on a transparent ground, since it sits on the toolbar's colour.  Two
texture pixels per cell: the panel is laid out at constant physical size, so on
a 2x screen the texture lands 1:1, and on a 1x screen it is an exact 2:1
reduction that puts one pixel back on each cell.  No padding -- the element
carries its own margin.
"""
import os
from jacquard_grid import COLS, ROWS, grid
from pngwrite import write_rgba

PPC = 2            # texture pixels per cell
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "logo-bar.png")

write_rgba(OUT, COLS * PPC, ROWS * PPC,
           lambda px, py: grid[py // PPC][px // PPC])
print(f"wrote {OUT} ({COLS * PPC}x{ROWS * PPC}px, "
      f"{COLS}x{ROWS} cells at {PPC}px/cell)")
