"""Google Play's feature graphic: the plane, a scrim, and the wordmark on it.

The store wants 1024x500 exactly, opaque, and it is cropped by whatever surface
shows it -- so nothing that has to be read goes near an edge.  The background is
the app's own plane and not a drawing of one: Jacquard > Capture Score Plane
writes the plate this cuts from, which is the whole of sample4 at twice the size
the interface is laid out at.  What the crop takes is the piece of it around the
CH2 lane and down, so that the picture is a score being played rather than a
corner of an empty grid.

The mark stands alone on it, and nothing here is set in type.  The picture
already says tiles by being a picture of them, and the store has a name and a
description of its own either side of this image.

Needs Pillow as well as fontTools, which no other script here does, for the
resample the reduction from the plate needs.
"""
import math
import os

from PIL import Image, ImageDraw

from jacquard_grid import COLS, ROWS, grid

HERE = os.path.dirname(os.path.abspath(__file__))
PLATE = os.path.join(HERE, "plane.png")
GRID = os.path.join(HERE, "plane.txt")
OUT = os.path.join(HERE, "feature-graphic.png")

WIDTH, HEIGHT = 1024, 500

# The crop, in the plane's own cells.  What is set is the two rows it runs between
# and the column it is centred on; the width follows from the store's aspect, so
# taking in a row moves both side edges with it and the score stays where it is in
# the frame.
#
# The top row is the one above the CH2 lane, which is a gutter and half a lane of
# CH1's own -- the picture starts inside the score rather than at the top of it,
# and what it says is that there is more of this above the frame.  Both edges fall
# in a gutter rather than through a cell, which is what the four units either side
# of the rows are.
TOP_ROW = 10
BOTTOM_ROW = 26
CENTRE_COLUMN = 32.5

# The scrim, which is two washes and not one.
#
# The ramp is the one that is asked for: the score is nearly untouched at the top
# and most of the way under by the bottom, so the picture recedes rather than
# sitting there flat behind the mark.  A flat wash over the whole thing would take
# the tiles down with the ground they stand on and leave nothing to look at.
RAMP_TOP = 0.10
RAMP_BOTTOM = 0.90

# And it is not quite black.  The interface itself is grey through and through and
# argues for it -- Style says a tint left the mid greys sitting between two hues
# rather than on one scale -- but that argument is about a screen being worked in
# for an hour, and this is one image in a list of them.  A wash with a little
# indigo in it reads as depth rather than as a shade of the same grey the tiles
# stand on, which is the one thing a flat black wash cannot do.
#
# It has to be darker than the ground it is going over, and that is the whole of
# why this number is as low as it is.  The first indigo here was a mid one, and it
# put the far end of the ramp at a higher value than the near end: the score sank
# into it as it was meant to, and the ground came up as it went, so what the eye
# read down the picture was a wash getting lighter.  A ramp whose far end is paler
# than its near end is not one.  Against the plane's own 0x16 that leaves very
# little room, so the colour is carried by being cold rather than by being strong.
#
# Carried by the ramp rather than set on its own: the colour is the wash's own
# colour, so it arrives exactly where the wash is thick and the top of the picture
# stays the grey the app draws.
RAMP_INK = (6, 10, 24)

# The halo is what the ramp cannot do.  The wordmark is white and so are the note
# tiles, and at the point on the ramp the middle of the picture sits at, a lit cell
# behind a letter eats the letter's counters -- which is the one thing this graphic
# cannot afford, since the mark is the whole of what a reader has to come away with.
# Ramping harder to fix it would put the bottom half of the score out altogether.
# So there is a second wash under the mark, wide and soft enough that what it reads
# as is the ground being darker where the mark is.
HALO = 0.68
HALO_WIDTH = 0.62
HALO_HEIGHT = 0.60

# The wordmark, at whole pixels to the cell because the type is a pixel font and a
# fractional cell smears every dot it has.  Eight comes to 576 of the 1024, which is
# as large as the mark can be drawn and still leave a fifth of the width either side
# of it for a surface that crops this.
CELL = 8

# How far the mark sits above the middle.  Dead centre reads low, because the scrim
# is darkest under it and the eye takes the dark under a word as part of the word.
MARK_RISE = 10


def _metrics():
    """The plate's cell geometry, as the capture wrote it down."""
    if not os.path.exists(PLATE) or not os.path.exists(GRID):
        raise SystemExit("no plate here yet -- take one with Jacquard > Capture "
                         "Score Plane, in play mode")

    with open(GRID) as f:
        return {k: float(v) for k, v in
                (line.split() for line in f if line.strip())}


def _crop(plate, m):
    """The piece of the plate the graphic is cut from, at the store's size."""
    scale = m["scale"]
    gutter = (m["stridey"] - m["cellheight"]) * scale

    def y(row):
        return (m["padding"] + row * m["stridey"]) * scale

    top = y(TOP_ROW) - gutter
    height = y(BOTTOM_ROW) + m["cellheight"] * scale + gutter - top
    width = height * WIDTH / HEIGHT
    left = (m["padding"] + CENTRE_COLUMN * m["stridex"]) * scale - width / 2

    box = (round(left), round(top), round(left + width), round(top + height))

    if box[0] < 0 or box[1] < 0:
        raise SystemExit(f"the crop {box} starts off the top or left of the plate")

    if box[2] > plate.width or box[3] > plate.height:
        raise SystemExit(f"the crop {box} runs off a plate of "
                         f"{plate.width}x{plate.height}")

    return plate.crop(box).resize((WIDTH, HEIGHT), Image.LANCZOS)


def _scrim(image, middle):
    """The two washes, over each other, over the picture."""
    ramp = []

    for py in range(HEIGHT):
        t = py / (HEIGHT - 1)
        ramp.append(RAMP_TOP + (RAMP_BOTTOM - RAMP_TOP) * t * t * (3 - 2 * t))

    rx, ry = HALO_WIDTH * WIDTH, HALO_HEIGHT * HEIGHT
    wash = Image.new("L", (WIDTH, HEIGHT))
    pixels = wash.load()

    for py in range(HEIGHT):
        dy = ((py - middle) / ry) ** 2

        for px in range(WIDTH):
            r = math.sqrt(((px - WIDTH / 2) / rx) ** 2 + dy)
            halo = HALO * (1 + math.cos(math.pi * min(r, 1.0))) / 2
            # Two sheets one behind the other, which is what is left of the light
            # after each has had its share rather than the sum of the two.
            pixels[px, py] = round(255 * (1 - (1 - ramp[py]) * (1 - halo)))

    # The ink, at the strength the ramp alone reaches -- so the halo darkens the
    # middle of the picture without also colouring it, which would put a blue patch
    # behind the mark rather than a blue floor under the whole thing.
    ink = Image.new("RGB", (1, HEIGHT))

    for py in range(HEIGHT):
        share = (ramp[py] - RAMP_TOP) / (RAMP_BOTTOM - RAMP_TOP)
        ink.putpixel((0, py), tuple(round(c * share) for c in RAMP_INK))

    return Image.composite(ink.resize((WIDTH, HEIGHT)), image, wash)


def _wordmark(image, top):
    """The word as its own cells, white, one rectangle to a cell."""
    draw = ImageDraw.Draw(image)
    left = (WIDTH - COLS * CELL) // 2

    for cy, row in enumerate(grid):
        for cx, lit in enumerate(row):
            if not lit:
                continue
            x, y = left + cx * CELL, top + cy * CELL
            draw.rectangle((x, y, x + CELL - 1, y + CELL - 1), fill=(255, 255, 255))


def main():
    mark = ROWS * CELL
    top = (HEIGHT - mark) // 2 - MARK_RISE

    image = _scrim(_crop(Image.open(PLATE).convert("RGB"), _metrics()),
                   top + mark / 2)

    _wordmark(image, top)

    image.save(OUT)
    print(f"wrote {OUT} ({WIDTH}x{HEIGHT}px)")


if __name__ == "__main__":
    main()
