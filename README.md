Jacquard
========

A prototype grid sequencer. Lanes of steps are laid out anywhere on one plane; a
step stacks what happens at the same instant; gates, parameter locks and jumps
turn sixteen slots into something that changes as it repeats.

Built with Unity 6.5 (6000.5.8f1). Open the project and play `Assets/Main.unity`.

The synth runs on the Scriptable Audio Pipeline, which the Web platform does not
support; there the same DSP is rendered from `Update` and pushed to the Web Audio
API instead, at the cost of about 110ms more latency before a note can sound.
Nothing else differs, and no setting selects it.

Using it
--------

| Action | How |
| --- | --- |
| Move the cursor | Click a cell, or the arrow keys |
| Write a note | The `NOTE` button the Tile panel offers on a free cell |
| Transpose a note | Shift+up/down for a semitone, add command for an octave, which rewrites the tile |
| Add a gate or a lock | The buttons the Tile panel offers on a free cell |
| Which laps a cycle gate fires on | Its Period, and the switch per lap under it |
| Remove a tile | Delete on the Tile panel, or the delete key |
| Move a tile | Drag it; within its own step that reorders the stack |
| Move a sub-stack | Drag a tile to another step, and what hangs below it comes too |
| Copy a sub-stack | Double click a tile, which lights up what it took |
| Paste it | Double click a cell that would take a tile |
| Move a lane | Drag its `CHAN` or `JDST` cell |
| Lengthen a lane | Put a tile on its `TERM` cell, or use Steps on its `CHAN` cell |
| New lane | Select bare ground, then New lane; delete a lane from its `CHAN` cell |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor |
| Set a number | Drag its bar right or up, shift for fine; double click to type one |
| Timbre | Select a `CHAN` cell, which brings up the Sound panel for its channel |
| Move a channel in pitch | Transpose, the first row of its Sound panel, in semitones |
| Thicken a channel | Unison, under Pan on its Sound panel: above zero every note sounds twice, detuned apart and spread across the image. The image opens over the first three tenths and the rest of the bar goes on detuning, up to just past being out of tune. A note already panned to one side keeps its position and spreads by whatever room is left |
| Hold the piece to a key | The Global button opens the panel the Scale is set on, a switch per semitone laid out as a keyboard |
| Silence a channel, or hear one alone | The Channels button opens a row per channel, with a Mute and a Solo switch on each; a solo overrules every mute, and both are saved with the score |
| Go to a channel | Select on its row, which puts the cursor on the `CHAN` tile that names it |
| Reverb and delay | The Send FX button opens the panel they are set on; how much of a channel reaches each is the last two rows of its Sound panel |
| Play the sequence by hand | The Live FX button opens a row of buttons that act while they are held |
| Loudness and punch | The same Global panel holds the limiter; Threshold is the one that is played, and the make-up gain follows it so the mix gets louder as it gets harder; the bottom of that bar is the whole mix through the soft clip |
| What a lock holds | Select it, then move a bar on the Lock panel; click a name to let go |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play, which the delay is in time with |
| Pan the plane | Drag from an empty cell, two finger swipe, or command+drag |

The Visualizer button puts the synth behind the score, in a wash the eye can ignore:
the output as a trace across the middle and the twenty-four voice slots as a row along
the bottom. It is drawn by the camera rather than by the interface, which is transparent
over it.

A tile goes on free ground only: a lane's empty step, the cell under a stack, or
the `TERM` cell, which grows the lane by a step. A stack is therefore built from
the top down, the gate first and the note it governs in the cell underneath it,
which is the order the runner reads it in. A new note arrives at the pitch and
length of the last note edited.

Dragging is the exception: a tile dropped on an occupied cell opens the stack up
and takes its place, which is how one is reordered. A drop with nowhere to go —
off any lane, or with no room under the stack it would join — leaves nothing lit
up on the plane and does not happen.

Double clicking is the same reach without the carrying: on a tile it takes a copy
of that tile and everything under it, and on any cell that would take a tile it
puts the last copy down. Only notes, gates and locks travel — a `JUMP` in a stack
is stepped over and a double click on one does nothing, since a jump is the thing
its branch lane answers to and there cannot be two of it. What was taken lights
up for a moment, and with nothing taken yet a double click does nothing at all.

What a note sounds as is decided twice over on the way out, and neither pass touches
what is written: the channel's Transpose moves it, and then the Scale drops it onto the
nearest semitone it allows, the lower of the two when it sits exactly between. So a part
can be moved into another key and stay in it, and a scale can be tried against a piece
and taken off again. Everything on, which is how a score starts, is the scale that does
nothing; nothing on has nowhere to send a note, so every note stays where it was
written and the scale does nothing again. A parameter lock can
reach the Transpose like any other, which is how one step of a channel is lifted and the
rest left where they are.

The Live FX buttons stand outside all of this. They colour a note that has already been
made, so an octave or a rise reaches whatever it likes and the scale never catches it —
which is what a gesture should do and a key signature should not.

A lane holds its whole row from `CHAN` to `TERM` whether anything is written on
it yet or not, so nothing else can grow across it and no lane can be dropped on
one. Give a lane a clear row of its own and it will take tiles anywhere along it.

The plane keeps empty ground on every side of the score, so a lane can be carried
or started above and to the left of everything as freely as below and to the
right of it. Reaching that way is pan and then drag: the plane does not scroll
itself while a lane is in hand.

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

Loading while the sequence is running does not stop it. The score waits for the turn
of the piece — the lap of the topmost channel one lane — and then the music carries
straight on into it, with nothing between the two and whatever was sounding left to
ring out. While it waits, the plane and the two panels that edit the score are dimmed
and take no presses, since an edit could move the line the switch is measured on; the
mix and the live effects go on working, and the plane comes back the moment the music
turns over. A request cannot be taken back — Stop is what ends the wait, and the score
comes in there and then.

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
