Jacquard
========

A tile-based music sequencer: a score is tiles placed and stacked on a two-dimensional
plane, played by one runner per lane. Unity 6.6, UI Toolkit for everything except the
background visualizer, and a Burst FM synth on the Scriptable Audio Pipeline.

Where the reasoning lives
-------------------------

This codebase carries an unusual amount of written reasoning, and it is split on one
rule. **Read the right one and do not duplicate across the line.**

- **A code comment owns why that code is shaped the way it is.** This is the larger body
  and the authoritative one. Most files open with a header stating what the thing is for,
  what was tried, and what was measured; many members carry the argument for their own
  number. If you change what a comment describes, change the comment in the same edit.
- **`Docs/` owns what spans files or has no file.** The specification, the cross-cutting
  rules, the format's obligations, the rejected designs. The `impl-*.md` notes are maps —
  they say which rule lives where and point at the file that argues for it.

So: **do not restate a file's header comment in a doc, and do not move an argument out of
a file into a doc.** A doc section that only paraphrases one file is redundant and will
drift.

Read this before touching that
------------------------------

`Docs/README.md` is the full index. The short version:

| Working on | Read |
| --- | --- |
| Anything about the sequencer's rules | `Docs/sequencer-spec.md` — the terminology here is the terminology the code uses |
| Runners, timing, locks, the turn of the piece | `Docs/impl-sequencer.md` |
| The FM voice, the patch, lock targets | `Docs/impl-synth.md` |
| Buses, limiter, the output volume | `Docs/impl-mix.md` |
| Drivers, the DSP clocks, dropouts | `Docs/impl-audio.md` |
| The live effect buttons | `Docs/impl-live-fx.md` |
| The grid, cells, gestures | `Docs/impl-score-plane.md` |
| Panels and controls | `Docs/impl-panels.md` |
| Colours, metrics, type, marks | `Docs/impl-style.md` |
| The file format, the score folder | `Docs/impl-files.md` |
| The Web build | `Docs/impl-web.md` |
| Shipping a build, on any platform | `Docs/releasing.md` |

Read the one for the area you are touching, not all of them.

Layout
------

- **`Assets/Core`** — the model, the text format, the sequencer and the synth settings.
  The asmdef sets `noEngineReferences`, so **nothing here may reference `UnityEngine`**
  and the compiler enforces it. This is a goal rather than a law, but breaking it is a
  decision to raise rather than to make quietly.
- **`Assets/Jacquard`** — `Audio` (voice pool, buses, the Burst render job, the two
  drivers), `App` (the MonoBehaviour, editing operations, file access), `UI` (the plane,
  the panels), `Visual` (the visualizer, which nothing else may depend on).
- **`Assets/Editor`** — the scene builder and the self test.

`MathF` is not used in the DSP. Burst cannot resolve the externs behind it, and **one
`MathF` call takes the whole assembly's jobs down to managed execution** — so `FastMath`
spells out the sine and the exponential. See `Docs/architecture.md`.

Working on it
-------------

- **Jacquard > Run Self Test** — format round trip, four laps of the sample score without
  a device, the descent, the live effects, and the two effect buses. Run it after
  anything touching the format, the sequencer or the mix.
- **Jacquard > Rebuild Main Scene** — regenerates the scene from `SceneBuilder`.
- The project is meant to be driveable from the Unity CLI, so a change can be built, run
  and read back without a hand on the editor.

Two rules the format has cost mistakes for, both in `Docs/impl-files.md`: a target
leaving `ParamTargets` belongs in `ProjectFormat.Retired` in the same change, and a
target that changes what its number means belongs in a version bump with a conversion in
the same change.

Style
-----

Follow the surrounding code. The prose in comments is deliberate and has a voice — match
it rather than flattening it to summaries.

A commit message opens with a subject line: what changed, in the imperative, no full stop,
and under 72 characters — aim at 50, which is where the median of this history's own
opening clauses already sits. Then a blank line, then the reason: why the code is shaped
the way it is, what was tried, what was measured. The reason is the part that matters and
it is not rationed — as many sentences and paragraphs as the argument takes, wrapped at 72
columns. It was one sentence before, and the sentence had grown to a page sitting on line
one, which is a page `git log --oneline` prints as a wall.

Most of what is written here already splits at its own comma: *"Add Stage Mode, since this
screen carries controls that only an edit has any use for"* is a subject line and the
opening of a body, and the split is where the comma already is.
