Synth
=====

The two operator FM voice, the patch a channel holds, and what a parameter lock can
reach. The code is `Assets/Core/Synth` for the settings and `Assets/Jacquard/Audio` for
the voice pool and the render job. The buses everything here renders into are
[impl-mix.md]; what the parameters look like on screen is [impl-panels.md].

`FmPatch` and `ParamTargets` carry the argument for every field and every range, and
`FmVoiceState` for the oscillator. What is here is the three rules that decide where a
new parameter goes and what it costs.

[impl-mix.md]: impl-mix.md
[impl-panels.md]: impl-panels.md

Every field of the patch is a lock target
-----------------------------------------

`FmPatch` and `ParamTargets` name the same fifteen parameters, so there is nothing a
channel holds that a step cannot reach for as long as that step lasts — and no section a
panel has to keep for the ones it cannot. **A field added to one is added to both**, and
adding a target is a one line change in three switches.

Two consequences worth knowing before adding one:

- **It costs a row on two panels**, and on a tablet a row is 33pt of a column a hand has
  to drag. The screen is the real budget here, not the switch statements.
- **A field the synth never sees still belongs here if it answers to a channel.** The
  transpose is spent by the sequencer before an event exists and has nothing mirroring it
  in `FmNoteEvent`, and it is in the patch anyway — because the sequencer reads the
  *working* patch, the one this instant's locks have already coloured, which is what lets
  a lock lift one step's notes and no others.

**The names on the panel are a player's and not the code's.** `ParamTargets`' constants
and the file's keys keep the older spellings, since renaming those would make older files
unopenable for the sake of a word on screen. The order is the order the panels read in,
and it is argued in `ParamTargets`.

What is settled at note-on is never smoothed
--------------------------------------------

**A note event carries its whole timbre**, so nothing about a channel is left on the
synth side and a voice reads its event once. That is what makes the send amounts, the
pan and the unison plain fields of the patch rather than properties of a bus: a position
and a send decided at note-on are a position and a send that can never need smoothing,
and what moves when a panel moves is the next note.

So `FmVoicePool.Render` renders a voice once and splits the sample four ways — the two
sides of the dry bus and the two send buses — rather than mixing anything afterwards.
The sends take the voice **unpanned**, since each builds an image of its own out of a
mono feed and a tail leaning towards its note's side would be two answers to one
question.

**A send is in the patch; what it feeds is in the project.** That split is the whole
reason the effects are worth having on a sequencer like this one: a `PABS` above one note
of a chord puts that note in the reverb and leaves the note above it dry, and no amount
of per-channel effect settings could say that. The same reasoning puts the scale on the
project and the transpose in the patch — see [sequencer-spec.md].

**Timbre belongs to the channel.** The bank holds one patch per channel and a `CHAN`
number picks the sound as well as the stream, so lanes sharing a channel share a patch
and a branch lane borrows the one of whatever jumps into it.

[sequencer-spec.md]: sequencer-spec.md

A changed unit is a format version
----------------------------------

The reader has machinery for a target that retires (`Retired`) and for one that arrives
(it falls back to a default). **What it has no machinery for is a live target whose
number now means something else** — that looks exactly like a current one. So: **a target
that changes what its number means belongs in a version bump with a conversion, in the
same change.** The FM decay is the case that established this — version 10 and
`DecaySlope`, which converts an `md=` and an absolute lock to the same decay rate and
deliberately leaves a relative one as written.

The level is the second case, version 18, and it is the one that says **a relative lock
is a separate question from the value it shifts.** A shift has no image in a new unit on
its own — only against something the file also states. The FM decay had nothing to read
one against and kept the number it was written with; a level shift has the level of the
channel it stands on, so it converts exactly, and `ProjectFormat.LevelShifts` does it
after the whole file rather than at the token, because a branch lane's channel is
whichever lane jumps into it.

Where the rest is written
-------------------------

| | |
| --- | --- |
| The two envelopes' shapes, and why the modulator's decay is a slope and not a time | `FmPatch` — `ModulatorLevel` |
| What a parameter falls back to, and why the tone a new score starts in is not the same thing | `FmPatch.Default`, `Project.CreateInitial` |
| The equal-power pan law, and why unity sits at the centre rather than at the ends | `FmPatch.Gains` |
| Unison: the detune as an interval, the spread finishing earlier, the gain law pinned at both ends | `FmPatch` — `DetuneRatio`, `Spread`, `Reach`, `UnisonGain` |
| Why pan reaches the wall and the width is what gives way | `FmPatch.Reach` |
| Why a unison pair is one voice and not two | `FmVoiceState` |
| What two partials share, and the feedback memory they must not | `FmVoiceState` |
| The note budget the pan law implies | [impl-mix.md] |
