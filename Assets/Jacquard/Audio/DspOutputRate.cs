using UnityEngine;

namespace Jacquard.App {

// What rate the audio system runs its output at.
//
// On a desktop this is not a choice and there is nothing here to improve on: the audio
// system takes the rate the output device is already clocked at, and asking for another
// one only puts a resampler between the mix and the speaker. On a phone and a tablet it
// is a choice, because Unity does not take the device's rate — it hands out 24000 Hz
// whatever the hardware is doing, a figure chosen for a game's sound effects. Read off
// the iPad, which is what makes it a waste rather than a limit: the app boots at 24000
// while its own AVAudioSession is running at 48000, so the low rate is a resample on the
// way out rather than anything the hardware asked for.
//
// **What that costs here is not the top of the band, it is aliasing.** Losing everything
// above 12kHz would be the polite version of the trade and is nearly arguable for a
// sequencer. But this is an FM synth with no band limiting anywhere in it — FmVoicePool
// sums a carrier and its modulator and nothing looks at where the sidebands land — and
// FM's whole character is sidebands that go on past the carrier as the index rises. What
// folds does not come back as a dull note; it comes back as an inharmonic one that moves
// the wrong way when the pitch does.
//
// Read off the synth rather than argued: one note at 440Hz through the real core, the
// modulator seven times the carrier at an index of eight — inside the panel's own bars,
// which reach a ratio of eight and an index of twelve — rendered at three rates, and
// everything in the audible band that is not one of that note's own partials counted as
// stray against the loudest line in it.
//
//     96000Hz   loudest stray  -51.9dB    stray energy in band  -51.8dB
//     48000Hz   loudest stray  -29.6dB    stray energy in band  -32.0dB
//     24000Hz   loudest stray    0.0dB    stray energy in band   -1.8dB
//
// 96000 is the control and says what the measurement's own floor is. What the last row
// means is that at 24000 the loudest thing in the spectrum is not the note but a fold —
// 0.0dB is a stray line standing level with the highest partial, at 5960Hz, where the note
// has nothing — and that the stray content as a whole sits 1.8dB under everything in the
// band put together. 48000 does not abolish folding, since nothing here band limits; it
// puts what folds thirty decibels down, which is the distance between a character and a
// fault.
//
// **It is applied through the Reset that DspBuffer already makes**, not through a second
// one: a Reset reinitializes the output, and one at startup is enough. See DspBuffer.Apply
// for why that moment is the right one and why nothing happens in the ordinary case.
//
// **It is also what moved the buffer, and that is not a detail.** A frame count is a
// deadline in time only once a rate is known, so doubling the rate under the 256 frames
// this project used to ship would have halved what the audio thread has — and on the iPad
// the shorter deadline is one the thread does not hold. That is where DspBuffer.Default's
// 512 came from. The device ends up on the deadline it always had, and the rate is paid
// for in DSP alone.
//
// **Not AudioManager.asset**, which is where the buffer's default is and would be the
// obvious home. That file holds one figure for every platform, so 48000 there would also
// take a Mac running its interface at 44.1kHz off its own rate — and Unity's own manual
// says of it that "this only serves as a reference only, since certain platforms allow you
// to change the sample rate, such as iOS or Android", which is exactly the two platforms
// this is for. What settles it is that a Reset answers: the call reports a refusal, the
// rate can be read back afterwards, and DspBuffer.Apply writes down what it got. A setting
// that cannot be checked is one that will be believed while it is doing nothing.
//
// **Not a setting on the System panel**, which is the other thing it could be mistaken
// for. The buffer length is there because it belongs to the machine — a fast tablet and
// a busy one want different numbers out of the same score. A rate below the hardware's
// own belongs to nobody: there is no machine on which 24000 is the better answer, only
// one on which it is the cheaper one, and the cost of the rate is the DSP, which the
// iPad has room for — five minutes of the sample score at the doubled rate, twenty-one
// voices at the peak, six and a half thousand notes and not one of them dropped, stolen
// or late.

static class DspOutputRate
{
    // What to ask for, or zero to leave the device on whatever it chose.
    //
    // The editor is excluded even when it is building for one of these platforms: its
    // audio is the Mac's audio, so it never has the low default this is here to replace,
    // and the request would only be the resample the desktop case exists to avoid.
    public const int Desired =
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
      48000;
#else
      0;
#endif

    // What is actually in force, which is the only thing worth believing: a device is
    // free to grant something else, and Android in particular still has 44100 hardware
    // on which 48000 is the resample rather than the fix.
    public static int Current => AudioSettings.outputSampleRate;
}

} // namespace Jacquard.App
