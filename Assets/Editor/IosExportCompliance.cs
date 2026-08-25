using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Jacquard.Editor {

// Answers Apple's encryption question in the build, so that nobody has to answer it by
// hand again.
//
// Every build uploaded to App Store Connect is asked whether it uses encryption that is
// not exempt from US export regulations. For this app the answer is plainly no: it opens
// no network connection at all — the one URL it ever hands out is a local file:// path
// for revealing the score folder on a desktop — and nothing in it encrypts anything.
//
// The cost of leaving that unsaid is not a rejection but a stall. A build arriving
// without ITSAppUsesNonExemptEncryption turns the question into a prompt on the build in
// App Store Connect, and until somebody clicks through it the build cannot be given to
// TestFlight or attached to a submission. That is once per upload, forever, for an answer
// that will never change while the app makes no connections.
//
// So it is written into the plist instead, where the build carries its own answer.
// Should the app ever gain something that encrypts — a sync, an export to a service —
// this key is the first thing to revisit, since the honest answer would change with it.
//
// Done on the way out of a build for the same reason as IosFileSharing: the key has no
// player setting, the plist belongs to the generated Xcode project, and the project is
// generated afresh every build, so a hand-edit would last exactly one.

static class IosExportCompliance
{
#if UNITY_IOS
    [PostProcessBuild]
    public static void DeclareNoNonExemptEncryption(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        var file = System.IO.Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();

        plist.ReadFromFile(file);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(file);
    }
#endif
}

} // namespace Jacquard.Editor
