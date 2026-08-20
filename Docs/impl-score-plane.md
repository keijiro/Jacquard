Score plane
===========

The grid the score is written on: how a cell is drawn, what a gesture on one means, and
what a lane is allowed to occupy. The code is `ScoreView`, `ScrollArea` and `Style` in
`Assets/Jacquard/UI`, with the editing operations in `Assets/Jacquard/App`. The chrome
around the plane is [impl-panels.md]; the rules being implemented are
[sequencer-spec.md].

Each of those files argues for its own numbers. What is here is the four rules that hold
across them.

[impl-panels.md]: impl-panels.md
[sequencer-spec.md]: sequencer-spec.md

The cell pitch lives in one place
---------------------------------

A cell is 30x32 with a 4px gutter, set by what has to fit inside one rather than by
taste. **Keeping those numbers in `Style` alone is what lets the painted layers and the
tile elements agree on where a cell is to the pixel** — so a change to the pitch is a
change in `Style` and nowhere else, and anything that measures a cell asks `Style` rather
than writing 34 down.

The pitch is also the one metric that deliberately does **not** move with the touch
profile: the score already read right on the iPad and only the chrome did not. That will
change when the plane can be pinched — see [impl-style.md].

[impl-style.md]: impl-style.md

A lane owns its whole row, written on or not
--------------------------------------------

**What a lane occupies is the run it plays through** — the rail from the head to the
terminator, and whatever hangs under it — rather than the tiles written on it so far. An
empty step is where a lane is *going*, not ground going spare.

This is a model invariant and not a view rule. `Lane.Owns` answers for the rail whether a
tile sits on it or not, and `Score.IsFree` is one call to that per lane. Read off the
tiles instead — which is how it began — a stack could grow down across a rail plainly
drawn on screen, and whichever lane came second in the list lost those cells entirely,
since `Score.At` hands a contested cell to the first lane that claims it.

Two things follow, and both are asked of the model rather than of a caller:

- **Ground another lane owns refuses a lane.**
- **A lane with nowhere for its terminator to move into cannot grow** — `Score.HasRoomToGrow`,
  asked by the `TERM` cell and by the Steps control alike, which used to disagree.
- **A paste is refused whole or not at all** — `Score.PlaceStack` looks at the ground the
  whole run needs before writing a single tile.

A drag means whatever the cell under it holds
---------------------------------------------

A tile or a lane head has something to carry, so a drag there carries it; free ground has
nothing to carry, so a drag there moves the plane. **`ScoreView` stops only the presses it
takes and `ScrollArea` pans whatever reaches it**, which is to say whatever nobody
claimed — so neither has to know what the other is for. That division is what replaced a
modifier key the iPad could not offer.

Two numbers are shared rather than written twice, and both are properties of the hand
rather than of what is under it:

- **Four pixels of travel separate a pan from a tap**, since a fingertip does not hold
  still. `ScrollStrip` uses the same distance.
- **The double click interval lives on `Controls`**, so the plane and `ValueBar` cannot
  disagree about how quick a double click is. Both also count the presses themselves: the
  event's own count goes by the clock alone, and a press on one cell, one on its
  neighbour and one back on the first arrives as a count of three.

A third thing is shared and it is not the hand's doing: **a touch drag arrives as two
presses**, and the second one landed on the cell the drag started from, which every double
click test here read as a double click. Ignoring it is one line — see the rule in
[impl-panels.md] and `Controls.PressAlreadyHeld` for what the platform does.

**A drag beginning at the very bottom of the screen is not the plane's to have.** A phone
reads it as the gesture that puts the app away and claims it before the app hears the
finger land. `ScrollArea.DeadBottom` is that strip, set from the safe area by the chrome.
Only presses, and only that edge — a tap down there still edits a cell, since a tap is
not what the system is watching for.

The plane grows by moving the score
-----------------------------------

The plane keeps ten columns and eight rows of empty ground past the score. To the right
and below that falls out of the plane's own size; to the left and above it cannot, since
the plane starts at cell (0,0) and no lane can hold a coordinate before it. **So the score
is carried further in instead** — `ScoreView.Reframe` over `Score.Translate`.

That is safe to do while the sequence plays, and the reason is worth knowing: **a lane is
the only thing that holds a position at all.** A tile knows nothing about where it is, a
jump reaches its branch lane by reference, a runner carries a lane and a step index, and
everything positional read of a score is read relatively — the order `ChannelLanes` gives
and the `MasterLane` that falls out of it. An ordering does not notice a translation.

Three consequences:

- **The rule is one sided** — at *least* ten columns, so surplus margin is left where it
  is. Otherwise dragging a lane back to the right would haul the whole score after it and
  rewrite every coordinate in the file for nothing.
- **The scroll offset has to take up the same distance in the same breath**, against a
  plane that has not been laid out yet. That is what `ScrollArea` holding a *requested*
  offset apart from the one in force is for, and why `Reveal` reads the requested one.
- **A score arriving is not a score moving**, and has to be told apart from one. How far
  an incoming score sits from the corner is a fact about its file, so taking it up would
  carry the plane off by an arbitrary distance. `ScoreView.Score` normalises and stops
  there. What holds two scores together is that both end at the same corner, so the one
  coming in appears where the one going out was — which is why the seam at the turn of a
  piece needs no scrolling of its own.

Where the rest is written
-------------------------

| | |
| --- | --- |
| The accidental gutter, the note size, and why the gutter is fixed pixels | `Style`, `TileIcons` |
| Chain lines drawn only within a stack | `ScoreView` |
| Carrying a tile, a sub-stack, or a whole lane | `ScoreEditor`, `Score.DropLane` |
| Copying a stack, and why the flow tiles have no copy | `Tile.Copy`, `Score.PlaceStack` |
| Why a copy is written by hand rather than through `ProjectFormat` | `Tile.Copy` |
| Framing the startup score, and where the cursor lands on it | `ScoreView.ShowScore` |
