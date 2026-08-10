#if UNITY_WEBGL && !UNITY_EDITOR

using System.Runtime.InteropServices;

namespace Jacquard.App {

// The browser's audio output as seen from C#. The other half of it is
// Assets/Plugins/WebGL/JacquardAudioOut.jslib, and FmSynthWeb is the only caller.

static class WebAudioOut
{
    // Opens the output and returns the sample rate the browser chose — not a rate it
    // can be asked for, which is why nothing here decides one. Zero means there is no
    // Web Audio API to open.
    [DllImport("__Internal", EntryPoint = "JacquardAudioOpen")]
    public static extern int Open();

    // Frames pushed but not yet played. This is the clock: it is measured against the
    // audio context's own time, so it counts what has been consumed rather than how
    // long ago it was handed over.
    [DllImport("__Internal", EntryPoint = "JacquardAudioQueued")]
    public static extern int Queued();

    // Appends a block, to be played the instant the last one ends. The two sides go
    // over separately because that is how the Web Audio API stores them; interleaving
    // them here would only mean deinterleaving them there.
    [DllImport("__Internal", EntryPoint = "JacquardAudioPush")]
    public static extern void Push(float[] left, float[] right, int frames);

    [DllImport("__Internal", EntryPoint = "JacquardAudioClose")]
    public static extern void Close();
}

} // namespace Jacquard.App

#endif
