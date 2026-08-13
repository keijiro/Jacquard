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
// One of them is not addressed to the synth at all. The transpose is read by the
// sequencer as it makes the note and never reaches a voice, which makes it the one
// entry here that is about *which* note sounds rather than what it sounds like. It is
// in the list because the list is the fields of the patch and because it is worth
// locking — a step that lifts one channel an octave is a lock the way a step that
// throws one note into the reverb is.
//
// The order is the order the Sound and Lock panels read in. It opens with the note
// itself, then the two that place it in the mix rather than shape it — how loud, and
// where. The four FM parameters then run in the order a musician dials them: what the
// modulator is tuned to, how much of it arrives, how much of itself it hears, and
// how quickly all of that gets out of the way.
//
// The numbers these constants hold are an index into an array and nothing else: a
// file names a target by its key, so inserting one at the front costs nothing but a
// recompile.
//
// The names are the musician's rather than the synthesis textbook's, and they do not
// match the fields they address: an FM amount is a modulation index and an amp
// envelope is the carrier's. The constants and the file keys keep the older
// spellings, since renaming those would only be a way to make older files unopenable
// for the sake of a caption.

public static class ParamTargets
{
    public const int Transpose = 0;
    public const int Level = 1;
    public const int Pan = 2;
    public const int Gate = 3;
    public const int ModRatio = 4;
    public const int ModIndex = 5;
    public const int Feedback = 6;
    public const int ModDecay = 7;
    public const int CarAttack = 8;
    public const int CarRelease = 9;
    public const int PitchSweep = 10;
    public const int PitchDecay = 11;
    public const int ReverbSend = 12;
    public const int DelaySend = 13;

    public const int Count = 14;

    public static readonly string[] Names =
      { "Transpose", "Level", "Pan", "Gate ratio", "FM ratio", "FM amount",
        "Feedback", "FM decay", "Amp attack", "Amp release", "Pitch sweep",
        "Pitch decay", "Reverb send", "Delay send" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "transpose", "level", "pan", "gate", "ratio", "index", "feedback",
        "moddecay", "carattack", "carrelease", "pitchsweep", "pitchdecay",
        "rsend", "dsend" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. The gate ratio is a multiplier on the note's own length and the pitch
    // sweep is in octaves; the rest are the oscillator and envelope units. The two
    // sends name themselves in neither switch, and neither does the FM decay: a
    // fraction of the note and the slope of a decay are both exactly the zero to one
    // the defaults already give. Pan names itself in one, since it runs to the same
    // one at the top and to its mirror image at the bottom.
    public static float Min(int target) => target switch
    {
        // A twentieth, which is barely a ratio any more: down here the modulator turns
        // twenty times slower than the note and what it makes is a wobble rather than a
        // timbre. It stops well above zero because a modulator that does not turn is
        // not one — its output would be a constant, and a constant added to the
        // carrier's phase is a phase offset nobody can hear.
        ModRatio => 0.05f,
        Gate => 0.05f,
        CarAttack => 0.001f,
        // Two octaves either way, which is as far as a part can be moved and still be
        // the part that was written: past that a bass line is a lead and the scale it
        // is snapped to is the only thing it still has in common with the score.
        Transpose => -24.0f,
        // Symmetric about the centre, which is also what tells the bar to draw itself
        // out from where the note is unpanned rather than from the left edge.
        Pan => -1.0f,
        PitchSweep => -8.0f,
        _ => 0.0f
    };

    public static float Max(int target) => target switch
    {
        Transpose => 24.0f,
        Level => 1.0f,
        Gate => 4.0f,
        ModIndex => 12.0f,
        ModRatio => 8.0f,
        Feedback => 8.0f,
        // Two seconds, which is a pad's swell rather than an instrument's attack: the
        // curved bar keeps the first few milliseconds where a percussive onset lives,
        // and what the rest of the travel buys is a note that fades in as a gesture.
        CarAttack => 2.0f,
        // Well past anything musical, because the release also decides how long a
        // note holds on to its voice.
        CarRelease => 4.0f,
        // Eight octaves either way, and eight is deliberate: four covered a kick and
        // very little else, so everything the envelope is good for beyond a drum — a
        // dive, a siren, a riser — sat outside the bar and could only be typed. The
        // useful part of a kick is still the first inch of travel, but it was never
        // the part that was hard to reach.
        PitchSweep => 8.0f,
        // Two seconds, which is far longer than a thump: the short end is where a drum
        // lives and the rest of the travel is for a sweep meant to be heard as one —
        // and heard for as long as the note is, which a second was not quite enough
        // for. The bar is curved, so lengthening it does not cost the short end its
        // resolution. Zero is a meaningful setting at the other end, since it
        // switches the envelope off, which is why the range runs down to it rather
        // than to a shortest useful sweep.
        PitchDecay => 2.0f,
        _ => 1.0f
    };

    public static float Get(in FmPatch patch, int target) => target switch
    {
        Transpose => patch.transpose,
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
            case Transpose: patch.transpose = value; break;
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
}

} // namespace Jacquard
