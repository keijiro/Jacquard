using System;

namespace Jacquard {

// What a parameter lock can point at.
//
// sequencer.md leaves this set to the synth: the sequencer only carries an index
// and an amount, and everything about what the index means lives here alongside
// the patch it addresses. Adding a target is a one line change in three switches.

public static class ParamTargets
{
    public const int Level = 0;
    public const int Detune = 1;
    public const int Gate = 2;
    public const int ModIndex = 3;
    public const int ModRatio = 4;
    public const int Feedback = 5;
    public const int ModDecay = 6;
    public const int CarAttack = 7;
    public const int PitchSweep = 8;
    public const int PitchDecay = 9;

    public const int Count = 10;

    public static readonly string[] Names =
      { "Level", "Detune", "Gate", "Mod index", "Mod ratio",
        "Feedback", "Mod decay", "Car attack", "Pitch sweep", "Pitch decay" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "level", "detune", "gate", "index", "ratio",
        "feedback", "moddecay", "carattack", "pitchsweep", "pitchdecay" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. Detune is in semitones, Gate is a multiplier on the note's own
    // length and the pitch sweep is in octaves; the rest are the oscillator and
    // envelope units.
    public static float Min(int target) => target switch
    {
        Detune => -24.0f,
        ModRatio => 0.25f,
        Gate => 0.05f,
        ModDecay => 0.005f,
        CarAttack => 0.001f,
        PitchSweep => -4.0f,
        _ => 0.0f
    };

    public static float Max(int target) => target switch
    {
        Level => 1.0f,
        Detune => 24.0f,
        Gate => 4.0f,
        ModIndex => 12.0f,
        ModRatio => 8.0f,
        Feedback => 8.0f,
        ModDecay => 1.0f,
        CarAttack => 0.5f,
        PitchSweep => 4.0f,
        // Zero is a meaningful setting here, since it switches the envelope off,
        // so the range runs down to it rather than to a shortest useful sweep.
        PitchDecay => 0.5f,
        _ => 1.0f
    };

    public static float Get(in FmPatch patch, int target) => target switch
    {
        Level => patch.level,
        Detune => patch.detune,
        Gate => patch.gateScale,
        ModIndex => patch.modulationIndex,
        ModRatio => patch.modulatorRatio,
        Feedback => patch.feedback,
        ModDecay => patch.modulatorDecay,
        CarAttack => patch.carrierAttack,
        PitchSweep => patch.pitchSweep,
        PitchDecay => patch.pitchDecay,
        _ => 0.0f
    };

    public static void Set(ref FmPatch patch, int target, float value)
    {
        value = Math.Clamp(value, Min(target), Max(target));

        switch (target)
        {
            case Level: patch.level = value; break;
            case Detune: patch.detune = value; break;
            case Gate: patch.gateScale = value; break;
            case ModIndex: patch.modulationIndex = value; break;
            case ModRatio: patch.modulatorRatio = value; break;
            case Feedback: patch.feedback = value; break;
            case ModDecay: patch.modulatorDecay = value; break;
            case CarAttack: patch.carrierAttack = value; break;
            case PitchSweep: patch.pitchSweep = value; break;
            case PitchDecay: patch.pitchDecay = value; break;
        }
    }

    public static void Add(ref FmPatch patch, int target, float delta)
      => Set(ref patch, target, Get(in patch, target) + delta);

    public static float Default(int target) => Get(FmPatch.Default, target);

    // A sensible nudge for an inspector field, roughly a hundredth of the range.
    public static float Increment(int target)
      => target == Detune ? 1.0f : (Max(target) - Min(target)) / 100.0f;
}

} // namespace Jacquard
