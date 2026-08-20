Mix
===

What happens to the sum of the voices: the two send effects, the staging that decides
how loud a note is, the limiter the mix is played through, and the volume it leaves at.
The voice that feeds it is [impl-synth.md].

Each stage argues for itself in its own file, so what is here is the order they stand in
and the few things that belong to no one of them.

[impl-synth.md]: impl-synth.md

The chain, in order
-------------------

| Stage | Where the decision is written |
| --- | --- |
| Every voice, placed by its own pan | `FmVoicePool.Render` |
| The two sends, in parallel and not in series | `DelayBus`, `ReverbBus` |
| The dry sum, staged so full scale is four notes | `FmSynth.MasterGain` |
| The limiter, one ceiling with its make-up derived | `Limiter` (the settings), `LimiterBus` (the gain) |
| The soft clip, now the limiter's backstop | `FmSynthCore.RenderJob.SoftClip` |
| The scope the visualizer reads | `FmSynthCore.RenderJob.Execute` — deliberately here, above the volume |
| The output volume, ramped across the block | `OutputVolume` (the setting), `OutputBus` (the ramp) |

`FmSynthCore.RenderJob.Execute` is the one place the whole of it is written out in
order, and it is worth reading before changing any stage's place in it.

What belongs to no one stage
----------------------------

**The effect settings are the only mutable state the audio thread reads.** Everything
else reaches it stamped into a note — which is what `FmSynth.SetFx` and `MixFxRuntime`
exist to work around, since one reverb serving eight channels cannot ride on a note.
`JacquardApp.Update` hands them over whenever they differ from what it handed over
last, and that one comparison covers every way they can change: a bar on any of three
panels, the tempo the delay is locked to, a project loaded over the top of this one.
None of those has to know that anything downstream cares. The volume rides along
although it is not the project's, because what the audio thread is owed is the settings
as they stand.

**Where each setting lives is decided by whether it would mean anything to somebody the
file is handed to.** The sends and the limiter are on `Project` and travel with the
score; the volume is in `PlayerPrefs` and does not, because what a hand reaches for it
for is the room rather than the piece. That is the rule the volume was moved by — it was
built on Global first — and it is the rule to settle the next such question with. See
`OutputVolume` and `SystemPanel`.

**Changing the staging is a format version.** A threshold is a level, so a gain anywhere
above the limiter makes every saved threshold mean something else. `ProjectFormat`
converts rather than re-reads, and its header carries each version that did this and
what it shifted — version 17 for the quarter-scale staging, version 13 for the drive the
make-up replaced.

The panels
----------

The limiter and the scale are on Global, the sends on Send FX, the volume on System.
Why each is where it is, is in `GlobalPanel`, `SendPanel` and `SystemPanel`
respectively; how a panel is built at all is [impl-panels.md].

[impl-panels.md]: impl-panels.md
