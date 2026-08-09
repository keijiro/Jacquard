using System;

namespace Jacquard {

// What a parameter lock can point at.
//
// sequencer.md leaves this set to the synth: the sequencer only carries an index
// and an amount, and everything about what the index means lives here alongside
// the patch it addresses. Adding a target is a one line change in three switches,
// or in two of them for anything whose useful range is the zero to one both default
// to.
//
// The set is exactly the fields of FmPatch, so there is no parameter a lock cannot
// reach and no section a panel has to keep for the ones it cannot. Note that the
// two sends are in it while the effects they feed are not: how much of a note goes
// to the reverb is a property of that note, and what the reverb then does with it
// is a property of the project.

public static class ParamTargets
{
    public const int Level = 0;
    public const int Gate = 1;
    public const int ModIndex = 2;
    public const int ModRatio = 3;
    public const int Feedback = 4;
    public const int ModDecay = 5;
    public const int CarAttack = 6;
    public const int CarRelease = 7;
    public const int PitchSweep = 8;
    public const int PitchDecay = 9;
    public const int ReverbSend = 10;
    public const int DelaySend = 11;

    public const int Count = 12;

    public static readonly string[] Names =
      { "Level", "Gate ratio", "Mod index", "Mod ratio", "Feedback",
        "Mod decay", "Car attack", "Car release", "Pitch sweep", "Pitch decay",
        "Reverb", "Delay" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "level", "gate", "index", "ratio", "feedback",
        "moddecay", "carattack", "carrelease", "pitchsweep", "pitchdecay",
        "rsend", "dsend" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. The gate ratio is a multiplier on the note's own length and the pitch
    // sweep is in octaves; the rest are the oscillator and envelope units. The two
    // sends name themselves in neither switch: a fraction of the note is exactly the
    // zero to one both defaults already give.
    public static float Min(int target) => target switch
    {
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
        Gate => 4.0f,
        ModIndex => 12.0f,
        ModRatio => 8.0f,
        Feedback => 8.0f,
        ModDecay => 1.0f,
        CarAttack => 0.5f,
        // Well past anything musical, because the release also decides how long a
        // note holds on to its voice.
        CarRelease => 4.0f,
        PitchSweep => 4.0f,
        // Zero is a meaningful setting here, since it switches the envelope off,
        // so the range runs down to it rather than to a shortest useful sweep.
        PitchDecay => 0.5f,
        _ => 1.0f
    };

    public static float Get(in FmPatch patch, int target) => target switch
    {
        Level => patch.level,
        Gate => patch.gateScale,
        ModIndex => patch.modulationIndex,
        ModRatio => patch.modulatorRatio,
        Feedback => patch.feedback,
        ModDecay => patch.modulatorDecay,
        CarAttack => patch.carrierAttack,
        CarRelease => patch.carrierRelease,
        PitchSweep => patch.pitchSweep,
        PitchDecay => patch.pitchDecay,
        ReverbSend => patch.reverbSend,
        DelaySend => patch.delaySend,
        _ => 0.0f
    };

    public static void Set(ref FmPatch patch, int target, float value)
    {
        value = Math.Clamp(value, Min(target), Max(target));

        switch (target)
        {
            case Level: patch.level = value; break;
            case Gate: patch.gateScale = value; break;
            case ModIndex: patch.modulationIndex = value; break;
            case ModRatio: patch.modulatorRatio = value; break;
            case Feedback: patch.feedback = value; break;
            case ModDecay: patch.modulatorDecay = value; break;
            case CarAttack: patch.carrierAttack = value; break;
            case CarRelease: patch.carrierRelease = value; break;
            case PitchSweep: patch.pitchSweep = value; break;
            case PitchDecay: patch.pitchDecay = value; break;
            case ReverbSend: patch.reverbSend = value; break;
            case DelaySend: patch.delaySend = value; break;
        }
    }

    public static void Add(ref FmPatch patch, int target, float delta)
      => Set(ref patch, target, Get(in patch, target) + delta);

    // A sensible nudge for an inspector field, roughly a hundredth of the range.
    public static float Increment(int target)
      => (Max(target) - Min(target)) / 100.0f;
}

} // namespace Jacquard
