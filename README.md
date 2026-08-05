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
| Timbre | The Sound button, which edits the timbre of one channel |
| Play | Space, or the Play button |
| Pan the plane | Two finger swipe, or command+drag |

Placing a tile on a cell that already has one inserts above it rather than
replacing it, since a stack is read from the top down and a gate usually arrives
after the note it will govern. Typing a note over a note changes its pitch.

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

The FM synth and the Scriptable Audio Pipeline usage come from
[keijiro/unity-sap-test]; the two axis scrolling plane comes from
[keijiro/uitk-scrollarea].

[keijiro/unity-sap-test]: https://github.com/keijiro/unity-sap-test
[keijiro/uitk-scrollarea]: https://github.com/keijiro/uitk-scrollarea

Notes on the prototype
----------------------

- **Timing** rides the audio clock. Every step is handed to the synth with the
  exact sample it starts on, so a dropped frame delays the handover and never
  the note.
- **Runners** are one per `CHAN` lane, ordered by the vertical position of that
  tile. Runners landing on the same instant are executed as a slice, and the
  locks written during a slice are collected before any of its notes are
  stamped — which is what lets the accent lane, placed below the main one,
  overwrite what the main lane just played.
- **Timbre belongs to the channel**, not to the project: the bank holds one patch
  per channel and a `CHAN` tile's number picks the sound as well as the stream, so
  lanes sharing a channel share a patch and a branch lane borrows the one of
  whatever jumps into it. The Sound window follows the cursor onto the channel
  being worked on. An absolute lock moves that channel's patch as it plays; editing
  the patch puts it back.
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
  *Jacquard > Run Self Test* checks the file format round trip and plays four
  laps of the sample score without a device.
