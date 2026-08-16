Audio pipeline
==============

How rendered audio reaches an output, what the clock under it is worth, and how
far ahead of it a note has to be scheduled. The code is `Assets/Jacquard/Audio`
— `FmSynthCore` and the two drivers around it. The Web driver's own platform
notes are in [impl-web.md].

[impl-web.md]: impl-web.md

Two drivers, one DSP
--------------------

**The output has two drivers, and the DSP has none.** `FmSynthCore` holds the voices,
the buses and the render job, and is asked only to fill so many frames starting at
so many samples. Everywhere the Scriptable Audio Pipeline exists, `FmSynthPipeline`
is what asks — on the audio thread, against the pipeline's own clock. The Web
platform does not have it, and does not have `OnAudioFilterRead` or a streaming
`PCMReaderCallback` either, because the browser mixes audio somewhere a WebAssembly
main thread cannot be called from. So `FmSynthWeb` pushes instead: it renders blocks
from `Update` and hands each to the Web Audio API through a `.jslib`, which plays
them back to back. What it renders against is the browser's own clock, read back as
how much of what was pushed is still unplayed, so the position cannot drift from what
is being heard and a late frame is an audible gap rather than a synth that has
quietly stopped agreeing with the sequencer.

Watching for dropouts
---------------------

**The pipeline driver watches the clock for a deadline nothing else reports.** The
audio thread has one buffer's worth of time to answer in — 5.3ms at the 256 frames
this project asks for — and a miss is silent. What happens then is that those samples
are never mixed at all: the device is handed whatever was already in front of it, and
the music has a hole in it with a hard edge at either end, which is heard as a bang
and has nothing to do with what was played. So `DspClock.WatchForDropouts` measures
it, once a frame from the driver's `Pump`. The DSP clock counts samples the
audio system has processed and the device consumes them on a crystal of its own, so a
stretch that was never mixed leaves the two permanently that much further apart; what
is watched for is the step, not the offset.

A single reading of it is useless — the clock stands still between one mix cycle and
the next, so it carries a whole buffer of quantisation against an event worth a
buffer — but the highest reading over half a second is not, since some frame does
land just after a cycle begins. Measured on a healthy stream that peak moved by at
most 0.44ms from one window to the next, against a threshold of half a buffer.

**What the warning tells you to do is lengthen the buffer, and that rests on a
measurement.** The mix costs 0.24ms of the 5.3ms typically and 1.29ms with all
twenty-four voices sounding, so a miss is the thread being taken away rather than the
synth asking for more than it may have — in the editor, by whatever is importing
assets or compiling Burst beside it. Scheduling the render as a job and waiting on it
in `EndProcessing` was tried as the culprit and was not; running it inline changed
nothing and was reverted. The threshold is read from the buffer size rather than
written down, so raising the buffer raises what it takes to trip the warning by the
same amount.

It lives in the driver rather than in the app because it is only true of this driver.
`AudioSettings.dspTime` is Unity's audio system, and on the Web the synth does not go
through Unity's audio system at all, so the same reading there would be about a mixer
nobody is listening to.

Two DSP clocks
--------------

**There are two DSP clocks under this driver, and an interruption parts them.** What
the main thread can read is `AudioSettings.dspTime`; what a note's `startSample` is
interpreted in is the `dspTime` the pipeline hands the render job, which reaches this
side only inside the status report. They agree at boot, so nothing said they were two
numbers until iOS proved it: thirteen seconds in the background left the render job's
clock 2.40s ahead, permanently, and every note the app then scheduled landed two and a
half seconds in the render job's past — triggered, past its own length, and released in
the buffer it started in. The app went silent and pressing Play again did nothing but a
moment of release tails, which is what the reverb had left.

So `DspClock.CurrentSample` is the raw reading plus however far the two have parted,
taken from the peak of a window of reports and moved only when it crosses a tenth of a
second. **What it is moved back to is the difference this machine shows when nothing is
wrong, not zero.** The two stood three or four buffers apart on the iPad in perfect
health, and that difference is margin — a note is stamped against the one and rendered
against the other. Correcting to zero spent it and took 11ms off the front of every
note, which on a patch with a pitch sweep is heard as the sweep starting part way down.
The first four windows of a launch are therefore kept as the baseline, and a correction
restores that rather than zero.

**The app stops the transport when it goes away.** `JacquardApp.OnApplicationPause`
does it on the pause callback only — iOS is the platform that stops; a desktop losing
focus is a window behind another window with the music still playing. Nothing about a
run survives the gap: no note is scheduled for however long it lasts, and the audio
system that comes back is not the one that left. What that costs is that Play starts
the piece from the top rather than from where the hand left it, and what it buys is
that nothing comes back with a fistful of notes whose moment went by while the screen
was off.

The minimum lead
----------------

**What a driver costs is one number the app has to respect, and only one of them knows
it without asking.** Audio that has been rendered cannot be written into, so
`FmSynth.MinimumLead` is how far past the clock the earliest schedulable note lies. The
lookahead window and the note preview both add it, which is the whole of the change
outside `Audio`. On the Web it is a block past what the driver keeps queued, which it
knows by construction: around 110ms, so a tapped note answers in about 160 and the
sequence is unaffected.

**Under the pipeline it was nought, and nought was wrong.** The reasoning was that the
pipeline renders when asked and so can always start a note in the very next buffer —
the buffer, yes; the note, no. A note goes main thread, control part, realtime part,
queue, and each of those hops is served once a mix cycle. A lead shorter than that does
not delay a note: `FmVoicePool.Render` starts it anyway, from wherever the buffer being
rendered has got to, which takes the front off it.

Nothing up here can work that out, so it is asked. `FmClockProbe` goes down the same
pipe a note takes and comes back stamped with the earliest sample the render job could
still fill; the worst of eight answers, plus a buffer, is `MinimumLead`. It measured one
buffer on a Mac and between one and three on the iPad — **a different figure after every
interruption**, which is why `FmSynth.Recalibrate` is called on the way back to the front
and whenever `FmSynthControl.Configure` reports a new format generation. The figure in
force stays in force while the new one is gathered, so a hand that presses Play during
the tenth of a second it takes gets the old number rather than a guess.

**Behind it, the render job reports how late the worst note actually was** — the one
fault the audio side can see and the main thread cannot, since whether a stamp arrived
in time is only known where the stamp is read. `FmVoicePool.late` is that, with a count
of notes started beside it so that a lateness of zero can be told from a stretch with
nothing in it. `DspClock` grows the lead by whatever stands across two windows running,
which is what catches anything the measurement missed; a single long frame is not that,
and is still paid for with the head of one note. Both figures are gathered on the
control side rather than overwritten, because reports arrive once a mix cycle and the
main thread reads once a frame — at fifteen frames a second, five in six were being
thrown away, which is to say all the evidence there was.
