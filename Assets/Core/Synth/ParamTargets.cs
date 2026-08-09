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
//
// The order is the order the Sound and Lock panels read in, and it opens with the
// two that place the note in the mix rather than shape it — how loud, and where.

public static class ParamTargets
{
    public const int Level = 0;
    public const int Pan = 1;
    public const int Gate = 2;
    public const int ModIndex = 3;
    public const int ModRatio = 4;
    public const int Feedback = 5;
    public const int ModDecay = 6;
    public const int CarAttack = 7;
    public const int CarRelease = 8;
    public const int PitchSweep = 9;
    public const int PitchDecay = 10;
    public const int ReverbSend = 11;
    public const int DelaySend = 12;

    public const int Count = 13;

    public static readonly string[] Names =
      { "Level", "Pan", "Gate ratio", "Mod index", "Mod ratio", "Feedback",
        "Mod decay", "Car attack", "Car release", "Pitch sweep", "Pitch decay",
        "Reverb", "Delay" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "level", "pan", "gate", "index", "ratio", "feedback",
        "moddecay", "carattack", "carrelease", "pitchsweep", "pitchdecay",
        "rsend", "dsend" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. The gate ratio is a multiplier on the note's own length and the pitch
    // sweep is in octaves; the rest are the oscillator and envelope units. The two
    // sends name themselves in neither switch: a fraction of the note is exactly the
    // zero to one both defaults already give. Pan names itself in one, since it runs
    // to the same one at the top and to its mirror image at the bottom.
    public static float Min(int target) => target switch
    {
        ModRatio => 0.25f,
        Gate => 0.05f,
        ModDecay => 0.005f,
        CarAttack => 0.001f,
        // Symmetric about the centre, which is also what tells the bar to draw itself
        // out from where the note is unpanned rather than from the left edge.
        Pan => -1.0f,
        PitchSweep => -8.0f,
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
        // Eight octaves either way, and eight is deliberate: four covered a kick and
        // very little else, so everything the envelope is good for beyond a drum — a
        // dive, a siren, a riser — sat outside the bar and could only be typed. The
        // useful part of a kick is still the first inch of travel, but it was never
        // the part that was hard to reach.
        PitchSweep => 8.0f,
        // A second, which is far longer than a thump: the short end is where a drum
        // lives and the rest of the travel is for a sweep meant to be heard as one.
        // The bar is curved, so lengthening it does not cost the short end its
        // resolution. Zero is a meaningful setting at the other end, since it
        // switches the envelope off, which is why the range runs down to it rather
        // than to a shortest useful sweep.
        PitchDecay => 1.0f,
        _ => 1.0f
    };

    public static float Get(in FmPatch patch, int target) => target switch
    {
        Level => patch.level,
        Pan => patch.pan,
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
            case Pan: patch.pan = value; break;
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
