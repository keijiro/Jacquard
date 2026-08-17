Documentation
=============

Start here. Three of these say what the app is and what it is supposed to do;
the rest are the implementation notes, one per area of the code, holding the
decisions behind it that the code alone does not explain.

What it is
----------

| | |
| --- | --- |
| [overview.md] | The idea, what it takes from, the stack, and the two standing decisions |
| [manual.md] | What the app does from the player's side |
| [sequencer-spec.md] | The sequencer specification — terminology, rules, and what is still undecided |
| [architecture.md] | The two assemblies, the engine-free core, and the editor menu items |

How it is built
---------------

| | |
| --- | --- |
| [impl-sequencer.md] | Timing, the downward pass, locks, mutes, and the turn of the piece |
| [impl-synth.md] | The FM voice, the patch, the lock targets, pan and unison |
| [impl-mix.md] | The send effects, the mix staging, and the limiter |
| [impl-audio.md] | The two drivers, the DSP clocks, dropouts, and the minimum lead |
| [impl-live-fx.md] | The twelve buttons, the handover window, and the rolls |
| [impl-score-plane.md] | The grid: cells, gestures, what a lane owns, and how the plane grows |
| [impl-panels.md] | The panel the cursor answers to, the controls on it, the transport row, the screen's edges |
| [impl-style.md] | How a control answers a hand, the two metric profiles, the type, the marks |
| [impl-visualizer.md] | The wash behind the score |
| [impl-files.md] | The score folder, the sample, and what an older file is owed |
| [impl-web.md] | What the browser does differently |
| [Branding/README.md] | The marks, and how they are cut from the type |

[overview.md]: overview.md
[manual.md]: manual.md
[sequencer-spec.md]: sequencer-spec.md
[architecture.md]: architecture.md
[impl-sequencer.md]: impl-sequencer.md
[impl-synth.md]: impl-synth.md
[impl-mix.md]: impl-mix.md
[impl-audio.md]: impl-audio.md
[impl-live-fx.md]: impl-live-fx.md
[impl-score-plane.md]: impl-score-plane.md
[impl-panels.md]: impl-panels.md
[impl-style.md]: impl-style.md
[impl-visualizer.md]: impl-visualizer.md
[impl-files.md]: impl-files.md
[impl-web.md]: impl-web.md
[Branding/README.md]: ../Branding/README.md
