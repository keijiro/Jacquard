Implementation
==============

How the prototype is put together, and the decisions behind it that the code
alone does not explain. What it is meant to do is in [prototype.md]; what it is
meant to be is in [sequencer.md]; what it is meant to look like is in
[mockup.html].

[prototype.md]: prototype.md
[sequencer.md]: sequencer.md
[mockup.html]: mockup.html

Layout
------

`Assets/Core` is an assembly with **no engine references at all** — the asmdef
sets `noEngineReferences`, so the separation prototype.md asks for is enforced by
the compiler rather than by discipline:

| | |
| --- | --- |
| `Model` | `Project`, `Score`, `Lane`, `Step`, the tile hierarchy, pitch names |
| `Serialization` | The text format, written and read by hand |
| `Sequencer` | `Runner`, the scheduler that turns tiles into note events, and the live effects that colour them on the way out |
| `Synth` | The two operator FM voice, the per channel patch bank, the lock targets, the send effect and limiter settings |

`Assets/Jacquard` is the part that cannot help but know about Unity:

| | |
| --- | --- |
| `Audio` | Voice pool, the three effect buses, the Burst render job, and the two drivers that carry it to an output |
| `App` | The MonoBehaviour, the editing operations, file access |
| `UI` | The score plane, cell icons drawn with Painter2D, the panels |
| `Visual` | The background visualizer: one mesh, one unlit shader, and nothing else drawn by the camera |

The FM synth, the Scriptable Audio Pipeline usage and the value bar every number
is set on come from [keijiro/unity-sap-test]; the two axis scrolling plane comes
from [keijiro/uitk-scrollarea].

[keijiro/unity-sap-test]: https://github.com/keijiro/unity-sap-test
[keijiro/uitk-scrollarea]: https://github.com/keijiro/uitk-scrollarea

Notes on the prototype
----------------------

- **Timing** rides the audio clock. Every step is handed to the synth with the
  exact sample it starts on, so a dropped frame delays the handover and never
  the note.
- **One instant is one downward pass.** Runners are one per `CHAN` lane, ordered
  by the vertical position of that tile, and the ones landing on the same instant
  are read in that order, each from the rail row of its step down. Everything a
  tile does reaches what is read after it and nothing before it: a gate ends the
  descent, a lock colours the notes that follow it, a note takes the channel as it
  stands where it sits. That one rule covers both the inside of a stack and the
  lanes against each other, which is what lets the accent lane, placed above the
  main one, colour it.
- **A lock is over when its instant is.** There is no accumulating lock and no
  standing channel state; every channel starts each instant from its patch again.
- **What a written note sounds as is decided twice, and neither pass is an edit.**
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
- **Every field of the patch is a lock target.** `FmPatch` and `ParamTargets` name
  the same fifteen parameters, so there is nothing a channel holds that a step cannot
  reach for one instant. One of them, the gate ratio, multiplies the length written
  on the note rather than being a length itself, which is why the note reads in
  steps and the channel in percent: the two are the same multiplication and only
  the unit tells them apart.

  **One of them the synth never sees.** The transpose moves the note the sequencer is
  about to make, so it is spent before an event exists and is the one patch field with
  nothing mirroring it in `FmNoteEvent` — an event already knows what it sounds, and a
  number saying how far it was carried to get there would be a second answer nobody
  reads. It is in the patch because it answers to a channel, the way the sends do, and
  in the target list because the list *is* the fields of the patch. What that buys is
  the reason it is worth having there rather than beside the tempo: the sequencer reads
  the working patch, the one this instant's locks have already coloured, so a `PABS` or
  a `PREL` on the transpose lifts the notes under it in that step and no others.
- **The parameters are named and ordered for a player, not for the synthesis.** What is
  on the panel is `FM ratio`, `FM amount`, `Feedback`, `FM decay` and then `Amp attack`
  / `Amp release`, where the code says `modulationIndex`, `carrierAttack` and so on. A
  modulation index is a term from a textbook about a technique, and a musician reaching
  for a brighter sound is not thinking about which operator is the carrier; the four FM
  rows are in the order one is dialled in — what the modulator is tuned to, how much of
  it arrives, how much of itself it hears, how fast it gets out of the way. Only the
  captions moved: `ParamTargets`' constants and the file's keys keep the older
  spellings, since renaming those would be a way to make older files unopenable for the
  sake of a word on screen.
- **The FM decay is a slope rather than a length of time**, which is the one parameter
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
- **One lock reaches as many of them as it likes.** A lock carries a slot per
  target and holds whichever ones have been set, so a step that changes four
  parameters is one tile rather than four stacked cells between the gate and the
  note. What it does not hold it leaves entirely to the channel, which is why a
  lock that holds nothing — a freshly placed one — is inert rather than wrong.
- **Timbre belongs to the channel**, not to the project: the bank holds one patch
  per channel and a `CHAN` tile's number picks the sound as well as the stream, so
  lanes sharing a channel share a patch and a branch lane borrows the one of
  whatever jumps into it. The sound group of the Tile panel is where that patch is
  edited, and an edit is heard from the next instant with nothing to undo.
- **A send is in the patch; what it feeds is in the project.** There is one reverb and
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
- **Every path is stereo, and each became so for its own reason.** The wet one first:
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
  still on. The soft clip at the end of the mix is what a dense chord already relies
  on.

  **The sends take the voice unpanned.** Each is a mono feed into an effect that
  builds an image of its own, so a tail that also leaned towards the side its note
  came from would be two answers to one question.
- **A unison pair is one voice and not two**, which is the decision the rest of the
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
- **The delay time is the one number in the project that is smoothed**, and the reason
  is what kind of quantity it is. The reverb's size and damping are coefficients, so
  moving one changes how what is already in the lines decays and there is no seam. A
  delay tap is a *position*: moved outright, the read pointer lands somewhere
  unrelated to where it was and the join is a click. So it is rate limited rather than
  set — a constant speed, which is a constant interval of pitch while it catches up
  and nothing once it arrives, the sound a tape delay makes when its head is moved. An
  exponential approach was rejected for starting the glide at whatever speed the jump
  happened to be wide. A pair of taps and a crossfade is the alternative if the glide
  is ever unwanted; it costs a second read per sample and cannot be played.
- **The effect settings are the only mutable state the audio thread reads.** Everything
  else reaches it stamped into a note, which is what `SendFxRuntime` and the
  `FmSynth.SetFx` message exist to work around — one reverb serving eight channels
  cannot ride on a note. `JacquardApp.Update` sends it whenever it differs from the
  last one sent, and since the delay time is converted to samples on the way, that one
  comparison covers a bar being dragged, the tempo changing and a file being loaded
  without any of them knowing that anything downstream cares.
- **The output has two drivers, and the DSP has none.** `FmSynthCore` holds the voices,
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
- **The pipeline driver watches the clock for a deadline nothing else reports.** The
  audio thread has one buffer's worth of time to answer in — 5.3ms at the 256 frames
  this project asks for — and a miss is silent. What happens then is that those samples
  are never mixed at all: the device is handed whatever was already in front of it, and
  the music has a hole in it with a hard edge at either end, which is heard as a bang
  and has nothing to do with what was played. So `FmSynthPipeline.Pump`, which under
  this driver had nothing else to do, measures it. The DSP clock counts samples the
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
- **The Web page is the project's own, because the canvas has to be the window.** Both
  built-in templates ship the canvas at the size Player Settings names and leave it
  there, which for a plane that is panned around means the work area is whatever was
  guessed at build time. `Assets/WebGLTemplates/Jacquard` fixes the canvas to all four
  edges instead — Unity matches the drawing buffer to it every frame, device pixel ratio
  included, so a resized window is simply more score. It also hands the canvas
  `touch-action: none`, without which the browser keeps the drag, the pinch and the
  double tap that the chrome and the plane are built around. It is also where saving
  works at all: the runtime mounts `persistentDataPath` on IndexedDB, but persists it
  automatically only when the page passes `autoSyncPersistentDataPath`, and nothing here
  calls the sync by hand — the app writes plain files and never touches `PlayerPrefs`,
  which is the one thing the engine syncs for itself. Without the flag a save reported
  success and was gone on the next reload, in the in-memory filesystem the whole time.
  It is still the browser's storage, so a cleared site is a cleared score; what the flag
  fixes is losing one without leaving the page. Post-processing is off on
  the renderer for the same platform: URP loads the FSR upscaling material whether or
  not the upscaler is selected, and that shader does not exist on GLES3, so the only
  way to stop the warning is to not have a post-process stack — and nothing here has
  ever used one.
- **What the push driver costs is one number the app has to respect.** Audio that has
  been rendered cannot be written into, so `FmSynth.MinimumLead` is how far past the
  clock the earliest schedulable note lies — zero under the pipeline, and a block past
  the queue on the Web. The lookahead window and the note preview both add it, which is
  the whole of the change outside `Audio`. It comes to around 110ms there, so a tapped
  note answers in about 160 and the sequence is unaffected.
- **A number is a bar, not a field.** The readout sits on a bar that fills as the
  value rises, dragging scrubs it and a double click types an exact one, so a
  parameter shows where it sits inside its useful range as well as what it is. What
  that range is comes from the synth itself (`ParamTargets`), which is what lets a
  lock's amount be read against what it moves; typing is deliberately not held to
  it. A lane's step count is the one number still stepped, since each one is a cell
  and growing can be refused.

  **A bar reports twice, and the second report is what sounds a note.** The setter
  runs at every value a scrub passes through, because the model has to be current —
  the sequencer may well be playing through the edit. `ValueBar.Bind`'s optional
  `settled` runs once the number has stopped moving instead: at the end of a drag, or
  immediately for anything that was never a drag, since a typed value arrives already
  decided. The audition of a sound row hangs off it, and so does the note a pitch bar
  plays. Sounding a note per event turned a drag down a bar into a burst of a hundred,
  none of which was the value being chosen.

  That is now the whole of the auditioning. There was an *Audition* button under the
  sound rows asking for the same note on demand, and it is gone: a bar that has just
  been moved has already played it, and one that has not is one nothing was asked
  about.

  **Travel is a ratio wherever the range spans decades**, and an exponent is the wrong
  shape for one. `Range.Curve` was the first answer, and on an envelope time it put
  eleven pixels of travel inside the first millisecond: `pow(p, 3)` has no slope at all
  at the bottom, so the number would not move, the sound would not move, and the bar
  read as dead until the hand was a tenth of the way along it. `Range.Floor` makes the
  travel geometric instead — a step multiplies where a curved one adds, so every pixel
  is the same ratio, about a twentieth of the value, from one end to the other. A
  millisecond is the floor because it is the shortest time this synth has any use for,
  and where a parameter's own low end is zero — a release or a pitch sweep switched off
  rather than made brief — the bottom pixel keeps it, since no number of ratios reaches
  zero from anywhere. The exponent stays for the ranges it does suit: the gate ratio,
  the modulator ratio and the feedback each cover a few octaves at most and their low
  ends are audible.

  **The readout follows the same rule**, because a fixed number of decimals can only
  match a geometric travel at one point along it. Integer milliseconds hid a run of
  pixels that had each moved the value by a twentieth, and past a second one pixel
  stepped the last digit by ninety. So a geometric bar prints three figures wherever the
  value stands — 1.05, 44.7, 299, 2000 — which move exactly when it does, and prints a
  bare 0 at the bottom, where the number is a setting rather than a quantity. Neither
  half of this touches a value or a file: a taper decides where a number sits on the
  travel, not what it is.
- **A lap of a cycle gate is a switch and not a number.** `CycleGateTile` held one
  index into its period, so the only thing it could say was *one lap out of n*: a gate
  that wanted the first and the third of four was two gates in two cells, and most
  patterns could not be written at all. The whole cycle is one word of bits, so
  carrying a switch per lap costs the tile nothing and costs the panel a block of
  unlabelled boxes where a second bar used to be. A gate with nothing switched on
  never fires, which is inert rather than wrong — the same standing a lock that holds
  nothing has.

  **The period reaches 32, and the bits above it are kept rather than cleared.** A
  period pulled in and let back out finds its switches where it left them, since
  nothing but a save reads past the period; a save writes the period's own laps and
  forgets the rest, which is what keeps the file a round trip. Version 8 names one lap
  by number where version 9 spells the whole pattern, and the two tell themselves
  apart without the version reaching the tile at all: the shortest period is two and
  the longest lap number is one digit, so a run of digits as long as the period can
  only be the pattern.

  **The switches are hidden and not rebuilt**, all thirty-two of them standing from
  the moment the panel is built. The period is set on a bar directly over them, and a
  run that tore itself down as that bar moved would take the drag that was moving it
  with it — the same hazard `InspectorPanel.Refresh` exists to avoid.

  **What the cell can show gives out before the tile does.** The boxes wrap at four to
  a line, because a cell is thirty pixels across whatever the period is and
  thirty-two in a row would be a box a pixel wide against a pixel of ground; past
  eight it stops counting and draws six and an ellipsis, since nobody reads twelve
  boxes off a cell and the exact laps are the panel's business. Six is also what
  leaves the ellipsis somewhere to stand: a second line of two leaves two boxes' worth
  of ground at the bottom right, so the dots take no width of their own and an elided
  icon is the same block as a full one.

  **What the boxes give up is width, and they give it up for the margin.** A row fitted
  to the cell left the block a pixel and a half from the tile's own outline, which read
  as an icon jammed against its frame; it is held five pixels clear on each side now,
  which puts a box at three pixels where it used to be five. That is the right way
  round for this icon, because the figure is a shape to recognise and not a count to
  take off the cell — the panel is where a lap is read one at a time. Filled against
  hollow survives the narrowing, which is the one thing that had to.

  The panel grows by about a hundred points at the longest period, and it can afford
  to: eight switches to a line is a bar of sixteenths and puts thirty-two laps in four
  lines, and a gate cell is not a `CHAN` cell, so these switches never stand on the same
  panel as the fifteen rows of a channel's sound.
- **One panel shows what the cursor is on**, and nothing is toggled. The Tile panel
  keeps the corner and follows the cursor, and everything the cell decides is on it as a
  group of its own: the tile's own rows, the lane a head carries, the sound of the
  channel a `CHAN` names, the parameters a `PABS` or `PREL` takes hold of. There is no
  window to open, and so no state on screen that the score does not decide.

  The sound and the lock were panels of their own, stacked under this one and sharing a
  slot because no cell is both. Each could only ever be up while this panel was showing
  one particular kind of tile — which is a group of this panel wearing a frame, paying a
  header, an inset and a panel gap to repeat what the cursor had already said. They are
  still the same list of parameters read two ways, what a channel sounds like and what
  one step does to it, laid out alike so that one can be read against the other; what
  changed is that reading one no longer means reading two headers.

  **A panel's header is its subject, not its name.** It reads *Note Tile*, *Cycle Gate
  Tile*, *Channel Start Tile* — the kind of panel and the thing it is showing on one
  line, since which panel this is was never in doubt and the thing changes under the
  cursor. It used to say the kind in the header and repeat the subject in a caption row
  underneath, which cost a row to say half of what one line says now. `Controls.Panel`
  hands the header label back for this, and the panels that never change subject — Send
  FX, Global, Channels, Live FX — simply do not ask for it.

  A group inside a panel is named by the same rule. *Sound* needs no number, because the
  Channel row is two lines above it; a lock's group is headed *Channel 5*, because a
  lock is the one thing here that cannot say which channel it colours — the tile holds
  no number and a branch lane borrows one from the jump that reaches it.

  **A panel draws no outline and cuts no corners.** It used to do both, in the grey its
  own buttons are outlined in and to the same kind of radius they are cut to, which in
  the only vocabulary this chrome has says that a panel is a control with smaller ones
  inside it. What tells a panel from the plane is that it is a lighter ground with air
  around it, which is enough on a screen where nothing else is a filled rectangle that
  size. A corner radius now means one thing: a cell or a control, something a hand picks
  up.

  **A panel is spaced out of three numbers.** `Controls.Gap` is the space between any
  two things standing next to each other — two rows, two buttons, a heading and what it
  heads; `Controls.Inset` is the panel's own margin from its edge to everything it
  holds; and `Controls.GroupGap`, twice a gap, is what parts one group of rows from the
  next. Nothing in a panel is a number of its own; what is not one of the three is a
  stated subtraction from one.

  The subtraction is always the same one. **A gap is carried below and to the right, by
  the thing above and to the left of it**, so anything wanting more than a gap adds only
  what is missing — a heading that follows anything carries the difference over it — and
  the panel's bottom inset is short by a gap because the last row laid one down.

  **The rule belongs to the heading, and it is the only rule left inside a panel.** A
  line standing between two groups is owned by neither: it says that something ends here
  and something begins, which leaves the first row of a group looking as much like the
  end of the one above as the start of its own, so every group opened against a line and
  closed against nothing. Under the name it belongs to the name, and the group it heads
  runs from that line down to the next patch of air. A panel's own header carries no
  mark at all now — it is the one line on a panel in the bright text every caption below
  it is not — and a button at the foot of one, the *Delete* the Tile panel ends on, is
  parted from the rows above it by the same air rather than by a rule that would be
  heading nothing.

  The last piece is that **a heading is as tall as a row**, header included. A control
  is a twenty pixel box holding thirteen pixels of text, so a bare line of text between
  two of them is short by the air the boxes carry — every gap measures right and the
  words still crowd. Given the row height, the panels measure the same read either way.
  The rule under a heading runs the width of the panel and not the width of the caption
  column, which is the one thing a heading does not take from the captions it is set
  in: a caption is that wide so that a column of them lines up with the controls beside
  them, and a heading has no control beside it.

  **The send effects are the one exception, and they are the exception because they
  have to be.** One reverb and one delay for the whole project answer to no cell, so
  there is no cursor position that could bring them up; putting a tile on the plane for
  the sake of the rule would be inventing score to hold a setting. They pay for the
  state they add by not being up unless asked for — a button on the transport row,
  which is where what belongs to the project already lives — and that button is the
  whole of the switch. One of them wore a close of its own for a while, which was a
  control the other panels had no use for and a second way to do what the button on the
  row already did. `Controls.Panel` no longer offers one at all: a panel is put away by
  whatever put it up, and the header is a title and nothing else. They hang from the top
  right in a column of their own, on the inside of the cursor's: a panel that does not
  follow the cursor cannot queue up behind panels that do, and beside is where the two
  are read together — how much of a channel goes to the reverb is a row of that
  channel's sound group, and this is what it goes to. They held the opposite corner
  until the channels wanted it, which is the one place a column is never covered by the
  cursor's.

  **One panel with a heading over each effect, and not a panel each.** It was a panel
  each, on the argument that a panel is already the thing that says *this group of rows
  is about that*, so a heading inside one was a second answer to a question the panel had
  answered. What that left out is what the second panel costs to say it: a header, a
  rule, an inset above and below and the gap to the panel under it, paid over again for
  something raised by one switch and set in one sitting. Two headings come to about what
  that frame did — four units under it on the mouse profile and about as far over it on
  the touch one, since a heading is a row and both a row and a frame grow with the
  pointer — so the height was never what the split was buying. What the merge buys is
  that the column reads as the one thing the Send FX button raises rather than as two
  boxes that always arrived together and always left together.

  The button that raises them says **Send FX** and not Send. A send is what a *channel*
  does, and the amounts are rows of the sound group named after the effect each one
  feeds; a
  button called Send would be named after the sending and raise none of it. What comes
  up is the receiving end — which is why the button names the pair and each panel names
  an effect.
- **The limiter is not there to stop the mix clipping**; a soft clip was already doing
  that, and doing it without a control. It is there for the thing a limiter is actually
  reached for on a drum machine — squeezing the mix hard enough that the loud parts hold
  still and the quiet ones come up behind them. So there is one control, the
  **threshold**, and it says how far the mix is squeezed rather than where the output
  lands: **the make-up gain is derived from it and gives back exactly what it took off**,
  so pulling the bar down makes the thing louder and harder together. There is no ratio
  either — it is infinite, which is what makes this a limiter rather than something to be
  dialled in.

  The label is Threshold and the field is `ceiling`, which is the one deliberate
  disagreement of that kind here: with the make-up automatic the output always lands at
  full scale, so what the hand is choosing is where limiting begins, while down in the
  bus it is still the level the gain holds the mix under. Renaming the field would be a
  format bump for a word.

  **The bar reaches 48dB down**, which is a make-up of 251 and most of its travel spent
  somewhere no limiter is meant to be taken. That is the point: past a certain depth
  everything in the mix is above the threshold, the gain stops articulating anything and
  what is heard is the soft clip on the whole mix. It is an instrument, so the far end of
  a bar is a sound rather than a warning.

  **It used to be a pair, and the make-up is what collapsed them.** A drive pushed the
  mix up into a ceiling that held the output down where it was put, which is the same
  knob read from opposite ends: every setting worth having had one of them parked while
  the other did the work, a ceiling below the drive was the two of them fighting with the
  output quieter for it, and the only thing the drive was really for was getting the level
  back. Deriving that instead of offering it removes a bar, removes the way of setting the
  two against each other, and leaves the one number a hand reaches for. What it costs is
  that the output is no longer somewhere a project can put it — full scale is where every
  mix now lands, and the soft clip is what stands behind that.

  The make-up is applied **after** the moving gain rather than before it, which is not a
  detail: the detector has to go on reading the mix as it arrives, or the ceiling would be
  measured against a signal that has already been given back what the ceiling took off and
  nothing would ever settle. In that order the two multiply out to something simple —
  under the ceiling the mix is lifted by the make-up and nothing else, at the peaks the
  output lands on full scale however far down the ceiling is.

  **The attack is a hole in the limiting for as long as it lasts, and that hole is the
  punch.** The gain carries the attack and the release rather than a follower ahead of
  it: a detector smoothed on the way in reaches the ceiling late and then holds the whole
  note down, where a gain smoothed on the way out is wide open when the transient arrives
  and takes exactly as long as the attack says to arrive at where it should have been. A
  slow attack is therefore a kick with its front intact and everything under it ducking;
  a release short enough to recover inside a step is a tail that swells.

  The peak feeding it is **held rather than followed**, which is the one thing here that
  had to be found by measuring. Read sample by sample the loudness of a tone goes to
  nothing twice a cycle, so the gain climbed back between the peaks and met each one too
  high — at 220Hz a cycle is 4.5ms against an attack of 5, and the output sat a fifth
  over the ceiling however long it was given to settle. Holding the peak and letting it
  go at the release leaves the gain a constant to converge on, so the only thing over the
  ceiling is what the attack deliberately let past.

  What is over the ceiling is what the **soft clip is now for**. It used to be the whole
  of the output stage; it is now the limiter's backstop, rounding off the few samples a
  slow attack lets through — which is what makes a lookahead unnecessary, and a lookahead
  is the one thing here that would have cost latency.

  One limiter, across the sum of everything. Per channel limiting and a side chain are
  both a working answer to a real problem and both are more machinery than this asks for:
  what is wanted is a switch that makes the thing louder and harder, not a mixing desk.
  It sits on `Project` beside the tempo on a stronger reading of the rule that put the
  send effects there — a send is at least a thing a note can be given more or less of,
  and there is no per note share of a limiter to put in a patch.

  One of its three numbers is in **decibels, which nothing else in this project is.** A
  ceiling is a ratio of amplitude and the useful span of one is a few doublings, so a
  linear bar spends most of its travel on the first of them and every number on it reads
  as a multiplier nobody thinks in. A dB is already a logarithm, so the bar over one is
  straight and a pixel is worth the same amount wherever it is taken. The conversion to a
  gain, to the make-up that answers it, and of the two times to one pole coefficients all
  happen once on the way to the audio thread.

  An older file is **converted rather than having its drive skipped**, which is what every
  other retired key here gets: the two numbers together said what one of them says now, so
  `ProjectFormat` folds a drive of d dB into a ceiling c dB down as a ceiling of c − d and
  version 13 is what makes that possible. The shape survives exactly; the level does not,
  and cannot — the old pair left the output down at the ceiling and the make-up is
  precisely the decision to stop doing that, so a converted project comes back |c| dB
  louder.

  **The panel is Global rather than Limiter**, which was a name for what would be on it
  rather than for what was on it — and the scale is the first thing to arrive and prove
  it. A Limiter panel would have been the right name for exactly as long as the limiter
  was the only setting of its kind, and a panel per setting is a row of switches on the
  transport for what is really one question — *what is set for the whole thing?* So the
  panel answers that and each group inside it is headed. It was the one panel built
  that way, against an argument that a panel already says what a heading would; the send
  effects and the cursor's panel are grouped the same way now, and what is left of the
  distinction is that the groups here have nothing in common but being global. The
  scale
  stands above the limiter in the order a note meets them: what it is allowed to be,
  and then what the sum of everything is held under.

  **The scale is a keyboard because a run of twelve boxes is not one.** Seven switches
  across the bottom and five in the gaps above them, with the two gaps a keyboard does
  not have — E to F and B to C — left empty. That is the whole of the shape and
  deliberately so: what it has to do is let a hand find a semitone without counting, and
  the two missing blacks do all of it. Narrower blacks, an overlap, a drawn key would be
  a picture of a keyboard, and these are switches — a press allows a note rather than
  playing one. They carry no captions for the reason a lap switch carries none: position
  is what a switch in a run means, and here the position is a pitch. The size comes from
  `Controls.SwitchSize`, since it is a metric of the profile in force and the blacks have
  to be placed against it rather than laid out by a row.

  It comes up **in the middle of the screen**. The columns are all read against the plane
  and the dock is played over it, but a limiter is set while listening to the whole mix
  with the eye nowhere in particular — and the middle is the one position on this screen
  that says a panel is not part of the arrangement around the plane, which is what a
  setting nobody visits twice a session should say. It covers the score while it is up,
  and the switch that raised it is the way back.
- **Everything the transport row switches starts off.** Send FX, Live FX, Global, Channels
  and the visualizer are five things a cell cannot ask for and so five switches, and none
  of them is up until it is asked for: the plane is what the screen is for, and a switch
  that starts on is a decision nobody made. The visualizer is the odd one, since what it
  raises is not a panel — the switch moves the component's own `enabled` flag, which is
  where a MonoBehaviour's on and off already live, so a visualizer nobody asked for costs
  a frame nothing at all. It is also why that component wakes in `Awake` rather than
  `Start`: `Start` never runs on something that ships disabled.

  **The status line went with them.** It was a paragraph of diagnostics written across the
  widest part of the row — the cursor position, the voice count, each runner's step and
  lap — and it was read by nobody while the row grew five switches that have to be
  reachable on a tablet. What is left of it is the one thing that was not a running count:
  whatever the file controls have to say, now logged to the console once each time it
  changes, because a save that failed has to say so somewhere.
- **The visualizer draws the synth, not the sequence.** What the sequence is doing is
  already on the plane — the playheads say which step each runner is on — and that is a
  different question from what came out of it. A gate that did not fire, a note that lost
  its voice to a louder one, a limiter closing on a kick: none of it is visible on the
  plane and all of it is visible in a trace of the output and a row of the voice pool.

  It is **drawn rather than laid out**, which is why it is the one thing on screen that is
  not UI Toolkit. A trace is a few hundred columns rebuilt every frame; a panel would want
  an element or a `Painter2D` call per column, inside a layout with nothing to lay out. A
  mesh handed to `Graphics.RenderMesh` under a URP unlit shader is the shape this actually
  is — one draw call, vertex colours, and everything decided in C# because all of it is a
  reading of the mix rather than a shape a shader could interpolate.

  That reading arrives through `FmSynthScope`, which is the **opposite direction to the
  status pipe**: a message is the wrong shape for a waveform, so the driver allocates a
  ring and a level per voice slot on the main thread and hands them to the audio side,
  which writes them as it finishes a buffer. The two ends are not synchronised and
  deliberately not — what is at stake in the race is one column of a scope, on a frame
  nobody will see again, and paying a lock per buffer for that would be paying for the
  audio thread to wait on the drawing. The safety system is told so by hand, with
  `NativeDisableContainerSafetyRestriction`.

  **The trace is triggered**, like an oscilloscope's. Hung off the write cursor it slid
  sideways by whatever the buffer size happened to be each frame, so a held note came out
  as a smear travelling across the screen; anchored to the last rising zero crossing
  before that point, the same note stands still and what moves is only what changed. A
  dozen lines, and it is the difference between a background you can ignore and one that
  pulls the eye every frame.

  **The colours were measured, not picked.** The blend happens in linear light, where the
  background is 0.009 and the line colour is 0.81, so an alpha that looks far too small is
  the right one: the faintest thing the plane draws is its lattice, at a luminance of 80
  in a screenshot, and 0.10 lands the trace at 86 with the slots under it at 73. The 0.16
  it started at read 102 — brighter than the score's own guides, which is a background
  arguing with what is in front of it.

  What this cost elsewhere is one line each in two places: the panel root no longer paints
  the background, since the camera clears to exactly that colour and the visualizer draws
  over it, and the transport row now paints its own — a row of controls with a waveform
  running behind them is a row that has to be read through something. The camera's culling
  mask went from nothing to the default layer, which is the first time it has had anything
  to draw.
- **The live effects are the one thing that colours a note without being written
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

- **What the live effects cost is the margin against a slow frame**, and it is the one
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

- **A roll records what sounded rather than re-running the step it came from.** Holding
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

- **A mute is not an edit**, which is what makes the Channels panel worth having next to
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
- **The Live FX panel is the one that is played rather than read**, so it is the one in
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

- **The panel is also where a tile is put down**, since the cursor is already the
  answer to where. A cell that will take one — a lane's empty step, the cell under
  a stack, the `TERM` cell that grows the lane — offers the tiles instead of a
  description of nothing, and bare ground offers a lane to put one on. So there is
  no palette to keep in step with what the cursor can accept, no button that
  silently does nothing where it stands, and one less row of chrome above the
  plane. A tile therefore only ever lands on free ground: a stack is built from the
  top down rather than by inserting above what is already there, which is the order
  the runner reads it in anyway.
- **The chrome never shows a token.** `PABS` and `GCYC` are how a tile is spelled in
  a saved file and how this codebase names one; on screen a tile is *Absolute Lock*
  and *Cycle Gate*, on the button that places it and in the header over it afterwards,
  so the two read as the same thing. The palette used to be a row of the
  four letter codes, which was three buttons to a line and a code to be learned before
  any of them meant anything — and there is nowhere else to learn it, since this panel
  is the only place a tile is ever chosen. What the panel hands the editor is a
  `TileKind` for the same reason: a token passed between them is a token waiting to be
  printed. The one token still shown is a note's, because `A4` is the pitch itself
  rather than a code standing in for one.
- **A tile is moved by carrying it**, which is the one edit with no button behind
  it: where a tile goes is a position, and a plane is already the thing that
  answers positions. Dragging a tile within its own step reorders the stack, one
  tile at a time; dragging it to any other step takes the run of tiles hanging
  below it along, because what a gate or a lock governs is exactly what hangs under
  it and a sub-stack left behind would fall under whatever the move left above it.
  A drop lands wherever a placed tile could — a step, the cell under a stack, the
  `TERM` cell that grows the lane — with the one difference that it may land on an
  occupied cell and open the stack up, which is what reordering one is. Dragging a
  `CHAN` or `JDST` cell carries the whole lane, and is what replaced the nudge
  buttons the Tile panel used to carry: a lane further down runs later, so moving
  one is a thing to watch happen against the lanes it will now overwrite rather
  than to arrive at a cell at a time.
- **A double click copies a stack, where it used to write a note.** The gesture asks
  the same question a drag does — this cell, then that one — about a shape that stays
  where it is: on a tile it takes that tile and everything under it, and on ground that
  would take a tile it puts the last copy down. What it replaced was a second way of
  doing what the `NOTE` button does, on the cell the button was already offered on;
  what it does now is the edit that had no way of being made at all. A chord, or a gate
  with what it governs, could be carried to another step or built again a cell at a
  time, and nothing could make a second one.

  **The flow tiles have no copy, which is a fact about them rather than a rule over
  them.** A `CHAN` names a lane, a `JUMP` *is* the identity its branch lane answers to
  (`Lane.JumpSource` holds the tile itself), and a `TERM` is implied one column past
  the last step and never stored. None of them means anything a cell away from where
  it stands, so `Tile.Copy` returns nothing for them and that is the whole of what
  keeps them out: a jump in the middle of a stack is stepped over rather than ending
  the walk — it is not the bottom of the stack, and what hangs under it is still under
  everything above it — and a double click on one does nothing.

  A copy is written out by hand rather than round-tripped through `ProjectFormat`,
  which has a text for every tile already. That text is a file's spelling: reading it
  back is private, throws on anything it cannot parse, wants the file version to go
  with it, and forgets a cycle gate's laps above its period. Copying is also done
  twice, once into the clipboard and once out of it, so that editing the tiles a copy
  came from leaves it alone and two pastes are two stacks rather than one written in
  two places.

  **A paste is refused whole or not at all.** `Score.PlaceStack` looks at the ground
  the whole run needs before it writes a single tile, since `Score.Place` in a loop
  would leave half a stack growing out of a step when the cell after next turns out to
  be somebody else's. It lands only where a placed tile could, which is where the Tile
  panel offers one, and only at the bottom of a stack: `Place` also takes a depth that
  is already filled and overwrites it, which for one tile is a tile changing its mind
  and for a run would be the rest of the stack disappearing under it.

  **With nothing copied yet, the gesture does nothing** rather than falling back on
  the note it used to write. One gesture reading two ways depending on what was done
  ten minutes ago is a gesture nobody can aim, and the fallback would be invisible
  exactly when it fired — on the empty cell, which is where a paste is aimed too. The
  cells a copy came from light up for a fifth of a second instead, drawn by the same
  overlay as the drop cells and saying the same kind of thing: these cells and not
  those. What was stepped over does not light, so what a copy left behind is visible
  without a word for it.

  **The plane counts the clicks itself, because the event's own count goes by the
  clock alone.** A press on one cell, one on its neighbour and one back on the first
  arrives as a click count of three, and taking that at its word fired the gesture on
  a cell nobody had pressed twice running — which for a copy is a wrong cell copied
  and for a lane is a part stopping mid-piece. What the gesture means is *this cell
  and then this cell*, so `ScoreView` keeps the last cell pressed beside the time and
  asks for both. The cell is the half that matters: the copy this usually stands for
  is a question about a position and never about a rhythm. The interval is forgotten
  once it has been spent, so a third press starts a new pair rather than making a
  second double out of the same click.

  `ValueBar` already hand-rolled this for the same shape of reason — there a press
  that scrubbed must not count as the first of two — so the length of the interval
  lives on `Controls` and both read it. Two gestures on one screen disagreeing about
  how quick a double click is would be a hand that could learn neither.
- **A drag means whatever the cell under it holds.** A tile or a lane head has
  something to carry, so a drag there carries it; free ground has nothing to carry,
  so a drag there moves the plane instead. Panning used to ask for a wheel event or
  a drag with command held, and a touch screen offers neither, which left the plane
  fixed on the iPad — most of what a score plane is for. The modifier was never the
  point, only a way of telling a press that means *move this* from one that means
  *edit this*, and the cell answers that by itself. So `ScoreView` stops only the
  presses it takes and `ScrollArea` pans whatever reaches it, which is to say
  whatever nobody claimed; neither has to know what the other is for. Four pixels of
  travel separate a pan from a tap, since a fingertip does not hold still, and a
  click on bare ground still moves the cursor as it always did.
- **The chrome scrolls the way the plane does, and takes its presses the other way
  round.** A transport row is as wide as the words on its switches and a column of
  panels is as long as whatever the cursor is standing on, so both run past a small
  screen — and a switch off the edge cannot be pressed, a parameter under the bottom of
  the screen cannot be set. `ScrollStrip` moves either of them under a drag along its
  one axis, with the content translated rather than laid out again, which is the trick
  `ScrollArea` uses on the plane.

  What it cannot do is pan whatever press nobody claimed. There is no free ground on a
  strip of chrome: three pixels between two switches, three between two rows. So the
  press goes to the control it landed on as before, and is *taken away* from it once it
  has travelled four pixels along the strip — the capture moves to the strip, which is
  also what cancels the click that press was going to be. That cannot be left to the
  release landing outside the button, the way a list usually decides, because the button
  travels with the strip and stays under the pointer for the whole pan. A strip with
  nowhere to go takes nothing from anything, so on a screen that fits, every control
  behaves as it did before the strip existed.

  Two things are the whole of the difficulty. **A held pointer is delivered to the
  holder and to nothing else** — a `TrickleDown` handler on an ancestor sees nothing
  from the moment a `Clickable` takes the press — so the strip registers a second copy
  of its handlers on whatever the press landed on, for the length of that press, and a
  test on who holds the pointer decides which of the two copies answers a move. And **a
  control whose own gesture is a drag keeps it**: `ValueBar` is scrubbed with both axes,
  so a bar handed the strip's rule would be a bar that cannot be set on exactly the
  screens where the rule applies.
- **A lane owns its whole row, written on or not.** What a lane occupies is the run
  it plays through — the rail from the head to the terminator, and whatever hangs
  under it — rather than the tiles that happen to be written on it so far. An empty
  step is where a lane is *going*, not ground going spare, so `Lane.Owns` answers
  for the rail whether a tile sits on it or not, and `Score.IsFree` is one call to
  that per lane rather than a walk over every cell.

  Occupancy used to be read off the tiles, which let a stack grow down across a
  rail that is plainly drawn on the screen, and let a lane be carried onto one.
  Whichever lane came second in the list then lost those cells entirely, since
  `Score.At` hands a contested cell to the first lane that claims it. Nothing about
  that was specific to dragging — placing a tile had always allowed it, one cell at
  a time — so the fix is in what a lane *is* and every caller simply gets the
  stricter answer it already wanted.
- **Ground another lane owns refuses a lane**, and a lane with nowhere for its
  terminator to move into cannot grow. The nudge buttons never checked the first,
  and the `TERM` cell never checked the second, so a lane could be grown onto its
  neighbour by putting a tile down while the Steps control beside it refused the
  same growth. Both now ask `Score.HasRoomToGrow`. A drop that cannot happen has
  nowhere lit up for it, which says so without a second colour.
- **The plane grows on all four sides, and the score is what moves.** It keeps ten
  columns and eight rows of empty ground past the score, which for the right and below
  falls out of the plane's own size and for the left and above cannot: the plane starts
  at cell (0,0) and a coordinate before it is a coordinate no lane can hold. So
  `ScoreView.Reframe` carries the score further in instead — `Score.Translate` over
  every lane, since a lane is the only thing that holds a position at all. A tile knows
  nothing about where it is, a jump reaches its branch lane by reference, and a runner
  carries a lane and a step index, so this is safe to do while the sequence plays;
  everything positional that is read of a score is read relatively, both the order
  `ChannelLanes` gives and the `MasterLane` that falls out of it, and an ordering does
  not notice a translation.

  The alternative was negative coordinates and an origin on the view, which keeps a
  lane's saved position as an identity but has to be threaded through every pixel and
  every floor in the model. Neither avoids the hard part, which is that moving the score
  moves everything drawn: the scroll offset has to take up exactly the same distance in
  the same breath, and the plane it is being clamped against has not been laid out yet.
  That is what `ScrollArea` holding a requested offset apart from the one in force is
  for, and it is also why `Reveal` reads the requested one — a cursor moved by an edit is
  a cursor moved in the same frame the plane grew.

  A score *arriving* is the one case where none of that applies, and it has to be told
  apart from a score moving. How far a score coming in off a file has to travel to reach
  the corner is a fact about the file and nothing to do with what is on the screen, so
  taking it up would carry the plane off by an arbitrary distance — far enough, measured
  at 8 columns and 6 rows on one of the saved scores here, to leave the incoming score
  off the edge of the viewport. So `ScoreView.Score` notes that it was handed a different
  score and the next reframe normalises it and stops there: the cursor stays on the cell
  it was on and the viewport does not move. What holds two scores together is that both
  end up at the same corner, so the one coming in appears exactly where the one going out
  was — which at the turn of a piece is the whole point, and the reason the seam needs no
  scrolling of its own. The startup score is the one that is framed, by `ShowScore`, and
  that is also where the cursor is put on the score rather than left in the margin: it
  used to arrive on the first lane's head by standing still at cell (1,1), and a score
  that begins further in has to be asked for.

  The rule is one sided: at *least* ten columns, so surplus margin is left where it is.
  Dragging a lane back to the right or deleting the leftmost one would otherwise haul
  the whole score after it and rewrite every coordinate in the file for nothing. The one
  thing that had to move with it is where a branch lane goes, which used to be floored
  against the plane's edge and would have landed in the margin — a jump is not a request
  to widen the plane.
- **The cell pitch is what the rest of the plane is derived from.** A cell is
  30x32 with a 4px gutter, set by what has to fit inside one rather than by taste:
  a sharp note name is a little over twenty pixels wide, and
  the icons are drawn in a 15x15 box. Keeping those numbers in `Style` alone is
  what lets the painted layers and the tile elements agree on where a cell is to
  the pixel.

  **The accidental gutter stands only on the notes that have an accidental.** It used
  to stand on every one of them, so that the letter kept its place as a note was
  transposed through a sharp and back — which put a gap in the middle of every plain
  name, five pixels of the twenty a name has to fit in, to spare a movement nobody was
  watching for. A name is read; it is not aligned against the name it was a moment ago.
  What remains of the rule is that the gutter is a fixed five pixels rather than a
  share of the type size, because what it holds is four 1px strokes and a scaled gutter
  would put them on half pixels at most sizes.

  The note is set at 13 rather than 15 for the same reason the gutter went: a name that
  fills its cell to the border reads as a cell crammed with a name. Nothing else moved
  with it — the length under a name and the `CHAN` head were already smaller — so the
  note is still plainly the content of the cell and the rest is still plainly labels.
- **Chain lines** are drawn only between cells of the same stack. mockup.html joins
  whatever happens to sit directly above, which makes two unrelated lanes look
  connected; sequencer.md lists that as undecided, and knowing the lane settles
  it.
- **A score comes in at the turn of the piece, and the seam is a sample.** A load while
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
- **The plane is held still while a score waits, and the screen follows the sound.**
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
- **A lane starts on the turn of the piece, which is the second thing to want that
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
- **The score the app opens on is a file, not code.** `Project.CreateSample()` built one
  by hand, which was right while the demonstration case was small and wrong the moment
  it became a real piece of work: what is wanted now is eight patches and seven lanes,
  and transcribing that into C# literals would be two hundred lines of numbers that no
  one can read and that have to be re-transcribed every time the piece changes. So
  `Assets/Jacquard/Scores/Startup.jacquard.txt` holds it in the format the app already
  writes, `JacquardApp.StartupScore` reads it through the same `ProjectFormat.Read` a
  load uses, and replacing it is a file copy. A double extension because Unity imports
  a `TextAsset` by extension and `.jacquard` is not one it knows.
  `CreateSample()` stays, with only its other job: it is the self test's fixture, the
  one score that names every kind of tile, and a fixture is better as code than as an
  asset that can be edited out from under a check. What the split buys is that neither
  is answering to the other's requirements — the demonstration can become whatever
  sounds best without a test noticing.
  The cost is that nothing about the startup file is checked by compiling, and the way
  it goes wrong is quiet: the reader takes an older version, so a startup score left
  behind by a format bump loses whatever the bump added, silently and on every launch.
  Hence one self test that reads it and writes it back — the same round trip as above,
  used to say *this file is already what this build writes* rather than *the format is
  consistent*.
- **An old file loses what the synth no longer has, rather than being refused.** A
  patch key nothing answers to is skipped, so a deleted parameter simply falls back to
  the default; a *lock* on one has to be named in `ProjectFormat.Retired` to get the
  same treatment, because an unknown lock target is otherwise an error — a typo in a
  hand-edited score should not pass silently. Which makes the list a standing
  obligation: **a target leaving `ParamTargets` belongs in `Retired` in the same
  change.** It was not, twice. Version 2 dropped the carrier's decay and sustain
  without recording either, so for four versions a file holding a lock on one could
  not be opened at all — one of the saved scores in `persistentDataPath` was in
  exactly that state until 2026-08-09. Only `detune`, dropped by version 5, was
  entered at the time.
- **`MathF` is not used in the DSP.** Burst cannot resolve the externs behind
  it, and a job that calls `MathF.Sin` silently drops to managed execution on
  the audio thread, so `FastMath` spells out the sine and the exponential.

  Two things measured with a `[BurstDiscard]` probe on 2026-08-12, both worth knowing
  before believing a comment about this. **The fallout is per assembly, not per job**: the
  error is `Unable to find internal function System.MathF::Tanh` raised while the compiled
  library initialises, so one MathF call in one job takes unrelated jobs beside it down to
  managed execution too — a bare job in the same class reported managed until the offending
  one was deleted. And **`Unity.Mathematics` is not affected**: `math.sin` and `math.tanh`
  both stay compiled, at 0.7 to 0.8ns a call against 8ns managed. `FastMath` exists because
  `Jacquard.Core` is built with `noEngineReferences` and `Unity.Mathematics` is not, not
  because `math` is a problem for Burst — and it holds its own there anyway, timing the
  same as `math.sin` to within the noise.
- **The chrome has two metric profiles rather than a UI scale**, and what separates
  them is not the screen but the pointer: a mouse lands on whatever it is over, and a
  fingertip covers about nine millimetres of glass whatever is under it. `Controls`
  holds a `Touch` flag settled once by `LayOutFor` before the first element is built —
  every metric is read at construction — and `JacquardApp.Pointer` is `Auto`, which
  asks `UnityEngine.Device.Application` so a simulated device is believed, with
  `Mouse` and `Touch` overrides because the layout cannot be judged on the Mac it is
  written on without forcing it. Row height goes 20 to 30, type 11 to 13, the caption
  column 74 to 88 and a panel 192 to 248; `Controls.Width` stretches any other width
  by the type ratio with a floor of the row height, so **no call site ever passes a
  profile-aware number**.

  Two things deliberately do not move. `Style`'s cell pitch is untouched, because the
  score already read right on the iPad and only the chrome did not. And paddings,
  margins and dividers stay at their mouse values: the growth is spent on the targets
  and not on the air between them, which fifteen rows of sound cannot afford.

  That row count used to be the number to watch. In the touch profile a row costs 33pt,
  and the column — transport, Tile panel over a `CHAN` head, Sound panel — stood at
  roughly 919pt against 834 on an iPad Pro 11", 820 on an Air and 744 on a mini. The
  column did not scroll, so the shortest screens genuinely lost their bottom rows, and
  every further lock target cost another 33pt off the same budget. The transpose is the
  row that took it from 853 to 886, and it was spent knowingly: what it buys is a lock
  that moves a note rather than shapes one, which nothing else in the list can do. The
  unison took it from 886 to 919 for the same kind of reason, and 886 was already over
  every one of those screens — a second row in a row bought on credit, against a
  statement here that the column needed somewhere to put a row before it could honestly
  afford another.

  **That is the debt the scrolling column pays off**, and it was paid in both of the
  ways this section left open rather than in one. The cursor's column is a
  `ScrollStrip`, so a row past the bottom of the screen is a row that has to be dragged
  to rather than one that is gone. And the panels that used to stack in it were merged
  into the one panel the cursor answers to, which gives back a header, two insets and a
  panel gap per group — the sound and the lock each cost a frame to say what a heading
  now says. A row still costs 33pt of travel, but it costs nobody a control they cannot
  reach, which is what the budget was really counting.

  The mouse profile measured 630pt of column at the same cursor, so this was always a
  tablet's problem rather than a shape that is wrong everywhere. What is left of it is a
  question of how far a hand has to drag before it sees the row it wants, which is a
  thing to feel on glass and not a number to defend here.

  A scale on the panels was the alternative and it is ruled out by what is coming.
  Pinch zoom will put a continuous fractional scale on the plane's content, which
  makes the score's on-screen size something the hand holding it decides — so the
  chrome has to stay the one place where **layout values are the real sizes and no
  transform is applied**, or 1px borders and corner radii sit permanently off the
  pixel grid beside a plane that is legitimately smeared only while it is pinched.
- **The face is Antic Didone, and its weight is a decision rather than a detail.** It
  is put on the root element and inherited from there, so a control that chose a font of
  its own would be the only way this could go wrong; the font asset is built from the
  `Font` at startup rather than checked in beside it, since what such an asset holds is
  a glyph atlas and a material made from the one thing this project actually chose, and
  saving them is committing a cache.

  A Didone runs every stroke from a stem to a hairline. Reversed out in light on dark
  the hairlines hold, because a bright shape on a dark ground gains at its edges; the
  same glyph set dark on light does the opposite, and at the eleven and thirteen pixels
  this chrome is set at the counters fill in and the thin strokes go to grey. Measured
  on the lit Play switch, plain type carries a little over half the ink bold does — 1.8
  times, over the same box. So `Style.SetInk` sets the colour and the weight together:
  there is no light ground in this UI that takes plain type and no dark one that takes
  bold, and the three places that ask are a lit switch, a bar opened to be typed into
  and the solid `CHAN` cell, which is the only word the score itself sets on light.
  Bold is the one cut dilated rather than a second one, since the family ships a single
  weight.
- **The interface is sized by the inch, and the asset is the only thing that says
  so.** `Assets/UI/DefaultSettings.asset` is a constant *physical* size at a
  reference DPI of 132, a fallback of 264 and a scale of one; there is no pixel
  scale in code and nothing writes to the asset at startup. A unit is therefore a
  hundred-and-thirty-secondth of an inch, which on any @2x iPad — every model but
  the mini is 264 ppi — resolves to exactly two pixels. That is the arithmetic the
  touch metrics rest on: **one UI pixel is one iOS point there**, so a 30pt control
  row can be read against Apple's 44pt guideline rather than guessed at.

  It replaced a whole number on `JacquardApp`, and the reasoning behind that number
  is still true: the grid is drawn in whole pixels with hairlines on half-pixel
  centres, and a fractional scale smears all of it. What it had nothing to say about
  was a screen it had not met, and a touch target is a measurement of a fingertip,
  which does not shrink on a denser display. So the smearing is now accepted where
  it happens. The known weak spot is the other end: a 96 dpi non-retina screen
  resolves to 0.727 and is illegible, and there is nothing here that guards against
  it.

  The two platforms could not agree on a physical size. A unit was 0.168mm on a Mac
  reading 303 dpi against 0.192mm on a 264 ppi iPad, 14.8% apart, so any single
  reference DPI had to move one of them: 132 keeps the iPad exact and grows the Mac.
  Worth not re-deriving. And `Screen.dpi` on macOS is a property of the display
  *mode* rather than of the panel, so picking More Space used to shrink the interface
  and now does not — that is the mode doing what it says.

  **The browser is the one platform that cannot be sized by the inch at all**, and
  `JacquardApp.FollowTheBrowser` is where it stops being asked to. Web has no DPI to
  give: nothing in the platform's JavaScript reports one, and `Screen.dpi` answers 96 —
  the density a CSS pixel is nominally defined against — times the device pixel ratio
  the runtime applied to the canvas, measured in Chrome as 96, 192 and 288 at ratios of
  one, two and three. Against a reference of 132 that is 0.727, 1.455 and 2.182 pixels
  to the unit, and since the drawing buffer is larger than the page by the same ratio,
  the ratio cancels: **a unit came out 0.727 CSS pixels on every display there is** —
  the figure the weak spot above names, except that on the Web it was not a weak spot
  but the only outcome. On this Mac in More Space that is 0.122mm a unit against the
  iPad's 0.192mm, and in Safari on an iPad 0.140mm, which puts a 30pt touch row at
  21.8pt: under the 20pt row the desktop profile would have given it.

  So the physical size is given up there and the panel is handed the ratio as a
  constant pixel size instead — **one unit, one CSS pixel.** That is the browser's own
  device-independent unit rather than a fudge factor, and on iOS it is exactly one iOS
  point: an iPad's CSS pixel is a hundred-and-thirty-secondth of an inch, which is this
  project's reference DPI, so the chrome comes out the size the native build gives it on
  the one platform that has both. Browser zoom then works on the interface as well as
  on the score, since zooming is a change to the ratio — and because a page's ratio
  moves under a running app, unlike a device's density, `FollowTheZoom` reads it every
  frame and writes only when it has moved. The ratio is read as `Screen.dpi / 96`, which
  is the one the runtime actually applied rather than `window.devicePixelRatio`, so a
  page that turns off canvas matching or pins the ratio is followed rather than argued
  with. Measured in Chrome after the change: a 192 unit panel is 192, 384 and 576 device
  pixels at ratios of one, two and three, an emulated iPad gets the touch profile at 248,
  and changing the ratio under the running page rescales without a reload.

  **The editor does not preview any of this by itself** either, which is what
  `JacquardApp.StandInForTheDevice` is for: UUM-136603 has the panel resolve its
  density against whichever monitor the view is on rather than against the simulated
  device, so an editor-only copy of the settings is switched to a constant pixel size
  and given `Screen.dpi / referenceDpi` worked out from the DPI the Device Simulator
  does shim. The bug report records it as not reproducible under a constant pixel
  size, so that is stepping off the broken path rather than correcting a value it
  produced — which is why it needs no timer, unlike the workaround that stays in
  physical size and folds a ratio into the scale.
- **The marks are generated, not drawn.** The logo, the wordmark on the transport
  row, the app icon and the favicon are all cut from the same pixel font by the
  scripts in [Branding], which reduce the type to its own sixty unit grid and
  apply the glitch in whole cells, so nothing anywhere is off that grid. Only the
  wordmark on the row is an asset the app loads — `Assets/Branding/Logo.png`,
  wired to `JacquardApp.Logo` by the scene builder, and a bitmap rather than a
  Painter2D drawing like every other mark in the interface, since what would be
  drawn is the same grid of squares the texture already holds. Paying for its
  width is why the File chooser's caption is held to the width of the word
  instead of the caption column's: nothing on that row lines up with it.
- Editor menu items: *Jacquard > Rebuild Main Scene* regenerates the scene, and
  *Jacquard > Run Self Test* checks the file format round trip, plays four laps of
  the sample score without a device, reads a stack whose gate sits between two
  notes to prove the descent only ever reaches downwards, drives the live effects
  the way the app drives it — the sequencer a window ahead and the handover a shorter
  one, which is most of what there is to get wrong about it — and renders the two
  effect buses to measure that a repeat lands on the beat, that moving the delay time
  does not splice the signal, and that the reverb's tail settles.

[Branding]: ../Branding/README.md
