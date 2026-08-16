# Figures

The pictures the top level README illustrates the tile rules with, and the scores
they are pictures of.

Each `.jacquard` file here is a score written for one figure and nothing else: it
holds the tiles that paragraph is about and no others, so the picture and the
sentence beside it say the same thing. They are ordinary score files — the app
reads them, and the way to change a figure is to change the score and take the
picture again.

To retake one, load the score in the app and crop the plane to the score's own
bounds:

- copy the file into the score folder (`Application.persistentDataPath/Scores`)
  and load it,
- put the panel at a scale of 2 so a UI pixel is two device pixels, and move the
  cursor off the score so its outline is not in the picture,
- `ScreenCapture.CaptureScreenshot`, then cut out
  `Style.CellOrigin(MinX, MinY)` to the bottom right cell of the score plus a
  margin of ten, all times the panel scale.

The margin is what leaves the lattice dots room around the tiles. The scale is
what makes the crop exactly twice the size the UI is laid out at, which is why
the README asks for each one at half its pixel width: the figure is drawn at the
size the interface is drawn at, and stays sharp on a display that has the pixels
for it.
