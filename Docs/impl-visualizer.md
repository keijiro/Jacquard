Visualizer
==========

The wash behind the score, drawn by the camera rather than by the interface. The
code is `Assets/Jacquard/Visual` and `FmSynthScope` in `Assets/Jacquard/Audio`.
It is the one part of the app nothing else is allowed to depend on.

What the visualizer draws
-------------------------

**The visualizer draws the synth, not the sequence.** What the sequence is doing is
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
