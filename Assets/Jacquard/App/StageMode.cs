using UnityEngine;

namespace Jacquard.App {

// Whether the app is being played rather than worked in.
//
// Everything on this screen is built for making a piece, and a few of those things have
// no business being reachable while one is being played to a room. Save writes over the
// score that is currently sounding. The guide sends the app to the background and puts a
// browser over the top of it, which on a tablet is the whole screen gone. The text field
// a double click opens on a value bar takes the keyboard, and while it has it the keys
// the plane answers to are going into a number instead. None of the three is dangerous at
// a desk, where a mistake costs an undo and a moment; all three are one stray press away
// from something a room can hear, and a hand on dark glass presses what it did not mean
// to.
//
// So this is one switch saying which of the two the app is doing, and what it suppresses
// is chosen by one test: a control goes if pressing it by accident during a performance
// costs something and a performance has no use for it. Which is why Save goes and Load
// stays — the next piece being loaded between numbers is part of playing a set, and
// nothing about a set writes a file. What this never touches is anything the piece is
// played with: the transport, the live effects, the plane, the bars as bars. A mode that
// made the instrument harder to play would be paying for the wrong risk.
//
// The double click on a bar's *name* is the one that had to be asked about rather than
// reasoned out, since it fails half the test loudly — a lock lets go of its target and a
// channel's sound jumps back to the patch, which is a change a room hears at once. It
// stays, because it fails the other half: it is played with. Dropping a lock mid-piece is
// a thing this instrument is performed with, so taking it away would cost a gesture a set
// is built on to save an accident. Which is the test working — the two halves are an and,
// and the second one is the one that protects the instrument from the mode.
//
// Kept the way Audition is kept — a static over PlayerPrefs, read where it is used — and
// for the same reason, that the value bar's answer is wanted at the instant a second
// click arrives and nowhere else. What is different is that two of the three are buttons
// which have to leave the row as the switch moves rather than the next time anybody
// builds one, so the System panel also hands the press on to JacquardUI. The state is
// here either way, and neither of them keeps a copy of it.
//
// Off unless it was turned on, and then remembered. That it is remembered is the part
// worth arguing: this is a mode with an end, so somebody can be left in it for weeks and
// be puzzled by an app with no Save button. What settles it is the ending a tablet app
// actually has, which is being put away and then killed off screen — a mode lost to that
// would be lost in the middle of the set it was turned on for, which is the one moment it
// exists for. What says it is still on is the row itself: Save is missing from between the
// score's name and Load, and that is a gap in a place the eye already knows.
static class StageMode
{
    public static bool On
    {
        get => PlayerPrefs.GetInt(Key, 0) != 0;

        // Written through as it is pressed rather than left for the quit, the same as
        // everything else the System panel keeps, and for the reason the paragraph above
        // ends on: the ending this app has is not a clean one.
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    const string Key = "Jacquard.StageMode";
}

} // namespace Jacquard.App
