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
| Write a note | Double click a free cell, or its `NOTE` button on the Tile panel |
| Transpose | Shift+up/down for a semitone, add command for an octave |
| Add a gate or a lock | The buttons the Tile panel offers on a free cell |
| Remove a tile | Delete on the Tile panel, or the delete key |
| Lengthen a lane | Put a tile on its `TERM` cell, or use Steps on its `CHAN` cell |
| New lane | Select bare ground, then New lane; delete a lane from its `CHAN` cell |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor |
| Set a number | Drag its bar right or up, shift for fine; double click to type one |
| Timbre | Select a `CHAN` cell, which brings up the Sound panel for its channel |
| What a lock holds | Select it, then move a bar on the Lock panel; click a name to let go |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play |
| Pan the plane | Two finger swipe, or command+drag |

A tile goes on free ground only: a lane's empty step, the cell under a stack, or
the `TERM` cell, which grows the lane by a step. A stack is therefore built from
the top down, the gate first and the note it governs in the cell underneath it,
which is the order the runner reads it in. A new note arrives at the pitch and
length of the last note edited.

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
