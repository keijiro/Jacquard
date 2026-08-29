Branding
========

Every mark of the name is the word "Jacquard" set in [Jacquard 12], and each one
is cut from the same source rather than drawn again: the type is a pixel font on
a sixty unit grid, so a glyph reduces to a grid of cells without losing anything,
and `jacquard_grid.py` is that reduction. The glitch — the cap line and
the descenders torn sideways, a short tear through the middle, a few stray cells
at the seams — is applied in cells too, which is what keeps its dots the same size
and on the same grid as the letterforms.

The font is vendored here with its licence so that the marks can be regenerated
without fetching anything.

Regenerating
------------

Needs `fonttools` and nothing else.

| Script | Writes | Which then goes to |
| --- | --- | --- |
| `make_logo.py` | `jacquard-logo.svg` | the top of the repository README, at 500px |
| `make_logo_png.py` | `logo-bar.png` | copy to `Assets/Branding/Logo.png` |
| `make_icon.py` | `icon.png`, `favicon.png` | copy the first to `Assets/Branding/AppIcon.png` |
| `make_favicon_svg.py` | `favicon.svg` | with `favicon.png`, base64 into the two `<link rel="icon">` in `Assets/WebGLTemplates/Jacquard/index.html` |

Only `jacquard-logo.svg` is committed from that column; the rest are intermediates
whose used copy lives where the last column says, and they are ignored here.

The marks
---------

**The wordmark** is white on black with the glitch, and the app draws the same
thing on the left of its transport row from `Logo.png` — a bitmap at three texture
pixels to the cell. The app sizes it against the row rather than from the texture,
so a cell comes to one unit on the desktop and one and a half on a touch screen,
and since the panel resolves two device pixels to a unit on a 2x screen, a cell
there is exactly the three pixels this is cut at and the mark lands pixel for
pixel on an iPad. It was two pixels to the cell while the mark stood at one unit
to the cell, which was the same argument at the size the mark used to be.

**The app icon** is the wordmark's J alone, centred on a canvas of twenty-six cells.
Both current systems cut an icon into a rounded shape of their own, and the J is
given air to be cut into: at a canvas of one em it came within three cells of the
curve and read as crowded. It is a full bleed opaque square, which is what macOS 26
wants — the traditional inset-and-rounded Mac artwork is scaled up to fill the
shape, so it comes out larger than it was drawn. One file serves both systems, and
in Unity it is set as the **Default Icon** only.

**The favicon** is the same J on a canvas of sixteen, where nothing frames it and
air is the first thing that cannot be afforded at sixteen pixels. It is served as
an SVG that inks itself from `prefers-color-scheme`, with the PNG under it as a
fallback. Safari puts a white plate behind a favicon it reads as too dark for the
tab bar, which draws a ring around a black tile; a mark that inks itself light on
a dark tab gives it nothing to correct. The switch is inside the SVG because
Safari ignores `media` on the link element and honours it there.

[Jacquard 12]: https://fonts.google.com/specimen/Jacquard+12
