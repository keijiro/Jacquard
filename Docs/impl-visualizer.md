Visualizer
==========

The wash behind the score, drawn by the camera rather than by the interface. The code is
`Assets/Jacquard/Visual` and `FmSynthScope` in `Assets/Jacquard/Audio`, both of which
argue for what they draw and how the reading reaches them.

**It is the one part of the app nothing else is allowed to depend on.** It draws the synth
rather than the sequence, so nothing about the score, the mix or the timing is decided
here or read from here — and it may be removed outright. Anything that starts reading
`FmSynthScope` for a purpose other than drawing has broken that.

Five consequences that reach outside it
---------------------------------------

**It reads the mix and not the monitoring level.** The scope is written where the mix is
finished, ahead of the output volume — see [impl-mix.md] — so a hand turning the piece
down does not turn the drawing down with it.

**Both lines are on one vertical axis.** The channel's tap is staged by the same
`masterGain` the mix is, in `FmSynthScope.Write` and nowhere else, so the second line is
literally the share of the first that channel is worth and the two can be compared by
eye. It is the dry share only — [impl-mix.md] says why a per-channel tail is not
available at all.

**The rule at the top of this page is standing, and one line in `JacquardApp.Update`
is what stands it up.**
The second trace is a channel's, and a channel is a fact about the score, so the choosing
is done where every other reading of the score is turned into something the audio side
can hold: the app asks `FmSynth.WatchChannel` for the channel under the selection, or for
nothing when there is no selection or the visualizer is down. What `Visual` sees is a
second ring on the scope and a flag saying whether the synth is filling it. Moving that
decision into the visualizer would be the first time this part of the app read the score,
and it is the whole of what keeps that rule true rather than aspirational.

**The camera draws to the backbuffer, and it takes two settings to keep it there.** The
camera's HDR flag is off in `SceneBuilder` and `DefaultRenderer.asset` holds its
intermediate texture mode at Auto; either one alone puts URP back onto a full-screen
colour attachment and a depth attachment with a blit at the end, whatever is or is not
drawn. `SceneBuilder` carries the argument, since neither asset can hold a comment.

**Two elements paint a ground they would not otherwise need.** The camera clears to the
colour the UI panel used to paint and the visualizer draws over it, so the panel root
paints nothing; the transport row paints its own, because a row of controls with a
waveform running behind them is a row that has to be read through something. The camera's
culling mask went from nothing to the default layer, which is the first thing it has ever
had to draw.

[impl-mix.md]: impl-mix.md
