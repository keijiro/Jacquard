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

There are no tags yet, so the first release has no range to read: its notes describe the app
rather than what changed in it.

**The commit messages here are not release notes and must not be pasted into them.** This
repository states a change and its reason in one sentence, and that sentence is usually a
paragraph — the reason being why the code is shaped the way it is, which is exactly what
somebody deciding whether to download does not need. Read them for what happened, then write
short lines about what is different for somebody using the app.

Put the draft to the user and wait for it to come back. The notes are the part of a release
that is read the most and revised the least.

The archives
------------

```sh
Tools/package.sh              # build, sign, notarise, archive
Tools/package.sh --no-build   # reuse whatever is already in Build/
```

It leaves `Build/dist`: one zip per platform and a `SHA256SUMS` for both. The header of that
script argues the signing and the notarisation, and that argument is not repeated here.

Three things about the result are worth saying in the notes if the notes say anything at all:
the macOS archive is signed and notarised, so it opens without a warning; it is Apple silicon
only, so there is nothing on the page for an Intel Mac; and the Windows one is not signed, so
SmartScreen will call the publisher unknown until a certificate exists.

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
or not the version moved. 1.0.0 shipped as build 2 for that reason.

The path, as far as it has been walked:

- *Jacquard > Build iOS* writes the Xcode project. `BuildIos.cs` says why it is a menu item
  and why it appends rather than replaces.
- `asc xcode archive`, then `asc xcode export-options` and `asc xcode export`. The export
  needs manual signing with an `ExportOptions.plist`; automatic signing cannot export.
- `asc xcode validate` before uploading, `asc builds list` to watch it process.
- The encryption question is already answered in the build by `IosExportCompliance.cs`, so a
  build should not stall waiting for somebody to click through it.
- `asc validate` reports submission readiness. `asc publish appstore --submit` ships it.
- Expect to finish in the browser. Two declarations for 1.0.0 could not be verified through
  the API and had to be made in App Store Connect by hand.

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
at run time rather than having them compiled in. The DSP is unaffected, being Burst, and
what remains at risk is the sequencer's scheduling and the UI. Checked on the iPad rather
than reasoned about — four laps of the sample score twice over, a tile placed and stacked, a
score saved and read back, and six lane drags — the dropout detector counted none, the log
carried no exception, and the frame median stayed at the device's own 16.7 ms with a worst
frame of 48 ms during the drags, which is the UI Toolkit cost this project already knows
about. Should this app ever grow a managed hot path, this is the setting to question first.

The same setting is on for the Web, where what it buys and what it costs are both
different and are read off a measurement rather than an iPad — `Docs/impl-web.md` carries
that one, including the figure this platform has no equivalent of, the time a browser
spends compiling what it was sent.

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
