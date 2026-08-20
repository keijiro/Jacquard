Manual
======

What the app does from the player's side — the gestures, what each control
reaches, and what is saved with a piece. Why any of it is the way it is, is in
the `impl-*.md` notes beside this file; what the sequencer is meant to do is in
[sequencer-spec.md].

[sequencer-spec.md]: sequencer-spec.md

Using it
--------

| Action | How |
| --- | --- |
| Move the cursor | Click a cell, or the arrow keys |
| Write a note | The `NOTE` button the Tile panel offers on a free cell |
| Set a note's pitch | Note and Octave on the Tile panel, the letter and the register on a bar each |
| Transpose a note | Shift+up/down for a semitone, add command for an octave, which rewrites the tile |
| Hear the note under the cursor | Return, which sounds it whatever the Audition switch says |
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
| Stop or start a lane | Double click its `CHAN` cell, or Play on the panel that cell opens |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor; drag it up and down when it is longer than the screen |
| Set a number | Drag its bar right or up, shift for fine; double click to type one |
| Timbre | Select a `CHAN` cell, which puts its channel's sound on the Tile panel, under the lane |
| Move a channel in pitch | Transpose, the first row of its sound, in semitones |
| Thicken a channel | Unison, under Pan in its sound: above zero every note sounds twice, detuned apart and spread across the image. The image opens over the first three tenths and the rest of the bar goes on detuning, up to just past being out of tune. A note already panned to one side keeps its position and spreads by whatever room is left |
| Hold the piece to a key | The Global button opens the panel the Scale is set on, a switch per semitone laid out as a keyboard |
| Silence a channel, or hear one alone | The Channels button opens a row per channel, with a Mute and a Solo switch on each; a solo overrules every mute, and both are saved with the score |
| Go to a channel | Select on its row, which puts the cursor on the `CHAN` tile that names it |
| Reverb and delay | The Send FX button opens the panel they are set on; how much of a channel reaches each is the last two rows of its sound |
| Play the sequence by hand | The Live FX button opens a row of buttons that act while they are held |
| Loudness and punch | The same Global panel holds the limiter; Threshold is the one that is played, and the make-up gain follows it so the mix gets louder as it gets harder; the bottom of that bar is the whole mix through the soft clip |
| How loud it all is | Volume, on the System panel: it is after everything else in the mix, so it makes the piece quieter without making it any softer, and the bottom of the bar is off. The bar is tapered like a fader — the first few decibels take a quarter of it, so a trim is a drag rather than a pixel |
| What a lock holds | Select it, then move a bar on the Tile panel; click a name to let go |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play, which the delay is in time with |
| Pan the plane | Drag from an empty cell, two finger swipe, or command+drag |
| Reach a control off the edge | Drag the row or the panel it is on: the transport slides sideways and a column of panels slides up and down whenever it holds more than the screen does |

The System button opens what is set about the app rather than about the piece. Volume is
how loud the whole thing leaves — after everything else in the mix, so it makes the piece
quieter without making it any softer, tapered like a fader, and off at the bottom of its
travel. Visualizer puts the synth behind the score in a wash the eye can ignore — the
output as a trace across the middle and the twenty-four voice slots as a row along the
bottom. It is drawn
by the camera rather than by the interface, which is transparent over it. Nothing on that
panel is saved with a project; it is remembered for the machine it was set on, and is
there again the next time the app is opened. On a desktop it also carries Open score
folder, which shows the directory the files below are written to.

Audition is under it, and it is the only switch here that starts on. It is what makes an
edit sound the note it just made: a bar let go of, a note written, a stack pasted, a
transpose from the keys. Turned off, all of that goes quiet and the piece is heard only
when it is played. Return still sounds the note under the cursor either way — that is a
note asked for rather than one volunteered, and it is how a cell is heard with the
auditioning off.

Buffer size is on the same panel, and it is the one to reach for if the sound bangs or
drops out: it is how long a buffer the audio thread has to fill, from 256 frames — 5.3ms
at the usual rate, which is what the app ships with — up to 1024. A longer buffer
survives a busy moment, and costs that much delay between a Live FX button and what
comes out. It is taken up at the next launch, which the panel says while the two
disagree.

On a phone or a tablet, leaving the app stops the sequence. Nothing about a run
survives being sent to the background — the app is not running to schedule anything,
and the audio system it comes back to is not the one it left — so the piece ends at
the edge and Play starts it again from the top. What it saves you from is the
alternative, which was coming back to silence.

A tile goes on free ground only: a lane's empty step, the cell under a stack, or
the `TERM` cell, which grows the lane by a step. A stack is therefore built from
the top down, the gate first and the note it governs in the cell underneath it,
which is the order the runner reads it in. A new note arrives at the pitch and
length of the last note edited.

A note's pitch is set on two bars rather than one. Note is the letter — C through B, the
twelve of them across the whole bar — and Octave is the register under it, so the letter
can be changed without leaving the register and the register without losing the letter.
Note stops at B rather than turning the octave over; the row underneath is where that
goes. Both are still typed if an exact one is wanted, by the number behind the name: 0
for C, 11 for B.

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

The `CHAN` cell is the exception, and it is one on purpose: a double click there
stops the lane or starts it. That cell can be neither end of a copy — a `CHAN` has
no copy of its own, and it is not ground a tile can go on — so the gesture had
nothing to do on it, and what it does instead is the one control worth reaching for
without looking while a piece is playing. A stopped lane goes grey and keeps its
place; the master lane is drawn solid whatever its switch says, because it is the
one lane that cannot be stopped.

A lane stops at the end of its lane and never part way along one, so what is playing
is played out. It starts again on the turn of the piece — the same lap of the same
lane a score coming in waits for — so a lane switched back on is silent for up to a
lap and then comes in exactly in step with the rest, rather than wherever the hand
happened to land. A lane drawn while the sequence plays waits for that moment too.
Between the cell and the playhead the three states are all visible: grey is stopped,
solid with no playhead is about to come in, solid with one is playing.

The master lane is the topmost channel one lane, the same one that says how long the
piece is. Its switch can be thrown and it is saved, and the lane goes on playing
anyway: it is what hands out the moment every other lane starts on, so a silent one
would leave the rest with nothing to come in on. Which lane that is comes from where
it sits rather than from anything written on it, so moving lanes about moves which
switch is being ignored.

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

A roll pressed where there is nothing to catch waits rather than holding the
silence: if what it caught is a rest all the way through, the sequence carries on
and the roll starts on the next thing that sounds. So a sixteenth landing a step
wide of the note you meant still rolls a note.

Anything held at once applies at once — an octave up under a stab, both sends, a
rise through a reverb. The rolls are the exception, since all four stand in for
the same sequence: the one pressed last is the one that plays, and letting it go
hands back to whichever is still held. With a mouse only one button can be held at
a time; a touch screen holds one per finger.

Scores
------

Scores are saved under `Application.persistentDataPath/Scores` as plain text,
one line per step; pick one with the arrows beside Save. A copy of the app that has
saved nothing yet writes nine of them for itself — `score1` through `score9`, the first
holding the sample and the rest a bar with a note on each beat to start from — so every
name on the chooser is a file that is really there, in alphabetical order. It opens in
whichever was last saved or loaded, and comes back to the first name on the list when
that one has gone. On a desktop the System panel
has a button that opens that directory. In a Web build the path is the browser's own
storage, so a save keeps across a reload but not across clearing the site's data — and
there is nothing to open, so the button is not there.

Loading while the sequence is running does not stop it. The score waits for the turn
of the piece — the lap of the topmost channel one lane — and then the music carries
straight on into it, with nothing between the two and whatever was sounding left to
ring out. While it waits, the plane and the panel that edits the score are dimmed
and take no presses, since an edit could move the line the switch is measured on; the
mix and the live effects go on working, and the plane comes back the moment the music
turns over. A request cannot be taken back — Stop is what ends the wait, and the score
comes in there and then.

The sample that fills the first slot is one of those files, checked in at
`Assets/Jacquard/Scores/Sample.jacquard.txt`. To replace it, save the score from
the app and copy the file over that one, then run **Jacquard > Run Self Test** to
be told whether it still reads as the current version.
