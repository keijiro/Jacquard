Panels
======

The chrome around the plane: the panel the cursor answers to, the controls on it, the
transport row that raises the rest, and where all of it stands against the edges of the
screen. Each piece argues for itself in its own file — `Controls`, `InspectorPanel`,
`ValueBar`, `ScrollStrip` and `SafeArea` in `Assets/Jacquard/UI`, with the placing in
`JacquardUI` — so what is here is the rules that hold across all of them and the map of
which file to open.

How any of it is coloured and sized is [impl-style.md]; the one panel that is played
rather than read is [impl-live-fx.md].

[impl-style.md]: impl-style.md
[impl-live-fx.md]: impl-live-fx.md

The rules every panel obeys
---------------------------

These are the ones to check a new panel against. Each is argued where it is implemented;
the point of the list is that none of them is optional.

**One panel shows what the cursor is on, and nothing is toggled.** The Tile panel keeps
the corner and follows the cursor, and everything the cell decides is a group on it —
the tile's rows, the lane a head carries, the sound of the channel a `CHAN` names, the
parameters a lock takes hold of. There is no window to open, and so no state on screen
that the score does not decide. The send effects are the one exception and are the
exception because they have to be: one reverb for the whole project answers to no cell.
The three onboarding pages are the other, and are the exception the opposite way round:
what they are about is the interface rather than anything written on the plane, so there
was never a cell for them to have followed. See `InspectorPanel` and `SendPanel`.

**A panel's header is its subject, not its name** — *Note Tile*, *Channel Start Tile* —
since which panel it is was never in doubt and the thing changes under the cursor.
Panels that never change subject do not ask for one. A group inside a panel is named by
the same rule. See `Controls.Panel`.

**A panel draws no outline and cuts no corners.** What tells a panel from the plane is a
lighter ground with air around it. A corner radius means one thing here: something a
hand picks up. The onboarding panel is the exception and is not on the plane: it comes up
over whatever else is open, and two sheets of one grey overlapping is one shape with a
fold in it — so it alone takes a line, in a shade kept for it, and a shadow under it. See
`OnboardingPanel` and `Style.FrontLine`.

**A panel is spaced out of three numbers** — `Controls.Gap`, `Controls.Inset`,
`Controls.GroupGap` — and nothing in a panel is a number of its own; what is not one of
the three is a stated subtraction from one. **A gap is carried below and to the right, by
the thing above and to the left of it**, so anything wanting more than a gap adds only
what is missing. A control that does not carry its gap is where this goes wrong: see the
note on the tempo bar in `Controls`.

**The rule belongs to the heading**, and it is the only rule left inside a panel. A
heading is as tall as a row, header included.

**The chrome never shows a token.** `PABS` and `GCYC` are how a tile is spelled in a file
and how this codebase names one; on screen it is *Absolute Lock* and *Cycle Gate*. What
the panel hands the editor is a `TileKind` for the same reason. The one token still shown
is a note's, because `A4` is the pitch itself. The same split gives the Live FX buttons a
player's names rather than the code's.

**A number is a bar, not a field**, and its range comes from the synth (`ParamTargets`,
`ParamRanges`) rather than from the UI. A bar reports twice — every value a scrub passes
through for the model, and once more when it settles for whatever should sound. See
`ValueBar`, and `ScoreEditor.Preview` for what a settled bar is allowed to play.

**A bar's range is where its travel is spent, not what the parameter is allowed to be.**
A drag stays inside it; a typed number does not, and is held instead to whatever the
model keeps of it — `ParamTargets.Bound` for a synth parameter, which is wider wherever
there is a reason to be. So the bar reads the answer back after every change that is not
a drag rather than standing at what was typed.

**A name beside a bar is a control, and double clicking it takes the row back.** A lock
lets go of its target and a channel's sound returns to the patch's default, which is one
gesture because from the hand's side it is one thing: the row stops saying anything of
its own. Two clicks rather than one because the name is also what a hand drags a long
column by. It is written once, in `Controls.ActionCaption`, and a row that has no such
place to go back to keeps a plain caption and does nothing.

**A tile is placed from the panel**, since the cursor is already the answer to where. A
cell that will take one offers the tiles; bare ground offers a lane. So there is no
palette to keep in step with what the cursor can accept, and a tile only ever lands on
free ground.

The panels, and what raises each
--------------------------------

| Panel | Raised by | Stands |
| --- | --- | --- |
| Tile | The cursor | Right column, outermost |
| Send FX | Transport switch | Right column, inside the cursor's |
| Channels | Transport switch | Left column |
| Global | Transport switch | Centre |
| System | Transport switch | Centre |
| Live FX | Transport switch | Across the bottom, centred |
| Onboarding | The first launch | Centre, in a layer over all of them, with the screen behind it under a grey |

**Everything the transport row switches starts off**, and they stand in the order of how
much each one reaches. A switch that starts on is a decision nobody made — the one
exception is the auditioning, and `SystemPanel` argues why that one is the rule rather
than an exception to it.

**Onboarding is raised by no switch at all**, which makes it the one panel here that can
be up before anything has been pressed: it is up because a first launch has never been
read. It goes down from a button on itself — the only panel with one, since it is the
only panel with nothing else that could — and what stops it coming back is a box on it.
See `Onboarding` for what that box writes and `OnboardingPanel` for why it is the only
thing on the panel that reaches disk. It shields nothing and locks nothing: the transport
row is a sibling of the body the panel stands in, so the three pages cannot cover the
controls they point at.

**The screen behind it goes under one flat grey with one hole in it**, and the hole is
around the control the page on screen names. It is a sheet laid over rather than an
opacity taken off — the visualizer is drawn by the camera behind the whole interface, so
half of what has to go under is not an element at all. The hole is a vertical slot and
not a frame, since every subject stands on the transport row and fills its height; which
means the dark is in two parts for the same reason the panel cannot cover what it points
at, one sheet over the body and two bands inside the row. Nothing in it is picked, so
darkening the screen does not quietly make the panel the modal it is not. See
`OnboardingShade`, and `JacquardUI.FollowTheSubject` for the page-by-page subject and
for the row being sent to its end when the control a page names is off it.

`System` is the row's own way of not growing: the next question of its kind arrives as a
row on that panel rather than as a sixth switch. What belongs there is what is about the
app or the machine rather than about the piece, which is the same test the output volume
was moved by — see [impl-mix.md].

The guide button is the one thing that ever went the other way, and it could because it
is not a switch: it raises nothing, it carries one character rather than a word, and it stands
past the score controls at the far end of the row rather than among the five. What sent
it there is argued in `JacquardUI.BuildTransportRow`.

[impl-mix.md]: impl-mix.md

**A press a control is already holding is not a press.** A finger's drag arrives as two
`PointerDownEvent`s, a frame and a pixel or two apart, and the second is delivered to
whatever holds the pointer — so everything that begins a gesture on a press has to let it
go by, which is one line and the same line everywhere: `Controls.PressAlreadyHeld`. That
is where it is argued, along with what the Input System does to produce it and what it was
measured at. It is not only the panels' rule: `ScoreView`, `ScrollArea` and `ScrollStrip`
obey it too, and a new control that captures a pointer is the next place it can be
forgotten.

**A setting is pushed or it is pulled, and which one it is decides where it lives.**
Nothing on a panel can reach a MonoBehaviour's `enabled` flag, so the visualizer is
pushed — the panel keeps the answer and hands it to a callback. The auditioning, the
buffer size and the volume are read at the instant they are used, so they are pulled and
the panel only throws the switch. A second `Action<bool>` for one of those would be a
second copy of the answer to fall out of step with the first. See `SystemPanel`.

Where the rest is written
-------------------------

| | |
| --- | --- |
| The bar: travel, tapers, the geometric ranges, the two bars a pitch takes | `ValueBar`, `ParamRanges` |
| Cycle gate switches, and what a cell can show before the tile gives out | `InspectorPanel`, `TileIcons` |
| Why the Sound group and the lock rows are re-bound rather than made again | `InspectorPanel.Refresh` |
| Why a bar's text field waits for the first edit | `ValueBar.BuildInput` |
| The DSP buffer setting, and why it applies at the next launch | `DspBuffer`, `SystemPanel` |
| The one row that leaves the app — the score folder, on a desktop only | `SystemPanel` |
| Why the guide is a button on the row rather than a row on a panel, and the page it opens | `JacquardUI.BuildTransportRow`, `JacquardUI.GuideUrl` |
| The three pages a first launch opens on, and how their pictures are taken | `OnboardingPanel` |
| Darkening everything but the control a page names, while those pages are up | `OnboardingShade` |
| Scrolling a row or a column that holds more than the screen | `ScrollStrip` |
| What the screen keeps for itself, and the four insets | `SafeArea`, `JacquardUI.FollowTheSafeArea` |
| The wordmark, held off the corner rather than off the safe area | `JacquardUI.MarkAir` |
