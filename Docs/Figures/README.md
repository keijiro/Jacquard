# Figures

The pictures the tile rules are illustrated with, and the scores they are pictures
of. `01` to `12` are the top level README's; `13` to `17` are the user guide's, in
the jacquard-doc repository, and are copied into `assets/figures/` there.

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
- switch the visualizer off, since silent it still draws one flat line across the
  middle of the screen and the crop is as likely as not to be cut across it,
- `ScreenCapture.CaptureScreenshot`, then cut out
  `Style.CellOrigin(MinX, MinY)` to the bottom right cell of the score plus a
  margin of ten, all times the panel scale.

Two of the guide's figures carry labels — `13` names the parts of a lane and `14`
names each of the nine tiles — and the labels are not drawn on the picture
afterwards. They go on the screen as one more layer of the app's own interface, set
in Jura at `Controls.FontSize` in `Style.Label`, with leaders in axis-aligned
segments the way the plane draws a jump link, and are captured with everything else.
That is the same bargain the rest of this file makes: a figure is a picture of the
app rather than a drawing of one, and a label that is the app's own ink cannot drift
from the ink beside it.

The margin is what leaves the lattice dots room around the tiles. The scale is
what makes the crop exactly twice the size the UI is laid out at, which is why
the README asks for each one at half its pixel width: the figure is drawn at the
size the interface is drawn at, and stays sharp on a display that has the pixels
for it.
