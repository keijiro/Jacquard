Sequencer Specification
=======================

This is the specification of the sequencer: what each element means, why it has the
shape it has, and what is still undecided. The implementation notes that say how the
built app answers these questions are the `impl-*.md` files beside it, and the
terminology fixed here is the terminology the code uses.

Terminology
-----------

The following words are fixed as the symbol names used in the code.

| Word | What it refers to |
| --- | --- |
| `Project` | The unit a file is saved as. Above `Score` |
| `Score` | One grid plane |
| `Cell` | A square on the grid. Includes positions with nothing on them |
| `Lane` | A row of steps running horizontally |
| `Step` | One column within a lane |
| `Stack` | The run of tiles hanging down from a step. There is no limit on its length |
| `Rail` | The dotted time axis running through a lane |
| `Link` | The connection from a `JUMP` to a `JDST` |
| `Tile` | The functional unit placed on a cell. One per cell |
| `NoteTile` | The thing that sounds |
| `ParamTile` | The thing that operates on the attributes of a sound (`PABS` / `PREL`) |
| `GateTile` | The thing that decides whether what follows is processed (`GCYC` / `GPRB`) |
| `FlowTile` | The thing that controls the flow of the sequence (`CHAN` / `TERM` / `JUMP` / `JDST`) |
| `Runner` | The dynamic object that scans a lane and executes its tiles. It exists only during playback |

Everything from `Project` down is static data; the `Runner` alone is born at playback
and moves.

The concrete tile types correspond to the tokens as follows.

| Token | Type name |
| --- | --- |
| (a note name) | `NoteTile` |
| `PABS` | `AbsoluteParamTile` |
| `PREL` | `RelativeParamTile` |
| `GCYC` | `CycleGateTile` |
| `GPRB` | `ProbGateTile` |
| `CHAN` | `ChannelTile` |
| `TERM` | `TerminatorTile` |
| `JUMP` | `JumpTile` |
| `JDST` | `JumpDestTile` |

A category word (`Param` / `Gate`) goes into a concrete name **only where the modifier
alone would not carry the meaning**. `Absolute` and `Cycle` say nothing on their own and
so need the category word, while `Jump`, `Terminator` and `Channel` are perfectly clear
by themselves and are therefore not written as `JumpFlowTile`. What a type belongs to is
what the hierarchy already says, so the name does not say it twice.

### `Project` and `Score`

A `Score` is one plane. Handling several channels does not change that — the policy is
to **lay them out on the same plane**, so there is no plane per channel.

`Project` sits above it and is **the unit a file is saved as**. It holds the settings
that apply to the whole thing, which at present are **the tempo and the time
signature**.

A `Project` currently holds exactly one `Score`. There is an idea of binding several
`Score`s together in the future, but **no necessity for binding them has been found
yet**. So for now `Project` is given no meaning beyond "the thing above `Score` that is
the unit of saving". The definition will be extended the moment a reason to handle
several appears.

There are four categories of tile but only three appearances (`ParamTile` and `GateTile`
both sit on a grey ground and are told apart only by the drawing on the icon). **This
mismatch is known, and the appearance is what will be adjusted to resolve it.** Reducing
the categories to three is not the direction that solves it.

The basic model
---------------

The grid is one plane, and **lanes** are placed on it. A lane is a row of steps running
horizontally: **the horizontal axis is time**, and one column is one step.

**A lane can be placed anywhere on the plane.** This is the central character of this
sequencer, and the absence of constraints on where a lane goes is the point of it. A
lane of another channel, a lane running in parallel on the same channel, a lane that is
a branch destination — no placement rule distinguishes them. **What kind of lane it is
is said by the head cell, not by the position.**

That is not to say position is meaningless. **The vertical position of a `CHAN` tile
decides the execution order of the Runners** (see "Runner"). Kind is not decided by
position; precedence is — that is the split.

The start position is held in grid coordinates, and no two lanes ever occupy the same
cell.

**The character of a lane is decided by the kind of its head cell.**

- `CHAN` at the head → an **independent flow** on that channel. It runs at the same time as the other `CHAN` lanes
- `JDST` at the head → a **branch destination**. It is only ever entered from a `JUMP`

The worked example this specification was written against had three lanes, laid out from
the top like this.

- The accent lane — 4 steps, headed `CHAN:1`. It holds no notes and applies to the lanes below it
- The main lane — 16 steps, headed `CHAN:1`. It runs at the same time as the accent lane
- The variation lane — 6 steps, headed `JDST`. It is flown into from the main lane

The order of those three from top to bottom means something (see "Runner").

One step **stacks vertically the things that happen at the same instant**. The notes of
a chord and the control elements that apply to that step all go into this vertical
stack.

**There is no limit on the length of a stack.** As many tiles can hang down as the empty
ground on the plane allows, and there is no concept of a "slot":
**what a step holds is a variable-length run of tiles**.

The area a lane occupies is therefore not a rectangle. Only the cells that are actually
filled belong to that lane.

### The rule of the vertical chain — processing flows from top to bottom

Vertically adjacent cells are joined by a 1px solid line and read as one mass. That
join has a direction, and the direction means something.

**A stack is processed from the top down, and the effect of a tile reaches only what is
below it.**

- `GCYC` / `GPRB` (firing conditions) — if the condition holds the descent carries on downwards, and if it does not, **the processing ends there**. What is above has already been processed and is unaffected
- `PABS` / `PREL` (parameter locks) — apply to **the sounds made after them**. Not to the one directly below, but to every note below. Notes above are not touched
- Notes — sound with the parameter state as it stands at that depth

A stack therefore reads from the top as **firing condition → parameter → note**. The one
rule is to write a thing **before** whatever it is meant to apply to or control.

**The flow is one-way so that a stack can be read without knowing the tokens in it.**
Every chain line is the same 1px line, so the direction of an effect has to come from the
line and not from the kinds it joins. The grey ground behind the control elements is
therefore nothing but an ornament that helps tell the kinds apart.

This rule works **the same way on a mixed stack** as it does on one arranged by kind. If
`E4 / GCYC4:1000 / C4` is written, for instance,

- `E4` is above the gate, so it **sounds every lap**
- `C4` is below the gate, so it **sounds once every four laps**

**What is placed above a gate is not affected by that gate.** A gate does not apply to
the step it sits in; it applies to what is read after it.

As a consequence, **a tile placed on the rail row (depth 0) is subject to nothing within
its own stack.** Nothing can be placed above it, so it can be given neither a condition
nor a lock. The constraint already stated about `JUMP` reaches notes and locks in
exactly the same way. To make a note on the rail row conditional, put the condition on
the rail row and move the note down by one.

### The rail and its endpoints

A dotted line runs horizontally through each lane. That is the lane's time axis, and it
is bracketed at both ends by cells filled in white.

**Every endpoint that does anything other than simply advance to the right is a cell
filled in white.** That was made a rule. There are four kinds at present.

| Token | Icon | Behaviour |
| --- | --- | --- |
| `CHAN:n` | Text (shown as `CH1`) | The start point of the sequence on channel n |
| `TERM` | An arrow U-turning to the left | The terminator. Returns to the `CHAN` it started from |
| `JUMP` | A rounded Z with an arrow to the right | Flies to another lane |
| `JDST` | A vertical line with an arrow to the right | A destination. The start of a lane |

`TERM` is placed automatically to the right of the last step. It is an explicit cell
rather than something inferred from where the tiles end, which is what keeps the rail's
length independent of what is written on the last step.

**Reaching `TERM` returns to the `CHAN` it started from.** That is true of the `TERM` of
any lane: the variation lane's `TERM` returns to the start point of the channel too,
not to the head of its own lane (the `JDST`). Which is to say that a `JDST` is only ever
entered from a `JUMP`.

The `▶` marker appears when the stack of that step is empty. What results is a row of ▶
standing on nothing but the beats with nothing written on them, which works as a mark
for the stretches the runner passes straight through. That was not designed; it fell out
of the implementation, and it is kept because it helps the score read.

### The three kinds of line

The kind of a line carries meaning.

| Appearance | Meaning |
| --- | --- |
| 1px white solid (vertical) | Joins the stack within one step. Read from top to bottom |
| 2px white dotted (horizontal) | The time axis of a lane. Flows from left to right |
| 1px grey solid (rounded corners, offset from the axes) | The path of a jump across lanes |

The jump line alone does not pass through cell centres: it runs 7.5px to the right of
the column centre and 7.5px below the row centre. That is so it can cross the grid
without flattening the `・` of the lattice, and the result reads as though it sits on a
layer of its own, apart from the rails.

The icons are drawn in a 15×15 viewBox at a stroke width of 1px with the coordinates on
half-integers, so that the centre of a 1px stroke lands on a pixel boundary rather than
smearing across two — which is also why the jump line's offset is the half-pixel 7.5
rather than a round number.

A `JUMP` cell is the one place where two of these three kinds meet. The white solid line
descending the stack arrives from above, and the grey solid line — where the sequence
goes next in time — leaves below.

A chain line is drawn only between cells of the same stack, and not from whether the
cell above happens to be filled — otherwise the tail of one lane's stack and the head of
another read as connected wherever they sit one above the other (see
`impl-score-plane.md`).

The detailed settings go in a separate window
---------------------------------------------

A tile holds more than what is drawn on its cell. **Selecting a tile opens a separate
window, and it is set in detail there.** A cell on the grid need only show the kind of
tile and the values that cannot be left out when reading the score; there is no need to
cram every setting into 30px.

The items this policy resolved.

- **What a parameter lock targets and by how much** — not shown on the tile. The `PABS` / `PREL` icon says only which kind of lock it is, and which parameters move by how much is set in the separate window. There can be several targets, so it would not fit on a cell in the first place
- **The real length of one step** — an option on the `CHAN` tile. The default is a sixteenth note
- **The probability of a `GPRB`** — any float, given as a percentage
- **The period of a `GCYC` and which laps it fires on** — the period runs 2 to 32. The laps it fires on are a run of switches as long as the period, **each lap switched on and off individually**

The last two express their value in the shape of the icon itself, so **the cell does show
their outline**. Exact values are entered in the dialog and the cell shows them as a
figure — that is the division of labour. For `GCYC`, the run of switches on the panel and
the run of rectangles on the cell are **the same arrangement shown at two sizes**.

**A token string is therefore not the data itself but a code for displaying it.** The
argument of `CHAN:1` is the channel number alone, and the step length does not appear in
the token.

Tokens
------

The token that names the kind of a tile is **fixed at four characters**, so that the
option of treating one as a FourCC outright is left open for the future. The ones that
take arguments postfix digits and a colon, as in `GCYC4:0010` / `GPRB:4`, but the part
that names the kind is four characters.

The prefix says the category. `P` is a parameter (`PABS` / `PREL`), `G` is a gate
(`GCYC` / `GPRB`). As with the rule for type names, **a prefix goes only on the ones the
modifier alone leaves ambiguous**, so `JUMP` / `JDST` / `TERM` / `CHAN` do not carry one.

The kind and its argument are always separate, as in `CHAN:1` — no token fuses the two.

### Notes

Written as a note name plus an octave, `C4` / `F#4` / `G#4`. The accidental is shown
raised within the cell.

**The gutter for an accidental is opened only when there is an accidental**, rather than
standing on every note so that a letter keeps its place as it is transposed through a
sharp and back. It is 5px of the cell's 30px width, and **a note name is read; it is not
aligned against the name that was there a moment ago.**

**Length is in units of steps, and the default is one step.** At the default the name
alone is shown, and a number is added as in `C4/4` only when some other length is set.
Within the cell it comes out small, under the name. Values below one such as `0.5` can
be written too.

Which note value one step is is an option on the `CHAN` tile, defaulting to a sixteenth.
How many seconds of real time that note value is depends on the tempo, and the tempo and
the time signature belong to the `Project`. So at the default combination a note with no
number on it is one sixteenth long.

**A semitone is written with ♯ only; ♭ was abolished.** Being able to write the same
pitch two ways would make the same cell look different on the grid, so the notation was
fixed at one. `G#4` cannot be written as `Ab4`.

### Parameter locks

These operate on parameters. **One lock can apply several parameters at once.** It has a
slot per target and applies only the ones it has been given a value for. Nothing happens
to the parameters it does not hold, and the channel's own value passes through
untouched.

**A lock that applies nothing is allowed**, which is what a freshly placed one is. It is
the same standing as a lock with nowhere to go, below.

There are two constraints on what a lock reaches.

- **It always applies to the whole channel.** It is not a per-note attribute
- **It lasts for that step only.** Nothing is left over for the step after it. A channel no lane is holding starts every instant from its own patch

Putting those two together, what a lock actually colours is "whatever is processed after
it, while the step it sits on lasts". How far **after** reaches is decided by where it
was placed: the notes below it in the same stack, and the sounds the lanes further down
the plane make. **A lock therefore goes above the sounds it is meant to reach.**

- **Placed above a note** → it applies to the notes under it
- **Placed in the middle of a chord** → it applies only to the notes below the boundary
- **Placed alone on a lane that holds no notes** → it applies to the sounds the lanes below make while that step lasts. This is the accent lane usage (see "Lanes running in parallel")

**The step is the one on the lane the lock is written on, and a lane's step is as long
as its division says.** While every lane on a channel divides the bar the same way,
"that step" and "that instant" are the same thing. When one of them is coarser they are
not: an eighth-note lock lane over a sixteenth-note lane of notes covers two of them
with one step, and both are coloured, because the step holding the lock has not ended
when the second note sounds. **The cell after it is what lets the channel go** — a step
is read whether or not anything is written on it, and what it holds replaces what the
step before it left, so an empty cell on a lock lane releases.

A lock still standing is read **at the place in the pass its own lane occupies**, so it
reaches exactly what a freshly placed one would: the lanes below it, never the ones
above. An accent lane placed below the notes therefore colours nothing on any instant,
which is the same answer it gets at the instant it is placed on.

**A lock with nowhere to go is a meaningless tile.** A `PABS` placed at the bottom of
the bottommost lane does nothing, because there is nothing processed after it. **This
state is allowed and is not treated as an error.** It is treated the same way as a ring
of `JDST` lanes that no `CHAN` can reach — it simply does not sound, and it does no
harm.

Both icons take a fader as their base shape and are told apart by a modifier on the
right.

| Token | Icon | Meaning |
| --- | --- | --- |
| `PABS` | A fader alone | Sets the parameter to an absolute value |
| `PREL` | A fader plus ↕ | Changes it by a relative amount from the current value |

**There is no third kind that accumulates.** Once the influence of a lock survives
outside the current step, the state the sound being heard is in can no longer be read off
the score, and holding the constraint "a lock lasts for that step only" was worth the
price of not having one. See "Rejected alternatives".

**Which parameters can be targeted is not decided here.** The synth is developed
separately as its own project, and the set of target parameters belongs to the synth
side. It is a matter to be settled at the stage the two are joined, not an open question
on the sequencer side.

**The separate window lists every target and shows the ones not being applied faintly.**
Moving a bar is what makes that parameter locked, and clicking the name lets go of it.
There is no operation called "apply" separate from entering a value, because **a value
nobody has set is not a lock**; a faint row is exactly "the range this tile has not put
a hand on". What a faint row reads out is what the channel would be without the lock —
the current patch value for a `PABS`, and 0 for a `PREL`.

### Firing conditions

These decide whether the processing of the stack continues, by the rule the vertical
chain already states. What they govern is not the one tile directly below but everything
below: notes, locks and `JUMP`s all stop.

Placing several in one stack means the lower ones are only judged when the upper ones
have passed. They become nested conditions.

| Token | Icon | Meaning |
| --- | --- | --- |
| `GCYCn:pattern` | n rectangles, the firing laps filled | Fires only on the laps named by the pattern. `GCYC4:0010` is the 3rd of 4 laps, `GCYC4:1010` is the 1st and the 3rd |
| `GPRB:n` | A pie chart | Fires at random with a probability of n percent |

**A `GCYC` has a switch per lap and not a single number**, so that firing on the 1st and
3rd of four laps is one gate rather than two stacked; a bit per lap of the period costs
nothing but one word. A gate with not one switch on never fires, which is not "wrong" but
"does nothing", and is treated the same way as a lock that applies nothing.

**The period runs 2 to 32.** That is 16 steps over two bars, and around the upper limit
of a length that is worth reading as a figure on a cell.

The rectangles run **at most 4 to a line**, and five or more wrap onto two lines. Laying
32 out on one line would put both the rectangles and the gaps at 1px, and since the
30px cell width does not change whatever the period is, what has to give is the number
per line. **Past 9 it stops counting and shows 6 with an ellipsis.** Nobody counts 12
rectangles off a cell, and the exact specification is the panel's job.

**The figure is not spread to the full width of the cell.** A 5px margin is taken on
each side and the rectangle width is decided inside that (3px when four stand in a row);
any less and the icon reads as stuck to the outline of the tile. The figure of a period is
there **to be recognised as a shape rather than to be counted**, so the rectangle width is
what gives way for the margin. The one thing that cannot give way is the contrast between
filled and hollow, and 3px keeps that.

The number six is also where the ellipsis stands. Since the second line ends at two
rectangles, two rectangles' worth of ground is free at its bottom right, so **the
ellipsis goes there**. It takes no width of its own, so an elided figure and an unelided
one are the same size of block.

**A `GPRB` takes any float as a percentage.** The pie chart shows that proportion as a
sector. The granularity is not tied to the number of divisions a clock-shaped icon is
easy to draw at.

Branching sequences
-------------------

On reaching a `JUMP` cell, the next step is not the one to the right in its own lane but
the head of the lane it connects to. That connection is always to a `JDST` cell.

**A `JUMP` on its own means almost nothing.** If all it does is fly unconditionally, it
is no different from joining the two lanes into one and writing that.

What gives it meaning is **combining it with a firing condition**. Put a condition above
a `JUMP` and it flies only on the laps the condition holds, and carries on to the right
along its own lane when it does not.

- Combined with `GCYC` → the sequence changes to another variation at regular intervals
- Combined with `GPRB` → the sequence changes at random

The worked example this was written against put a `GCYC4:0001` and a `JUMP` on the 10th
step. Once every four laps it flies from there into the six steps of the variation lane,
whose `TERM` returns to `CHAN:1`. Ten steps of the main lane plus six of the variation
make 16 steps, and it was placed so that the length of a lap is the same whether it
flies or not.

Giving it a condition requires the `JUMP` to be at the second level of the stack or
below (the first level is the rail row, and nothing can be placed above it). Therefore
**a `JUMP` placed directly on the rail row can be given no condition, and can only ever
be an unconditional jump.**

**Reaching a `JUMP` does not end the processing of that stack.** All it does is decide
where to go; the tiles below it are processed as belonging to the same instant. The
flying happens from the **next** step. If one stack holds two reachable `JUMP`s, the
lower one is the destination.

### `JUMP` and `JDST` are one to one

**Wherever there is one `JUMP` there is exactly one `JDST` answering to it.** The
converse holds too: there is no `JDST` that nothing flies to. Nor can two `JUMP`s point
at the same `JDST`.

On top of that constraint, these two are allowed.

- **Multi-level branching** — a further `JUMP` may be placed inside a destination lane. A variation of a variation can be built
- **Several `JUMP`s on one lane** — but since it is one to one, each has a destination lane of its own

Being one to one means a destination lane never has more than one entrance. There is no
need to consider which route it was reached by, and it meshes with the rule that a
`TERM` always returns to the `CHAN` it started from.

A closed ring of `JDST` lanes pointing at one another does not violate the one-to-one
rule, incidentally, but it can never sound because no `CHAN` can reach it.

**This state is allowed. It is not treated as an error.** It simply does not sound, so
it does no harm, and there is no motive for going to the trouble of detecting it and
deleting it. It can be passed through temporarily in the middle of an edit, and it may
be built deliberately as a working scratch space, somewhere on the plane to keep
material that is not in use. No reachability check is performed.

Lanes running in parallel
-------------------------

Placing several `CHAN`-headed lanes on the same channel means **they run at the same
time**. Where a branching sequence goes down one route or the other, this is both
flowing at once. Lanes headed by the same `CHAN` start running at the same instant.

Each lane returns to its own `CHAN` at its own `TERM`, so **each lane may have a period
of its own**. Stand a 16-step lane beside a 4-step lane and the latter runs four laps
in one lap of the former. Several uses come out of that.

- **Accent control by beat** — put nothing but parameter locks on a short lane and give the channel an accent at a fixed interval
- **Polyrhythmic auxiliary notes** — put notes on a lane whose length does not divide the main one, and use the drift between the periods
- **An auxiliary sequence of a different length from the main one** — layer something apart from the flow of pitches

The accent lane of the worked example is the first of these. It has a `PREL` on the 1st
and the 3rd of its four steps and holds no notes at all. It runs four laps in the time
the main lane runs one lap of 16 steps, giving the channel an accent every other beat.

The `PREL` on that lane **has nothing in its own stack to apply to**. What it applies to
is the sounds processed after it at the same instant — the notes of the main lane and
the variation lane. **After** is **below** on the plane, so **the accent lane goes at the
very top of it** (see "Runner").

The two `CHAN:1` cells were placed aligned to the same **column** (horizontal position),
so that starting at the same time can be read off the layout; that is not a rule. That
the two **rows** (vertical positions) differ, on the other hand, does mean something —
that is what decides the execution order.

Runner
------

A **Runner** is the dynamic object that scans a lane. It is born from a `CHAN` tile,
moves to the right along the rail, and executes the tiles it meets to control the synth.
Where the score is static data, a Runner **exists only during playback**.

The speed it moves at is decided in two layers. The `Project` holds **the tempo and the
time signature**, and the `CHAN` holds **which note value one step is** (a sixteenth by
default). The former is the measure of time for the whole thing and the latter is the
per-channel division; multiplied together they give the Runner's real speed. **Since the
division can be changed per channel, Runners of different speeds can be run over the
same tempo.**

One Runner is born per `CHAN` lane. A `JUMP` does not create a new Runner; it only moves
an existing one to another lane. Therefore **the number of Runners in existence at once
has the number of `CHAN` lanes as its upper bound, and branching does not increase it**.
Parallel lanes add Runners and branching changes where a Runner goes — that is the
division of labour.

An upper bound, because a `CHAN` lane may have no Runner. See the next section.

### A `CHAN` does not always send one out

**A `CHAN` tile carries an enabled/disabled switch.** A disabled `CHAN` sends no Runner
out, and the lane stops playing at the point the Runner currently running reaches its
`TERM`. **What stops is the end of the lane and not the instant the hand cut it** — the
lap being played is played out.

There are only two instants at which one is sent out.

- **The instant the master lane sends a Runner out**, which is to say the turn of the piece (see "The turn of the piece")
- **The instant a Runner reaches `TERM`** — which, if it is enabled, is the wrap-around it always was

At the start of playback it is the master lane that sends out, so every enabled `CHAN`
starts running at once. A `CHAN` that was disabled and is enabled again **waits for the
next turn of the piece**. The lane that came back therefore comes in at the head of the
lap and in step with the other lanes, rather than wherever the hand happened to move.
**A lane newly drawn during playback waits for the same moment** — there is nothing else
for it to line up with.

A Runner that has been sent out is a new Runner, so **the lap count starts over**. A
`GCYC` reads from the same place it does at the start of playback. Given that a lane
that came back and a lane that was just written have to sound the same, it can be no
other way.

**The master lane cannot be disabled.** The switch can be thrown and it is kept in the
file, but a Runner is sent out whether it is disabled or not. The very lane that hands
out the instant the others start on going silent would mean there is no instant to hand
out. Which lane is the master is decided by position (see "The turn of the piece"), so
**moving lanes about moves which switch it is that is being ignored**.

**This is a different thing from a channel mute.** A mute is **silent but running** —
the laps go on being counted, and letting go means hearing it from wherever the score
has got to. Disabling is **not running** — there is no current position to come back to,
so it starts from the head. There are two switches of similar shape, and that is exactly
where they differ.

### The execution order is decided by the vertical position of the `CHAN`

Runners have an execution order. **The higher the `CHAN` tile that bore it, the earlier;
the lower, the later.**

**That order is the continuation of the same flow as reading a stack from top to
bottom.** What happens at a given instant is processed as one pass, sweeping down the
plane from top to bottom once. The stack of the upper lane is read from top to bottom,
and then the stack of the lower lane is read from top to bottom. The rule that a
parameter lock applies to "whatever is processed after it" is one and the same rule
inside a stack and between lanes.

This is what makes the accent lane usage work. In the worked example the accent lane's
`CHAN` was at y=1 and the main lane's `CHAN` at y=3. The accent Runner runs first and
builds up the channel state with its `PREL`, and the main Runner comes after and sounds
its notes in that state. **The side giving the accent has to be placed above what it
applies to.**

**Nothing here overwrites anything.** Because a lock reaches only what is processed after
it, two locks applying to the same parameter are not in contention: the one read later is
simply the one in force for the sounds after it.

The precedence follows the Runner around, and is not the position of the lane it is
currently running along. Moving to a destination lane by a `JUMP` leaves the precedence
at that of the `CHAN` it was born from, so **a destination lane can be placed anywhere
on the plane without changing the execution order**. Which is to say that "from top to
bottom" is a statement about the vertical position of the `CHAN`, and how far down the
rows a stack actually reaches, or where a destination lane happens to be placed, has
nothing to do with the order.

### Lanes at the same height

Two or more lanes can be placed at the same height, but **it is not recommended**. The
execution order in that case is undefined and the implementation may decide it however
it likes. Given that precedence is expressed by which is above which, there is no way to
decide it once they are level, and it is an awkward layout to work with in the first
place.

The turn of the piece
---------------------

**Of the lanes on channel 1, the topmost — the leftmost, where two are at the same
height — is called the master lane.** It is what defines the **turn** of the piece. The
turn is the instant that lane's Runner reaches `TERM` and returns to the head. Flying to
a branch and coming back from the destination's `TERM` is the same one lap, and is the
same unit a `GCYC` counts laps in.

**Which lane it is is decided by position and is not a flag.** In the same manner as the
execution order being decided by the vertical position of a `CHAN`, a score says where
it ends not by putting a mark on something but by where the channel 1 lanes were
written. In a score with no channel 1 lane at all, the first `CHAN` lane to run is read
— something has to be the answer.

**The length of a lap cannot be computed in advance.** A `JUMP` carrying a gate sends
the lap down a different road each time, and a `GPRB` throws dice outright. A turn is
therefore **something known when it happens** and cannot be known before. At most one
lookahead's worth can be foreseen. A display counting down what is left of the piece
does not hold up under this definition.

Two things ask for the turn: **switching the score** (the next section), and **a `CHAN`
sending a Runner out** (see "A `CHAN` does not always send one out"). No new unit was
defined when the second was added, because this section had already decided that "if a
later specification asks for the same kind of turn, it uses this rather than defining a
new one" — and it turned out to be usable as it stood.

**Two things never take one turn at the same time.** While a switch is waiting, that
turn belongs to the switch. On the far side of the turn the score that has arrived runs
its own `CHAN`s itself, so running a waiting `CHAN` on this side would be work that gets
thrown away.

### Switching the score without stopping playback

**A load during playback does not take effect immediately; it waits for the turn and
swaps over there.** While it waits the old score is played to the end, and from exactly
the sample of the turn the Runners of the new score all start running at once. **The two
scores carry on as though they had been written that way from the start** — no gap and
no overlap between them, and whatever was sounding across the seam is left to decay
rather than being cut.

This holds because a sound is handed over as a one-shot instruction to start sounding,
and nothing about the state of a channel is left on the synth side. There is nothing at
the seam to stop.

**While it waits, the score cannot be edited.** What the switch is waiting on is a point
measured on the lanes that are running, so an edit that moves or deletes a lane would
move that point. The plane, and the detail windows for tiles and locks, go dim and stop
accepting the hand. **The mix and the performance controls stay alive**, on the other
hand — timbre, sends, the limiter, the tempo, the channel mutes, and Live FX. Being able
to go on playing across the switch is the whole purpose of the feature, so stopping
those would defeat it.

**The request cannot be taken back.** Load does not answer while the wait is on. What is
left to a hand that has changed its mind is Stop, which ends the very thing being waited
for, and the load then takes effect there and then.

Simplifications already decided
-------------------------------

### The only way to tell the notes of a chord apart is the shape "below the boundary"

Because processing flows from top to bottom, writing `F4 / PREL / G#4 / C5` means `G#4`
and `C5` alone are affected by the lock and `F4` sounds plain. **Giving different
parameters to the notes of a chord can be done in this shape.**

What cannot be done is **naming them with gaps in between**. Changing `G#4` alone while
leaving `C5` plain cannot be specified. A lock flows to everything below the boundary,
so all that can be expressed is a division into "some above and some below", and there
is no means of naming a note individually. This is accepted.

Ways of naming a note individually were looked for and none was adopted — see
"Rejected alternatives".

Matters left to the synth side
------------------------------

The synth is developed separately as its own project. The following are not decided on
the sequencer side and are settled at the stage the two are joined.

- The **set of target parameters** a parameter lock can name. It belongs to the synth side (15 at present, matching every field of `FmPatch`)
- The unit and range of the amount. It should differ per target, and cannot be decided until the targets are

These are set in the separate window that opens when a tile is selected. They do not go
on the cell, so they have no effect on the display design of the sequencer side.

**Timbre is held per channel.** The synth side holds a patch per channel bundled together, and the number on a `CHAN`
picks the timbre as well as the stream. Lanes running in parallel on the same channel
therefore share a timbre, which lines the unit up with the rule that a lock always
applies to the whole channel. A branch lane has no `CHAN` of its own, so it sounds with
the timbre and the division of the channel that called it. The upper bound on channel
numbers is decided by the size of that patch bank (8 at present).

**A patch is a set of reference values, not state.** A lock lasts for the step it sits on
only, so a channel no lane is holding starts every instant from the patch values. Rewriting a patch during playback
takes effect straight away from the next instant, and there is nothing a lock has piled
up that has to be undone.

**The effects are per project and the send amounts are per channel.** There is one
reverb and one delay for the
whole score, and their settings belong to the `Project` alongside the tempo. **How much
of a sound goes to each, on the other hand, is part of the patch**, and is therefore
also a target for a parameter lock. That split is the whole meaning of having effects on
a sequencer like this one — a `PABS` placed in the middle of a chord sends only the
notes below the boundary to the reverb while the ones above sound dry. No amount of
splitting the effect's own settings per channel could express that.

The delay time is **synced to the tempo** and given as a note value against the beat.
That is the same unit as a lane's division but a separate table, and it has dotted
values (a lane does not, since it has to divide a bar evenly).

**The scale is per project and the transpose is per channel.** The reason for the split
is the same as it is for the effects. A piece is in one key, and something in a
different key per channel is two pieces. How far a part is moved, on the other hand, is
plainly a property of that part, so it goes in the patch alongside the send amounts and
**is therefore also a target for a parameter lock** — one step alone can be written up
an octave.

**Both take effect at the moment of sounding and do not rewrite the score.** A
`NoteTile` goes on holding the pitch it was written with and the cell goes on showing
it. A design that rewrote could only be applied once; taking effect at the moment of
sounding means it **can be tried and taken off again** — being able to move a piece into
another key and come back is the whole meaning of the feature.

The order is fixed as **transpose → scale**. Reversed, the transpose would carry the
notes that were just snapped into the key straight back out of it, making both settings
meaningless at once. A note the scale does not allow is **snapped rather than dropped**.
A note that does not sound is a hole in the music, and no arrangement of a stack fills
one. Where the two candidates are equidistant the lower is taken — something has to be
the answer, and nobody can hear a reason for which way it goes to vary by place.

**Live FX stands outside all of this.** It is a performance control on sounds that have
already been made, so an octave or a ramp goes freely outside the scale. A key signature
is a premise the piece is written under and Live FX lasts only as long as a hand is on a
button, and the two are on different layers.

Rejected alternatives
---------------------

Each of these was in the specification or was seriously considered, and each is recorded
so that it is not proposed again. The reason is what matters; the history is in the commit
log.

| What was rejected | Why |
| --- | --- |
| **`PACC`** — a third lock applying a relative change permanently | Once a lock outlives its step, the state a sound is in cannot be read off the score. See the Open question it leaves behind |
| **Governing and being governed** — the cell above governs the cell below, and a lock below attaches to the note above | Two readings pointing opposite ways, on chain lines that all look alike. `GPRB:4 / A4 / PABS` could not be read without already knowing the tokens |
| **A gate applying to the whole step it sits in** | Nothing above a gate could then be excepted from it, and a mixed stack had no meaning |
| **One target per lock** | Moving four parameters meant four stacked tiles, which stretched the distance between the gate and the note for nothing the stacking expressed |
| **The same parameter across the whole chord** | A boundary inside a chord is exactly what a lock in a stack is for |
| **Naming a note individually** — the attached cell placed horizontally, or a subordinate cell indented by half a cell into a tree | The first breaks the promise that the horizontal axis is time; the second loses the regularity of the grid |
| **Whoever executes later wins** — a lower `CHAN` overwriting a higher one | Made redundant once a lock reaches only what is read after it. There is nothing left to overwrite |
| **At most 4 slots per step** | No ground for choosing any number, and the depth each lane needs differs |
| **A `GCYC` holding one lap number** | A bit per lap costs one word, and most patterns could not be written at all |
| **A `GPRB` in twelve divisions** | That was the granularity a clock-shaped icon is easy to draw at, which is not a reason |
| **A gutter for an accidental on every note** | Spent 5px of a 30px cell on every plain name, to spare a movement nobody was watching for |
| **♭ as well as ♯** | One pitch would have two spellings and the same cell two appearances |
| **The rail ending at the last ▶ marker** | Cut the rail short whenever a note went into the top row of the last step |
| **A chain line drawn from "is the cell above filled"** | Made one lane's tail and another's head read as connected |
| **`PROB`, `NGTE`, `CH1`** | `PROB` collided with the `P` of the parameters; "note gate" stopped matching what it did once a `JUMP` could be gated; `CH1` fused a kind with its argument |

Open questions
--------------

- Given that lanes can be placed freely, it is easy to lose track of what is where on the plane as the number of lanes grows. Some means of alignment or of seeing the whole may be needed
- **A means of changing a little at a time across laps.** This is the expression lost with the abolition of `PACC`. There is currently no way other than running a different step per lap with a `GCYC`. The decision to tie a lock to one step will not be overturned, so if it becomes necessary it will be added as a separate mechanism.

  **The Rise / Fall of Live FX is not this.** Looking only at the fact that it transposes by a semitone per step it looks like a replacement for `PACC`, but it is **a performance control that applies only while a hand is pressing**, and the score holds none of it. It does not survive in the file and it does not reach sounds already made. What is wanted here is a means of change **held by the score**, so this item is unfilled
- The necessity for binding several `Score`s together. If one is found, the definition of `Project` gets extended. **Playing pieces one after another does not require it** — the load that waits for the turn does the same thing on the file side (see "The turn of the piece")
- Editing operations. That selecting a tile opens a separate window is decided, but how tiles are placed and deleted and how a new lane is created is undecided — the built app has since settled these, in `impl-score-plane.md` and `impl-panels.md`
