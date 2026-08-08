Jacquard
========

A prototype of the grid sequencer described in [prototype.md] and specified in
[sequencer.md]. Lanes of steps are laid out anywhere on one plane; a step stacks
what happens at the same instant; gates, parameter locks and jumps turn sixteen
slots into something that changes as it repeats.

Built with Unity 6.5 (6000.5.6f1). Open the project and play `Assets/Main.unity`.

[prototype.md]: prototype.md
[sequencer.md]: sequencer.md
[bp.html]: bp.html

Using it
--------

| Action | How |
| --- | --- |
| Move the cursor | Click a cell, or the arrow keys |
| Write a note | `A`–`G`, which also steps right. `0`–`8` picks the octave |
| Transpose | Shift+up/down for a semitone, add command for an octave |
| Add a gate or a lock | The palette buttons, which insert at the cursor |
| Lengthen a lane | Put a tile on its `TERM` cell, or use Steps in the panel |
| New lane | The New lane button; delete a lane from its `CHAN` cell |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor |
| Set a number | Drag its bar right or up, shift for fine; double click to type one |
| Timbre | Select a `CHAN` cell, which brings up the Sound panel for its channel |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play |
| Pan the plane | Two finger swipe, or command+drag |

Placing a tile on a cell that already has one inserts above it rather than
replacing it, since a stack is read from the top down and a gate or a lock usually
arrives after the note it will govern. Typing a note over a note changes its pitch.

Scores are saved under `Application.persistentDataPath/Scores` as plain text,
one line per step; pick a slot with the File arrows.

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
  keeps the corner and follows the cursor; the Sound panel comes up beside it while
  a `CHAN` cell is selected, since that tile is what a channel is on the plane.
  There is no window to open, and so no state on screen that the score does not
  decide.
- **Chain lines** are drawn only between cells of the same stack. bp.html joins
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
