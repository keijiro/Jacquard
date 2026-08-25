Architecture
============

How the project is laid out, what the split between its two assemblies is for,
and the rules that hold across all of it. What the app is meant to be is in
[overview.md]; what the sequencer is meant to do is in [sequencer-spec.md]; the
notes on each area of the code are the `impl-*.md` files listed in [README.md].

[overview.md]: overview.md
[sequencer-spec.md]: sequencer-spec.md
[README.md]: README.md
[socket-api.md]: socket-api.md

Layout
------

`Assets/Core` is an assembly with **no engine references at all** — the asmdef
sets `noEngineReferences`, so the separation [overview.md] asks for is enforced by
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
| `Audio` | Voice pool, the three effect buses, the Burst render job, the two drivers that carry it to an output, and the clock one of them has to measure itself against |
| `App` | The MonoBehaviour, the editing operations, file access |
| `UI` | The score plane, cell icons drawn with Painter2D, the panels |
| `Visual` | The background visualizer: one mesh, one unlit shader, and nothing else drawn by the camera |

The FM synth, the Scriptable Audio Pipeline usage and the value bar every number
is set on come from [keijiro/unity-sap-test]; the two axis scrolling plane comes
from [keijiro/uitk-scrollarea].

[keijiro/unity-sap-test]: https://github.com/keijiro/unity-sap-test
[keijiro/uitk-scrollarea]: https://github.com/keijiro/uitk-scrollarea

MathF and Burst
---------------

**`MathF` is not used in the DSP.** Burst cannot resolve the externs behind
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

Editor menu items
-----------------

Editor menu items: *Jacquard > Rebuild Main Scene* regenerates the scene, and
*Jacquard > Run Self Test* checks the file format round trip, plays four laps of
the sample score without a device, reads a stack whose gate sits between two
notes to prove the descent only ever reaches downwards, drives the live effects
the way the app drives it — the sequencer a window ahead and the handover a shorter
one, which is most of what there is to get wrong about it — and renders the two
effect buses to measure that a repeat lands on the beat, that moving the delay time
does not splice the signal, and that the reverb's tail settles.

Socket control surface
----------------------

The optional localhost control surface is documented separately in [socket-api.md]. It
is deliberately outside the score and audio architecture: the Unity bridge schedules
all score changes through the existing `ProjectFormat`, `ScoreEditor` and `Sequencer`,
while the .NET relay only authenticates peers and routes JSON-RPC messages. Its
black-box integration test suite lives at `Tools/Jacquard.Socket.Tests` and does not
require Unity or an audio device.
