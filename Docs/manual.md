Manual
======

What the app does from the player's side: the gestures and keys, what each control
reaches, and what is saved with a piece. **Why** any of it behaves as it does is in
[sequencer-spec.md] and the `impl-*.md` notes beside this file, and is deliberately not
repeated here.

[sequencer-spec.md]: sequencer-spec.md

Using it
--------

| Action | How |
| --- | --- |
| Move the cursor | Click a cell, or the arrow keys |
| Write a note | The `NOTE` button the Tile panel offers on a free cell |
| Set a note's pitch | Note and Octave on the Tile panel — the letter and the register on a bar each, so either moves without disturbing the other. Note stops at B rather than turning the octave over. Both can be typed by the number behind the name: 0 for C, 11 for B |
| Transpose a note | Shift+up/down for a semitone, add command for an octave, which rewrites the tile |
| Hear the note under the cursor | Return, which sounds it whatever the Audition switch says |
| Add a gate or a lock | The buttons the Tile panel offers on a free cell |
| Which laps a cycle gate fires on | Its Period, and the switch per lap under it |
| Remove a tile | Delete on the Tile panel, or the delete key |
| Move a tile | Drag it; within its own step that reorders the stack |
| Move a sub-stack | Drag a tile to another step, and what hangs below it comes too. A tile dropped on an occupied cell opens the stack up and takes its place; a drop with nowhere to go lights nothing and does not happen |
| Copy a sub-stack | Double click a tile, which lights up what it took. Only notes, gates and locks travel — a `JUMP` is stepped over |
| Paste it | Double click a cell that would take a tile |
| Move a lane | Drag its `CHAN` or `JDST` cell |
| Lengthen a lane | Put a tile on its `TERM` cell, or use Steps on its `CHAN` cell |
| New lane | Select bare ground, then New lane; delete a lane from its `CHAN` cell |
| Stop or start a lane | Double click its `CHAN` cell, or Play on the panel that cell opens |
| Branch | The `JUMP` button, which brings its `JDST` lane with it |
| Details of a tile | The panel on the right follows the cursor; drag it up and down when it is longer than the screen |
| Set a number | Drag its bar right or up, shift for fine; double click to type one. A typed number may go past the ends of the bar, which is how a part is moved further or a tail held longer than the bar reaches; past what the synth will take it is held there, and the bar says which it was |
| Take a number back | Double click its name: a lock lets go of that parameter, and a channel's sound goes back to where a fresh patch holds it |
| Timbre | Select a `CHAN` cell, which puts its channel's sound on the Tile panel, under the lane |
| Move a channel in pitch | Transpose, the first row of its sound, in semitones |
| Thicken a channel | Unison, under Pan in its sound: above zero every note sounds twice, detuned apart and spread across the image. The image opens over the first three tenths and the rest of the bar goes on detuning |
| Hold the piece to a key | The Global button opens the panel the Scale is set on, a switch per semitone laid out as a keyboard |
| Silence a channel, or hear one alone | The Channels button opens a row per channel, with a Mute and a Solo switch on each; a solo overrules every mute, and both are saved with the score |
| Go to a channel | Select on its row, which puts the cursor on the `CHAN` tile that names it |
| Reverb and delay | The Send FX button opens the panel they are set on; how much of a channel reaches each is the last two rows of its sound |
| Play the sequence by hand | The Live FX button opens a row of buttons that act while they are held |
| Loudness and punch | The same Global panel holds the limiter; Threshold is the one that is played, and the make-up gain follows it so the mix gets louder as it gets harder |
| How loud it all is | Volume, on the System panel: after everything else in the mix, so it makes the piece quieter without making it any softer. Tapered like a fader, and off at the bottom of its travel |
| Play | Space, or the Play button |
| Tempo | The bpm bar beside Play, which the delay is in time with |
| Pan the plane | Drag from an empty cell, two finger swipe, or command+drag |
| Reach a control off the edge | Drag the row or the panel it is on: the transport slides sideways and a column of panels slides up and down whenever it holds more than the screen does |

Two things a tile placement will not do, which is what makes the panel's offer worth
reading: **a tile goes on free ground only** — a lane's empty step, the cell under a
stack, or the `TERM` cell, which grows the lane by a step — and **a lane holds its whole
row from `CHAN` to `TERM`** whether anything is written on it yet or not, so nothing can
grow across it. A new note arrives at the pitch and length of the last note edited.

The System panel
----------------

What is set about the app rather than about the piece. **Nothing here is saved with a
project**; it is remembered for the machine it was set on.

| | |
| --- | --- |
| Volume | How loud the whole thing leaves. See the table above |
| Visualizer | Puts the synth behind the score in a wash the eye can ignore — the output as a trace across the middle, the twenty-four voice slots as a row along the bottom |
| Audition | The one switch here that starts **on**. It is what makes an edit sound the note it just made: a bar let go of, a note written, a stack pasted, a transpose from the keys. Turned off, all of that goes quiet — but Return still sounds the note under the cursor, since that is a note asked for rather than one volunteered |
| Buffer size | Reach for this if the sound bangs or drops out: how long a buffer the audio thread has to fill, from 256 frames — 5.3ms at the usual rate, which is what the app ships with — up to 1024. A longer buffer survives a busy moment and costs that much delay between a Live FX button and what comes out. **Taken up at the next launch**, which the panel says while the two disagree |
| Open score folder | Desktop only — shows the directory the scores are written to |

**On a phone or a tablet, leaving the app stops the sequence.** Nothing about a run
survives being sent to the background, so the piece ends at the edge and Play starts it
again from the top.

Live FX
-------

Twelve buttons along the bottom that act only while they are held, on whatever the
sequence is about to play. A note already sounding is never touched, and nothing here is
written to the score or saved with it.

| | |
| --- | --- |
| Reverb / Delay | Every note goes all the way into that effect |
| Stab / Sustain | Gate and release cut short, or both doubled |
| Oct - / Oct + | An octave down or up |
| Fall / Rise | A semitone a step away from where the button was pressed, back to nothing after two bars |
| Roll 1/16 … 1/4 | One, two, three or four steps of the sequence, caught from where the button was pressed and played in place of what follows |

A roll of a sixteenth plays from the moment it is asked for; the longer ones let the
sequence through for the rest of their own length, recording it, before they stand in for
anything. A roll pressed where there is nothing to catch waits rather than holding the
silence, so a sixteenth landing a step wide of the note you meant still rolls a note.

**Anything held at once applies at once** — an octave up under a stab, both sends, a rise
through a reverb. The rolls are the exception, since all four stand in for the same
sequence: the one pressed last is the one that plays, and letting it go hands back to
whichever is still held. With a mouse only one button can be held at a time; a touch
screen holds one per finger.

Scores
------

Scores are saved under `Application.persistentDataPath/Scores` as plain text, one line
per step; pick one with the arrows beside Save. A copy of the app that has saved nothing
yet writes nine of them for itself — `score1` through `score9`, the first holding the
sample and the rest a bar with a note on each beat — so **every name on the chooser is a
file that is really there**. It opens in whichever was last saved or loaded.

The folder can be reached from outside the app: on a desktop through the System panel's
button, and on iOS in the Files app under On My iPhone (or On My iPad). A file dropped
into it appears in the score list, since that list is the folder read out rather than
anything the app remembers. In a Web build the path is the browser's own storage, so a
save keeps across a reload but not across clearing the site's data — and there is nothing
to open, so the button is not there.

**Loading while the sequence is running does not stop it.** The score waits for the turn
of the piece and the music carries straight on into it. While it waits, the plane and the
panel that edits the score are dimmed; the mix and the live effects go on working. A
request cannot be taken back — Stop is what ends the wait.

The sample that fills the first slot is checked in at
`Assets/Jacquard/Scores/Sample.jacquard.txt`. To replace it, save the score from the app
and copy the file over that one, then run **Jacquard > Run Self Test** to be told whether
it still reads as the current version — see [impl-files.md].

[impl-files.md]: impl-files.md
