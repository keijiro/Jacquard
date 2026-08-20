Live effects
============

The twelve buttons that colour a note on its way out and are written nowhere. `LiveFx`
in `Assets/Core/Sequencer` is the layer itself and argues for all of it; what is here is
the shape it makes with the sequencer and the format, which is in no one file.

The layer, and the two windows
------------------------------

`LiveFx` sits between the sequencer and the synth. The sequencer produces events into
it, it parks them, and it hands over something else for as long as a button is down —
which means **the reach of a live effect is exactly whatever has not been handed over
yet.** A voice reads its event once, so a note already sounding is never touched.

That reach is what splits one window into two, and it is the one property this project
has deliberately given back:

| | |
| --- | --- |
| `JacquardApp.Lookahead` | How far ahead the sequencer runs. Covers the gap between two updates |
| `JacquardApp.HandoverSeconds` | How far ahead a note actually leaves the queue. Two frames, floored at `LiveLead` and capped at `Lookahead` |

The sequencer still runs the full window ahead; only the moment of the handover moved,
and the sample a note starts on is never touched. `HandoverSeconds` carries the whole
argument for the frame-rate reading and the two clamps — it is a requirement in *frames*
that was first written down in milliseconds, which is the bug it exists to have fixed.

What that costs is the margin against a slow frame. It is affordable because a note
handed over late is not played late: `FmVoicePool.Render` triggers against the clock, so
a hitch takes the head off a note rather than its place in the bar. On the Web the
driver's own floor sits under both windows, so the margin there is where it was.

**The grid is the project's sixteenth and not the lane's step**, for the ramps and the
rolls alike. See `LiveFx`.

Nothing here is saved
---------------------

A press is a gesture rather than a setting, so there is no file key, no version bump,
no lock target and no row on any sound panel. That is what keeps a feature this wide
out of [impl-files.md] entirely, and it is why the live effects stand outside the scale
and the transpose — see the last note of [sequencer-spec.md].

[impl-files.md]: impl-files.md
[sequencer-spec.md]: sequencer-spec.md

Where the rest is written
-------------------------

| | |
| --- | --- |
| What the twelve are, and how two held at once compose | `LiveFx` — the `LiveEffect` enum and `Apply` |
| Rolls: recording rather than re-running, the empty window, the grid arithmetic | `LiveFx` — the `Roll` class, `Arm` and `Repeat` |
| The panel's placing, its column order, the player's names on the buttons | `LivePanel` |
| Why a held button cannot use the stock `Clickable` | `Controls.Hold` |

How a panel is built at all is [impl-panels.md].

[impl-panels.md]: impl-panels.md
