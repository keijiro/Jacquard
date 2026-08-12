using System;

namespace Jacquard {

// The one effect on the finished mix, as the project holds it.
//
// Four numbers, and the fourth is only there to leave headroom. What this is for is not
// keeping the peaks under one — a soft clip was already doing that, and doing it
// without a control — but the thing a limiter is actually reached for on a drum
// machine: pushing the mix into it hard enough that the loud parts hold still and the
// quiet ones come up behind them. A kick with an attack in front of it keeps its punch
// and everything under it ducks; a release short enough to recover inside a step gives
// the tail of a note a swell it did not have.
//
// So the control that matters is the drive. Threshold and ratio are the two a
// compressor usually offers, and neither is worth a bar here: the ratio is infinite,
// which is what makes this a limiter rather than something to be dialled in, and the
// threshold and the drive are the same knob read from opposite ends. Pushing the signal
// up into a fixed ceiling is the end a player thinks in, and it is the one that leaves
// the output where it was put rather than moving it every time the amount of squeeze
// changes.
//
// One of them, for the whole mix. Per channel limiting and a side chain are both a
// working answer to a real problem and both are more machinery than this prototype is
// asking for: what is wanted is a switch that makes the thing louder and harder, not a
// mixing desk.
//
// Two of the four are in decibels, which nothing else in this project is. A drive and a
// ceiling are ratios of amplitude, and the whole useful span of one is a few doublings:
// on a linear bar the difference between a gentle push and a hard one is a few pixels
// at the bottom, and every number on it reads as a multiplier nobody thinks in. The
// conversion to a gain is done once on the way to the audio thread.

public struct Limiter
{
    public float drive;   // How hard the mix is pushed into the ceiling, in dB
    public float ceiling; // What the output is held under, in dB below full scale
    public float attack;  // How long the gain takes to come down, in seconds
    public float release; // And how long it takes to let go again

    // Off, in the sense that matters: no drive and a ceiling at full scale leaves
    // everything under it untouched, so a project that never opens the Global panel
    // sounds exactly as it did before there was one. The two times are where a mix
    // would want them if it were switched on — fast enough to catch a peak, slow
    // enough to let the front of a kick through.
    public static Limiter Default => new Limiter
      { drive = 0.0f,
        ceiling = 0.0f,
        attack = 0.005f,
        release = 0.15f };

    // The two ends of each bar. They are here rather than in the UI for the reason
    // ParamTargets keeps the patch's ranges: what a number is useful over belongs with
    // the number, and the audio thread clamps against the same pair.
    public const float MaxDrive = 24.0f;
    public const float MinCeiling = -24.0f;

    public const float MinAttack = 0.0002f, MaxAttack = 0.05f;
    public const float MinRelease = 0.01f, MaxRelease = 1.0f;

    // Decibels as a gain. The only place either of the two is a multiplier.
    public static float Gain(float decibels) => MathF.Pow(10.0f, decibels / 20.0f);
}

} // namespace Jacquard
