Live effects
============

The twelve buttons that colour a note on its way out and are written nowhere.
The code is `LiveFx` in `Assets/Core/Sequencer`, the handover window in
`JacquardApp`, and the panel in `Assets/Jacquard/UI`.

What the live effects are
-------------------------

**The live effects are the one thing that colours a note without being written
anywhere**, and that is what they are for. Everything else here is score: a lock is a
tile, a gate is a tile, a timbre is a patch the file carries. None of it can be held
for two beats and let go, so none of it can be *played* — and `LiveFx` is the layer
that can. Twelve buttons, on while they are held, sitting between the sequencer and
the synth: two throw every note into the reverb or the delay, two shorten or lengthen
it, two move it an octave, two ramp it a semitone a step and turn over after two bars,
and four catch one, two, three or four sixteenths of the sequence and play that in
place of what follows.

**Stab and Sustain reach the release as well as the gate**, because how long a note
lasts is the two of them and not the gate alone. A gate cut to a tenth of a step under
a release of a quarter of a second is not a short note: it is let go early and then
takes exactly as long as it ever did to go quiet, so what a stab would sound like is a
stab and a wash. Stab therefore holds the release down to ten milliseconds — and only
holds it *down*, so a patch already clipped tighter than that keeps what it has rather
than being lengthened by a button that means shorter. Sustain doubles it alongside the
gate, since a note held twice as long wants a tail in proportion; doubling the gate
alone would leave a fixed tail on a note of a different length, which is a change of
envelope rather than of length.

**What it reaches is what has not been handed over**, which is also the whole of the
promise it makes: a voice reads its event once, so a note already sounding is not
retuned, not shortened and not thrown anywhere by a button pressed after it began.
And nothing here is saved. A press is a gesture rather than a setting, so there is no
file key, no version bump, no lock target and none of the 33pt a row of the Sound
panel costs.

**The grid is the project's sixteenth and not the lane's step.** A ramp that climbed
faster under a lane running in eighths would be two answers to how far up the ramp is,
and a roll is a length of time rather than a lane's idea of one.

The handover window
-------------------

**What the live effects cost is the margin against a slow frame**, and it is the one
place this project has given a stated property back. The sequencer runs a lookahead
ahead because a dropped frame should delay the handover and never the note — but a
live effect reaches only what has not been handed over, and at 129bpm a sixteenth is
116ms against a window of 120, so the two being the same window is what would make a
press take a step to be heard. So the sequencer still runs the full window ahead and
`LiveFx` parks what it produces; `LiveLead` is the much shorter window a note actually
leaves the queue on. Nothing moves but the moment of the handover — the sample a note
starts on was decided by the runner and is never touched — so the sequence is as exact
as it was.

What is paid for that is 120ms of slack becoming 30. It is affordable because a note
handed over late is not played late: `FmVoicePool.Render` triggers it against the
clock and computes a positive elapsed time, so what a hitch takes is the head of a
note rather than its place in the bar. On the Web the driver's own floor is most of a
tenth of a second and sits under both windows, so the margin there is where it was and
the live effects are as late as everything else on that platform already is.

**That window is measured in frames, and thirty milliseconds is not a number of
frames.** The handover happens once an `Update` and takes everything already inside the
window, so a note leaves in the frame before the one it would have missed: the lead it
really gets is the window less however long that frame ran. The requirement is
therefore that the window be longer than a frame — 1.8 of them at sixty a second, and
0.45 at fifteen, which is where iOS puts this app when the device gets hot
(`adjustIOSFPSUsingThermalState`). Measured there with the fixed window: every note
losing up to 32.7ms of its front, and the lead trim stepping in a second later to cover
it under the wrong name. So `JacquardApp.HandoverSeconds` is two smoothed frames,
clamped between `LiveLead` and `Lookahead` — the floor so that a fast display does not
shrink a live effect's reach below what was chosen for it, the ceiling because a note
cannot be handed over before the sequencer has produced it. Two rather than one because
the second is the margin, and smoothed because a single long frame is the hitch the
paragraph above already pays for. At fifteen frames a second the ceiling binds and the
live effects stop reaching anything, which is the right trade in a state where the
alternative is every note arriving beheaded.

Rolls
-----

**A roll records what sounded rather than re-running the step it came from.** Holding
the step index and reading the tiles again would put the probability gates and the
cycle gates through a fresh judgement on every pass, so a roll would be a different
roll each time — which is a generator, and what is wanted is the thing that was just
heard, said again. So the window is a list of note events with a sample to start on,
laid down again from its far end with only that sample changed. Every other live
effect still applies on top, since they are applied after: a roll caught plain and then
thrown into the reverb goes into the reverb, and one caught under an octave comes back
down when the hand comes off the octave.

**The near end of a roll's window is always in the past**, because a press is seen
after the step it lands in has begun, and with the handover a fraction of a sixteenth
wide the notes of that step have usually gone already. Hence the record of what has
sounded: the part of the window behind us comes out of that and the part still to come
is written down as it is handed over, which is one mechanism for all four lengths. The
sixteenth is full the moment it is asked for; the longer ones spend the rest of their
own length letting the sequence through and writing it down before they stand in for
anything. That is not a compromise — it is what the spec describes, and it is why the
four are one class with one number different, which is also why the number is the name
on the button.

**A roll counts its grid the way the sequencer counts its own.** Both put a step at
`origin + (long)(index × sixteenth)`, truncated rather than rounded, and the agreement
has to be exact: a sixteenth is a whole number of samples at about half the tempos and
a fraction at the rest, and where the two differed by a sample the step at the far end
of a window landed one sample inside it. It was then neither suppressed nor left
behind — it played, and it was written into the window as an extra member, so a roll of
two came out as three notes with the third a sample ahead of every repetition of the
first, for as long as the button was held. For the same arithmetic each pass is placed
on the grid rather than a window's length past the last one: the length is rounded to
whole samples once, and adding it over and over walked the roll off the beat by about
twelve milliseconds a minute and went on walking. Placing each pass separately leaves
the rounding as a sample at one end of one pass instead of a sample carried into every
pass after it.

**A window with nothing in it never stands in for the score.** A roll pressed into a
gap has nothing to lay down, and standing in with nothing is silence held for as long
as the button, which is the one thing a hand reaching for a roll can never have wanted.
So an empty window stops nothing, and once it has closed still empty it is let go and
the next one along is taken, until one of them catches something. What decides is the
window and not the step it opens on, so a longer roll waits only for a stretch that is
silent all the way through, and a sixteenth — the one length that can miss a note by
landing a step out, which is the case a hand actually meets — starts on the next note.
The search moves at the speed of the music rather than all at once, since a step has to
be handed over before there is anything to know about it, and it is not capped: four
silent bars are four bars of waiting, which is at least a thing that can be explained.

**The rolls are the only thing here that does not stack.** Everything else is summed:
two octaves the opposite way come to no semitones at all, and a stab under a sustain
is a fifth of a step, because the modifiers are applied in one order to one event. The
rolls cannot be, since all four answer the question *what plays instead of the score*
and there is one answer. The one pressed last owns it — counted rather than read off
the clock, so that two arriving in the same frame still have an order — and letting
that one go hands back to whichever is still down. All four catch and record whether
or not they are the one playing, so a hand-back is to a window that is already full.

What that costs is one line in `Repeat`: a roll covered by another stops being laid
down while that lasts, so its own mark stays where it was left, and every pass it
missed would come out at once the moment it was handed back — a handful of notes in
the past, with their heads already cut off. So the mark it resumes from is never
behind the handover.

The Live FX panel
-----------------

**The Live FX panel is the one that is played rather than read**, so it is the one in
neither column. The columns are where the eye goes — the cursor's panels and the send
effects on the right, the channels on the left — and reading is done
at arm's length from what is being said. A column would also put this under one hand
and out of reach of the other, and take a corner of the plane for something that is up
only while it is being used. Across the bottom, centred, it is as wide as its own
contents and sits where both thumbs already are on a tablet held in two hands.

It is six columns of two, and the top of a column is always the smaller of the pair:
the two sends, the short gate over the long one, down over up, falling over rising,
and then the four rolls in length order, read down each column and then across. So
where a button is says what it does before the word on it does.

**The names are a player's and not the code's**, the same split the sound captions
make. *Stab* is a gate of a tenth of a step with the tail cut back to match
and *Sustain* is both doubled; *Rise* and *Fall* name what is heard where *Ramp* names
the shape; *Reverb* and *Delay* name what receives, for the reason the Send FX button
gives. The rolls are named by their length — *Roll 1/16*, *Roll 1/8*, *Roll 3/16*,
*Roll 1/4* — because the length is the only thing that tells them apart, and a note
value is the thing itself rather than a code standing in for one, which is the same
reason a note tile shows `A4`. Roll and not Loop, since a lane already loops at its
terminator and that is a different thing entirely; a roll is the one that is held.

**`Controls.Hold` takes the stock `Clickable` off rather than working around it.** A
Clickable reports on the release and captures the pointer to decide whether the
release counts, so a press and a release read through it arrive together at the end
and the effect is never on for any length of time. What is left is a box dressed as a
button with `PointerDownEvent` and the lost capture read directly — the lost capture
and not the release, because a capture can go without one, and an effect latched on is
the one failure this control cannot have. The capture is per pointer id, which is what
makes two fingers two independent holds; a mouse has one id, so with a mouse the
second press takes the first one's capture and only one effect is ever held, which is
the truth about a mouse rather than a limit of the panel.
