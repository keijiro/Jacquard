using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Jacquard.Editor {

// Puts the saved scores where a hand can reach them on iOS.
//
// Every score is a file in one folder — see ProjectStore — and on a phone that folder
// is inside the app's sandbox, which nothing outside the app may look into unless the
// app says otherwise. Saying otherwise is two keys in the generated Info.plist, and it
// is both or neither: UIFileSharingEnabled is the older iTunes and Finder sharing
// switch, which the Files app takes as the permission to show the folder at all, and
// LSSupportsOpeningDocumentsInPlace is what makes the browser open the files where
// they lie instead of handing out copies of them. With the two of them the Documents
// directory — which is what Application.persistentDataPath is on this platform —
// appears under "On My iPhone" as the app, holding the Scores folder the app writes,
// and a score can be copied off the device, mailed, renamed or dropped back in. One
// dropped in is on the chooser as soon as it is next read, since that list is the
// folder read out rather than anything remembered.
//
// It is the same errand as the row on the System panel that opens the score folder in
// the desktop's file manager, which is compiled out here because a phone has no such
// thing to open. What a phone has is the Files app, and this is how it is let in.
//
// Done on the way out of a build rather than ticked in the player settings because
// neither key has a setting; the plist belongs to the generated Xcode project, and the
// project is generated afresh every build, so a hand-edit would last exactly one.
//
// Under UNITY_IOS in full, so that the Xcode plist API — which is in the iOS build
// support module and only referenced when that is the target — is never so much as
// named by a compile for anything else.

static class IosFileSharing
{
#if UNITY_IOS
    [PostProcessBuild]
    public static void AllowFileSharing(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        var file = System.IO.Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();

        plist.ReadFromFile(file);
        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
        plist.WriteToFile(file);
    }
#endif
}

} // namespace Jacquard.Editor
