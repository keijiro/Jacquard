Sequencer
=========

How the runner turns a score into note events, and what the sequencer promises
about when a note sounds. The code is `Assets/Core/Sequencer` with the model it
reads in `Assets/Core/Model`; the rules being implemented are in
[sequencer-spec.md]. What happens to an event after this is
[impl-synth.md]; when it reaches the device is [impl-audio.md].

[sequencer-spec.md]: sequencer-spec.md
[impl-synth.md]: impl-synth.md
[impl-audio.md]: impl-audio.md

Timing and the audio clock
--------------------------

**Timing** rides the audio clock. Every step is handed to the synth with the
exact sample it starts on, so a dropped frame delays the handover and never
the note.

One instant, one downward pass
------------------------------

**One instant is one downward pass.** Runners are one per `CHAN` lane, ordered
by the vertical position of that tile, and every lane is asked in that order,
each from the rail row of its step down. Everything a tile does reaches what is
read after it and nothing before it: a gate ends the descent, a lock colours the
notes that follow it, a note takes the channel as it stands where it sits. That
one rule covers both the inside of a stack and the lanes against each other,
which is what lets the accent lane, placed above the main one, colour it.

**A lane takes part in a pass whether or not it is due on it.** A lane that has
come to a step reads it. A lane part way through one puts back the locks that step
placed and sounds nothing. Which of the two it is, is read off the same number and
the same half sample of tolerance the slice itself is gathered with — for a
running lane the hold a step opens runs to exactly where the next one begins, so
being due and being part way through are the only two answers. A lane that has
stopped is the one case that is neither for a while: its position is out past
every comparison while the last step it played is still sounding, and the hold is
what carries that step's locks to the end of it.

The lifetime of a lock
----------------------

**A lock is over when the step it sits on is.** There is no accumulating lock and
no standing channel state: a channel no lane is holding starts each instant from
its patch again.

**The step is the lane's own, so a division says how far a lock carries.** While
every lane divides the bar the same way that is one instant and nothing more,
which is all this ever meant while it was written as one. An eighth-note lock
lane over a sixteenth-note lane of notes covers two of them with one step, and
both are lifted — the lock lane's step has not ended at the instant the second
note sounds. The empty cell after it is what lets the channel go, since a step is
read whether or not it holds anything and what it holds replaces what the step
before it left.

**A held lock is put back at the place in the pass its own lane occupies**, not at
the top of the slice, so it reaches exactly what a fresh one would: the lanes
below it and nothing above. The rule that a lock goes above the sounds it colours
therefore holds whether or not the instant being played is the one it was placed
on — an accent lane below the notes reaches them on no instant at all.

**What a runner holds is the lock tiles it reached, not the numbers they came to.**
Reading them again writes the same working patch as the pass that placed them:
the relative ones stack and clamp in the same order, and no gate is asked twice,
since a tile a gate cut off never entered the hold. A runner put back at the top
of its lane lets go of everything, because what it was holding was the reading of
a step in a run that has ended.

Transpose and scale
-------------------

**What a written note sounds as is decided twice, and neither pass is an edit.**
`Project.SoundingPitch` is the whole of it: the channel's transpose moves the note,
and then the scale drops it onto the nearest semitone it allows. The plane is not
touched — a note tile keeps the pitch it was given and goes on showing it — which is
the point rather than a limitation. A scale that rewrote the notes could be applied
once; one that decides what they sound as can be tried against a piece, moved, and
taken off again, and the piece underneath is still the piece that was written.

**The order is the feature.** Snapping first and transposing after would carry every
note straight back out of the key, which makes both settings useless at once, so the
two live in one function rather than at the two places that ask. And what the scale
does to a note it will not have is snap and not drop: a note that does not sound is a
hole in the music, and no arrangement of a stack fills one.

**The scale is the project's and the transpose is the channel's**, which is the same
split the effects already make: one reverb for the whole thing, and how much of a
channel reaches it in the patch. A piece is in a key — two channels in two keys is
two pieces — while how far a part is moved is plainly a property of that part.
Everything on is the scale that does nothing and is where a score starts, so a file
from before there was one sounds exactly as it did. Nothing on has nowhere to send a
note, so every note stays where it was written; that is inert rather than wrong, the
same way a cycle gate switched on nowhere is.

**The live effects are outside it, and no code says so.** `LiveFx.Colour` stands
after the sequencer has made the event and works in hertz, having no semitone left to
move by then — so an octave or a rise reaches whatever pitch it likes and the scale
never sees it. That is the right answer as well as the free one: a key signature is
something the piece is written under and a live effect is a hand on a button, and a
gesture that could only reach the notes already allowed would be a gesture with the
interesting part taken out.

Mutes and solos
---------------

**A mute is not an edit**, which is what makes the Channels panel worth having next to
a delete key. `ChannelMutes` holds a mute and a solo per channel and the sequencer asks
it one question, at the last moment it can: a note tile on a silent channel is dropped
on the way out and nothing above that line is skipped. The gates have already turned
over, the locks have already coloured the working patch, the jump under it is still
taken and the lap still counts — so letting a channel back in is hearing it from where
the sequence has got to, and that is precisely what deleting the lane and undoing it
could not do.

**A solo is a mute of everything else**, so the two live in one object rather than two:
with anything soloed the question is whether a channel is one of them and the mutes are
not consulted at all. They are kept rather than cleared, which is why the switches grey
out instead of clearing themselves — dropping the last solo gives back the mix that was
underneath it.

**Both sets are saved**, on a `mutes` line of two digit runs — one switch per channel,
the spelling a cycle gate's laps already use — and `ChannelMutes` hangs off `Project`
rather than off the app so that the file is the only thing that has to carry it. It was
the other way round first, on the argument the live effects are still kept out of the
format by: a hand held over a channel is played rather than set, and what a file holds
is the piece. What that missed is that the hand comes off again. A performance is gone
the moment it stops; a mute left on is a decision about how the piece is heard, and
version 12 is the admission that reopening a mix to find every channel back in is
losing one silently rather than declining to record it.

The soloed set is written as well as the muted one even though only ever one of them is
consulted, because the mutes underneath a solo are exactly what dropping the last solo
gives back — a file that recorded only the audible answer would come back having
cleared them, which is the one thing the rule above promises cannot happen.

What this costs is a second home for the same state, and it is paid by not having one:
the sequencer, the panel and the format all read `Project.Mutes` through the project
they are already holding, and a load is wired by the assignment that was already there.
Anything keeping a `ChannelMutes` of its own would go on pressing switches on the file
that was closed, and nothing about the sound would say so.

The panel stands in the top left, the one corner the cursor's panels never reach, and
is raised by a switch on the transport row like everything else there. Being played
rather than set is an argument for having it up already, and it lost: eight rows of
chrome over the plane is a lot to leave standing, and a hand that is muting is a hand
that can press the switch first. It shows all eight channels at once because that is
what it is for — a mute is only pressed against what the rest of the mix is doing. *Select* is the row's way onto the plane rather than
a second way of opening a panel — it moves the cursor to the `CHAN` tile that names the
channel, and the Tile panel comes up showing that channel's sound because the cursor
is on it, the same rule as ever. A channel with no lane has nowhere to go and greys out, which is also the only
place on screen that says which of the eight are in use.

Switching score without stopping
--------------------------------

**A score comes in at the turn of the piece, and the seam is a sample.** A load while
the transport is running does not stop it: `Sequencer.SwitchTo` parks the project and
the runners are rebuilt on the lap line of the master lane — `Score.MasterLane`, the
first channel one lane in the order runners are born in. The line cannot be worked out
ahead, since a gate over a `JUMP` decides how long a lap is and one of them throws
dice, so it is found as it happens: while something is waiting, the slice loop watches
the master's `Pass` across each slice, and the moment it turns over `master.NextSample`
is the answer. That sample then becomes a nearer horizon than the window, the outgoing
score is run out to it, and the takeover happens inside the same pass — which is what
leaves nothing between the two scores and nothing over them.

Three things make this small. Slice times only ever increase, so at the moment the
wrap is seen nothing at or after the line has been emitted. A note is a one-shot
event carrying its own gate, release and whole timbre, so nothing needs a note-off at
the seam and what was already sounding rings on into the new score by itself. And the
outgoing score's last window is already parked in `LiveFx`, so the load must **not**
touch it — `Live.Stop()` empties that queue, and emptying it is exactly the hole this
is here to avoid.

The one figure to get right is the comparison at the line, which is `S - Tolerance`
and not `S`. A lane whose lap divides the master's — four steps against sixteen — lands
on the line bit for bit, since both positions are the same power-of-two multiple of a
step accumulated in a double. Letting it run there would sweep the master into that
slice as well and play the first step of the new lap twice. So half a sample before
the line is where the outgoing score stops, and the cost is that a step landing inside
that half sample without being coincident with it is dropped rather than played early
— about one in twelve thousand, against a flam on every divisor lane at every seam.

What the shape is reusable for is the point of writing it this way. A second thing
wanting to happen on the turn of the piece adds a slot beside the pending project,
one term to the predicate that arms the watch, and a branch in the takeover. What is
deliberately not offered is a public reading of where the line is: at the only moment
it exists it points up to a lookahead into the future, so anything drawing from it
would run ahead of what is heard. `MasterRunner.PlayingStep` is the playhead-corrected
answer to how far through the lap the music has got.

Holding the plane while a score waits
-------------------------------------

**The plane is held still while a score waits, and the screen follows the sound.**
`ScoreEditor.Locked` refuses every path into the score and the panel that edits one
dims itself and stops taking presses, because an edit that moved a lane would
move the line the switch is measured on. Nothing about the mix is held — sound, sends,
limiter, tempo, mutes and the live effects all go on working, since playing across the
seam is the whole point of waiting for it. The plane takes the same 0.45 the mutes and
a released lock row take, and a press on it is let through rather than stopped, so the
scroll area still pans it. A panel is put out of reach by one stretched picking shield
rather than by a flag on each of its dozen controls, and never by `SetEnabled`, which
would bring the default theme's grey with it.

The sequencer changes hands up to a lookahead before the seam is audible, so
`JacquardApp` holds the sample the `Switched` event carried and adopts the project
only once the clock reaches it. The plane therefore comes back exactly as the music
turns over rather than a tenth of a second early. What it costs is that for that
fraction of a second the app's project is one behind the sequencer's, so a mute
pressed inside the window is written to the score that is leaving.

Starting and stopping a lane
----------------------------

**A lane starts on the turn of the piece, which is the second thing to want that
moment.** The item above left a slot for one, and this is it: a `CHAN` carries a
switch, a lane switched off stops when its runner reaches the end of the lane, and a
lane switched on runs from the sample the next master lap begins on. The lap line
needed no new machinery at all — `Sequencer.Schedule` already read `_master.Pass`
across each slice, and the change is that it reads it always rather than only while a
score waits. What it costs is one branch: a lap that a load is waiting on belongs to
the load, since the score arriving seats its own runners a moment later and starting
a lane on the far side of that line is work thrown away by `TakeOver`.

**Not running is said by the sample and not by a flag beside it.** A stopped runner's
`NextSample` is `Runner.Never`, which is `double.MaxValue`, so the two loops that
decide who plays — the scan for the earliest sample and the gather of everything
within half a sample of it — exclude it with nothing written in either of them for
the purpose. A `bool` would have to be read in both, and the failure it invites is
the one that would be worst here: a flag saying running with a sample that says
otherwise. `Runner.Running` is a reading of the number rather than a second copy of
it. `NoBoundary` is the same trick on the lap line, and the invariant that keeps the
loops from stalling is that the master runner is always running — which is also why
the master lane cannot be switched off, since a silent one would leave every other
lane with nothing to come in on.

Two edits can leave a master that is not running: deleting the master lane hands the
title to whatever lane is topmost now, which may be one that has stopped, and putting
a project in outright reassigns `_master` over the runners of the score going out. The
repair is in `Schedule` rather than in `Resync` because it needs a sample and `Resync`
has no clock — it is the standing start `Play` uses, one lookahead ahead.

**A lane that comes back counts its laps from zero**, so a cycle gate on it fires on
the lap it fires on from a standing start. The argument is not about cycle gates
though: a lane switched back on and a lane drawn a moment ago both wait for the same
line and both then start, so they have to sound the same, and a lane that has just
been written has run no laps. It also parts company with the mute here, deliberately —
a muted channel is heard from wherever the sequence has got to because it never stopped
running, and this one stopped. The two switches look alike and that is the whole
difference between them, so both say so in their own comments.

**The playhead is told where the lane ends rather than cleared.** A stopping runner
records a marker with no lane on it at the sample after its last step, and
`AdvancePlayhead` dequeues it when the clock arrives — so the light goes out exactly
as the last step is heard. `ClearPlayhead` empties the queue instead, which would
throw away a lookahead of steps that are scheduled and still to sound, and the drawing
would stop before the music did. The sample matters as much as the method: a marker
sharing a sample with the last step is dequeued in the same breath as it, and the
final cell of the lane would never light.

**The cell is drawn from what will happen and the switch from what is written.** A
stopped lane's `CHAN` gives up its solid field for the grey one a lock sits on, which
is the pair of colours `Controls.SetActive` already dresses a switch in; the master
lane stays solid whatever its switch says. So the two disagree on exactly one lane, and
that is the lane where the specification disagrees with itself on purpose. What the
cell does not say is whether a lane switched on has come in yet — up to a lap goes by
first — and the playhead already says that, so between them there are three states and
two drawings and no third look to invent.

The plane still says nothing about *which* lane is the master. It can be read off the
one lane whose switch and cell disagree, which is a poor way to say it; a mark for it
is a change of its own, and this one does not need it.
