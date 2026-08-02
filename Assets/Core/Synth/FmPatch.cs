using System;

namespace Jacquard {

// ADSR envelope, ported from the unity-sap-test prototype.
//
// The level is a pure function of elapsed note time and gate length, so a voice
// keeps no envelope state and needs no stage bookkeeping.

public struct FmEnvelope
{
    public float attack;  // Time to reach full level (seconds)
    public float decay;   // Time to fall from full level to sustain (seconds)
    public float sustain; // Sustain level [0,1]
    public float release; // Time to fall from the gate-off level to silence

    // Exponential fade from 1 to 0 over x in [0,1], normalized so that it hits
    // exactly 0 at x = 1: a voice is therefore guaranteed to end in silence
    // rather than being cut off.
    const float FadeCurve = 5.0f;
    const float FadeTail = 0.006737947f; // exp(-FadeCurve)

    static float Fade(float x)
      => (FastMath.Exp(-FadeCurve * x) - FadeTail) / (1.0f - FadeTail);

    public float LevelWhileGated(float time)
    {
        if (time < attack) return attack > 0.0f ? time / attack : 1.0f;
        var t = time - attack;
        if (t >= decay) return sustain;
        return sustain + (1.0f - sustain) * Fade(t / decay);
    }

    // Release always starts from the level actually reached at gate-off, so a
    // short note fades out of mid-attack without a discontinuity.
    public float Level(float time, float gate)
    {
        if (time < gate) return LevelWhileGated(time);
        var t = time - gate;
        if (t >= release) return 0.0f;
        return LevelWhileGated(gate) * Fade(t / release);
    }
}

// The timbre, held by the project.
//
// The synth stores no patch of its own: this is stamped into every note event as
// it is scheduled, which is also why a parameter lock can alter one note without
// disturbing anything else. detune and gateScale are not oscillator settings but
// live here so that all ten lock targets are plain fields of one struct.

public struct FmPatch
{
    public float level;      // Output level [0,1]
    public float detune;     // Pitch offset in semitones
    public float gateScale;  // Multiplies the note's gate length

    public float carrierRatio;
    public float modulatorRatio;
    public float modulationIndex; // Peak modulation depth in radians
    public float feedback;        // Modulator self-feedback depth in radians

    public FmEnvelope carrier;   // Shapes the output level
    public FmEnvelope modulator; // Shapes the modulation depth, i.e. the timbre

    public static FmPatch Default => new FmPatch
      { level = 0.8f,
        detune = 0.0f,
        gateScale = 1.0f,
        carrierRatio = 1.0f,
        modulatorRatio = 2.0f,
        modulationIndex = 3.0f,
        feedback = 0.0f,
        carrier = new FmEnvelope
          { attack = 0.005f, decay = 0.25f, sustain = 0.45f, release = 0.12f },
        modulator = new FmEnvelope
          { attack = 0.001f, decay = 0.12f, sustain = 0.2f, release = 0.05f } };
}

// A note-on event: the complete patch alongside pitch, timing and the exact
// sample to start on. Nothing about how it sounds is stored anywhere else.

public struct FmNoteEvent
{
    public long startSample;
    public float frequency;
    public float velocity;
    public float duration; // Gate length in seconds; release follows it
    public int priority;   // Higher priority wins when voices are stolen

    public float carrierRatio;
    public float modulatorRatio;
    public float modulationIndex;
    public float feedback;

    public FmEnvelope carrier;
    public FmEnvelope modulator;

    // Total time the note occupies a voice, gate plus carrier release.
    public float TotalDuration => duration + carrier.release;

    // Builds an event from a resolved patch. The patch has already had every
    // parameter lock applied to it by the time this is called.
    public static FmNoteEvent FromPatch(in FmPatch patch, int note,
                                        float gateSeconds, long startSample)
      => new FmNoteEvent
        { startSample = startSample,
          frequency = Pitch.ToFrequency(note + patch.detune),
          velocity = Math.Clamp(patch.level, 0.0f, 1.0f),
          duration = MathF.Max(gateSeconds * patch.gateScale, 0.005f),
          // Louder notes outrank quieter ones when the pool runs out of voices,
          // so an accent survives a dense chord.
          priority = (int)MathF.Round(Math.Clamp(patch.level, 0.0f, 1.0f) * 8.0f),
          carrierRatio = patch.carrierRatio,
          modulatorRatio = patch.modulatorRatio,
          modulationIndex = patch.modulationIndex,
          feedback = patch.feedback,
          carrier = patch.carrier,
          modulator = patch.modulator };
}

} // namespace Jacquard
