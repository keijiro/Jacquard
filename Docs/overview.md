Overview
========

What the app is meant to be, where it came from, and the standing decisions
about how it is built that are older than any part of the code. What it does
from the player's side is [manual.md]; what the sequencer is meant to do is
[sequencer-spec.md]; how the code is laid out is [architecture.md].

[manual.md]: manual.md
[sequencer-spec.md]: sequencer-spec.md
[architecture.md]: architecture.md

The idea
--------

Jacquard is a music app built around a procedural sequencer with an unusual
amount of freedom in it. A sequence is built by putting tiles on a plane, which
to a beginner is a plain step sequencer — sixteen slots with notes laid along
them — and which becomes something else as soon as tiles are stacked vertically
or the tiles with a function of their own are used.

A player is not held to one horizontal axis either. Lanes are laid out anywhere
on the plane, and they can jump between one another and branch. It is the
combination of several lanes that gives a sequence its complicated changes.

What it takes from
------------------

- [Trackers]
- [100R Orca]
- [Fors FMS]
- Elektron hardware synths

[Trackers]: https://en.wikipedia.org/wiki/Music_tracker
[100R Orca]: https://100r.co/site/orca.html
[Fors FMS]: https://lo-bit.club/fms

The stack
---------

Unity 6.5. Everything on screen is UI Toolkit except the visualizer, which is
one mesh under URP drawn by the camera; input is the new input system. The synth
runs on the Scriptable Audio Pipeline where the platform has one, and is pushed
to the Web Audio API where it does not ([impl-web.md]). The project is meant to
be driveable from the Unity CLI, so that an agent can build it, run it and read
it back without a hand on the editor.

[impl-web.md]: impl-web.md

Two standing decisions
----------------------

**The engine is kept out of the model, the format, the sequencer and the DSP.**
Object lifetime and serialization use nothing from `UnityEngine`. What that
buys is a core that can be compiled, tested and reasoned about without an
editor, and it is enforced by the compiler rather than by discipline — see
[architecture.md] for how. It is a goal rather than a law: if holding to it ever
becomes plainly disadvantageous, it is the goal that gives way.

**The look is flat and monochrome.** One ramp of greys, no gradients, no
shadows, nothing coloured to carry meaning. What a thing is saying is said by
where it sits on that ramp and by how much air is around it — see
[impl-style.md].

[impl-style.md]: impl-style.md
