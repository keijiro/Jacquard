using UnityEngine;

namespace Jacquard.App {

// Whether the three pages a first launch opens on have been put away for good.
//
// The app opens on a score with the plane, the transport row and nothing that says
// what to do with either. Three things have to be known before anything else works —
// that Play starts it, that another score is loaded from the right of the row, and
// that the rest is written in the guide — and this is the one bit of state behind
// them: whether they have already been read.
//
// Kept the way Audition is kept and not the way the visualizer is: there is no apply,
// because nothing has to be told when it moves. It is read once, while the screen is
// being assembled, and by nothing else — so a static over PlayerPrefs is the whole of
// it and the panel only writes it.
//
// Off unless it was turned on, which is the opposite of everything else here and is
// the point: what is remembered is somebody saying they have read it, and there is
// nothing to remember until they have. So a fresh install comes up on the pages, and
// so does the launch after it, and the one after that.
//
// Only the box writes this. Done on the last page closes the panel for the launch it is
// pressed on and leaves nothing behind — a player who read all three pages and did not
// tick the box has said nothing about the next launch.
// And there is no way back: nothing on the System panel resets it, because a switch
// for *show me the three pages again* would be a control on the machine's own panel
// for something a player does once and never revisits.
static class Onboarding
{
    public static bool Dismissed
    {
        get => PlayerPrefs.GetInt(Key, 0) != 0;

        // Written through as it is pressed rather than on the way out, for the reason
        // every other setting here is: a tablet app is not quit, it is put away and
        // then killed off screen. A player who ticks the box and then force-quits has
        // asked for something, and it is already on disk by then.
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    const string Key = "Jacquard.Onboarding";
}

} // namespace Jacquard.App
