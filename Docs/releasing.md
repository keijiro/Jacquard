Releasing
=========

This is a procedure rather than a script because three of its steps are agreements with a
person and not commands: which version this is, what the notes say, and whether to publish.
Only the third is dangerous, and it is dangerous quietly — a release whose artefact somebody
has already downloaded cannot be taken back. So `Tools/package.sh` stops at `Build/dist`, and
everything past that point is written down here instead.

It is written for whoever is driving, which is as likely to be a coding agent as a person.
The rule under all of it is the same: **propose, then wait.** Nothing below picks a version,
settles a sentence or publishes anything on its own.

The version
-----------

Decide it with the user, before anything is built.

`bundleVersion` in `ProjectSettings/ProjectSettings.asset` is the one place it is written.
`Tools/package.sh` reads it from there and names both archives after it, so it has to be
right going in rather than corrected coming out.

Read what has landed since the last release first, then put a version to the user together
with what it is for — a fix, a feature, a break. Do not increment by habit and do not pick
one silently.

The notes
---------

The draft comes from the log. The wording comes from the user.

```sh
git log --oneline <last tag>..HEAD
```

The first release had no range to read, and its notes describe the app rather than what
changed in it. `v1.0.0` is the tag every range since reads from.

**The commit messages here are not release notes and must not be pasted into them.** A
subject line names what changed and the body under it argues why the code is shaped that way,
which is exactly what somebody deciding whether to download does not need. The subject lines
are the closer of the two and are what `--oneline` hands you, but they are still written for
somebody who knows the code. Read them for what happened, then write short lines about what
is different for somebody using the app.

Put the draft to the user and wait for it to come back. The notes are the part of a release
that is read the most and revised the least.

Write them one line to a paragraph and one line to a bullet, wrapped nowhere. Everything
else here is wrapped at ninety columns because it is read in a file; this is read in a
column GitHub chooses, and a hard wrap inside a sentence there is a rag down the middle of
the page rather than a line ending.

The archives
------------

```sh
Tools/package.sh              # build, sign, notarise, archive
Tools/package.sh --no-build   # reuse whatever is already in Build/
```

It leaves `Build/dist`: one zip per platform and a `SHA256SUMS` for both. The header of that
script argues the signing and the notarisation, and that argument is not repeated here.

The archives do not get a section in the notes. Three things about them are true and were
written there once — that the macOS zip is signed and notarised, that it is Apple silicon
only, and that the Windows one is unsigned and SmartScreen will say so — but the notes are
read by somebody deciding whether this version is worth having, and none of that is about
this version. The file names carry the platform and the architecture, and the signing is
argued where it is done. Notes say what changed in the app.

The release
-----------

Only once the version and the notes are both agreed.

A tag that already exists must not be reused, so look before writing:

```sh
gh release view "v$VERSION"
```

Then, with what `Tools/package.sh` left:

```sh
gh release create "v$VERSION" \
    --title "Jacquard $VERSION" \
    --notes-file <notes> \
    Build/dist/Jacquard-$VERSION-macOS-arm64.zip \
    Build/dist/Jacquard-$VERSION-windows-x64.zip \
    Build/dist/SHA256SUMS
```

Add `--draft` when the notes want looking at in place. A draft has no tag until it is
published, so the URL it comes back with is a temporary one and not the release's own.

**An agent creates drafts and does not publish.** Publishing is the user's, every time, and
saying so out loud costs nothing.

iOS, in brief
-------------

The two halves share the version string and the bundle id `jp.radiumsoftware.jacquard`, and
little else. In App Store Connect the app is id `6804390464`.

iOS carries a build number as well as a version, and App Store Connect tells two uploads of
one version apart by that number and by nothing else — so it rises on every upload, whether
or not the version moved. 1.0.0 shipped as build 2 for that reason and 1.1.0 as build 3: a
new version string does not restart the count, and `asc builds next-build-number` answers
off the app's whole processed history rather than off one version's.

The path, as far as it has been walked:

- *Jacquard > Build iOS* writes the Xcode project. `BuildIos.cs` says why it is a menu item
  and why it appends rather than replaces.
- `asc xcode archive --scheme Jacquard`, then `asc xcode export`. The scheme name is worth
  reading twice: Unity calls the *target* `Unity-iPhone` and the scheme after the product,
  so the obvious guess is the one name xcodebuild will not take.
- The export needs manual signing with an `ExportOptions.plist`; automatic signing cannot
  export. `.asc/export-options-app-store.plist` is this app's.
- `asc xcode validate` is optional and asks for a second set of credentials. It wraps
  `xcrun altool --validate-app`, which cannot see the key `asc` keeps in the keychain and
  wants an `--api-key` and `--api-issuer` handed to it. The upload runs the same check on
  Apple's side, so skipping it costs the earlier answer and nothing else.
- The encryption question is already answered in the build by `IosExportCompliance.cs`, so a
  build should not stall waiting for somebody to click through it.
- `asc publish appstore` without `--submit` uploads, creates the version and attaches the
  build. `asc builds list` watches it process.
- `asc validate` reports submission readiness, and `asc review submit --confirm` ships it.
- Expect to finish in the browser. Two declarations for 1.0.0 could not be verified through
  the API and had to be made in App Store Connect by hand, and App Privacy is reported as
  unverifiable through it on every release since, so it is looked at rather than trusted.

`metadata/` is the copy and the store is the original, so it is pulled before it is pushed.
That is not a formality. By 1.1.0 the folder held a description the listing had since
replaced and a privacy policy URL pointing somewhere else, and a push from it would have
reverted both — silently, since a push says only that it succeeded. So: `asc metadata pull`,
then write, then `asc metadata push --dry-run` and read the plan. What a release is entitled
to change is `whatsNew` and `promotionalText`, which are the two fields App Store Connect
leaves empty when it carries a version's localizations forward. Anything else standing in
the plan is the stale copy talking.

The `asc-*` skills carry the detail, and `asc search "<what you want>"` finds the command.

The size of the iOS app
-----------------------

Three settings decide it, and none of them is content. The whole of `Assets` is under two
megabytes, so an app that measures in the tens of megabytes is measuring the engine and the
C++ il2cpp wrote for the managed code — which means a size question starts by measuring the
archive, never the assets. Measured on 1.0.0's own archive and two rebuilds of it:

| | `.app` | `UnityFramework` | `__LINKEDIT` | `il2cpp` section |
| --- | --- | --- | --- | --- |
| as submitted | 91.8 MiB | 81.4 MiB | 36.4 MiB | 22.2 MiB |
| with `STRIP_STYLE` | 57.7 MiB | 47.3 MiB | 0.6 MiB | 22.2 MiB |
| and `OptimizeSize` | 38.5 MiB | 28.1 MiB | 0.4 MiB | 8.9 MiB |

**`STRIP_STYLE = non-global`** on the UnityFramework target, written into the generated
project by `IosSymbolStripping.cs`, which argues it. It is a third of the app and it took no
decision, only a setting Unity's own template stopped supplying.

**`il2cppCodeGeneration = OptimizeSize`** for iPhone, in `ProjectSettings.asset`. The
argument is here rather than beside it because that file carries no comments. It is Unity's
"Faster (smaller) builds": il2cpp is handed `--generics-option=EnableFullSharing`, so one
shared implementation serves many generic instantiations instead of each being written out.
That is where the size is — the generated C++ fell from 392 MB to 172 MB, and the generic
instantiations in it from 158 MB to 11 MB, which is most of the change on its own.

What it costs is speed, and only managed speed: a shared implementation resolves its types
at run time rather than having them compiled in. That is a reading rather than a claim. Two
release players were built A/B off the same tree with nothing but this setting flipped and
driven on the iPad, three sessions each, best of five in-app passes: a synthetic run of
generic containers costs 4.1 times what it costs under `OptimizeSpeed` — 17.5 ms against
4.3 — the sample score scheduled for four laps 3.3 times, 3.6 against 1.1, and the format's
round trip 1.05 times, which is at the edge of the noise. Both controls measure the same to
three decimal places in every session: a managed float loop through `FmVoiceState` at
1.186 ms, and 256 blocks of the real mix through its Burst job at 151.0. So the DSP is
untouched, and so is managed code with no generic in it — what pays is generics, and the
sequencer's scheduling and the UI are what this app has made of them.

In the app that is 0.45 ms a frame. Main-thread frame time from `FrameTimingManager` over
twenty seconds of the sample score playing, the window opening ten seconds in so that
startup is not counted: 2033 ms against 1498 ms, **+36%**, with no overlap between the two
arms' three sessions. Nothing else moves — the frame interval's median stays at the device's
own 16.66 ms, its p99 at 16.8, and not one frame in six runs crosses 20 ms, the whole of the
main thread's work being 1.8 ms of a 16.66 ms budget. No dropout, no exception and no
restart in any run either. So the trade is the table's 19 MiB of app against under three
per cent of a frame that no frame timing can see, and the two things that would change that
answer are worth naming rather than leaving to be rediscovered: a score large enough for
scheduling to cost milliseconds, and a panel rebuilt every frame.

Two notes for whoever measures it next. `FrameTimingManager` reads zeros without
`enableFrameTimingStats`, which is off in `ProjectSettings.asset`, so the build entry has to
set it — for both arms, since it is not free. And the flag on the il2cpp command line does
not prove the arms differ: Bee replays cached il2cpp output, so both builds can return in
seconds and one of them be the other. What proves it is the volume — generated C++ at
169 MB against 401 MB, and `UnityFramework` at 47.9 MiB against 81.6 — the 81.6 being what
1.0.0's own framework measured before either setting, this configuration doing no stripping
of its own, so the loop closes with the first row of the table above rather than the second.

The same setting is on for the Web, where what it buys and what it costs are both different
— `Docs/impl-web.md` carries that measurement, including the figure this platform has no
equivalent of, the time a browser spends compiling what it was sent. The two app-level
percentages are not comparable, being different instruments: Chrome's main-thread busy time
there, Unity's own main-thread frame time here.

**`managedStrippingLevel = High`** for iPhone, also in `ProjectSettings.asset`, and already
there — it is not a lever left to pull. This is worth writing down because the file reads as
though it were: the dictionary holds an entry only for the platforms that have one, iPhone's
sits at the end of a list of a dozen consoles, and a glance at the top of it says the
platform is absent and therefore on the default. It is not, and this paragraph exists
because it was read that way once.

One-time setup on a machine
---------------------------

`Tools/package.sh` refuses to start without both of these, and says so.

**A Developer ID Application certificate.** Not the store's distribution certificate — that
one signs for Apple's side of a submission, this one signs software Apple never sees. Only
the Account Holder can issue it and only in the browser; the App Store Connect API refuses,
whatever the key's role. A CSR is kept at `~/.asc/signing/developer-id.csr`.

```sh
# https://developer.apple.com/account/resources/certificates
#   Developer ID Application → upload the CSR → download the .cer, then:
cd ~/.asc/signing
openssl x509 -inform DER -in developer-id.cer -out developer-id.pem
openssl pkcs12 -export -inkey developer-id.key -in developer-id.pem \
               -out developer-id.p12 -passout "pass:$(cat developer-id-p12-password)"
security import developer-id.p12 -k ~/Library/Keychains/login.keychain-db \
                -P "$(cat developer-id-p12-password)" -T /usr/bin/codesign -T /usr/bin/security
```

Keep the `.p12`. A certificate whose private key is only in one keychain is a certificate
that dies with the machine, and the account already carries one orphaned that way.

**A notarytool credential.** Stored in the keychain rather than a file, so that nothing in
the repository can hold it.

```sh
xcrun notarytool store-credentials jacquard-notary \
      --key ~/.asc/AuthKey_98ZW2JAUS3.p8 --key-id 98ZW2JAUS3 --issuer <issuer-uuid>
```

The issuer UUID is on the Keys page of App Store Connect. `asc` stores its own copy in the
keychain and will not print it.
