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
    public const int CarDecay = 7;
    public const int CarSustain = 8;
    public const int CarAttack = 9;

    public const int Count = 10;

    public static readonly string[] Names =
      { "Level", "Detune", "Gate", "Mod index", "Mod ratio",
        "Feedback", "Mod decay", "Car decay", "Car sustain", "Car attack" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "level", "detune", "gate", "index", "ratio",
        "feedback", "moddecay", "cardecay", "carsustain", "carattack" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. Detune is in semitones and Gate is a multiplier on the note's own
    // length; the rest are the oscillator and envelope units.
    public static float Min(int target) => target switch
    {
        Detune => -24.0f,
        ModRatio => 0.25f,
        Gate => 0.05f,
        ModDecay => 0.005f,
        CarDecay => 0.01f,
        CarAttack => 0.001f,
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
        CarDecay => 2.0f,
        CarSustain => 1.0f,
        CarAttack => 0.5f,
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
        ModDecay => patch.modulator.decay,
        CarDecay => patch.carrier.decay,
        CarSustain => patch.carrier.sustain,
        CarAttack => patch.carrier.attack,
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
            case ModDecay: patch.modulator.decay = value; break;
            case CarDecay: patch.carrier.decay = value; break;
            case CarSustain: patch.carrier.sustain = value; break;
            case CarAttack: patch.carrier.attack = value; break;
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
