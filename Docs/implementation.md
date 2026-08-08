Implementation
==============

How the prototype is put together, and the decisions behind it that the code
alone does not explain. What it is meant to do is in [prototype.md]; what it is
meant to be is in [sequencer.md]; what it is meant to look like is in
[mockup.html].

[prototype.md]: prototype.md
[sequencer.md]: sequencer.md
[mockup.html]: mockup.html

Layout
------

`Assets/Core` is an assembly with **no engine references at all** — the asmdef
sets `noEngineReferences`, so the separation prototype.md asks for is enforced by
the compiler rather than by discipline:

| | |
| --- | --- |
| `Model` | `Project`, `Score`, `Lane`, `Step`, the tile hierarchy, pitch names |
| `Serialization` | The text format, written and read by hand |
| `Sequencer` | `Runner` and the scheduler that turns tiles into note events |
| `Synth` | The two operator FM voice, the per channel patch bank, the lock targets |

`Assets/Jacquard` is the part that cannot help but know about Unity:

| | |
| --- | --- |
| `Audio` | Voice pool, Burst render job and the Scriptable Audio Pipeline output |
| `App` | The MonoBehaviour, the editing operations, file access |
| `UI` | The score plane, cell icons drawn with Painter2D, the panels |

The FM synth, the Scriptable Audio Pipeline usage and the value bar every number
is set on come from [keijiro/unity-sap-test]; the two axis scrolling plane comes
from [keijiro/uitk-scrollarea].

[keijiro/unity-sap-test]: https://github.com/keijiro/unity-sap-test
[keijiro/uitk-scrollarea]: https://github.com/keijiro/uitk-scrollarea

Notes on the prototype
----------------------

- **Timing** rides the audio clock. Every step is handed to the synth with the
  exact sample it starts on, so a dropped frame delays the handover and never
  the note.
- **One instant is one downward pass.** Runners are one per `CHAN` lane, ordered
  by the vertical position of that tile, and the ones landing on the same instant
  are read in that order, each from the rail row of its step down. Everything a
  tile does reaches what is read after it and nothing before it: a gate ends the
  descent, a lock colours the notes that follow it, a note takes the channel as it
  stands where it sits. That one rule covers both the inside of a stack and the
  lanes against each other, which is what lets the accent lane, placed above the
  main one, colour it.
- **A lock is over when its instant is.** There is no accumulating lock and no
  standing channel state; every channel starts each instant from its patch again.
- **Every field of the patch is a lock target.** `FmPatch` and `ParamTargets` name
  the same ten parameters, so there is nothing a channel holds that a step cannot
  reach for one instant. One of them, the gate ratio, multiplies the length written
  on the note rather than being a length itself, which is why the note reads in
  steps and the channel in percent: the two are the same multiplication and only
  the unit tells them apart.
- **One lock reaches as many of them as it likes.** A lock carries a slot per
  target and holds whichever ones have been set, so a step that changes four
  parameters is one tile rather than four stacked cells between the gate and the
  note. What it does not hold it leaves entirely to the channel, which is why a
  lock that holds nothing — a freshly placed one — is inert rather than wrong.
- **Timbre belongs to the channel**, not to the project: the bank holds one patch
  per channel and a `CHAN` tile's number picks the sound as well as the stream, so
  lanes sharing a channel share a patch and a branch lane borrows the one of
  whatever jumps into it. The Sound panel is where that patch is edited, and an
  edit is heard from the next instant with nothing to undo.
- **A number is a bar, not a field.** The readout sits on a bar that fills as the
  value rises, dragging scrubs it and a double click types an exact one, so a
  parameter shows where it sits inside its useful range as well as what it is. What
  that range is comes from the synth itself (`ParamTargets`), which is what lets a
  lock's amount be read against what it moves; typing is deliberately not held to
  it. A lane's step count is the one number still stepped, since each one is a cell
  and growing can be refused.
- **A panel shows what the cursor is on**, and nothing is toggled. The tile panel
  keeps the corner and follows the cursor; beside it comes up either the Sound
  panel, while a `CHAN` cell is selected, or the Lock panel, while a `PABS` or
  `PREL` cell is. Those two are the same list of parameters read two ways — what a
  channel sounds like, and what one step does to it — and they share a slot because
  no cell is both. There is no window to open, and so no state on screen that the
  score does not decide.
- **The panel is also where a tile is put down**, since the cursor is already the
  answer to where. A cell that will take one — a lane's empty step, the cell under
  a stack, the `TERM` cell that grows the lane — offers the tiles instead of a
  description of nothing, and bare ground offers a lane to put one on. So there is
  no palette to keep in step with what the cursor can accept, no button that
  silently does nothing where it stands, and one less row of chrome above the
  plane. A tile therefore only ever lands on free ground: a stack is built from the
  top down rather than by inserting above what is already there, which is the order
  the runner reads it in anyway.
- **The cell pitch is what the rest of the plane is derived from.** A cell is
  30x32 with a 4px gutter, set by what has to fit inside one rather than by taste:
  a note name with its accidental gutter is a little over twenty pixels wide, and
  the icons are drawn in a 15x15 box. Keeping those numbers in `Style` alone is
  what lets the painted layers and the tile elements agree on where a cell is to
  the pixel.
- **Chain lines** are drawn only between cells of the same stack. mockup.html joins
  whatever happens to sit directly above, which makes two unrelated lanes look
  connected; sequencer.md lists that as undecided, and knowing the lane settles
  it.
- **`MathF` is not used in the DSP.** Burst cannot resolve the externs behind
  it, and a job that calls `MathF.Sin` silently drops to managed execution on
  the audio thread, so `FastMath` spells out the sine and the exponential.
- **Pixel scale** is a whole number on the `JacquardApp` component, not a DPI
  ratio: the grid is drawn in whole pixels with hairlines on half-pixel
  centres, and a fractional scale smears all of it. Two suits a retina display.
- Editor menu items: *Jacquard > Rebuild Main Scene* regenerates the scene, and
  *Jacquard > Run Self Test* checks the file format round trip, plays four laps of
  the sample score without a device, and reads a stack whose gate sits between two
  notes to prove the descent only ever reaches downwards.
