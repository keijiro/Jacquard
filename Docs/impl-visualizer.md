Visualizer
==========

The wash behind the score, drawn by the camera rather than by the interface. The code is
`Assets/Jacquard/Visual` and `FmSynthScope` in `Assets/Jacquard/Audio`, both of which
argue for what they draw and how the reading reaches them.

**It is the one part of the app nothing else is allowed to depend on.** It draws the synth
rather than the sequence, so nothing about the score, the mix or the timing is decided
here or read from here — and it may be removed outright. Anything that starts reading
`FmSynthScope` for a purpose other than drawing has broken that.

Three consequences that reach outside it
----------------------------------------

**It reads the mix and not the monitoring level.** The scope is written where the mix is
finished, ahead of the output volume — see [impl-mix.md] — so a hand turning the piece
down does not turn the drawing down with it.

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
