using System;

namespace Jacquard {

// What a parameter lock can point at.
//
// sequencer-spec.md leaves this set to the synth: the sequencer only carries an index
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
// What is not free about that is the screen. Adding one here adds a row to the Sound
// group and a row to a lock's, and on a tablet a row costs 33pt of a column that has
// to be dragged to reach past the bottom of the screen — so a target arriving in this
// list is a decision about how far a hand has to travel as much as it is one about
// what a lock can say. It used to be a harder limit than that: the column did not
// scroll at all, and a row past the screen was a control nobody could reach.
//
// One of them is not addressed to the synth at all. The transpose is read by the
// sequencer as it makes the note and never reaches a voice, which makes it the one
// entry here that is about *which* note sounds rather than what it sounds like. It is
// in the list because the list is the fields of the patch and because it is worth
// locking — a step that lifts one channel an octave is a lock the way a step that
// throws one note into the reverb is.
//
// The order is the order the Sound and Lock panels read in, and it runs in six groups
// rather than fifteen rows: the note, the mix, the FM, the two envelopes, and what is
// sent on. Groups below carries their names, and both panels head each run with one.
//
// It opens with the two that are not about the timbre at all — which note the sequencer
// makes and how much of its step that note holds — then the three that place the note
// in the mix rather than shape it: how loud, where, and how wide. The four FM
// parameters run in the order a musician dials them: what the modulator is tuned to,
// how much of it arrives, how much of itself it hears, and how quickly all of that gets
// out of the way. An envelope follows the thing it moves, and the sends go last because
// they are the only two that leave the voice.
//
// The gate ratio moved up to sit beside the transpose, which is the one place the
// grouping asked for more than a heading over the order that was already here. The two
// belong together for the same reason FmPatch keeps the gate ratio at arm's length from
// the oscillator settings around it: neither says anything about what a note sounds
// like, only which note it is and how long it lasts. With the mix between them, a hand
// reading down the list met a length dropped into the middle of three settings about
// loudness, and the transpose was left opening the list on its own.
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
//
// A name is read under its heading and does not repeat it. The four FM rows each opened
// with "FM" and the two sends each ended in "send", from when fifteen rows ran under one
// name and a row had to say for itself which part of a patch it was: the heading says it
// now, and a row saying it again is a caption arguing with the line above it. What is
// left is the half that differs, which is the half being read.
//
// The pitch envelope's depth is the one row that could not simply drop its word. "Sweep"
// alone names the gesture rather than how far it goes, and how far it goes is what the
// number is. "Amount" would have said that, and says it four rows up for the FM — two
// rows of one name in a panel answer a glance with whichever the eye reached first.
// "Depth" is what a pitch envelope's range is called, and it carries a sign as naturally
// as that range does.

public static class ParamTargets
{
    public const int Transpose = 0;
    public const int Gate = 1;
    public const int Level = 2;
    public const int Pan = 3;
    public const int Unison = 4;
    public const int ModRatio = 5;
    public const int ModIndex = 6;
    public const int Feedback = 7;
    public const int ModDecay = 8;
    public const int CarAttack = 9;
    public const int CarRelease = 10;
    public const int PitchSweep = 11;
    public const int PitchDecay = 12;
    public const int ReverbSend = 13;
    public const int DelaySend = 14;

    public const int Count = 15;

    public static readonly string[] Names =
      { "Transpose", "Gate ratio", "Level", "Pan", "Unison", "Ratio",
        "Amount", "Feedback", "Decay", "Attack", "Release",
        "Depth", "Decay", "Reverb", "Delay" };

    public static string Name(int target)
      => target >= 0 && target < Count ? Names[target] : "?";

    // Where the order above breaks, as the target that opens each run and the name the
    // panels head it with.
    //
    // Here rather than on the panel because it is a fact about the order and the order
    // is here: a target inserted between two of these joins the group above it, which
    // is a thing to settle while looking at the list it is going into rather than to
    // find out from a screen. The panel only walks the list and asks each target
    // whether a heading stands over it.
    //
    // A name says why those rows are together and never what they are — "Note" over a
    // transpose and a gate ratio, not "Note settings" over two rows that already spell
    // themselves. The envelopes are the two that take a second word, and they take it
    // because their rows gave one up: Attack, Release, Depth and Decay each name a part
    // of an envelope and none of them names an envelope, so the word is here or it is
    // nowhere on the panel.
    //
    // The Sound panel and a lock's rows both read these, which is the same obligation
    // the shared order already carries: the two are read against each other, so a break
    // that is in one of them is in both.
    public static readonly (int First, string Name)[] Groups =
      { (Transpose, "Note"), (Level, "Mix"), (ModRatio, "FM"),
        (CarAttack, "Amp envelope"), (PitchSweep, "Pitch envelope"),
        (ReverbSend, "Sends") };

    // The heading standing over this target, or null for one that falls under a heading
    // already standing.
    public static string GroupAt(int target)
    {
        foreach (var (first, name) in Groups) if (first == target) return name;
        return null;
    }

    // Spelling used in a saved file, where a space would break the tokenizer.
    public static readonly string[] Keys =
      { "transpose", "gate", "level", "pan", "unison", "ratio", "index",
        "feedback", "moddecay", "carattack", "carrelease", "pitchsweep",
        "pitchdecay", "rsend", "dsend" };

    public static string Key(int target)
      => target >= 0 && target < Count ? Keys[target] : "level";

    public static int Parse(string key) => Array.IndexOf(Keys, key);

    // Ranges. The gate ratio is a multiplier on the note's own length and the pitch
    // sweep is in octaves; the rest are the oscillator and envelope units. The two
    // sends name themselves in neither switch, and neither does the FM decay: a
    // fraction of the note and the slope of a decay are both exactly the zero to one
    // the defaults already give. Pan names itself in one, since it runs to the same
    // one at the top and to its mirror image at the bottom.
    //
    // The level names itself in both and holds neither number, because it is the one
    // target measured in decibels: what its two ends are is argued where the conversion
    // that has to agree with them is, in FmPatch. A ratio of amplitude is dialled and
    // *shifted* in dB or it is neither — a fifth off is a different thing at every point
    // of the range, which is what a relative lock on it could not do anything useful
    // with.
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
        Level => FmPatch.MinLevel,
        _ => 0.0f
    };

    public static float Max(int target) => target switch
    {
        Transpose => 24.0f,
        Level => FmPatch.MaxLevel,
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
        // resolution. Two was settled on while FmCurve.SnapCurve was still 16, where a
        // dialled decay was heard as roughly a quarter of itself; halving the curve
        // doubled what the same number reaches, and the top was kept because 8 was
        // chosen by ear against this range, so what is here is what was listened to.
        // Zero is a meaningful setting at the other end, since it switches the envelope
        // off, which is why the range runs down to it rather than to a shortest useful
        // sweep.
        PitchDecay => 2.0f,
        _ => 1.0f
    };

    // What the synth will accept, which is a wider question than what the bar is for.
    //
    // Min and Max above are a dial: the travel is spent where the sound is chosen, and
    // both ends were placed by ear against a written part. That is the right range for
    // a hand and the wrong one for a score. A part sometimes wants moving further than
    // any bar should spend its length on, a tail sometimes wants to run past where a
    // bar would leave no resolution for the first ten milliseconds, and a file written
    // by hand has never been held to either. So a bar reaches Min to Max, and this is
    // what a number typed into one, read out of a file, or arrived at by a relative
    // lock is held to instead.
    //
    // Only the targets with a reason to differ are named. Everything else falls through
    // to the dial's own range, which is the honest answer where the two ends are the
    // parameter's own: a pan of two is not a wider stereo image, a send of two is only
    // a level, a modulator decay is a slope and 0 to 1 is the whole of it, and the
    // level's ceiling is what the mix is promised it will never be handed more than
    // (FmPatch.MaxLevel).
    //
    // Nothing here is generous for its own sake. Every ceiling below is either the
    // point where the parameter stops meaning anything more or the point where the
    // arithmetic under it stops holding, whichever comes first.
    public static float Bound(int target, float value) => target switch
    {
        // As far as one note can be from another, which is the whole of what moving a
        // part can be asked to do: a transpose that reaches from the bottom of what
        // this program can hold to the top can put any part anywhere, and one that
        // reaches further is only asking for a pitch there is no note at.
        Transpose => Math.Clamp(value, -PitchSpan, PitchSpan),

        // The three envelope times share a ceiling because they are three answers to
        // one question — how long a part of a note takes — and there is no reason for
        // a swell, a tail and a sweep to be allowed different amounts of it.
        //
        // Sixteen seconds is where a tail stops being part of a note. It is also well
        // past what the voice pool can carry: a release is how long a note holds its
        // slot, so a few seconds of it already outruns twenty-four voices at any tempo
        // a piece is written at, and everything above that is the same trade made
        // harder. The number is here to stop the absurd, not to promise the long ones
        // will all sound.
        CarAttack or CarRelease or PitchDecay => Math.Clamp(value, 0.0f, LongestTime),

        // Sixteen times the length written on the cell, which is a note holding a bar
        // of sixteenths on its own, and a hundredth of it at the other end, which is
        // shorter than the shortest gate FmNoteEvent will make.
        Gate => Math.Clamp(value, 0.01f, 16.0f),

        // Sixty-four times the note, which is where the modulator leaves the band: from
        // the middle of the keyboard up it is already past half the sample rate, so a
        // higher ratio buys aliases rather than harmonics. Zero is allowed here where
        // the bar stops short of it — a modulator that does not turn is a phase offset
        // nobody can hear, which is a useless setting rather than a dangerous one.
        ModRatio => Math.Clamp(value, 0.0f, 64.0f),

        // Both are a depth in radians into the same sine, so they take the same number.
        // A hundred radians is far past where the spectrum stops changing shape and
        // only gets denser, and it is far short of where the sine's own range reduction
        // gives out.
        ModIndex or Feedback => Math.Clamp(value, 0.0f, 100.0f),

        // Thirty-two octaves, which is a sweep from below hearing to above the sample
        // rate and back, and is also the last round number the arithmetic survives:
        // the envelope is a power of two, and past sixty-four the scale it hands the
        // oscillator is float.MaxValue, whose phase increment poisons the voice.
        PitchSweep => Math.Clamp(value, -32.0f, 32.0f),

        _ => Math.Clamp(value, Min(target), Max(target))
    };

    // Read off the two ends of what a note can be, since that is what it means: the
    // transpose is the one target the synth never sees — the sequencer applies it as it
    // makes the note — so what bounds it is the keyboard and not a voice.
    const float PitchSpan = Pitch.Highest - Pitch.Lowest;

    const float LongestTime = 16.0f;

    public static float Get(in FmPatch patch, int target) => target switch
    {
        Transpose => patch.transpose,
        Level => patch.level,
        Pan => patch.pan,
        Unison => patch.unison,
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
        // A number that is not one is not an edit, and it is refused here because here
        // is where every write meets it. A clamp does not stop a NaN — both of its
        // comparisons are false, so it passes through untouched — and nothing
        // downstream survives one: a note whose total duration is NaN never reaches its
        // end and holds its voice for the rest of the session, and a NaN reaching a
        // send latches in the tail, which is recursive. Both entrances parse it
        // willingly, since "NaN" is a number float.TryParse knows.
        if (!float.IsFinite(value)) return;

        value = Bound(target, value);

        switch (target)
        {
            case Transpose: patch.transpose = value; break;
            case Level: patch.level = value; break;
            case Pan: patch.pan = value; break;
            case Unison: patch.unison = value; break;
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
