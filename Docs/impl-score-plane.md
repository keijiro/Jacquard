Score plane
===========

The grid the score is written on: how a cell is drawn, what a gesture on one
means, and what a lane is allowed to occupy. The code is `ScoreView`,
`ScrollArea` and `Style` in `Assets/Jacquard/UI`, with the editing operations in
`Assets/Jacquard/App`. The chrome around the plane is [impl-panels.md].

[impl-panels.md]: impl-panels.md
[sequencer-spec.md]: sequencer-spec.md

Cell pitch, and what is derived from it
---------------------------------------

**The cell pitch is what the rest of the plane is derived from.** A cell is
30x32 with a 4px gutter, set by what has to fit inside one rather than by taste:
a sharp note name is a little over twenty pixels wide, and
the icons are drawn in a 15x15 box. Keeping those numbers in `Style` alone is
what lets the painted layers and the tile elements agree on where a cell is to
the pixel.

**The accidental gutter stands only on the notes that have an accidental.** It used
to stand on every one of them, so that the letter kept its place as a note was
transposed through a sharp and back — which put a gap in the middle of every plain
name, five pixels of the twenty a name has to fit in, to spare a movement nobody was
watching for. A name is read; it is not aligned against the name it was a moment ago.
What remains of the rule is that the gutter is a fixed five pixels rather than a
share of the type size, because what it holds is four 1px strokes and a scaled gutter
would put them on half pixels at most sizes.

The note is set at 13 rather than 15 for the same reason the gutter went: a name that
fills its cell to the border reads as a cell crammed with a name. Nothing else moved
with it — the length under a name and the `CHAN` head were already smaller — so the
note is still plainly the content of the cell and the rest is still plainly labels.

Chain lines
-----------

**Chain lines** are drawn only between cells of the same stack. The mockup
joined whatever happened to sit directly above, which made two unrelated lanes
look connected; [sequencer-spec.md] left that undecided, and knowing the lane
settles it.

Moving a tile, and moving a lane
--------------------------------

**A tile is moved by carrying it**, which is the one edit with no button behind
it: where a tile goes is a position, and a plane is already the thing that
answers positions. Dragging a tile within its own step reorders the stack, one
tile at a time; dragging it to any other step takes the run of tiles hanging
below it along, because what a gate or a lock governs is exactly what hangs under
it and a sub-stack left behind would fall under whatever the move left above it.
A drop lands wherever a placed tile could — a step, the cell under a stack, the
`TERM` cell that grows the lane — with the one difference that it may land on an
occupied cell and open the stack up, which is what reordering one is. Dragging a
`CHAN` or `JDST` cell carries the whole lane, and is what replaced the nudge
buttons the Tile panel used to carry: a lane further down runs later, so moving
one is a thing to watch happen against the lanes it will now overwrite rather
than to arrive at a cell at a time.

Copying a stack
---------------

**A double click copies a stack, where it used to write a note.** The gesture asks
the same question a drag does — this cell, then that one — about a shape that stays
where it is: on a tile it takes that tile and everything under it, and on ground that
would take a tile it puts the last copy down. What it replaced was a second way of
doing what the `NOTE` button does, on the cell the button was already offered on;
what it does now is the edit that had no way of being made at all. A chord, or a gate
with what it governs, could be carried to another step or built again a cell at a
time, and nothing could make a second one.

**The flow tiles have no copy, which is a fact about them rather than a rule over
them.** A `CHAN` names a lane, a `JUMP` *is* the identity its branch lane answers to
(`Lane.JumpSource` holds the tile itself), and a `TERM` is implied one column past
the last step and never stored. None of them means anything a cell away from where
it stands, so `Tile.Copy` returns nothing for them and that is the whole of what
keeps them out: a jump in the middle of a stack is stepped over rather than ending
the walk — it is not the bottom of the stack, and what hangs under it is still under
everything above it — and a double click on one does nothing.

A copy is written out by hand rather than round-tripped through `ProjectFormat`,
which has a text for every tile already. That text is a file's spelling: reading it
back is private, throws on anything it cannot parse, wants the file version to go
with it, and forgets a cycle gate's laps above its period. Copying is also done
twice, once into the clipboard and once out of it, so that editing the tiles a copy
came from leaves it alone and two pastes are two stacks rather than one written in
two places.

**A paste is refused whole or not at all.** `Score.PlaceStack` looks at the ground
the whole run needs before it writes a single tile, since `Score.Place` in a loop
would leave half a stack growing out of a step when the cell after next turns out to
be somebody else's. It lands only where a placed tile could, which is where the Tile
panel offers one, and only at the bottom of a stack: `Place` also takes a depth that
is already filled and overwrites it, which for one tile is a tile changing its mind
and for a run would be the rest of the stack disappearing under it.

**With nothing copied yet, the gesture does nothing** rather than falling back on
the note it used to write. One gesture reading two ways depending on what was done
ten minutes ago is a gesture nobody can aim, and the fallback would be invisible
exactly when it fired — on the empty cell, which is where a paste is aimed too. The
cells a copy came from light up for a fifth of a second instead, drawn by the same
overlay as the drop cells and saying the same kind of thing: these cells and not
those. What was stepped over does not light, so what a copy left behind is visible
without a word for it.

**The plane counts the clicks itself, because the event's own count goes by the
clock alone.** A press on one cell, one on its neighbour and one back on the first
arrives as a click count of three, and taking that at its word fired the gesture on
a cell nobody had pressed twice running — which for a copy is a wrong cell copied
and for a lane is a part stopping mid-piece. What the gesture means is *this cell
and then this cell*, so `ScoreView` keeps the last cell pressed beside the time and
asks for both. The cell is the half that matters: the copy this usually stands for
is a question about a position and never about a rhythm. The interval is forgotten
once it has been spent, so a third press starts a new pair rather than making a
second double out of the same click.

`ValueBar` already hand-rolled this for the same shape of reason — there a press
that scrubbed must not count as the first of two — so the length of the interval
lives on `Controls` and both read it. Two gestures on one screen disagreeing about
how quick a double click is would be a hand that could learn neither.

What a drag means
-----------------

**A drag means whatever the cell under it holds.** A tile or a lane head has
something to carry, so a drag there carries it; free ground has nothing to carry,
so a drag there moves the plane instead. Panning used to ask for a wheel event or
a drag with command held, and a touch screen offers neither, which left the plane
fixed on the iPad — most of what a score plane is for. The modifier was never the
point, only a way of telling a press that means *move this* from one that means
*edit this*, and the cell answers that by itself. So `ScoreView` stops only the
presses it takes and `ScrollArea` pans whatever reaches it, which is to say
whatever nobody claimed; neither has to know what the other is for. Four pixels of
travel separate a pan from a tap, since a fingertip does not hold still, and a
click on bare ground still moves the cursor as it always did.

What a lane owns
----------------

**A lane owns its whole row, written on or not.** What a lane occupies is the run
it plays through — the rail from the head to the terminator, and whatever hangs
under it — rather than the tiles that happen to be written on it so far. An empty
step is where a lane is *going*, not ground going spare, so `Lane.Owns` answers
for the rail whether a tile sits on it or not, and `Score.IsFree` is one call to
that per lane rather than a walk over every cell.

Occupancy used to be read off the tiles, which let a stack grow down across a
rail that is plainly drawn on the screen, and let a lane be carried onto one.
Whichever lane came second in the list then lost those cells entirely, since
`Score.At` hands a contested cell to the first lane that claims it. Nothing about
that was specific to dragging — placing a tile had always allowed it, one cell at
a time — so the fix is in what a lane *is* and every caller simply gets the
stricter answer it already wanted.

Room to grow
------------

**Ground another lane owns refuses a lane**, and a lane with nowhere for its
terminator to move into cannot grow. The nudge buttons never checked the first,
and the `TERM` cell never checked the second, so a lane could be grown onto its
neighbour by putting a tile down while the Steps control beside it refused the
same growth. Both now ask `Score.HasRoomToGrow`. A drop that cannot happen has
nowhere lit up for it, which says so without a second colour.

Growing the plane on all four sides
-----------------------------------

**The plane grows on all four sides, and the score is what moves.** It keeps ten
columns and eight rows of empty ground past the score, which for the right and below
falls out of the plane's own size and for the left and above cannot: the plane starts
at cell (0,0) and a coordinate before it is a coordinate no lane can hold. So
`ScoreView.Reframe` carries the score further in instead — `Score.Translate` over
every lane, since a lane is the only thing that holds a position at all. A tile knows
nothing about where it is, a jump reaches its branch lane by reference, and a runner
carries a lane and a step index, so this is safe to do while the sequence plays;
everything positional that is read of a score is read relatively, both the order
`ChannelLanes` gives and the `MasterLane` that falls out of it, and an ordering does
not notice a translation.

The alternative was negative coordinates and an origin on the view, which keeps a
lane's saved position as an identity but has to be threaded through every pixel and
every floor in the model. Neither avoids the hard part, which is that moving the score
moves everything drawn: the scroll offset has to take up exactly the same distance in
the same breath, and the plane it is being clamped against has not been laid out yet.
That is what `ScrollArea` holding a requested offset apart from the one in force is
for, and it is also why `Reveal` reads the requested one — a cursor moved by an edit is
a cursor moved in the same frame the plane grew.

A score *arriving* is the one case where none of that applies, and it has to be told
apart from a score moving. How far a score coming in off a file has to travel to reach
the corner is a fact about the file and nothing to do with what is on the screen, so
taking it up would carry the plane off by an arbitrary distance — far enough, measured
at 8 columns and 6 rows on one of the saved scores here, to leave the incoming score
off the edge of the viewport. So `ScoreView.Score` notes that it was handed a different
score and the next reframe normalises it and stops there: the cursor stays on the cell
it was on and the viewport does not move. What holds two scores together is that both
end up at the same corner, so the one coming in appears exactly where the one going out
was — which at the turn of a piece is the whole point, and the reason the seam needs no
scrolling of its own. The startup score is the one that is framed, by `ShowScore`, and
that is also where the cursor is put on the score rather than left in the margin: it
used to arrive on the first lane's head by standing still at cell (1,1), and a score
that begins further in has to be asked for.

The rule is one sided: at *least* ten columns, so surplus margin is left where it is.
Dragging a lane back to the right or deleting the leftmost one would otherwise haul
the whole score after it and rewrite every coordinate in the file for nothing. The one
thing that had to move with it is where a branch lane goes, which used to be floored
against the plane's edge and would have landed in the margin — a jump is not a request
to widen the plane.
