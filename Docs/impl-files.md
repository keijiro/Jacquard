Files
=====

Where a score is kept, what the app opens in, and what an older file is owed.
The code is `ProjectStore` in `Assets/Jacquard/App` and `ProjectFormat` in
`Assets/Core/Serialization`.

The sample score
----------------

**The sample score is a file, not code.** `Project.CreateSample()` built one by hand,
which was right while the demonstration case was small and wrong the moment it became
a real piece of work: what is wanted now is eight patches and seven lanes, and
transcribing that into C# literals would be two hundred lines of numbers that no one
can read and that have to be re-transcribed every time the piece changes. So
`Assets/Jacquard/Scores/Sample.jacquard.txt` holds it in the format the app already
writes, `JacquardApp.SampleScore` reads it through the same `ProjectFormat.Read` a
load uses, and replacing it is a file copy. A double extension because Unity imports
a `TextAsset` by extension and `.jacquard` is not one it knows.
`CreateSample()` stays, with only its other job: it is the self test's fixture, the
one score that names every kind of tile, and a fixture is better as code than as an
asset that can be edited out from under a check. What the split buys is that neither
is answering to the other's requirements — the demonstration can become whatever
sounds best without a test noticing.
The cost is that nothing about the file is checked by compiling, and the way it goes
wrong is quiet: the reader takes an older version, so a sample left behind by a format
bump loses whatever the bump added, silently and in the one slot a fresh install is
meant to be impressed by. Hence one self test that reads it and writes it back — the
same round trip as above, used to say *this file is already what this build writes*
rather than *the format is consistent*.

The score folder, and what the app opens in
-------------------------------------------

**The app opens in a file it saved, and the folder is never empty.** It used to open on
the sample asset every launch, whatever had been made since, and the chooser offered
four names — `sketch` and three takes — that were slots to save into rather than scores
that existed. Two things were wrong with that at once: work made in the app was never
what the app came back to, and half the names on the chooser did nothing when Load was
pressed.
So `ProjectStore` owns the whole of it. An install whose score folder is empty gets
nine files written into it, `score1` through `score9`, the first holding the sample and
the eight beside it the initial score — one lane of sixteen steps with a C4 on every
fourth, which is a bar of sixteenths with a note on each beat, and which is
`Project.CreateInitial()` and what a score is initialized to anywhere else. The chooser
is then that folder read out, alphabetically, and nothing else; every name on it is a
file. Which score to come up in is remembered in `PlayerPrefs` — it is a fact about
this copy of the app rather than about any piece, so it lives where the visualizer's
switch does — and checked against the folder before it is used, so a score deleted from
under the app comes up as the first slot rather than as a failed load. A first launch of
all remembers nothing and lands on `score1`, which is the sample.
Both ends of a file operation record it: a load is the plain case, and a save is the
same thing from the other end, since the slot just written is the one holding the work.
The folder is read again whenever the app comes back to the front — `OnApplicationFocus`
and `OnApplicationPause` both, since which one arrives is the platform's business —
because *away* is exactly where a file manager does its work, and the System panel hands
somebody that folder on purpose. A folder emptied while the app was away is seeded again
rather than left empty: the chooser has to have something on it, and `Controls.Chooser`
tolerating an empty list is the belt to that braces.

Older files
-----------

**An old file loses what the synth no longer has, rather than being refused.** A
patch key nothing answers to is skipped, so a deleted parameter simply falls back to
the default; a *lock* on one has to be named in `ProjectFormat.Retired` to get the
same treatment, because an unknown lock target is otherwise an error — a typo in a
hand-edited score should not pass silently. Which makes the list a standing
obligation: **a target leaving `ParamTargets` belongs in `Retired` in the same
change.** It was not, twice. Version 2 dropped the carrier's decay and sustain
without recording either, so for four versions a file holding a lock on one could
not be opened at all — one of the saved scores in `persistentDataPath` was in
exactly that state until 2026-08-09. Only `detune`, dropped by version 5, was
entered at the time.
