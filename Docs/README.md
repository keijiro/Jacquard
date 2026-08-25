Documentation
=============

Start here. Four of these say what the app is and what it is supposed to do; the rest are
the implementation notes, one per area of the code.

**The implementation notes are maps, not essays.** What a particular piece of code is
shaped that way *for* is written in the file itself, next to the code, and is not repeated
here. What these notes hold is what spans several files or has no file at all — the rules
every panel obeys, the invariants two classes share, the obligations the format imposes,
the alternatives that were rejected — with a table at the end of each saying which file
argues for the rest. Read the note for the area you are touching, then the files it names.

`../CLAUDE.md` states that split as a rule, and is the shortest thing here.

What it is
----------

| | |
| --- | --- |
| [overview.md] | The idea, what it takes from, the stack, and the two standing decisions |
| [sequencer-spec.md] | The sequencer specification — terminology, rules, rejected alternatives, and what is still undecided |
| [architecture.md] | The two assemblies, the engine-free core, and the editor menu items |
| [socket-api.md] | The localhost WebSocket bridge, protocol, lifecycle and test contract |
| [manual.md] | The gestures and keys, from the player's side |

How it is built
---------------

| | |
| --- | --- |
| [impl-sequencer.md] | Where each rule of the spec lives, and the invariants under them |
| [impl-synth.md] | What decides where a new parameter goes, and what it costs |
| [impl-mix.md] | The chain in order, and which file owns each stage |
| [impl-audio.md] | The two drivers, and the three numbers that had to be measured |
| [impl-live-fx.md] | The layer, the two windows, and why none of it is saved |
| [impl-score-plane.md] | What a lane owns, what a drag means, how the plane grows |
| [impl-panels.md] | The rules every panel obeys, and what raises each one |
| [impl-style.md] | The one ramp, the two profiles, and sizing by the inch |
| [impl-files.md] | The two standing obligations on the reader, and the score folder |
| [impl-visualizer.md] | What nothing else may depend on |
| [impl-web.md] | What the browser does differently |
| [Branding/README.md] | The marks, and how they are cut from the type |

[overview.md]: overview.md
[manual.md]: manual.md
[sequencer-spec.md]: sequencer-spec.md
[architecture.md]: architecture.md
[socket-api.md]: socket-api.md
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
