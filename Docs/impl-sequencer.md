Sequencer
=========

How the runner turns a score into note events, and what the sequencer promises about
when a note sounds. The code is `Assets/Core/Sequencer` with the model it reads in
`Assets/Core/Model`; the rules being implemented are in [sequencer-spec.md]. What happens
to an event after this is [impl-synth.md]; when it reaches the device is [impl-audio.md].

`Sequencer`'s own header states the model — the slice, the downward pass, the lock's
lifetime, the turn of the piece — and each method argues for its part of it. What is here
is how the spec's rules map onto the code, and the invariants that hold across files.

[sequencer-spec.md]: sequencer-spec.md
[impl-synth.md]: impl-synth.md
[impl-audio.md]: impl-audio.md

Where each rule of the spec lives
---------------------------------

| The spec says | The code that answers it |
| --- | --- |
| Timing rides the audio clock; a dropped frame delays the handover, never the note | `Sequencer.Schedule` |
| One instant is one downward pass, lanes in `CHAN` order, each from the rail row down | `Sequencer.Descend` |
| A lock lasts for the step it sits on, and reaches only what is read after it | `Runner`'s held tiles, put back at its own place in the pass |
| The execution order is the vertical position of the `CHAN` | `Sequencer.Populate` — `Score.ChannelLanes` hands them over in that order |
| A note sounds as transpose then scale, and the plane is never rewritten | `Project.SoundingPitch` — one function, so the order cannot be got wrong twice |
| A score comes in on the turn of the piece | `Sequencer.SwitchTo`, `TakeOver`, `Score.MasterLane` |
| A `CHAN` switched on runs from the next turn, counting laps from zero | `Sequencer.StartPending` and the lap watch in `Schedule` |
| The master lane cannot be switched off | The invariant below |
| A mute is silent but running; disabling is not running | `ChannelMutes` — asked at the last moment, so nothing above the note is skipped |
| Live FX stands outside the scale | Nothing says so. `LiveFx.Colour` works in hertz, having no semitone left to move by |

The invariants
--------------

**One tolerance, asked three times.** Gathering a slice, telling a lane that is due from
one part way through a step, and deciding which side of the lap line a runner falls on
are the same question, and they are asked with the same half sample. `Sequencer.Tolerance`
is the one place it is written down; `Lands` and the hold are deliberate complements, so
a running lane is either due or mid-step and never neither.

The one figure worth knowing before touching the seam: the outgoing score stops at
`S - Tolerance` and not at `S`. A lane whose lap divides the master's lands on the line
bit for bit, and letting it run there plays the first step of the new lap twice.

**Not running is said by the sample, never by a flag beside it.** A stopped runner's
`NextSample` is `Runner.Never`; `Runner.Running` is a reading of that number rather than
a second copy of it, and `NoBoundary` is the same trick on the lap line. A `bool` would
have to be read in both loops that decide who plays, and the failure it invites is the
worst one available here — a flag saying *running* over a sample that says otherwise.

**While the transport runs, the master runner is running.** This is what keeps those
loops from stalling, and it is the real reason the master lane's switch is ignored rather
than a rule about music. Two edits can break it — deleting the master lane hands the
title to a lane that may have stopped, and assigning a project outright reassigns
`_master` over the outgoing score's runners — so the repair is in `Schedule`, which has a
clock, rather than in `Resync`, which does not.

**A load must not touch the live effects queue.** The outgoing score's last window is
parked in `LiveFx`, and emptying it is exactly the hole that waiting for the turn exists
to avoid. `Live.Stop()` is not part of a load.

What spans this and the interface
---------------------------------

**The plane is held still while a score waits, and the screen follows the sound.**
`ScoreEditor.Locked` refuses every path into the score; the panel that edits one dims and
stops taking presses, because an edit that moved a lane would move the line the switch is
measured on. Nothing about the mix is held — playing across the seam is the whole point of
waiting for it. A panel is put out of reach by one stretched picking shield rather than by
a flag on each of its controls, and never by `SetEnabled`, which would bring the default
theme's grey with it.

The sequencer changes hands up to a lookahead before the seam is audible, so `JacquardApp`
holds the sample the `Switched` event carried and adopts the project only when the clock
reaches it. The plane therefore comes back exactly as the music turns over. What it costs
is that for that fraction of a second the app's project is one behind the sequencer's, so
a mute pressed inside the window is written to the score that is leaving.

**The playhead is told where a lane ends rather than cleared.** A stopping runner records
a marker with no lane on it at the sample *after* its last step, and `AdvancePlayhead`
dequeues it when the clock arrives. Emptying the queue instead would throw away a
lookahead of steps still to sound, and the drawing would stop before the music did.

**The cell is drawn from what will happen and the switch from what is written**, so the
two disagree on exactly one lane — the master, which is the lane the spec disagrees with
itself about on purpose. Between the cell and the playhead there are three states and two
drawings: grey is stopped, solid with no playhead is about to come in, solid with one is
playing. The plane still says nothing about *which* lane is the master; it can be read off
the one lane whose switch and cell disagree, which is a poor way to say it, and a mark for
it is a change of its own.

What is reusable
----------------

A second thing wanting to happen on the turn of the piece adds a slot beside the pending
project, one term to the predicate that arms the watch, and a branch in the takeover.
Starting a lane was the first of those and needed no new machinery at all.

What is deliberately not offered is a public reading of where the lap line is: at the only
moment it exists it points a lookahead into the future, so anything drawing from it would
run ahead of what is heard. `Sequencer.MasterRunner.PlayingStep` is the playhead-corrected
answer to how far through the lap the music has got.
