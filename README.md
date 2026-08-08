Jacquard
========

A prototype grid sequencer. Lanes of steps are laid out anywhere on one plane; a
step stacks what happens at the same instant; gates, parameter locks and jumps
turn sixteen slots into something that changes as it repeats.

Built with Unity 6.5 (6000.5.6f1). Open the project and play `Assets/Main.unity`.

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
| What a lock holds | Select it, then move a bar on the Lock panel; click a name to let go |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play |
| Pan the plane | Two finger swipe, or command+drag |

Placing a tile on a cell that already has one inserts above it rather than
replacing it, since a stack is read from the top down and a gate or a lock usually
arrives after the note it will govern. Typing a note over a note changes its pitch.

Scores are saved under `Application.persistentDataPath/Scores` as plain text,
one line per step; pick a slot with the File arrows.

Documentation
-------------

| | |
| --- | --- |
| [Docs/prototype.md] | What this prototype is for |
| [Docs/sequencer.md] | The sequencer specification |
| [Docs/mockup.html] | The static mockup the look comes from |
| [Docs/implementation.md] | How it is built, and the decisions behind it |

[Docs/prototype.md]: Docs/prototype.md
[Docs/sequencer.md]: Docs/sequencer.md
[Docs/mockup.html]: Docs/mockup.html
[Docs/implementation.md]: Docs/implementation.md
