Audio pipeline
==============

How rendered audio reaches an output, what the clock under it is worth, and how far ahead
of it a note has to be scheduled. The code is `Assets/Jacquard/Audio` — `FmSynthCore` and
the two drivers around it. `DspClock` carries the whole argument for the three measured
numbers; the Web driver's own platform notes are [impl-web.md].

[impl-web.md]: impl-web.md

Two drivers, one DSP
--------------------

**The output has two drivers, and the DSP has none.** `FmSynthCore` holds the voices, the
buses and the render job, and is asked only to fill so many frames starting at so many
samples. Everything platform-shaped lives in the driver around it:

| | |
| --- | --- |
| `FmSynthPipeline` | Pulls, on the audio thread, against the Scriptable Audio Pipeline's own clock |
| `FmSynthWeb` | Pushes, from `Update`, because the browser mixes audio somewhere a WebAssembly main thread cannot be called from |

`FmSynthWeb` renders against the browser's own clock — read back as how much of what was
pushed is still unplayed — so its position cannot drift from what is being heard, and a
late frame is an audible gap rather than a synth that has quietly stopped agreeing with
the sequencer.

**Nothing measured about the clock belongs above the driver.** `DspClock` is the pipeline
driver's alone: `AudioSettings.dspTime` is Unity's audio system, and on the Web the synth
does not go through it at all, so the same readings there would be about a mixer nobody is
listening to. The Web driver knows its own numbers by construction.

What iOS has to be told
-----------------------

**A device in silent mode plays nothing until the app has said that its audio is music**,
and nothing in Unity says it: the AVAudioSession category is not reachable from managed
code or from the player settings. `Assets/Plugins/iOS/JacquardAudioSession.mm` says it and
`FmSynthPipeline` is what calls it — one more thing that belongs to the driver rather than
above it. The plugin carries the whole argument: which category, which option, why it is
said after the engine's own audio init rather than before, and what was measured on the
device about saying it again.

What a phone hands out
----------------------

**Unity's output is 24000 Hz on iOS and Android unless it is asked for something else** —
read off the iPad, which boots at 24000 and then grants 48000 exactly, with no rounding and
no refusal, the first time it is asked for it. `DspOutputRate` carries the argument for
replacing it and what it costs; the ask itself is one line in `DspBuffer.Apply`, since the
rate rides along on the Reset the buffer already makes rather than paying for a second
reinitialization.

One thing follows that is not about the rate at all and is easy to get wrong. **A frame
count is a deadline only once a rate is known.** The 256 frames this project used to ship
were 10.7ms of the audio thread's time on the device, and doubling the rate under them
would have made it 5.3ms — a deadline this iPad does not hold, which the stream says
rather than the frame timing. So the buffer went to 512 and the device stayed on the
deadline it always had, the rate being paid for in DSP alone. `DspBuffer.Default` carries
those readings, and the one platform that is not handed the figure they arrived at: iOS
ships 1024, which is bought from the system screen recorder rather than from the audio
thread.

The three measured numbers
--------------------------

None of these can be reasoned out, and each one was wrong when it was. `DspClock` states
what each is and what it measured; what is here is what depends on them.

| | |
| --- | --- |
| **Where the clock is** | `DspClock.CurrentSample`. Two clocks count the same samples and an interruption parts them — measured at 2.40s on an iPad after thirteen seconds in the background |
| **How far ahead a note must be placed** | `FmSynth.MinimumLead`. The lookahead window and the note audition both add it, which is the whole of the correction outside `Audio` |
| **Whether the stream lost anything** | `DspClock.WatchForDropouts`. The one fault down there that leaves no trace up here |

Two things about the first that are easy to get backwards:

- **What the offset is corrected *to* is the difference this machine shows when nothing is
  wrong, not zero.** The two clocks stand three or four buffers apart on the iPad in
  perfect health, and that difference is margin — a note is stamped against one and
  rendered against the other. Correcting to zero spent it and took 11ms off the front of
  every note.
- **The lead changes after every interruption**, which is why `FmSynth.Recalibrate` is
  called on the way back to the front and whenever `FmSynthControl.Configure` reports a new
  format generation. The figure in force stays in force while a new one is gathered.

**A dropout means the buffer is too short, and that rests on a measurement**: the mix
costs 0.24ms of the 5.3ms typically and 1.29ms with all twenty-four voices sounding, so a
miss is the thread being taken away rather than the synth asking for more than it may
have. Where the setting is is [impl-panels.md]. Scheduling the render as a job was tried
as the culprit and was not it; running it inline changed nothing and was reverted.

[impl-panels.md]: impl-panels.md

**Behind the lead, the render job reports how late the worst note actually was** —
`FmVoicePool.late`, with a count of notes started beside it so a lateness of zero can be
told from a stretch with nothing in it. Both figures are gathered on the control side
rather than overwritten, because reports arrive once a mix cycle and the main thread reads
once a frame: at fifteen frames a second, five in six were being thrown away.

Going away and coming back
--------------------------

**The app stops the transport when it goes away.** `JacquardApp.OnApplicationPause` does
it on the pause callback only — iOS is the platform that stops, and a desktop losing focus
is a window behind another window with the music still playing.

Nothing about a run survives the gap: no note is scheduled for however long it lasts, and
the audio system that comes back is not the one that left. What it costs is that Play
starts from the top rather than from where the hand left it; what it buys is that nothing
comes back with a fistful of notes whose moment went by while the screen was off.
