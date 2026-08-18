Synth
=====

The two operator FM voice, the patch a channel holds, and what a parameter lock
can reach. The code is `Assets/Core/Synth` for the settings and
`Assets/Jacquard/Audio` for the voice pool and the render job. The buses
everything here renders into are [impl-mix.md]; what the parameters look like on
screen is [impl-panels.md].

[impl-mix.md]: impl-mix.md
[impl-panels.md]: impl-panels.md

The patch and its lock targets
------------------------------

**Every field of the patch is a lock target.** `FmPatch` and `ParamTargets` name
the same fifteen parameters, so there is nothing a channel holds that a step cannot
reach for as long as that step lasts. One of them, the gate ratio, multiplies the
length written on the note rather than being a length itself, which is why the
note reads in steps and the channel in percent: the two are the same
multiplication and only the unit tells them apart.

**One of them the synth never sees.** The transpose moves the note the sequencer is
about to make, so it is spent before an event exists and is the one patch field with
nothing mirroring it in `FmNoteEvent` — an event already knows what it sounds, and a
number saying how far it was carried to get there would be a second answer nobody
reads. It is in the patch because it answers to a channel, the way the sends do, and
in the target list because the list *is* the fields of the patch. What that buys is
the reason it is worth having there rather than beside the tempo: the sequencer reads
the working patch, the one the locks standing at this instant have already coloured, so
a `PABS` or a `PREL` on the transpose lifts the notes under it in that step and no
others.

Parameter names and their order
-------------------------------

**The parameters are named and ordered for a player, not for the synthesis.** What is
on the panel is `FM ratio`, `FM amount`, `Feedback`, `FM decay` and then `Amp attack`
/ `Amp release`, where the code says `modulationIndex`, `carrierAttack` and so on. A
modulation index is a term from a textbook about a technique, and a musician reaching
for a brighter sound is not thinking about which operator is the carrier; the four FM
rows are in the order one is dialled in — what the modulator is tuned to, how much of
it arrives, how much of itself it hears, how fast it gets out of the way. Only the
captions moved: `ParamTargets`' constants and the file's keys keep the older
spellings, since renaming those would be a way to make older files unopenable for the
sake of a word on screen.

The FM decay slope
------------------

**The FM decay is a slope rather than a length of time**, which is the one parameter
here that is not in the unit it obviously wants to be. As a time it was unplayable at
both ends and unplayable in the middle for a different reason: 30ms is a bite under a
stab and nothing at all under a pad, so the number had to be re-entered for every note
length it met, and the two settings either side of the useful range — an FM patch with
no modulation, and one whose modulation never leaves — are not quantities of
milliseconds at all. So `modulatorDecay` runs 0 to 1 and sets how steeply the depth
falls: 0 stands the decay up vertically and the note is a plain sine, 1 lays it flat
and the full depth holds for the life of the note, and in between it is an exponential
with a time constant of a tenth of a second times `v / (1 - v)`. That puts a click in
the first tenth of the travel, a drum's bite around a fifth, and a modulation meant to
be heard moving in the last third — so the bar over it is straight, since the mapping
is already the curve.

The cost is a file that means something else, and the *change of units* is a case the
reader had no machinery for: a retired target is skipped and a new one falls back to a
default, but a live target holding a stale number looks exactly like a current one.
Hence version 10 and `DecaySlope`, which converts an `md=` and an absolute lock on the
way in — to the same decay rate, not to something near it. A relative lock is left as
written, because a shift has no image under that curve and needs none: the old
parameter ran over the same span of numbers as the new one, so a shift reaches as far
across its bar as it ever did. Which is the second standing obligation on this reader,
next to `Retired`: **a target that changes what its number means belongs in a version
bump with a conversion, in the same change.**

One lock, many targets
----------------------

**One lock reaches as many of them as it likes.** A lock carries a slot per
target and holds whichever ones have been set, so a step that changes four
parameters is one tile rather than four stacked cells between the gate and the
note. What it does not hold it leaves entirely to the channel, which is why a
lock that holds nothing — a freshly placed one — is inert rather than wrong.

Timbre belongs to the channel
-----------------------------

**Timbre belongs to the channel**, not to the project: the bank holds one patch
per channel and a `CHAN` tile's number picks the sound as well as the stream, so
lanes sharing a channel share a patch and a branch lane borrows the one of
whatever jumps into it. The sound group of the Tile panel is where that patch is
edited, and an edit is heard from the next instant with nothing to undo.

Sends: the patch and the project
--------------------------------

**A send is in the patch; what it feeds is in the project.** There is one reverb and
one delay for the whole score, so their settings sit on `Project` beside the tempo —
but *how much* of a note reaches each is two more fields of `FmPatch`, which makes
them lock targets like everything else there. That split is the whole reason the
effects are worth having on a sequencer like this one: a `PABS` above one note of a
chord puts that note in the reverb and leaves the note above it dry, and no amount of
per-channel effect settings could say that.

It also means **no send ever has to be smoothed.** The send gains are read off the
note event, so a voice holds them for its whole life and what moves when the Sound
panel moves is the next note. `FmVoicePool.Render` therefore renders a voice once
and splits the sample four ways — the two sides of the dry bus and the two send
buses — rather than mixing anything afterwards.

Stereo and the pan law
----------------------

**Every path is stereo, and each became so for its own reason.** The wet one first:
a reverb with no width and a delay that cannot cross sides would be most of both
effects thrown away, so `ReverbBus` and `DelayBus` each keep two lines and
`EndProcessing` writes L and R where it used to copy one buffer everywhere. The dry
one followed, because **pan is a field of the patch** rather than a property of a
lane: it is a position per note, which is finer than either bus could say, and it is
the only thing here that can spread a chord out at all. `FmVoicePool` therefore
renders into `dryL` and `dryR` at a pair of gains read off the note, the same
arrangement the sends have and for the same reason — a position fixed at note-on
never has to be smoothed.

**The law is equal power, normalized to unity at the centre and not at the ends.**
A pair of straight fades sags 3dB as it crosses; a circle does not. Putting the
unity point in the centre is what makes a patch that never touches pan render
exactly as it did before there was one — the same thing the silent sends bought —
and it is paid for at the extremes, where a note is 3dB up on the one side it is
still on. Which also means a note at level 1 arrives at the mix already at full scale
on both sides, so **the mix budget is counted in notes** — see the staging below,
which is the number that says how many.

**The sends take the voice unpanned.** Each is a mono feed into an effect that
builds an image of its own, so a tail that also leaned towards the side its note
came from would be two answers to one question.

Unison
------

**A unison pair is one voice and not two**, which is the decision the rest of the
parameter follows from. Above zero, `unison` sounds the note twice — the two halves
tuned a little apart and stood either side of where the pan puts them — and both are
rendered by the same slot, so `FmVoiceState.Next` hands back two numbers where it
used to return one and `FmVoicePool` sums them for the sends and spreads them across
the dry bus.

Two voices was the obvious alternative and it loses three ways. The pool is
twenty-four slots, so a pair per note halves the polyphony, and slots are the scarce
thing here where CPU is not — the mix costs 1.29ms of a 5.3ms buffer with all
twenty-four sounding. A note is made in three places — the sequencer, a sound bar's
audition and `LiveFx`, which colours events after the fact — so a pair made
upstream is a pair three call sites have to keep making correctly. And `Trigger`
knows nothing about pairs, so stealing would take one half and leave the other
sounding alone: a note that goes half out of tune exactly when the music is densest.

**The detune is an interval and the spread is a position, and they finish at
different places on purpose.** Sixty cents end to end at the top of the travel,
which is 15Hz of beating at A4 and a pair that is audibly arguing about which note
it is — the edge the parameter is aimed at. In cents rather than in Hz for the
reason the pitch envelope is in octaves: a fixed number of Hz is most of a semitone
under a bass line and nothing under a lead, so one setting would mean a different
amount of detune on every part it was used on. The image, meanwhile, is somewhere a
pair can be *put*, and once it is at the sides there is nowhere further; so the
spread finishes at 0.3 and the rest of the bar is detune alone. Tying the two
together would have meant no setting where a wide pair is only just detuned, which
is most of what this is for — the first third buys the image and a chorus at 18
cents, and the rest reaches for the edge.

It was thirty cents first, picked as the point a pair stops reading as one note, and
that number was arrived at by reasoning and not by listening. Played, thirty is
where that *begins*: the top of the bar was a wide chorus, the sourness the
parameter is named for was not on the bar at all, and everything interesting was
crowded into the last inch of travel. Sixty puts a genuinely different sound at the
top and leaves the chorus around 0.2 to 0.3, which is where the spread is finishing
anyway — so the two halves of the bar each have something of their own to do.

**The gain law is pinned at both ends and loose in the middle, because the two ends
are exact for different reasons.** At the bottom of the travel the pair is on one
spot and barely detuned, so every channel hears both halves in step and a half each
is the single voice this was — which is what makes that end continuous with a note
that has no unison at all, rather than stepping 3dB the instant the bar leaves zero.
At the top each half is a signal of its own, so their powers add rather than their
amplitudes, and root two down is unity again. The crossing between the two runs over
the spread's travel because that is what it is a statement about: **how far apart the
pair is tuned is what decides whether it adds as one signal or as two.** What it
costs is a fraction of a decibel around the middle, in whichever direction the note's
own pitch and length have left the pair coherent — which is not a number the law can
know, and is why the ends are what it is pinned to.

**The pan reaches the end of its travel at every unison, and what gives way is the
width.** Each half is thrown out by the spread cut down by the room the pan has left
it, so a pair opened at the centre reaches the sides and the same pair on a note
already thrown right closes up as it travels and lands on the wall as one.

Reaching by the whole spread and clamping was the obvious arrangement and it was
wrong, in a way that only showed up when both controls were used at once. The outer
half stopped at the wall while the inner one went on travelling, so the pair narrowed
and its centre moved half as far as the number said: at full unison a hard panned
note came out 4.8dB to one side, where an unpanned note is *silent* on the other.
That makes pan a control that quietly means less the more unison is used, and leaves
two parameters fighting over the same wall. Proportional instead, and each keeps its
own meaning — pan says where, unison says how wide, and the second one spends
whatever the first one left. What it costs is width at the extremes, where a hard
panned pair is two copies on one spot; they are still detuned, so what a note loses
out there is its image and not its thickness. The clamp inside the pan law is now a
guard rather than the mechanism, since nothing reaches it.

What that travel must *not* do is reach into the gain law, and the first attempt at
it did exactly that. The argument was that a pair squeezed shut against a wall is two
halves on one spot again, so it wants a half each — and it is wrong for the reason
stated above: a pair sixty cents apart stopped agreeing with itself long before
anything panned it, and putting it back on one spot does not put it back in step.
Measured, that mistake took a hard panned note down a full 3dB. The reason the gain
can ignore the pan entirely is that the pan has no say in the level anyway — under
the equal power law the four gains of a pair square and sum to four wherever the two
are put. Position moves the sound, the spread decides what it weighs, and neither
reaches into the other. The self test sweeps both axes rather than the unison alone,
which is what would have caught this the first time.

Two things are deliberately shared and one deliberately is not. Both halves follow
the one pitch envelope, so a sweep lands with the interval still open instead of the
pair closing up as it arrives; both take the same ratio, index and envelopes, since
this is one note sounded twice and not two sounds. What they cannot share is the
feedback memory: the two modulators run at different frequencies, and one loop fed
from both would couple them into something that is neither. And neither half starts
phase-offset, which would decorrelate the onset at the price of hollowing it out —
a percussive patch is mostly onset.
