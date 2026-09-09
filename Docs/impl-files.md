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

This second one is about any value a file holds and not only a lock target. Version 19 is
the case that is not a target at all: `dtone` on the `fx` line kept its spelling and its
range and started meaning the other end of the same brightness, which no reader can tell
from a current file. `ProjectFormat`'s header argues for where that conversion has to sit.

[impl-synth.md]: impl-synth.md

The sample scores are files, not code
-------------------------------------

`Assets/Jacquard/Scores/sample1.jacquard.txt` through `sample5.jacquard.txt` hold them in
the format the app already writes, read through the same `ProjectFormat.Read` a load
uses, so **replacing one is a file copy** rather than a transcription. `SceneBuilder`
keeps the list and the order, and how many there are is not written down anywhere else —
adding a sixth is a path on the end of it.

`Project.CreateSample()` stays, with only its other job: it is the self test's fixture,
the one score that names every kind of tile. Keeping the two apart is what lets the
demonstration become whatever sounds best without a test noticing.

**The cost is that nothing about the file is checked by compiling, and the way it goes
wrong is quiet** — the reader takes an older version, so a sample left behind by a format
bump silently loses whatever the bump added, in the pieces a fresh install is meant to be
impressed by. Hence the self test that reads each of them and writes it back: that check
says *this file is already what this build writes*, which is a different question from
whether the format is self-consistent. It names them one by one, since they are replaced
one by one. Run **Jacquard > Run Self Test** after replacing any of them.

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
something. An install whose folder is empty gets fourteen written for it: the five sample
pieces the build carries as `sample1` through `sample5`, and nine blank scores as `score1`
through `score9`.

**The samples stand beside the slots rather than in them.** `score1` used to hold the one
sample there was, which put the only piece worth hearing in the first slot a hand would
save over. Named apart, they sort above the slots — so the first launch of all still opens
a real piece, and nothing done to the nine can destroy one.

The consequence worth remembering is that **the folder is authoritative and the app is
not**: which score to come up in is remembered in `PlayerPrefs` and checked against the
folder before it is used, and the folder is read again whenever the app comes back to the
front — because *away* is exactly where a file manager does its work, and both the System
panel and iOS file sharing hand somebody that folder on purpose. See `ProjectStore` and
`IosFileSharing`.

Two platforms where the folder cannot be handed over
----------------------------------------------------

**Android and the Web are the exceptions, and neither is a choice.** They arrive at the
same place from different directions, which is why each reason is worth stating as its
own.

**On Android the folder is hidden by the platform.** Since API 30 the app's own directory
under `Android/data` — which is what `Application.persistentDataPath` resolves to — is
hidden from the Files app, from the document picker and from MTP, and hidden even from a
file manager holding `MANAGE_EXTERNAL_STORAGE`. There is no folder to show and none to
drop a file into. The two roads back were weighed and refused: a SAF tree grant takes
every file access out of `System.IO` and into `ContentResolver` and hangs the whole store
off a permission the user can revoke, and `MANAGE_EXTERNAL_STORAGE` is restricted by Play
policy to file managers and backup tools.

**In the browser the folder is not a place at all.** `persistentDataPath` there is a mount
on IndexedDB — the runtime's own filesystem, reaching the browser's storage only through
the flag [impl-web.md] argues for. It is a real path to the runtime and to nothing else on
the machine: there is no folder for anybody to open, no file anybody can put into it, and
clearing the site takes the lot.

**What stands in its place, on both, is one file at a time.** Export writes the score in
memory to wherever the picker is pointed; Import reads one back over the top of it.
`ScoreTransfer` is the seam and argues its own shape; the other halves are the
`.androidlib` under `Assets/Plugins/Android` and `JacquardScoreTransfer.jslib` under
`Assets/Plugins/WebGL`, and each of those argues what its own platform makes of a picker.

**An imported score is an unsaved score.** It goes in through the same seam a load goes
in through — `JacquardApp.BringIn`, the turn of the piece, the same lock — and nothing
about it reaches the score folder. `Save` is what decides which slot it ends up in, the
same as for a score just written, and until then `Load` takes the file on disk back
unchanged. So the rule above still holds where it is hardest to hold: the folder is
authoritative, and on both of these it is authoritative over a folder nothing outside the
app can put a file into.

**Whether the press stops the sequence is where the two part.** On Android either button
backgrounds the app, and that is `Sequencer.Stop` by way of
`JacquardApp.GoQuietForTheBackground`. `Stop` ends in `SettleIfIdle`, so a pending switch
lands and the editing lock is given back on the way out — which means an imported score
always arrives at a stopped sequencer and never waits. A browser on a phone may hide the
page for its picker and the same account holds there. A desktop browser's file dialog
hides nothing: nothing pauses, `Stop` is never reached and the lock is still on, so **the
Web is the one platform where an imported score can wait for the turn of the piece** the
way a load does. Nothing is needed to make that safe. `Sequencer.SwitchTo` overwrites the
one pending score, so a file arriving late can only take the place of a switch that had
not landed yet — which is what `Load`, gated at press time in exactly the same way,
already stands on.

[impl-web.md]: impl-web.md
