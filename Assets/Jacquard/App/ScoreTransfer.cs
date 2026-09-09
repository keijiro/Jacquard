using System.Runtime.InteropServices;
using UnityEngine;

namespace Jacquard.App {

// What a transfer has come back with, which is what the app has to say about it.
//
// Outside the platform gate below, because it is what the caller switches on and the
// caller has no #if in it.
enum Transfer
{
    // Still out. The picker is up, or the app is in the background behind it.
    Waiting,
    Exported,
    Imported,
    // The picker was backed out of, which is a decision and not a failure.
    Cancelled,
    Failed
}

// One score out to a place the user picks, or one score in from a file they pick.
//
// This is the answer, on the two platforms that have no other, to a question every
// other platform answers with the score folder itself. Everywhere else the folder is
// reachable from outside — a desktop opens it in a file manager, iOS shows it in the
// Files app — and `Docs/impl-files.md` states the rule that follows: the folder is
// authoritative and the app is not. Two platforms cannot hand it over at all, and the
// reason is different on each. On Android, since API 30, the app's own directory under
// Android/data is hidden from the Files app, from the document picker and from MTP, and
// hidden even from a file manager holding MANAGE_EXTERNAL_STORAGE. On the Web
// persistentDataPath is a mount on IndexedDB: it is a real path to the runtime and to
// nothing else on the machine, so there is no folder to show anybody and no file
// anybody can drop into it.
//
// So what stands in for it, on both, is one file at a time through the picker the
// platform does show: Export writes what is on the plane to wherever the user says,
// Import reads one back over the top of it. The other halves are the .androidlib under
// Assets/Plugins/Android and JacquardScoreTransfer.jslib under Assets/Plugins/WebGL,
// each of which argues its own shape; what belongs here is only the seam.
//
// An imported score lands in memory and nowhere else. Nothing here writes to the score
// folder, so the rule above still holds — nothing outside the app can put a file into
// that folder on either platform, and an import is not the app pretending otherwise.
// What decides which slot an imported score ends up in is Save, the same as for a score
// just written.
//
// **Whether the sequence stops depends on where the picker comes up**, and the
// difference is the one thing about this pair that is not the same on both. On Android
// either button backgrounds the app, which is OnApplicationPause, which is
// JacquardApp.GoQuietForTheBackground, which is Sequencer.Stop — and Stop ends in
// SettleIfIdle, so a pending switch lands and the editing lock is given back on the way
// out. An imported score therefore always arrives at a stopped sequencer there. A
// browser on a phone may hide the page for its picker and that account holds; a desktop
// browser's file dialog does not hide the tab at all, so nothing pauses, Stop is never
// reached, the lock is still on and the score arrives at a sequencer that is still
// running. The Web has both behaviours and this needs nothing from either end of the
// seam: SwitchTo overwrites the one pending score — *"There is one next, so asking
// twice is asking once"* — so the worst a file arriving late can do is take the place of
// a switch that had not landed yet, which is the guarantee the sequencer already makes
// and the one Load, gated at press time in exactly the same way, already stands on.

static class ScoreTransfer
{
    // Written as a property with the platform inside it so that nothing else has to know
    // the spelling — a compile time constant either way, and the panel simply does not
    // build the row where it is false. The same shape as DspBuffer.Supported, which is
    // where that argument is made.
    //
    // The editor is excluded on both, unlike the desktop foot on the same panel, and the
    // difference between the two conditions is the point. That row includes the editor
    // because Application.OpenURL works there whatever the target is, so the control can
    // be tried without a build. This one has nothing on the other end in the editor at
    // all: the Java side is a picker on a phone and the jslib is not linked into an
    // editor player in the first place, so a row that appeared the moment the target was
    // set would be two buttons that fail.
    public static bool Supported =>
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
      true;
#else
      false;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR

    // Both of these are a press and nothing more: what comes back comes back through
    // Poll, whenever it comes.
    //
    // A second press while one is out is dropped. The picker is a modal screen belonging
    // to another app, so there is no way to make one — but the app is coming back to the
    // front behind it in the process-death case, and a button that started a second
    // transfer then would be launching a picker the first one is still waiting on.
    public static void Export(string name, string text) => Begin(true, name, text);

    public static void Import() => Begin(false, null, null);

    // Once a frame, and the first line is what makes that cheap: a frame with nothing
    // out costs no JNI call at all. Whether one is out is kept here rather than asked
    // of Java for exactly that reason.
    public static Transfer Poll(out string name, out string text, out string problem)
    {
        name = text = problem = null;

        if (!_busy) return Transfer.Waiting;

        var state = (Transfer)Proxy.CallStatic<int>("poll");

        if (state == Transfer.Waiting) return Transfer.Waiting;

        name = Proxy.CallStatic<string>("name");
        text = Strip(Proxy.CallStatic<string>("text"));
        problem = Proxy.CallStatic<string>("problem");

        Proxy.CallStatic("clear");
        _busy = false;

        return state;
    }

    // Private members

    static AndroidJavaClass _proxy;
    static bool _busy;

    // Held for the life of the app rather than made per transfer: it is a class
    // reference and finding it is the expensive half of a JNI call.
    //
    // AndroidJavaClass and the rest of the JNI surface arrive with
    // com.unity.modules.androidjni, which the package manifest here had to be given.
    // The built-in modules in this project are pruned to the ones actually used, and a
    // pruned one does not read as a missing package: UnityEngine.dll forwards the type
    // to an assembly nothing references, so what the compiler says is that the name is
    // not in the namespace. It is added for every platform, since a manifest has no
    // per-target arm, and it is a small module.
    static AndroidJavaClass Proxy =>
      _proxy ??= new AndroidJavaClass(
        "jp.radiumsoftware.jacquard.scoretransfer.ScoreTransferActivity");

    // AndroidApplication.currentActivity and not UnityPlayer.currentActivity. The
    // second is the road from before GameActivity, which is what this project builds
    // with (AndroidApplicationEntry is set to it), and the first is the one Unity now
    // answers for either entry point.
    //
    // The Java side takes the activity, builds the intent and launches it, so this is
    // one crossing. It sets its own state to waiting before it launches, so a picker
    // that cannot be reached is a failure the first poll finds rather than a wait with
    // no end.
    static void Begin(bool export, string name, string text)
    {
        if (_busy) return;

        _busy = true;

        Proxy.CallStatic("begin", UnityEngine.Android.AndroidApplication.currentActivity,
                         export, name, text);
    }

    const char Bom = '\uFEFF';

    // The byte order mark, off the front of an imported score.
    //
    // File.ReadAllText eats one, so no score that came off disk has ever reached the
    // format reader with it attached. This is the one road in that does not go through
    // File, and the reader would see U+FEFF followed by `jacquard` and refuse a file
    // that is a perfectly good score written by an editor on a desktop. The Web arm
    // needs no equivalent: Blob.text() decodes by the Encoding Standard, which takes a
    // leading mark off as part of decoding.
    static string Strip(string text)
      => text != null && text.Length > 0 && text[0] == Bom ? text.Substring(1) : text;

#elif UNITY_WEBGL && !UNITY_EDITOR

    // The same two presses over the same seam, against JacquardScoreTransfer.jslib.
    // What the browser makes of them — why the picker is opened with showPicker(), why
    // a download needs no gesture at all, and why a second Import supersedes the first
    // where Android drops it — is that file's argument and not this one's.
    //
    // Export drops a second press and Import does not, which is the one asymmetry to
    // carry across the seam: there is one outcome slot, so an export that is still out
    // has to keep it, while an import is free to take it because the picker it
    // supersedes gives its answer to nobody.
    public static void Export(string name, string text)
    {
        if (_busy) return;

        _busy = true;

        JacquardTransferExport(name, text);
    }

    public static void Import()
    {
        _busy = true;

        JacquardTransferImport();
    }

    // Once a frame, and the first line buys the same thing it buys on Android: a frame
    // with nothing out costs no call into JavaScript at all.
    public static Transfer Poll(out string name, out string text, out string problem)
    {
        name = text = problem = null;

        if (!_busy) return Transfer.Waiting;

        var state = (Transfer)JacquardTransferPoll();

        if (state == Transfer.Waiting) return Transfer.Waiting;

        name = Marshal.PtrToStringUTF8(JacquardTransferName());
        text = Marshal.PtrToStringUTF8(JacquardTransferText());
        problem = Marshal.PtrToStringUTF8(JacquardTransferProblem());

        JacquardTransferClear();
        _busy = false;

        return state;
    }

    // Private members

    static bool _busy;

    // Going out is the easy direction: a string parameter arrives in the jslib as a
    // pointer into the heap and UTF8ToString reads it there.
    //
    // Coming back is the question, and the answer is a pointer read by hand. Declaring
    // these three as returning string instead would have IL2CPP's marshaller copy the
    // UTF-8 into a managed string and then leave the native buffer where it is, since
    // it has no way to know who owns it — a few kilobytes per import, bounded by
    // presses, but a leak with nobody to answer for it. So the jslib keeps the three
    // buffers, hands over pointers, and frees them in the clear above, which already
    // existed as the mirror of the Java side's clear() and is already called at exactly
    // the moment the outcome has been taken. No extra crossing and no ownership left
    // open. It is the first place in this project that passes a string back this way —
    // the audio jslib's pointers go the other direction, into HEAPF32.
    [DllImport("__Internal")]
    static extern void JacquardTransferExport(string name, string text);

    [DllImport("__Internal")]
    static extern void JacquardTransferImport();

    [DllImport("__Internal")]
    static extern int JacquardTransferPoll();

    [DllImport("__Internal")]
    static extern System.IntPtr JacquardTransferName();

    [DllImport("__Internal")]
    static extern System.IntPtr JacquardTransferText();

    [DllImport("__Internal")]
    static extern System.IntPtr JacquardTransferProblem();

    [DllImport("__Internal")]
    static extern void JacquardTransferClear();

#else

    // The third arm, which is every platform that can hand its score folder over and
    // the editor on all of them. It is here rather than left to an #if at the call site
    // because the caller is JacquardApp, and what this shape buys is that JacquardApp
    // has no #if in it at all: Export and Import are ordinary methods there, and the
    // panel's row is the one thing that knows the difference — at runtime, off Supported
    // above.

    public static void Export(string name, string text) { }

    public static void Import() { }

    public static Transfer Poll(out string name, out string text, out string problem)
    {
        name = text = problem = null;
        return Transfer.Waiting;
    }

#endif
}

} // namespace Jacquard.App
