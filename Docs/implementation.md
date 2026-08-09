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
| `Sequencer` | `Runner` and the scheduler that turns tiles into note events |
| `Synth` | The two operator FM voice, the per channel patch bank, the lock targets, the send effect settings |

`Assets/Jacquard` is the part that cannot help but know about Unity:

| | |
| --- | --- |
| `Audio` | Voice pool, the two effect buses, Burst render job and the Scriptable Audio Pipeline output |
| `App` | The MonoBehaviour, the editing operations, file access |
| `UI` | The score plane, cell icons drawn with Painter2D, the panels |

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
- **Every field of the patch is a lock target.** `FmPatch` and `ParamTargets` name
  the same thirteen parameters, so there is nothing a channel holds that a step cannot
  reach for one instant. One of them, the gate ratio, multiplies the length written
  on the note rather than being a length itself, which is why the note reads in
  steps and the channel in percent: the two are the same multiplication and only
  the unit tells them apart.
- **One lock reaches as many of them as it likes.** A lock carries a slot per
  target and holds whichever ones have been set, so a step that changes four
  parameters is one tile rather than four stacked cells between the gate and the
  note. What it does not hold it leaves entirely to the channel, which is why a
  lock that holds nothing — a freshly placed one — is inert rather than wrong.
- **Timbre belongs to the channel**, not to the project: the bank holds one patch
  per channel and a `CHAN` tile's number picks the sound as well as the stream, so
  lanes sharing a channel share a patch and a branch lane borrows the one of
  whatever jumps into it. The Sound panel is where that patch is edited, and an
  edit is heard from the next instant with nothing to undo.
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
  decided. The Sound panel's audition hangs off it, and so does the note the Tile
  panel's pitch bar plays. Sounding a note per event turned a drag down a bar into a
  burst of a hundred, none of which was the value being chosen.
- **A panel shows what the cursor is on**, and nothing is toggled. The tile panel
  keeps the corner and follows the cursor; beside it comes up either the Sound
  panel, while a `CHAN` cell is selected, or the Lock panel, while a `PABS` or
  `PREL` cell is. Those two are the same list of parameters read two ways — what a
  channel sounds like, and what one step does to it — and they share a slot because
  no cell is both. There is no window to open, and so no state on screen that the
  score does not decide.

  **The Send FX panel is the one exception, and it is the exception because it has to
  be.** One reverb and one delay for the whole project answer to no cell, so there is
  no cursor position that could bring them up; putting a tile on the plane for the sake
  of the rule would be inventing score to hold a setting. It pays for the state it adds
  by not being up unless it has been asked for — a button on the transport row, which
  is where what belongs to the project already lives — and it takes the close button
  `Controls.Panel` has always offered and nothing had used. It hangs from the top left,
  the opposite corner from the cursor's column, so that reaching for an effect never
  covers what the cursor is saying about the note the effect is for.

  It is called **Send FX** and not Send, on the panel and on the button both. A send
  is what a *channel* does, and the amounts are on the Sound panel named after the
  effect each one feeds; a panel called Send would be named after the sending and hold
  none of it. What is here is the receiving end.
- **The panel is also where a tile is put down**, since the cursor is already the
  answer to where. A cell that will take one — a lane's empty step, the cell under
  a stack, the `TERM` cell that grows the lane — offers the tiles instead of a
  description of nothing, and bare ground offers a lane to put one on. So there is
  no palette to keep in step with what the cursor can accept, no button that
  silently does nothing where it stands, and one less row of chrome above the
  plane. A tile therefore only ever lands on free ground: a stack is built from the
  top down rather than by inserting above what is already there, which is the order
  the runner reads it in anyway.
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
- **The cell pitch is what the rest of the plane is derived from.** A cell is
  30x32 with a 4px gutter, set by what has to fit inside one rather than by taste:
  a note name with its accidental gutter is a little over twenty pixels wide, and
  the icons are drawn in a 15x15 box. Keeping those numbers in `Style` alone is
  what lets the painted layers and the tile elements agree on where a cell is to
  the pixel.
- **Chain lines** are drawn only between cells of the same stack. mockup.html joins
  whatever happens to sit directly above, which makes two unrelated lanes look
  connected; sequencer.md lists that as undecided, and knowing the lane settles
  it.
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
  and not on the air between them, which a thirteen row Sound panel cannot afford.

  That row count is the number to watch. In the touch profile a row costs 33pt, and
  the column — transport, Tile panel over a `CHAN` head, Sound panel — now stands at
  roughly 853pt against 834 on an iPad Pro 11", 820 on an Air and 744 on a mini. The
  column does not scroll, so the shortest screens genuinely lose their bottom rows,
  and **every further lock target costs another 33pt off the same budget.**

  A scale on the panels was the alternative and it is ruled out by what is coming.
  Pinch zoom will put a continuous fractional scale on the plane's content, which
  makes the score's on-screen size something the hand holding it decides — so the
  chrome has to stay the one place where **layout values are the real sizes and no
  transform is applied**, or 1px borders and corner radii sit permanently off the
  pixel grid beside a plane that is legitimately smeared only while it is pinched.
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

  **The editor does not preview any of this by itself**, which is what
  `JacquardApp.StandInForTheDevice` is for: UUM-136603 has the panel resolve its
  density against whichever monitor the view is on rather than against the simulated
  device, so an editor-only copy of the settings is switched to a constant pixel size
  and given `Screen.dpi / referenceDpi` worked out from the DPI the Device Simulator
  does shim. The bug report records it as not reproducible under a constant pixel
  size, so that is stepping off the broken path rather than correcting a value it
  produced — which is why it needs no timer, unlike the workaround that stays in
  physical size and folds a ratio into the scale.
- Editor menu items: *Jacquard > Rebuild Main Scene* regenerates the scene, and
  *Jacquard > Run Self Test* checks the file format round trip, plays four laps of
  the sample score without a device, reads a stack whose gate sits between two
  notes to prove the descent only ever reaches downwards, and renders the two effect
  buses to measure that a repeat lands on the beat, that moving the delay time does
  not splice the signal, and that the reverb's tail settles.
