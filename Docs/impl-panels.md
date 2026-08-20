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
See `InspectorPanel` and `SendPanel`.

**A panel's header is its subject, not its name** — *Note Tile*, *Channel Start Tile* —
since which panel it is was never in doubt and the thing changes under the cursor.
Panels that never change subject do not ask for one. A group inside a panel is named by
the same rule. See `Controls.Panel`.

**A panel draws no outline and cuts no corners.** What tells a panel from the plane is a
lighter ground with air around it. A corner radius means one thing here: something a
hand picks up.

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

**Everything the transport row switches starts off**, and they stand in the order of how
much each one reaches. A switch that starts on is a decision nobody made — the one
exception is the auditioning, and `SystemPanel` argues why that one is the rule rather
than an exception to it.

`System` is the row's own way of not growing: the next question of its kind arrives as a
row on that panel rather than as a sixth switch. What belongs there is what is about the
app or the machine rather than about the piece, which is the same test the output volume
was moved by — see [impl-mix.md].

[impl-mix.md]: impl-mix.md

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
| The DSP buffer setting, and why it applies at the next launch | `DspBuffer`, `SystemPanel` |
| Opening the score folder, and why only on a desktop | `SystemPanel` |
| Scrolling a row or a column that holds more than the screen | `ScrollStrip` |
| What the screen keeps for itself, and the four insets | `SafeArea`, `JacquardUI.FollowTheSafeArea` |
| The wordmark, held off the corner rather than off the safe area | `JacquardUI.MarkAir` |
