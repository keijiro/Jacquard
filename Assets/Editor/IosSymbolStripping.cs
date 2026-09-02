using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Jacquard.Editor {

// Takes the engine's local symbols out of the framework that ships, which Unity's own
// project template used to ask for and the one it generates now does not.
//
// UnityFramework is where the whole player lives: the engine, linked in from the prebuilt
// UnityRuntime static archive, and every line of C++ that il2cpp wrote for the managed
// code. It is 89% of the app, and 34 MiB of it was a symbol table — 351,677 entries, all
// but 1,209 of them local, with a 29 MiB string table to name them. Nothing reads that at
// runtime, and no crash report wants it either: the DWARF is already carried off into the
// dSYM the archive keeps beside the app, and strip leaves alone the UUID that ties the two
// together, so a stack trace symbolises exactly as it did before.
//
// It is written here because the template stopped saying it. Unity 6 generates the iOS
// project from a Swift trampoline rather than the Objective-C one, and where the old
// template pinned STRIP_STYLE to non-global on this target in all four configurations, the
// new one names it nowhere — so Xcode's own default stands instead, which is `debugging`.
// That is strip -S: it drops debug information the linked binary never carried in the
// first place and keeps every local symbol. non-global is strip -x, which is what takes
// the 34 MiB, and it is safe on a dynamic library because the global symbols the dynamic
// linker resolves against are precisely the ones it keeps.
//
// The reason this was easy to miss is that the strip step does run — the app's own target
// comes out with 72 symbols. STRIP_STYLE is per target, and only the one that was left to
// the default kept its own.
//
// Done on the way out of a build rather than by hand in Xcode for the reason the two iOS
// plist post-processes beside this one give, and one more: a build setting typed into the
// window is a setting that lives outside the repository, shaped by whoever last opened it,
// which is the trap the Windows player's architecture was already caught in once.

static class IosSymbolStripping
{
#if UNITY_IOS
    [PostProcessBuild]
    public static void StripLocalSymbols(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        var file = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject();

        project.ReadFromFile(file);
        project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(),
                                 "STRIP_STYLE", "non-global");
        project.WriteToFile(file);
    }
#endif
}

} // namespace Jacquard.Editor
