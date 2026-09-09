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

Needs `fonttools` and nothing else, bar the last row.

| Script | Writes | Which then goes to |
| --- | --- | --- |
| `make_logo.py` | `jacquard-logo.svg` | the top of the repository README, at 500px |
| `make_logo_png.py` | `logo-bar.png` | copy to `Assets/Branding/Logo.png` |
| `make_icon.py` | `icon.png`, `icon-android.png`, `favicon.png` | copy the first two to `Assets/Branding/AppIcon.png` and `AppIconAndroid.png` |
| `make_favicon_svg.py` | `favicon.svg` | with `favicon.png`, base64 into the two `<link rel="icon">` in `Assets/WebGLTemplates/Jacquard/index.html` |
| `make_feature_graphic.py` | `feature-graphic.png` | uploaded to Google Play by hand |

Only `jacquard-logo.svg` is committed from that column; the rest are intermediates
whose used copy lives where the last column says, and they are ignored here. The
feature graphic is the one that is neither -- see below.

The store graphic
-----------------

Google Play's feature graphic is 1024x500 of the app's own plane with the wordmark
over it, and it takes two steps rather than one because the background is a
capture and not a drawing. **Jacquard > Capture Score Plane** writes `plane.png`,
which is the whole of sample4 at twice the size the interface is laid out at, and
`make_feature_graphic.py` cuts the graphic out of that. Each of the two argues for
itself where it lives: `Assets/Editor/PlaneCapture.cs` for why a plate is rendered
rather than screenshotted, the script for what the crop takes, what the two washes
over it are for, and why nothing is written on it but the name.

What spans them is `plane.txt`, written beside the plate: the cell pitch and the
scale it was captured at, so the crop can be written in the plane's own cells and
neither file holds a second copy of the interface's metrics.

That script wants Pillow as well as fontTools, which no other script here does,
for the resample the reduction from the plate needs.

Neither `plane.png` nor `feature-graphic.png` is committed. The plate is an
intermediate like the rest of this folder's output; the graphic is not one, and it
is still not committed, because the store listing lives in the store -- which is
the argument the repository's own `.gitignore` makes for `/metadata/` and
`/screenshots/`.

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
Both of Apple's systems cut an icon into a rounded shape of their own, and the J is
given air to be cut into: at a canvas of one em it came within three cells of the
curve and read as crowded. It is a full bleed opaque square, which is what macOS 26
wants — the traditional inset-and-rounded Mac artwork is scaled up to fill the
shape, so it comes out larger than it was drawn. One file serves both of those, and
in Unity it is set as the **Default Icon**.

Android takes a second file rather than a second drawing. Its adaptive icon is a
layer of 108dp of which only the middle 72 is guaranteed — the ring around that is
what a launcher may slide for parallax or shave off with a mask of its own — so the
same J is cut again on a canvas half again as wide, thirty-nine cells, and comes
out of the mask the size the other systems cut it to. Unity 6.6 offers no other
icon kind for the platform: the Default Icon is not among them, which is why the
device showed Unity's own mark until this was set. The file goes into **both** the
adaptive background and the adaptive foreground. The foreground is opaque and full
bleed, so it is the whole icon and the background is only what a launcher slides
into view when it animates; giving both the same file makes that the black the
mark already stands on.

**The favicon** is the same J on a canvas of sixteen, where nothing frames it and
air is the first thing that cannot be afforded at sixteen pixels. It is served as
an SVG that inks itself from `prefers-color-scheme`, with the PNG under it as a
fallback. Safari puts a white plate behind a favicon it reads as too dark for the
tab bar, which draws a ring around a black tile; a mark that inks itself light on
a dark tab gives it nothing to correct. The switch is inside the SVG because
Safari ignores `media` on the link element and honours it there.

[Jacquard 12]: https://fonts.google.com/specimen/Jacquard+12
