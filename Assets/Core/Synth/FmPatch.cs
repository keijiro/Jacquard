using System;

namespace Jacquard {

// Exponential fades from 1 to 0 over x in [0,1], ported from the unity-sap-test
// prototype.
//
// Both are normalized so that they reach exactly 0 at x = 1, which is what lets a
// voice end in silence instead of being cut off, and a pitch envelope land on the
// note's own frequency instead of near it. A level is a pure function of the
// elapsed note time and the gate length, so a voice keeps no envelope state and
// needs no stage bookkeeping.
//
// Snap is the same shape with a far steeper curve: a tenth of the way in it is
// already down to a fifth of its depth. That is too abrupt for a level, which is
// why the amplitude envelopes do not use it, and exactly what a pitch envelope
// needs to come out as a thump rather than as an audible sweep.

static class FmCurve
{
    const float Curve = 5.0f;
    const float Tail = 0.006737947f; // exp(-Curve)

    const float SnapCurve = 16.0f;
    const float SnapTail = 1.1253517e-7f; // exp(-SnapCurve)

    public static float Fade(float x)
      => (FastMath.Exp(-Curve * x) - Tail) / (1.0f - Tail);

    public static float Snap(float x)
      => (FastMath.Exp(-SnapCurve * x) - SnapTail) / (1.0f - SnapTail);
}

// The timbre, held by the project.
//
// The synth stores no patch of its own: this is stamped into every note event as
// it is scheduled, which is also why a parameter lock can alter one note without
// disturbing anything else. gateScale is not an oscillator setting but lives here
// so that every lock target is a plain field of one struct — and every field is a
// lock target, which is what makes ParamTargets a list of these twelve and nothing
// else.
//
// The last two are neither, in the sense that they describe nothing about the
// oscillator: they are how much of the note goes to each of the two send effects,
// whose own settings belong to the project rather than to a timbre (SendFx). They
// are in the patch because a send is worth locking — a reverb on one note of a
// chord is exactly the accent a lock exists for — and because a send decided at
// note-on is a send that never has to be smoothed.
//
// The two operators get deliberately different envelope shapes, matching what
// each one actually does. The carrier gates the output, so it is an AR: rise,
// hold for the note, fall. The modulator only colours the tone, so it is a single
// decay from full depth, which is what gives a two operator patch its bite. The
// carrier always runs at the note frequency; only the modulator has a ratio.
//
// A third envelope moves the pitch itself, which is what turns this patch into a
// kick drum: a steep drop onto the note frequency is most of what a kick is. It
// is kept to two numbers, how far the pitch moves and how long it takes to get
// there, because a percussive sweep is over before any more detail than that
// could be heard.

public struct FmPatch
{
    public float level;      // Output level [0,1]
    public float gateScale;  // Multiplies the note's gate length

    public float modulatorRatio;  // Modulator frequency as a ratio of frequency
    public float modulationIndex; // Peak modulation depth in radians
    public float feedback;        // Modulator self-feedback depth in radians
    public float modulatorDecay;  // Time for the modulation to fall to zero

    public float carrierAttack;   // Time to reach full level (seconds)
    public float carrierRelease;  // Time to fall to silence after the gate

    public float pitchSweep;      // Depth of the pitch envelope in octaves,
                                  // negative to bend up into the note instead
    public float pitchDecay;      // Time for the pitch to arrive at frequency

    public float reverbSend;      // How much of the note reaches the reverb [0,1]
    public float delaySend;       // How much of it reaches the delay [0,1]

    // The pitch envelope starts out at no depth, so a fresh patch sounds like it
    // did before there was one, but with a decay already set to something a kick
    // would use: entering a depth is then enough to hear what it does. The sends
    // start silent for the same reason: a project that never opens the Send panel
    // sounds exactly as it did before there was one.
    public static FmPatch Default => new FmPatch
      { level = 0.8f,
        gateScale = 1.0f,
        modulatorRatio = 2.0f,
        modulationIndex = 3.0f,
        feedback = 0.0f,
        modulatorDecay = 0.12f,
        carrierAttack = 0.005f,
        carrierRelease = 0.12f,
        pitchSweep = 0.0f,
        pitchDecay = 0.05f,
        reverbSend = 0.0f,
        delaySend = 0.0f };
}

// A note-on event: the complete patch alongside pitch, timing and the exact
// sample to start on. Nothing about how it sounds is stored anywhere else.
//
// Note that level is an output level, not a velocity: nothing in here describes
// how a note was played, only what comes out. A tracker's velocity column would
// be one of the things that map onto it.

public struct FmNoteEvent
{
    public long startSample;
    public float frequency;
    public float level;    // Peak output level [0,1]
    public float duration; // Gate length in seconds; release follows it
    public int priority;   // Higher priority wins when voices are stolen

    public float modulatorRatio;
    public float modulationIndex;
    public float feedback;
    public float modulatorDecay;

    public float carrierAttack;
    public float carrierRelease;

    public float pitchSweep;
    public float pitchDecay;

    // Constant for the life of the voice, which is the whole reason a send needs no
    // smoothing: the gain a note is rendered at never moves under it.
    public float reverbSend;
    public float delaySend;

    // Total time the note occupies a voice, gate plus carrier release.
    public float TotalDuration => duration + carrierRelease;

    // Carrier level: rise over the attack, hold for the rest of the gate, then
    // release from whatever level was actually reached, so a note shorter than
    // its own attack still fades out without a discontinuity.
    public float CarrierLevel(float time)
    {
        if (time < duration) return AttackLevel(time);

        var t = time - duration;
        if (t >= carrierRelease) return 0.0f;

        return AttackLevel(duration) * FmCurve.Fade(t / carrierRelease);
    }

    float AttackLevel(float time)
      => time < carrierAttack ? time / carrierAttack : 1.0f;

    // Modulation depth: full at the note start, decaying to nothing. It ignores
    // the gate, so the tail of a long note settles into a plain sine, which is
    // the classic two operator behaviour.
    public float ModulatorLevel(float time)
      => time >= modulatorDecay ? 0.0f : FmCurve.Fade(time / modulatorDecay);

    // Pitch envelope, as a multiplier on the note frequency. Measured in octaves
    // rather than in Hz, so one setting bends every note by the same interval:
    // transposing a kick down does not flatten its sweep with it.
    //
    // Past the decay it is exactly 1, which also covers a decay of zero: the
    // pitch envelope is then simply off.
    public float PitchScale(float time)
      => time >= pitchDecay ? 1.0f
         : FastMath.Pow2(pitchSweep * FmCurve.Snap(time / pitchDecay));

    // Builds an event from a resolved patch. The patch has already had every
    // parameter lock applied to it by the time this is called.
    public static FmNoteEvent FromPatch(in FmPatch patch, int note,
                                        float gateSeconds, long startSample)
      => new FmNoteEvent
        { startSample = startSample,
          frequency = Pitch.ToFrequency(note),
          level = Math.Clamp(patch.level, 0.0f, 1.0f),
          duration = MathF.Max(gateSeconds * patch.gateScale, 0.005f),
          // Louder notes outrank quieter ones when the pool runs out of voices,
          // so an accent survives a dense chord.
          priority = (int)MathF.Round(Math.Clamp(patch.level, 0.0f, 1.0f) * 8.0f),
          modulatorRatio = patch.modulatorRatio,
          modulationIndex = patch.modulationIndex,
          feedback = patch.feedback,
          modulatorDecay = patch.modulatorDecay,
          carrierAttack = patch.carrierAttack,
          carrierRelease = patch.carrierRelease,
          pitchSweep = patch.pitchSweep,
          pitchDecay = patch.pitchDecay,
          reverbSend = patch.reverbSend,
          delaySend = patch.delaySend };
}

} // namespace Jacquard
