Jacquard
========

A prototype grid sequencer. Lanes of steps are laid out anywhere on one plane; a
step stacks what happens at the same instant; gates, parameter locks and jumps
turn sixteen slots into something that changes as it repeats.

Built with Unity 6.5 (6000.5.6f1). Open the project and play `Assets/Main.unity`.

The synth runs on the Scriptable Audio Pipeline, which the Web platform does not
support; there the same DSP is rendered from `Update` and pushed to the Web Audio
API instead, at the cost of about 110ms more latency before a note can sound.
Nothing else differs, and no setting selects it.

Using it
--------

| Action | How |
| --- | --- |
| Move the cursor | Click a cell, or the arrow keys |
| Write a note | Double click a free cell, or its `NOTE` button on the Tile panel |
| Transpose | Shift+up/down for a semitone, add command for an octave |
| Add a gate or a lock | The buttons the Tile panel offers on a free cell |
| Which laps a cycle gate fires on | Its Period, and the switch per lap under it |
| Remove a tile | Delete on the Tile panel, or the delete key |
| Move a tile | Drag it; within its own step that reorders the stack |
| Move a sub-stack | Drag a tile to another step, and what hangs below it comes too |
| Move a lane | Drag its `CHAN` or `JDST` cell |
| Lengthen a lane | Put a tile on its `TERM` cell, or use Steps on its `CHAN` cell |
| New lane | Select bare ground, then New lane; delete a lane from its `CHAN` cell |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor |
| Set a number | Drag its bar right or up, shift for fine; double click to type one |
| Timbre | Select a `CHAN` cell, which brings up the Sound panel for its channel |
| Silence a channel, or hear one alone | Its Mute or Solo switch on the Channels panel; a solo overrules every mute |
| Go to a channel | Select on its row, which puts the cursor on the `CHAN` tile that names it |
| Reverb and delay | The Send FX button opens the panel they are set on; how much of a channel reaches each is the last two rows of its Sound panel |
| Play the sequence by hand | The Live FX button opens a row of buttons that act while they are held |
| Loudness and punch | The Global button opens the panel the limiter is set on; Drive is the one that is played |
| What a lock holds | Select it, then move a bar on the Lock panel; click a name to let go |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play, which the delay is in time with |
| Pan the plane | Drag from an empty cell, two finger swipe, or command+drag |

A tile goes on free ground only: a lane's empty step, the cell under a stack, or
the `TERM` cell, which grows the lane by a step. A stack is therefore built from
the top down, the gate first and the note it governs in the cell underneath it,
which is the order the runner reads it in. A new note arrives at the pitch and
length of the last note edited.

Dragging is the exception: a tile dropped on an occupied cell opens the stack up
and takes its place, which is how one is reordered. A drop with nowhere to go —
off any lane, or with no room under the stack it would join — leaves nothing lit
up on the plane and does not happen.

A lane holds its whole row from `CHAN` to `TERM` whether anything is written on
it yet or not, so nothing else can grow across it and no lane can be dropped on
one. Give a lane a clear row of its own and it will take tiles anywhere along it.

Live FX
-----------

Twelve buttons along the bottom of the screen that act only while they are held,
on whatever the sequence is about to play. A note already sounding is never
touched, and nothing here is written to the score or saved with it.

| | |
| --- | --- |
| Reverb / Delay | Every note goes all the way into that effect |
| Stab / Sustain | Gate and release cut short, or both doubled |
| Oct - / Oct + | An octave down or up |
| Fall / Rise | A semitone a step away from where the button was pressed, back to nothing after two bars |
| Roll 1/16 … 1/4 | One, two, three or four steps of the sequence, caught from where the button was pressed and played in place of what follows |

A roll of a sixteenth is playing from the moment it is asked for, since the step
it caught has already been heard. The longer ones let the sequence through for the
rest of their own length, recording it, before they stand in for anything.

Anything held at once applies at once — an octave up under a stab, both sends, a
rise through a reverb. The rolls are the exception, since all four stand in for
the same sequence: the one pressed last is the one that plays, and letting it go
hands back to whichever is still held. With a mouse only one button can be held at
a time; a touch screen holds one per finger.

Scores are saved under `Application.persistentDataPath/Scores` as plain text,
one line per step; pick a slot with the File arrows. In a Web build that path is
the browser's own storage, so a save keeps across a reload but not across clearing
the site's data.

The score the app opens on is one of those files, checked in at
`Assets/Jacquard/Scores/Startup.jacquard.txt`. To replace it, save the score from
the app and copy the file over that one, then run **Jacquard > Run Self Test** to
be told whether it still reads as the current version.

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
