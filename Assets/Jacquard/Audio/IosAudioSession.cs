#if UNITY_IOS && !UNITY_EDITOR

using System.Runtime.InteropServices;

namespace Jacquard.App {

// iOS's audio session as seen from C#. The other half is
// Assets/Plugins/iOS/JacquardAudioSession.mm, which is where the argument for all of
// it is, and FmSynthPipeline is the only caller.

static class IosAudioSession
{
    // Says that this app's audio is music: it plays through the silent switch, and it
    // mixes with whatever was already playing rather than stopping it. Said once at
    // startup and again by the far side whenever the system takes it back.
    //
    // False means iOS refused, which the far side logs with the reason. There is
    // nothing to be done about it from here — the app has no second way to be heard —
    // so the return is for a probe to read rather than for a caller to handle.
    [DllImport("__Internal", EntryPoint = "JacquardAudioSessionApply")]
    public static extern bool Apply();
}

} // namespace Jacquard.App

#endif
