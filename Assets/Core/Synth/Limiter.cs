using System;

namespace Jacquard {

// The one effect on the finished mix, as the project holds it.
//
// Three numbers, and only the first of them is played. What this is for is not keeping
// the peaks under one — a soft clip was already doing that, and doing it without a
// control — but the thing a limiter is actually reached for on a drum machine: squeezing
// the mix hard enough that the loud parts hold still and the quiet ones come up behind
// them. A kick with an attack in front of it keeps its punch and everything under it
// ducks; a release short enough to recover inside a step gives the tail of a note a
// swell it did not have.
//
// So the one control is the ceiling, and it is how far the mix is squeezed rather than
// where the output lands. Whatever the ceiling takes off is given straight back by a
// make-up gain of exactly its own size, worked out from it rather than set beside it —
// so the output stays at full scale and what the bar moves is how much of the mix had to
// be held down to get there. That is the whole of the arrangement: pull the ceiling down
// and the thing gets louder and harder together, which is what a hand reaching for this
// is after.
//
// It replaces a pair. There used to be a drive as well, pushing the mix up into a
// ceiling that held the output down where it was put, and the two were the same knob
// read from opposite ends: every useful setting had one of them parked while the other
// did the work, and a ceiling below the drive was the two of them fighting with the
// output quieter for it. Making the make-up automatic is what collapses them, since the
// only thing the drive was really for was getting the level back.
//
// The ratio is the other number a compressor usually offers and it is not worth a bar
// either: it is infinite, which is what makes this a limiter rather than something to be
// dialled in. The panel calls this one Threshold, since with the make-up automatic what
// the number decides is where limiting starts rather than where the output lands; the
// field keeps the name ceiling, because that is still literally the level the gain holds
// the mix under before the make-up gives it back.
//
// One of them, for the whole mix. Per channel limiting and a side chain are both a
// working answer to a real problem and both are more machinery than this prototype is
// asking for: what is wanted is a switch that makes the thing louder and harder, not a
// mixing desk.
//
// The ceiling is in decibels, which nothing else in this project is. It is a ratio of
// amplitude and it now runs over eight doublings: on a linear bar the difference between
// a gentle squeeze and a hard one would be a pixel or two at the very bottom, and every
// number on it would read as a multiplier nobody thinks in. The conversion to a gain, and
// to the make-up that answers it, is done once on the way to the audio thread.

public struct Limiter
{
    public float ceiling; // How hard the mix is squeezed, in dB below full scale
    public float attack;  // How long the gain takes to come down, in seconds
    public float release; // And how long it takes to let go again

    // Off, in the sense that matters: a ceiling at full scale has nothing to hold down
    // and a make-up of one, so a project that never opens the Global panel sounds
    // exactly as it did before there was one. The two times are where a mix would want
    // them if it were switched on — fast enough to catch a peak, slow enough to let the
    // front of a kick through.
    public static Limiter Default => new Limiter
      { ceiling = 0.0f,
        attack = 0.005f,
        release = 0.15f };

    // The two ends of each bar. They are here rather than in the UI for the reason
    // ParamTargets keeps the patch's ranges: what a number is useful over belongs with
    // the number, and the audio thread clamps against the same pair.
    //
    // The threshold reaches 48dB down, which is a make-up of 251 and far past the point
    // where a limiter is politely holding peaks: at the bottom of the bar everything in
    // the mix is above the threshold, the gain is doing nothing but tracking the loudest
    // thing present, and what is heard is the soft clip. That is the useful end of it
    // rather than a mistake to be guarded against — this is an instrument — and it is why
    // the far end of the bar is a sound and not a warning.
    public const float MinCeiling = -48.0f;

    public const float MinAttack = 0.0002f, MaxAttack = 0.05f;
    public const float MinRelease = 0.01f, MaxRelease = 1.0f;

    // Decibels as a gain. The only place either of the two is a multiplier.
    public static float Gain(float decibels) => MathF.Pow(10.0f, decibels / 20.0f);
}

} // namespace Jacquard
