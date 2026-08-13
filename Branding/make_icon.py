"""Write the Jacquard app icon: the wordmark's J, centred, white on black.

A flat square RGB PNG with no platform frame, mask, rounding or alpha.  macOS 26
and iOS 26 both mask a full bleed square into their own shape, so this one file
serves both; the traditional inset-and-rounded Mac artwork is scaled up by those
systems and is worse.  The cells are scaled by a whole number of pixels so every
dot stays square, and the leftover pixels become black margin, split evenly.
"""
import os
from jacquard_grid import crop, grid, j_only
from pngwrite import write_rgb

HERE = os.path.dirname(os.path.abspath(__file__))

# (file, pixels, canvas in cells, spend the odd cell below the mark).
#
# The app icon is cut into a rounded shape by both systems, so the J is given air
# to be cut into: at a canvas of one em it came within three cells of the curve
# and read as crowded against it. Twenty-six leaves it standing in its frame
# about as deep as Apple's own marks do, and it is an odd number of cells clear
# of the J, which is what makes the last line below possible.
#
# Centred on its ink the mark reads as sitting high -- it is a cap with a
# descender hung under it -- so the odd cell is spent underneath.
#
# The favicon keeps the tighter canvas and the older bias. Nothing frames it, it
# is read at sixteen pixels where air is the first thing that cannot be
# afforded, and with one cell to give the choice is only which edge the mark
# touches: the cap at the top rather than the descender off the bottom.
OUTPUTS = [("icon.png", 1024, 26, True), ("favicon.png", 64, 16, False)]

j = crop(j_only(grid))
jh, jw = len(j), len(j[0])


def draw(name, size, canvas, sink):
    if jw > canvas or jh > canvas:
        raise SystemExit(f"the J is {jw}x{jh} cells, too big for {canvas}")

    # Centre the J on the canvas; the odd cell, if there is one, goes to the left
    # and to whichever side of the mark the caller asked for.
    cx0, cy0 = (canvas - jw) // 2, (canvas - jh + (1 if sink else 0)) // 2
    cells = [[False] * canvas for _ in range(canvas)]
    for cy, row in enumerate(j):
        for cx, v in enumerate(row):
            cells[cy0 + cy][cx0 + cx] = v

    scale = size // canvas
    margin = (size - canvas * scale) // 2

    def is_lit(px, py):
        cx, cy = (px - margin) // scale, (py - margin) // scale
        return (margin <= px and margin <= py
                and 0 <= cx < canvas and 0 <= cy < canvas and cells[cy][cx])

    path = os.path.join(HERE, name)
    write_rgb(path, size, size, is_lit)
    print(f"wrote {path} ({size}x{size}, J is {jw}x{jh} cells on a {canvas} "
          f"canvas at {scale}px/cell, {margin}px spare margin, "
          f"{cy0} cells over it and {canvas - jh - cy0} under)")


for output in OUTPUTS:
    draw(*output)
