Files
=====

Where a score is kept, what the app opens in, and what an older file is owed. The code is
`ProjectStore` in `Assets/Jacquard/App` and `ProjectFormat` in `Assets/Core/Serialization`,
both of which argue for their own arrangements — `ProjectFormat`'s header carries every
format version and what it converted.

Two standing obligations
------------------------

These are the two ways this reader has been got wrong, and both were got wrong twice.
Neither is caught by compiling.

**A target leaving `ParamTargets` belongs in `ProjectFormat.Retired` in the same change.**
An unknown patch key is skipped and falls back to a default, but an unknown *lock* target
is an error — a typo in a hand-edited score should not pass silently. So a dropped target
that is not recorded makes every file holding a lock on it unopenable. Version 2 took the
carrier's decay and sustain out without recording either, and files holding a lock on one
could not be opened at all for four versions.

**A target that changes what its number means belongs in a version bump with a conversion,
in the same change.** A retired target is skipped and a new one defaults, but a live target
holding a stale number looks exactly like a current one. See [impl-synth.md].

[impl-synth.md]: impl-synth.md

The sample score is a file, not code
------------------------------------

`Assets/Jacquard/Scores/Sample.jacquard.txt` holds it in the format the app already
writes, read through the same `ProjectFormat.Read` a load uses, so **replacing it is a
file copy** rather than a transcription.

`Project.CreateSample()` stays, with only its other job: it is the self test's fixture,
the one score that names every kind of tile. Keeping the two apart is what lets the
demonstration become whatever sounds best without a test noticing.

**The cost is that nothing about the file is checked by compiling, and the way it goes
wrong is quiet** — the reader takes an older version, so a sample left behind by a format
bump silently loses whatever the bump added, in the one slot a fresh install is meant to be
impressed by. Hence the self test that reads it and writes it back: that check says *this
file is already what this build writes*, which is a different question from whether the
format is self-consistent. Run **Jacquard > Run Self Test** after replacing it.

What a file is allowed to say
-----------------------------

**A file may hold a value no bar could have been dragged to, and it is kept.** A bar's
range is where a parameter is dialled; what the synth accepts is `ParamTargets.Bound`,
which is wider wherever there is a reason, and a file is held to that and nothing else.
This is deliberate — a hand-edited score is one of the ways a part gets moved further
than the chrome will move it.

**The one thing refused is a value that is not a number.** `NumberStyles.Float` accepts
`NaN` and `Infinity` as readily as a digit, and a clamp does not stop either — both of
its comparisons are false for a NaN, so it passes through untouched. One reaching the
synth costs a voice for the session and latches in the effect tails, which are
recursive. `ProjectFormat.ReadFloat` reads such a token as nothing, and
`ParamTargets.Set` refuses one from any direction; both argue it where they do it.

The folder is the list
----------------------

**Every score is a file in one folder, and the chooser is that folder read out.** There
are no names of the app's own on it, so every name is a file and pressing Load always does
something. An install whose folder is empty gets nine written for it.

The consequence worth remembering is that **the folder is authoritative and the app is
not**: which score to come up in is remembered in `PlayerPrefs` and checked against the
folder before it is used, and the folder is read again whenever the app comes back to the
front — because *away* is exactly where a file manager does its work, and both the System
panel and iOS file sharing hand somebody that folder on purpose. See `ProjectStore` and
`IosFileSharing`.
