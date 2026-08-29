"""Write the guide icon: a book, white on a transparent ground.

The one picture here that is not the word "Jacquard".  Everything else in this
folder is a reduction of the type and could be nothing else, because a mark of a
name has only one thing it can say; this says *the manual is behind here*, which
no arrangement of the letterforms says at all.  So it is drawn, and it is drawn
on the same cell grid the marks are cut on so that it is the same kind of object
as the wordmark standing at the other end of the row -- squares of one size, all
of them lit or clear, and nothing in between for the resampler to argue with.

The cells are written out below rather than derived, which is the honest form for
a shape nobody computed.  Thirteen by twelve: two pages with a gutter between
them, joined along the bottom, and the outer top corners taken in by two cells
and then one so the pages read as curved rather than as a pair of boxes.  The
gutter runs the whole height but the last row, which is what makes it a book and
not two leaves -- covered up, it reads as a butterfly.

Three texture pixels per cell, the same number and the same argument as
make_logo_png.py.  The app draws this at IconOfBox of a control's box, which is
eighteen units in the touch profile, so a cell comes to one and a half units
there; the panel resolves two device pixels to a unit on a 2x screen, so a cell
is exactly the three pixels this is cut at and the icon lands 1:1 on an iPad. On
the desktop a cell is one unit and two device pixels, so there it is resampled
from three -- which is the wordmark's story as well.

Nothing in the app reads this number.  It takes the icon's height from the row
and its width from the texture's shape, so a master at more pixels to a cell is
sharper and never a different layout.
"""
import os
from pngwrite import write_rgba

BOOK = ["..XXXX.XXXX..",
        ".XXXXX.XXXXX.",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXX.XXXXXX",
        "XXXXXXXXXXXXX"]

ROWS = len(BOOK)
COLS = len(BOOK[0])

if any(len(row) != COLS for row in BOOK):
    raise SystemExit("the rows of BOOK are not all the same width")

PPC = 3            # texture pixels per cell
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "guide-icon.png")

write_rgba(OUT, COLS * PPC, ROWS * PPC,
           lambda px, py: BOOK[py // PPC][px // PPC] == "X")
print(f"wrote {OUT} ({COLS * PPC}x{ROWS * PPC}px, "
      f"{COLS}x{ROWS} cells at {PPC}px/cell)")
