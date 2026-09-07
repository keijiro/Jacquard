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
// This is the Android answer to a question every other platform answers with the score
// folder itself. Everywhere else the folder is reachable from outside — a desktop opens
// it in a file manager, iOS shows it in the Files app — and `Docs/impl-files.md` states
// the rule that follows: the folder is authoritative and the app is not. Android is the
// one platform where the folder cannot be handed over at all. Since API 30 the app's own
// directory under Android/data is hidden from the Files app, from the document picker and
// from MTP, and hidden even from a file manager holding MANAGE_EXTERNAL_STORAGE.
//
// So what stands in for it is one file at a time, through the picker the system does
// show: Export writes what is on the plane to wherever the user says, Import reads one
// back over the top of it. The other half is the .androidlib under Assets/Plugins/Android,
// whose activity argues its own shape; what belongs here is only the seam.
//
// An imported score lands in memory and nowhere else. Nothing here writes to the score
// folder, so the rule above still holds — on Android nothing outside the app can put a
// file into that folder, and an import is not the app pretending otherwise. What decides
// which slot an imported score ends up in is Save, the same as for a score just written.
//
// **Either button stops the sequence**, and that is not a bug to work around. Launching
// the picker backgrounds the app, which is OnApplicationPause, which is
// JacquardApp.GoQuietForTheBackground, which is Sequencer.Stop — and Stop ends in
// SettleIfIdle, so a pending switch lands and the editing lock is given back on the way
// out. There is therefore no lock to survive the trip and an imported score always
// arrives at a stopped sequencer. It is the rule the manual already states for leaving
// the app on a phone.

static class ScoreTransfer
{
    // Written as a property with the platform inside it so that nothing else has to know
    // the spelling — a compile time constant either way, and the panel simply does not
    // build the row where it is false. The same shape as DspBuffer.Supported, which is
    // where that argument is made.
    //
    // The editor is excluded, unlike the desktop foot on the same panel, and the
    // difference between the two conditions is the point. That row includes the editor
    // because Application.OpenURL works there whatever the target is, so the control can
    // be tried without a build. This one has nothing on the other end in the editor at
    // all: a row that appeared the moment the target was set to Android would be two
    // buttons that fail.
    public static bool Supported =>
#if UNITY_ANDROID && !UNITY_EDITOR
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
    // that is a perfectly good score written by an editor on a desktop.
    static string Strip(string text)
      => text != null && text.Length > 0 && text[0] == Bom ? text.Substring(1) : text;

#else

    // The other arm, which is every platform but Android and the editor on all of them.
    // It is here rather than left to an #if at the call site because the caller is
    // JacquardApp, and what this shape buys is that JacquardApp has no #if in it at all:
    // Export and Import are ordinary methods there, and the panel's row is the one thing
    // that knows the difference — at runtime, off Supported above.

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
