#!/bin/bash
#
# The two desktop platforms, from source to a pair of archives that can be handed to
# somebody. What it produces is Build/dist: one zip per platform and the checksums for both.
#
# It stops there. Deciding the version, writing the notes and creating the release are the
# procedure in Docs/releasing.md, which is a conversation with somebody rather than a script:
# a version is a judgement, notes are prose, and a published release is the one step here
# that cannot be taken back quietly — an artefact that has been downloaded has been
# downloaded. So nothing below this line talks to GitHub, and this can be run as often as it
# is useful without anything leaving the machine except the notarisation.
#
# The stages are one script because they are one argument to each other and nothing else
# calls them: what the notarisation submits is what the signing produced, and what the
# checksums name is what the packaging wrote. Splitting them would mean agreeing on those
# names in three places instead of one.
#
# The security work is the substance of the macOS half. An unsigned app off the internet
# carries a quarantine flag, and since macOS 15 the way past it is no longer a right click:
# the reader has to go down into System Settings, which is a thing most will read as "it does
# not work". So the bundle is signed with a Developer ID, hardened, timestamped, sent to
# Apple to be notarised, and stapled — stapled because that is what lets Gatekeeper clear it
# without asking the network, which is the difference between opening and appearing to hang.
#
# The Windows half has no equivalent yet: a code signing certificate's private key has had to
# sit on hardware since 2023, so there is no file to sign with here. The zip goes out
# unsigned and SmartScreen will say so. When a certificate exists this is where signtool goes.
#
# Usage:  Tools/package.sh              build, sign, notarise, archive
#         Tools/package.sh --no-build   reuse whatever is already in Build/
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD="$ROOT/Build"
DIST="$BUILD/dist"
NOTARY_PROFILE="jacquard-notary"

# The builds are written relative to the project, so the script owns where it stands rather
# than inheriting it from whoever called.
cd "$ROOT"

MAC_APP="$BUILD/macOS/Jacquard.app"
WIN_DIR="$BUILD/Windows"

SKIP_BUILD=0
[ "${1:-}" = "--no-build" ] && SKIP_BUILD=1

say() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[31merror: %s\033[0m\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- preflight

VERSION=$(awk '/^  bundleVersion:/ {print $2; exit}' "$ROOT/ProjectSettings/ProjectSettings.asset")
[ -n "$VERSION" ] || die "could not read bundleVersion from ProjectSettings.asset"

EDITOR_VERSION=$(awk '/^m_EditorVersion:/ {print $2; exit}' "$ROOT/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$EDITOR_VERSION/Unity.app/Contents/MacOS/Unity"
[ -x "$UNITY" ] || die "no editor at $UNITY"

say "Jacquard $VERSION  (editor $EDITOR_VERSION)"

# The signing identity. Note this is Developer ID Application and not the App Store's
# distribution certificate: the store signs on Apple's side, this signs on ours.
IDENTITY=$(security find-identity -v -p codesigning \
           | awk -F'"' '/Developer ID Application/ {print $2; exit}')
[ -n "$IDENTITY" ] || die "no Developer ID Application identity in the keychain.
  Only the Account Holder can issue one, and only in the browser:
    https://developer.apple.com/account/resources/certificates
    Developer ID Application, upload ~/.asc/signing/developer-id.csr, download the .cer
  Then bring the pair into the keychain — see Docs/releasing.md."

# The notarisation credentials, which are a keychain item rather than a file so that the
# script never holds them.
if ! xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" >/dev/null 2>&1; then
    die "no notarytool profile '$NOTARY_PROFILE' — see Docs/releasing.md."
fi

if pgrep -f "Unity.app/Contents/MacOS/Unity.*$ROOT" >/dev/null 2>&1; then
    die "an editor is open on this project — batch mode cannot take the project lock"
fi

echo "  identity: $IDENTITY"

# ---------------------------------------------------------------- build

if [ "$SKIP_BUILD" = 0 ]; then
    mkdir -p "$BUILD/logs"
    for arm in "BuildMac macOS" "BuildWindows Windows"; do
        set -- $arm
        say "building $2"
        rm -rf "${BUILD:?}/$2"
        "$UNITY" -batchmode -quit -projectPath "$ROOT" \
                 -executeMethod "Jacquard.Editor.BuildDesktop.$1" \
                 -logFile "$BUILD/logs/$2.log" \
            || die "the $2 build failed — see $BUILD/logs/$2.log"
    done
else
    say "reusing Build/ as it stands"
fi

[ -d "$MAC_APP" ] || die "no app bundle at $MAC_APP"
[ -f "$WIN_DIR/Jacquard.exe" ] || die "no player at $WIN_DIR/Jacquard.exe"

rm -rf "$DIST"; mkdir -p "$DIST"

# ---------------------------------------------------------------- macOS

say "signing $MAC_APP"

# Inside out: a bundle's own signature covers what is under it, so anything signed after it
# invalidates it. Apple deprecated --deep for exactly this reason — it signs in an order it
# picks rather than the one the nesting asks for.
while IFS= read -r -d '' nested; do
    echo "  $(basename "$nested")"
    codesign --force --timestamp --options runtime --sign "$IDENTITY" "$nested"
done < <(find "$MAC_APP/Contents" \( -name '*.dylib' -o -name '*.bundle' -o -name '*.so' \) -print0)

codesign --force --timestamp --options runtime --sign "$IDENTITY" "$MAC_APP"
codesign --verify --strict --verbose=2 "$MAC_APP"

# The zip that goes to Apple is not the zip that ships: a ticket staples to the bundle, and
# the bundle has to be packed again afterwards to carry it.
say "notarising"
ditto -c -k --sequesterRsrc --keepParent "$MAC_APP" "$BUILD/notarize.zip"
xcrun notarytool submit "$BUILD/notarize.zip" --keychain-profile "$NOTARY_PROFILE" --wait
rm -f "$BUILD/notarize.zip"

xcrun stapler staple "$MAC_APP"
xcrun stapler validate "$MAC_APP"

# What a downloader's Mac will actually decide — and it has to be read rather than trusted to
# an exit code. On the machine that signed the bundle spctl accepts a Developer ID that was
# never notarised at all, because the certificate is sitting right here; the word that
# separates the two cases is in the output and nowhere else.
ASSESS=$(spctl --assess --type exec --verbose=4 "$MAC_APP" 2>&1)
echo "$ASSESS"
grep -q "source=Notarized Developer ID" <<<"$ASSESS" \
    || die "Gatekeeper does not see this as notarised — a download would be warned about"

# Named for the chip, the way the Windows archive beside it is. The Mac player is arm64
# alone, and a name that says so is the last place a reader on an Intel Mac can be told
# before the download rather than by an app that will not open.
MAC_ZIP="$DIST/Jacquard-$VERSION-macOS-arm64.zip"
ditto -c -k --sequesterRsrc --keepParent "$MAC_APP" "$MAC_ZIP"

# ---------------------------------------------------------------- Windows

say "packaging Windows"

# Burst leaves its debug information beside the player in a folder that says in its own name
# that it must not ship. The back-up folder is matched too: this build is Mono and does not
# produce one, but the macOS build beside it does, and a pattern that costs nothing is worth
# more than the day the backend changes and nobody looks. Staged rather than filtered at the
# zip, so that what is packed is a directory somebody can look at first.
#
# The macOS zip needs none of this — ditto --keepParent packs the bundle alone, and both
# folders are siblings of it rather than anything inside.
STAGE="$BUILD/stage/Jacquard-$VERSION-windows-x64"
rm -rf "$BUILD/stage"; mkdir -p "$STAGE"
(cd "$WIN_DIR" && for item in *; do
    case "$item" in
        *_DoNotShip|*_BackUpThisFolder_ButDontShipItWithYourGame) echo "  skipping $item" ;;
        *) cp -R "$item" "$STAGE/" ;;
    esac
done)

WIN_ZIP="$DIST/Jacquard-$VERSION-windows-x64.zip"
(cd "$BUILD/stage" && zip -qr -X "$WIN_ZIP" "$(basename "$STAGE")")
rm -rf "$BUILD/stage"

# ---------------------------------------------------------------- checksums

say "checksums"
(cd "$DIST" && shasum -a 256 ./*.zip | sed 's|\./||' > SHA256SUMS && cat SHA256SUMS)

say "ready in Build/dist — Docs/releasing.md takes it from here"
ls -lh "$DIST"
