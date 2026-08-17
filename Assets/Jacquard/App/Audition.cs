using UnityEngine;

namespace Jacquard.App {

// Whether an edit sounds the note it just made.
//
// Every audition on this screen goes through ScoreEditor.Preview, and this is the one
// thing that decides whether it comes out: a bar that has stopped moving, a tile just
// placed, a stack just pasted, a transpose from the keys. What is not here is the
// Return key, which is a hand asking for the note outright rather than an edit
// remarking on itself — see ScoreEditor.Sound.
//
// Kept the way the buffer size is kept and not the way the visualizer is: there is no
// apply, because nothing has to be told when this moves. It is read at the instant a
// note would sound and by nothing else, so a static over PlayerPrefs is the whole of
// it and the System panel only throws the switch.
//
// On unless it was turned off, which is the other way round from the visualizer and
// for a reason. That switch offers something the app was not doing; this one takes
// away something it already does, and a default of off would mean an app that went
// quiet on the launch after an update without anybody having asked it to.
static class Audition
{
    public static bool On
    {
        get => PlayerPrefs.GetInt(Key, 1) != 0;

        // Written through rather than left for the quit, the same as everything else
        // the System panel keeps: a tablet app is not quit, it is put away and then
        // killed off screen. There is no drag behind this one to spare the disk from,
        // which is what the buffer size needs a Flush for.
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    const string Key = "Jacquard.Audition";
}

} // namespace Jacquard.App
