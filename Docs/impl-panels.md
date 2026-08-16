Panels
======

The chrome around the plane: the panel the cursor answers to, the controls on
it, and the transport row that raises the rest. The code is `Controls`,
`InspectorPanel`, `ValueBar` and `ScrollStrip` in `Assets/Jacquard/UI`. How any
of it is coloured and sized is [impl-style.md]; the one panel that is played
rather than read is [impl-live-fx.md].

[impl-style.md]: impl-style.md
[impl-live-fx.md]: impl-live-fx.md

The panel the cursor answers to
-------------------------------

**One panel shows what the cursor is on**, and nothing is toggled. The Tile panel
keeps the corner and follows the cursor, and everything the cell decides is on it as a
group of its own: the tile's own rows, the lane a head carries, the sound of the
channel a `CHAN` names, the parameters a `PABS` or `PREL` takes hold of. There is no
window to open, and so no state on screen that the score does not decide.

The sound and the lock were panels of their own, stacked under this one and sharing a
slot because no cell is both. Each could only ever be up while this panel was showing
one particular kind of tile — which is a group of this panel wearing a frame, paying a
header, an inset and a panel gap to repeat what the cursor had already said. They are
still the same list of parameters read two ways, what a channel sounds like and what
one step does to it, laid out alike so that one can be read against the other; what
changed is that reading one no longer means reading two headers.

**A panel's header is its subject, not its name.** It reads *Note Tile*, *Cycle Gate
Tile*, *Channel Start Tile* — the kind of panel and the thing it is showing on one
line, since which panel this is was never in doubt and the thing changes under the
cursor. It used to say the kind in the header and repeat the subject in a caption row
underneath, which cost a row to say half of what one line says now. `Controls.Panel`
hands the header label back for this, and the panels that never change subject — Send
FX, Global, Channels, Live FX — simply do not ask for it.

A group inside a panel is named by the same rule. *Sound* needs no number, because the
Channel row is two lines above it; a lock's group is headed *Channel 5*, because a
lock is the one thing here that cannot say which channel it colours — the tile holds
no number and a branch lane borrows one from the jump that reaches it.

**A panel draws no outline and cuts no corners.** It used to do both, in the grey its
own buttons are outlined in and to the same kind of radius they are cut to, which in
the only vocabulary this chrome has says that a panel is a control with smaller ones
inside it. What tells a panel from the plane is that it is a lighter ground with air
around it, which is enough on a screen where nothing else is a filled rectangle that
size. A corner radius now means one thing: a cell or a control, something a hand picks
up.

**A panel is spaced out of three numbers.** `Controls.Gap` is the space between any
two things standing next to each other — two rows, two buttons, a heading and what it
heads; `Controls.Inset` is the panel's own margin from its edge to everything it
holds; and `Controls.GroupGap`, twice a gap, is what parts one group of rows from the
next. Nothing in a panel is a number of its own; what is not one of the three is a
stated subtraction from one.

The subtraction is always the same one. **A gap is carried below and to the right, by
the thing above and to the left of it**, so anything wanting more than a gap adds only
what is missing — a heading that follows anything carries the difference over it — and
the panel's bottom inset is short by a gap because the last row laid one down.

A control that does not carry its gap is where this goes wrong, and the transport row
had the one case of it. A `ValueBar` is built for a panel, where it is the last thing
on its row and the row carries the gap under it, so it has no margin of its own; the
tempo bar stands in a *run* of controls, and the rule that follows it is short on its
left by exactly one gap because whatever precedes a separator has already laid one
down. With the bar carrying nothing, that subtraction came off air that was never
added, and the rule sat three pixels nearer the tempo than the switch after it — the
kind of thing that reads as *wrong* long before it can be named. The bar carries the
gap now, at the one place it stands in a run, rather than the separator learning what
is in front of it.

**The rule belongs to the heading, and it is the only rule left inside a panel.** A
line standing between two groups is owned by neither: it says that something ends here
and something begins, which leaves the first row of a group looking as much like the
end of the one above as the start of its own, so every group opened against a line and
closed against nothing. Under the name it belongs to the name, and the group it heads
runs from that line down to the next patch of air. A panel's own header carries no
mark at all now — it is the one line on a panel in the bright text every caption below
it is not — and a button at the foot of one, the *Delete* the Tile panel ends on, is
parted from the rows above it by the same air rather than by a rule that would be
heading nothing.

**A chooser's arrows are drawn, not typeset.** They were a `<` and a `>`, which is
punctuation borrowed to point: a pair of hairlines at the weight of the type, sitting
where a glyph sits in its line box rather than in the middle of the button, and set in
the same face as the name between them — so the two controls of the row read as more
of the row's text. A filled triangle is the mark itself. It is a `VisualElement` with a
`Painter2D` fill inside the button, the way the sharp beside a note name is an element
and not a glyph, sized off `Controls.FontSize` so the pair grows with the touch profile
and rounded to an even height so the tip lands on a boundary rather than half way
across a pixel. The buttons keep the width they had, since a run of chrome should not
change width because one mark in it stopped being a letter. The stepper keeps its minus
and plus: those are not directions, and there is no shape that says *one less* better
than the word does.

The last piece is that **a heading is as tall as a row**, header included. A control
is a twenty pixel box holding thirteen pixels of text, so a bare line of text between
two of them is short by the air the boxes carry — every gap measures right and the
words still crowd. Given the row height, the panels measure the same read either way.
The rule under a heading runs the width of the panel and not the width of the caption
column, which is the one thing a heading does not take from the captions it is set
in: a caption is that wide so that a column of them lines up with the controls beside
them, and a heading has no control beside it.

**The send effects are the one exception, and they are the exception because they
have to be.** One reverb and one delay for the whole project answer to no cell, so
there is no cursor position that could bring them up; putting a tile on the plane for
the sake of the rule would be inventing score to hold a setting. They pay for the
state they add by not being up unless asked for — a button on the transport row,
which is where what belongs to the project already lives — and that button is the
whole of the switch. One of them wore a close of its own for a while, which was a
control the other panels had no use for and a second way to do what the button on the
row already did. `Controls.Panel` no longer offers one at all: a panel is put away by
whatever put it up, and the header is a title and nothing else. They hang from the top
right in a column of their own, on the inside of the cursor's: a panel that does not
follow the cursor cannot queue up behind panels that do, and beside is where the two
are read together — how much of a channel goes to the reverb is a row of that
channel's sound group, and this is what it goes to. They held the opposite corner
until the channels wanted it, which is the one place a column is never covered by the
cursor's.

**One panel with a heading over each effect, and not a panel each.** It was a panel
each, on the argument that a panel is already the thing that says *this group of rows
is about that*, so a heading inside one was a second answer to a question the panel had
answered. What that left out is what the second panel costs to say it: a header, a
rule, an inset above and below and the gap to the panel under it, paid over again for
something raised by one switch and set in one sitting. Two headings come to about what
that frame did — four units under it on the mouse profile and about as far over it on
the touch one, since a heading is a row and both a row and a frame grow with the
pointer — so the height was never what the split was buying. What the merge buys is
that the column reads as the one thing the Send FX button raises rather than as two
boxes that always arrived together and always left together.

The button that raises them says **Send FX** and not Send. A send is what a *channel*
does, and the amounts are rows of the sound group named after the effect each one
feeds; a
button called Send would be named after the sending and raise none of it. What comes
up is the receiving end — which is why the button names the pair and each panel names
an effect.

Placing a tile from the panel
-----------------------------

**The panel is also where a tile is put down**, since the cursor is already the
answer to where. A cell that will take one — a lane's empty step, the cell under
a stack, the `TERM` cell that grows the lane — offers the tiles instead of a
description of nothing, and bare ground offers a lane to put one on. So there is
no palette to keep in step with what the cursor can accept, no button that
silently does nothing where it stands, and one less row of chrome above the
plane. A tile therefore only ever lands on free ground: a stack is built from the
top down rather than by inserting above what is already there, which is the order
the runner reads it in anyway.

No tokens in the chrome
-----------------------

**The chrome never shows a token.** `PABS` and `GCYC` are how a tile is spelled in
a saved file and how this codebase names one; on screen a tile is *Absolute Lock*
and *Cycle Gate*, on the button that places it and in the header over it afterwards,
so the two read as the same thing. The palette used to be a row of the
four letter codes, which was three buttons to a line and a code to be learned before
any of them meant anything — and there is nowhere else to learn it, since this panel
is the only place a tile is ever chosen. What the panel hands the editor is a
`TileKind` for the same reason: a token passed between them is a token waiting to be
printed. The one token still shown is a note's, because `A4` is the pitch itself
rather than a code standing in for one.

A number is a bar
-----------------

**A number is a bar, not a field.** The readout sits on a bar that fills as the
value rises, dragging scrubs it and a double click types an exact one, so a
parameter shows where it sits inside its useful range as well as what it is. What
that range is comes from the synth itself (`ParamTargets`), which is what lets a
lock's amount be read against what it moves; typing is deliberately not held to
it. A lane's step count is the one number still stepped, since each one is a cell
and growing can be refused.

**A bar reports twice, and the second report is what sounds a note.** The setter
runs at every value a scrub passes through, because the model has to be current —
the sequencer may well be playing through the edit. `ValueBar.Bind`'s optional
`settled` runs once the number has stopped moving instead: at the end of a drag, or
immediately for anything that was never a drag, since a typed value arrives already
decided. The audition of a sound row hangs off it, and so does the note a pitch bar
plays. Sounding a note per event turned a drag down a bar into a burst of a hundred,
none of which was the value being chosen.

That is now the whole of the auditioning. There was an *Audition* button under the
sound rows asking for the same note on demand, and it is gone: a bar that has just
been moved has already played it, and one that has not is one nothing was asked
about.

**Travel is a ratio wherever the range spans decades**, and an exponent is the wrong
shape for one. `Range.Curve` was the first answer, and on an envelope time it put
eleven pixels of travel inside the first millisecond: `pow(p, 3)` has no slope at all
at the bottom, so the number would not move, the sound would not move, and the bar
read as dead until the hand was a tenth of the way along it. `Range.Floor` makes the
travel geometric instead — a step multiplies where a curved one adds, so every pixel
is the same ratio, about a twentieth of the value, from one end to the other. A
millisecond is the floor because it is the shortest time this synth has any use for,
and where a parameter's own low end is zero — a release or a pitch sweep switched off
rather than made brief — the bottom pixel keeps it, since no number of ratios reaches
zero from anywhere. The exponent stays for the ranges it does suit: the gate ratio,
the modulator ratio and the feedback each cover a few octaves at most and their low
ends are audible.

**The readout follows the same rule**, because a fixed number of decimals can only
match a geometric travel at one point along it. Integer milliseconds hid a run of
pixels that had each moved the value by a twentieth, and past a second one pixel
stepped the last digit by ninety. So a geometric bar prints three figures wherever the
value stands — 1.05, 44.7, 299, 2000 — which move exactly when it does, and prints a
bare 0 at the bottom, where the number is a setting rather than a quantity. Neither
half of this touches a value or a file: a taper decides where a number sits on the
travel, not what it is.

Cycle gate switches
-------------------

**A lap of a cycle gate is a switch and not a number.** `CycleGateTile` held one
index into its period, so the only thing it could say was *one lap out of n*: a gate
that wanted the first and the third of four was two gates in two cells, and most
patterns could not be written at all. The whole cycle is one word of bits, so
carrying a switch per lap costs the tile nothing and costs the panel a block of
unlabelled boxes where a second bar used to be. A gate with nothing switched on
never fires, which is inert rather than wrong — the same standing a lock that holds
nothing has.

**The period reaches 32, and the bits above it are kept rather than cleared.** A
period pulled in and let back out finds its switches where it left them, since
nothing but a save reads past the period; a save writes the period's own laps and
forgets the rest, which is what keeps the file a round trip. Version 8 names one lap
by number where version 9 spells the whole pattern, and the two tell themselves
apart without the version reaching the tile at all: the shortest period is two and
the longest lap number is one digit, so a run of digits as long as the period can
only be the pattern.

**The switches are hidden and not rebuilt**, all thirty-two of them standing from
the moment the panel is built. The period is set on a bar directly over them, and a
run that tore itself down as that bar moved would take the drag that was moving it
with it — the same hazard `InspectorPanel.Refresh` exists to avoid.

**What the cell can show gives out before the tile does.** The boxes wrap at four to
a line, because a cell is thirty pixels across whatever the period is and
thirty-two in a row would be a box a pixel wide against a pixel of ground; past
eight it stops counting and draws six and an ellipsis, since nobody reads twelve
boxes off a cell and the exact laps are the panel's business. Six is also what
leaves the ellipsis somewhere to stand: a second line of two leaves two boxes' worth
of ground at the bottom right, so the dots take no width of their own and an elided
icon is the same block as a full one.

**What the boxes give up is width, and they give it up for the margin.** A row fitted
to the cell left the block a pixel and a half from the tile's own outline, which read
as an icon jammed against its frame; it is held five pixels clear on each side now,
which puts a box at three pixels where it used to be five. That is the right way
round for this icon, because the figure is a shape to recognise and not a count to
take off the cell — the panel is where a lap is read one at a time. Filled against
hollow survives the narrowing, which is the one thing that had to.

The panel grows by about a hundred points at the longest period, and it can afford
to: eight switches to a line is a bar of sixteenths and puts thirty-two laps in four
lines, and a gate cell is not a `CHAN` cell, so these switches never stand on the same
panel as the fifteen rows of a channel's sound.

The transport row and the System panel
--------------------------------------

**Everything the transport row switches starts off.** Channels, Send FX, Live FX, Global
and System are five things a cell cannot ask for and so five switches, and none of them
is up until it is asked for: the plane is what the screen is for, and a switch that
starts on is a decision nobody made. They stand in the order of how much each one
reaches — one channel of the mix, what those channels feed, what is played across the
whole of it, what is set across the whole of it, and then what is not about the piece at
all.

**System is the row's own way of not growing.** Every switch up there raises a panel
except one: the visualizer's raised nothing, since what it moves is the component's own
`enabled` flag, which is where a MonoBehaviour's on and off already live — so a
visualizer nobody asked for costs a frame nothing at all, and it is also why that
component wakes in `Awake` rather than `Start`, which never runs on something that ships
disabled. That switch was a setting standing among the panels the project is made on,
and a second setting of its kind would have been a sixth switch on a row that already
has to be reachable on a tablet. `SystemPanel` is where such a question goes now, and
the visualizer's on and off is the first of them: the panel keeps what was chosen, hands
it to a callback that knows what to do with it, and the next one arrives as a row rather
than as a button on the row.

**What is on it is the app's, not the project's.** Everything else on this screen is
written into the file and comes back with it; this outlasts one project being closed
and another being opened, and would mean nothing to anybody the file is handed to. So
it lives in `PlayerPrefs`, written through on every press rather than left for the quit
— a tablet app is not quit, it is put away and then killed off screen — and the panel
reads it once at construction and applies it, so there is no second place a default is
written down to disagree with. It comes up in the middle beside Global, since neither
is read against anything on the plane; two centred panels stack the way a column does
rather than take turns, so neither switch has to know what the other raised.

**The one button on it opens the folder the scores are in, and only where that means
anything.** Where a file lands is a fact about the machine rather than about the piece,
which is what puts it on this panel and at its foot — `Controls.Foot`, since it is not
one of the settings above it and should not read as the end of that list. It is handed
to `Application.OpenURL` as a file URL built through `System.Uri`, not by writing
`file://` in front of the path: `persistentDataPath` on macOS runs through *Application
Support*, and a raw space is where a URL handler stops reading. The directory is made
first, since it does not exist until the first save and a player who has saved nothing
should still be shown where the scores would go. The whole thing — the row, the handler
and the field it would need — is inside `#if UNITY_EDITOR || UNITY_STANDALONE`. Both
halves are meant: a standalone player is a machine with a file manager on it, and the
editor is one whatever it is currently building for, so the row does not vanish the
moment the target is set to iOS and leave the control untryable. On the phone, the
tablet and the browser it is not built at all rather than built and dimmed — a dimmed
control says *not now*, and this one is *not here*.

**The DSP buffer is the second setting, and the first one that is about the machine
rather than about taste.** The audio thread has exactly one buffer's worth of time to
render one, and what happens when it misses is a hole in the music with a hard edge at
either end — `FmSynthPipeline.Pump` watches for exactly that and now sends whoever is
listening here instead of to Project Settings. A longer buffer buys tolerance and costs
latency: 256 frames is 5.3ms at 48kHz and 1024 is 21.3ms, which is the whole distance
between a live effect that answers the hand and one that answers a moment later. A bar
rather than a chooser, because this is one number with two ends and which end it is
near is the whole of what a hand setting it wants to know. It moves in half buffers,
which is 2.7ms of deadline and the smallest move worth making: seven stops, enough that
the bar is read as a bar rather than as four positions and few enough that every one of
them is a different answer.

**It is applied once, before the synth is built, and the panel says so.** Unity takes
its own figure from `AudioManager.asset` at boot, so a stored number does nothing until
something asks: `AudioSettings.Reset` is that ask, and `JacquardApp.Start` makes it on
the line before `new FmSynth`, while nothing has been allocated against the figure it
replaces. In the ordinary case — the stored number being the one Unity booted with — it
does nothing at all. Reset *does* work with the pipeline running: the audio system
renegotiates the format, `FmSynthControl.Configure` runs again and the mix buffer is
reallocated, which was measured by watching the scope advance in lockstep with the DSP
clock across a change from 256 to 1024. What is not measured is the two numbers
`FmSynthPipeline`'s constructor reads once and hands to `DspClock` — the sample rate and
the buffer, which the slip tolerance and every figure about the lead are grains of — so
applying it live would leave both calibrated for the buffer that is gone. The lead
itself would recover, since a format change is one of the things that asks for it again;
the two constants under it would not. A setting that is honest about when it lands beats one that is
nearly right, so the note under the row reads *Applies at the next launch*, and it is up
only while the setting has moved since the app started.

**Measured against what this launch asked for, not against what is in force.** The two
are the same number on a device that gives exactly what it is handed, and on one that
rounds they are not: comparing against the buffer actually running would leave that note
standing after the launch that applied it, which is the one thing a note about
restarting must never say. Rounding is real and worth knowing about, so it is said once
in the console instead — as is a device refusing the figure outright. Neither stops
anything: the mix is rendered to whatever length the audio system reports, so what is
lost is only that the thread's deadline is not the one that was chosen. Arbitrary
figures do work, incidentally — the three names in Project Settings are an editor
inspector's enum over a plain int, and 768, 400, 333 and 300 were each granted exactly
and rendered in lockstep on macOS.

**The status line went with them.** It was a paragraph of diagnostics written across the
widest part of the row — the cursor position, the voice count, each runner's step and
lap — and it was read by nobody while the row grew five switches that have to be
reachable on a tablet. What is left of it is the one thing that was not a running count:
whatever the file controls have to say, now logged to the console once each time it
changes, because a save that failed has to say so somewhere.

Scrolling the chrome
--------------------

**The chrome scrolls the way the plane does, and takes its presses the other way
round.** A transport row is as wide as the words on its switches and a column of
panels is as long as whatever the cursor is standing on, so both run past a small
screen — and a switch off the edge cannot be pressed, a parameter under the bottom of
the screen cannot be set. `ScrollStrip` moves either of them under a drag along its
one axis, with the content translated rather than laid out again, which is the trick
`ScrollArea` uses on the plane.

What it cannot do is pan whatever press nobody claimed. There is no free ground on a
strip of chrome: three pixels between two switches, three between two rows. So the
press goes to the control it landed on as before, and is *taken away* from it once it
has travelled four pixels along the strip — the capture moves to the strip, which is
also what cancels the click that press was going to be. That cannot be left to the
release landing outside the button, the way a list usually decides, because the button
travels with the strip and stays under the pointer for the whole pan. A strip with
nowhere to go takes nothing from anything, so on a screen that fits, every control
behaves as it did before the strip existed.

Two things are the whole of the difficulty. **A held pointer is delivered to the
holder and to nothing else** — a `TrickleDown` handler on an ancestor sees nothing
from the moment a `Clickable` takes the press — so the strip registers a second copy
of its handlers on whatever the press landed on, for the length of that press, and a
test on who holds the pointer decides which of the two copies answers a move. And **a
control whose own gesture is a drag keeps it**: `ValueBar` is scrubbed with both axes,
so a bar handed the strip's rule would be a bar that cannot be set on exactly the
screens where the rule applies.
