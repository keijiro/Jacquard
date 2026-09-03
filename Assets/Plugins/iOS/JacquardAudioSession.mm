// What this app has to tell iOS about its own audio, which is that it is music.
//
// Its caller is Assets/Jacquard/Audio/IosAudioSession.cs, and the only thing that calls
// that is FmSynthPipeline — the driver, because this is as platform shaped as anything
// else in it.
//
// A device in silent mode plays nothing from an app whose AVAudioSession category is
// Ambient or SoloAmbient, and one of those two is where this app sits until it says
// otherwise — read off the iPad rather than assumed: SoloAmbient with no options at
// image load, before any engine code has run, and Ambient with mixWithOthers by the
// time managed Awake runs. Playback is the only category that ignores the switch.
// Nothing in Unity reaches it: there is no managed API for the category, and the player
// setting that looks like one — Mute Other Audio Sources — only chooses between the two
// Ambient categories, both of which go quiet. So these two calls are the whole of the
// fix, and this file exists because they have nowhere else to live.
//
// **After the engine and not before it**, which is why the caller is the driver rather
// than anything that runs earlier: the engine writes the category itself during its own
// audio init — that is the SoloAmbient to Ambient step above — so a category set before
// that step is a category the engine then overwrites.
//
// **mixWithOthers, because a sequencer is not the only thing that might be playing.**
// Bare Playback interrupts every other non-mixable session on the device, which means
// opening this app would stop the music somebody was already listening to — a rude
// thing for a toy to do, and worse here than in a game, since a person trying a rhythm
// against a track they have on is a use rather than a mistake. It is also what the
// engine had already chosen: the option is set both before and after this runs, so what
// changes on the device is the category alone and nothing about who else is heard. The
// player setting for that side of it — Mute Other Audio Sources, which is off — has
// nothing left to do, since it selected between the categories now overwritten, and
// this option is what carries the same intent past them.
//
// **The observers are insurance, and the measurement says so.** The category is
// process-wide state that other things in the process also write, and a version of this
// engine is on record for writing it at the wrong moment: Unity accepted a regression
// for 6000.0.73f1 in which the new UIScene lifecycle events reach FMOD's own reset and
// put the category back to Ambient on the way into the background. That does not happen
// here. Read at the two moments either side of a background trip, before anything of
// ours re-applied anything, it was Playback with mixWithOthers on the way out and the
// same on the way back, twice over, and no reading anywhere in a four-minute session
// after startup was anything but Playback. So nothing below keeps the category in force
// on this version — what it does is cost one call on the way back rather than leave a
// silent app to a patch version's whim.
//
// The interruption half earns its place differently, and this has not been measured:
// a session the system deactivates stays deactivated until it is asked for back, which
// is Apple's contract rather than a precaution, and a timer dismissed from the lock
// screen interrupts the session without ever pausing the app — so the app-side pause
// callback cannot be the only place this is said. Saying it again rather than asking
// first, because setting the category it already holds costs a call and no state change.

#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

static bool JacquardApplyPlaybackCategory(void)
{
    AVAudioSession* session = [AVAudioSession sharedInstance];

    NSError* categoryError = nil;
    BOOL categorySet = [session setCategory: AVAudioSessionCategoryPlayback
                               withOptions: AVAudioSessionCategoryOptionMixWithOthers
                                     error: &categoryError];

    // Again after every interruption, which is Apple's contract rather than a
    // precaution: a session the system deactivated stays deactivated until it is
    // asked for back.
    NSError* activationError = nil;
    BOOL activated = [session setActive: YES error: &activationError];

    if (!categorySet)
        NSLog(@"Jacquard: audio session refused Playback: %@", categoryError);
    if (!activated)
        NSLog(@"Jacquard: audio session would not activate: %@", activationError);

    return categorySet && activated;
}

// The two ways the category is taken away again, watched for once. Blocks on the main
// queue rather than a class with selectors: there is no state to keep, and this file
// has no other reason to declare a type.
static void JacquardWatchForWhatUndoesIt(void)
{
    NSNotificationCenter* centre = [NSNotificationCenter defaultCenter];

    [centre addObserverForName: AVAudioSessionInterruptionNotification
                       object: [AVAudioSession sharedInstance]
                        queue: [NSOperationQueue mainQueue]
                   usingBlock: ^(NSNotification* note)
    {
        NSNumber* type = note.userInfo[AVAudioSessionInterruptionTypeKey];
        if (type.unsignedIntegerValue == AVAudioSessionInterruptionTypeEnded)
            JacquardApplyPlaybackCategory();
    }];

    // On becoming active rather than on entering the foreground, because what is being
    // undone happens on the way out and the engine restarts its own audio on the way
    // back: this is the later of the two moments, and the one after which nothing else
    // of the engine's is still to run.
    [centre addObserverForName: UIApplicationDidBecomeActiveNotification
                       object: nil
                        queue: [NSOperationQueue mainQueue]
                   usingBlock: ^(NSNotification* note)
    {
        JacquardApplyPlaybackCategory();
    }];
}

extern "C" bool JacquardAudioSessionApply(void)
{
    static dispatch_once_t once;
    dispatch_once(&once, ^{ JacquardWatchForWhatUndoesIt(); });

    return JacquardApplyPlaybackCategory();
}
