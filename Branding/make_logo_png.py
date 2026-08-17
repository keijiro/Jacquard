"""Write the wordmark as a bitmap for the app's own toolbar.

White on a transparent ground, since it sits on the toolbar's colour.  No
padding -- the element carries its own margin.

Three texture pixels per cell, which is what the app's own sizing asks for.  The
mark is drawn at three quarters of a control's box, so a cell comes to one unit
on the desktop and one and a half in the touch profile; the panel is laid out at
constant physical size and resolves two device pixels to a unit on a 2x screen,
so a cell there is three device pixels and this lands 1:1 on the iPads the app is
made for.  It was two pixels to a cell while the mark stood at one unit to a
cell, which was the same argument at the size the mark used to be.

Nothing in the app reads this number -- it takes the mark's height from the row
and its width from the texture's shape -- so a master at more pixels to a cell is
only ever sharper or larger, never a different layout.
"""
import os
from jacquard_grid import COLS, ROWS, grid
from pngwrite import write_rgba

PPC = 3            # texture pixels per cell
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "logo-bar.png")

write_rgba(OUT, COLS * PPC, ROWS * PPC,
           lambda px, py: grid[py // PPC][px // PPC])
print(f"wrote {OUT} ({COLS * PPC}x{ROWS * PPC}px, "
      f"{COLS}x{ROWS} cells at {PPC}px/cell)")
