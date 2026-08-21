using System.Globalization;
using UnityEngine;

namespace Jacquard.App {

// What a synth parameter's bar looks like.
//
// Where a parameter is useful belongs to the synth, and comes from ParamTargets:
// this only decides how one is read. A time reads out in milliseconds over a bar whose
// travel is geometric, because the useful part of an envelope time is spread over three
// decades and a straight bar — or a curved one, which is a straight bar with its dead
// end at the other side — resolves to nothing across most of them. The rest are bare
// numbers, since a modulation depth in radians and a ratio against the carrier have
// no unit worth printing.
//
// The FM decay is one of those bare numbers rather than one of the times, which is
// the one entry here worth stating rather than reading off. It holds the slope of a
// decay and not its length, and the slope is already curved against time by the synth
// that reads it: the bottom tenth of the travel is a modulation gone inside fifty
// milliseconds, and the top tenth one that outlasts the note. Curving the bar as well
// would only bunch the whole useful range into one end of it, so the bar over it is
// straight.
//
// The pitch sweep needs no case of its own: its range straddles zero, so the bar
// draws itself from where zero sits and shows the direction of the offset along with
// its size. Pan is drawn the same way and only reads out differently. The level
// straddles zero too and is the one range here that has to say it means nothing by it —
// zero on it is full scale, not a centre.

static class ParamRanges
{
    // The value a lock target holds, which is what an absolute lock and the sound
    // panel both set.
    public static ValueBar.Range Of(int target)
    {
        var (low, high) = (ParamTargets.Min(target), ParamTargets.Max(target));

        return target switch
        {
            ParamTargets.CarAttack or ParamTargets.CarRelease or
            ParamTargets.PitchDecay
              => ValueBar.Seconds(low, high),

            // A ratio against the note, curved for the same reason a time is: the
            // bottom of the range is a different kind of sound rather than a smaller
            // amount of the one above it, and a straight bar spends almost nothing on
            // it. Where the modulator runs slower than the note it is heard as movement
            // instead of as a timbre, and the whole of that — a twentieth of the note
            // up to unity — is worth about a third of the travel; the harmonic ratios
            // keep the other two thirds, with the two of a fresh patch at the middle.
            //
            // A pixel is worth about a hundredth of a ratio down there and a tenth at
            // the top, where the ratio itself is in whole numbers.
            //
            // Feedback takes the same shape for the same reason, on a depth in radians
            // rather than a ratio. What a player is choosing is inside the first two of
            // its eight: half a radian is the edge on a bass, one is a reed, and past
            // two or three every setting is a rasp that differs from the next only in
            // how much of it there is. A straight bar gave those two radians a quarter
            // of the travel and this gives them a half, with the noise above still
            // holding the rest.
            ParamTargets.ModRatio or ParamTargets.Feedback =>
              new ValueBar.Range(low, high, curve: 2.0f),

            // A multiplier on the note's own length, curved so that unity — which is
            // what the length written on the cell means — sits near the middle of the
            // travel instead of a fifth of the way along it.
            //
            // Read out as a percentage, which is the one thing that says it scales a
            // length rather than being one: a note written for two steps and a channel
            // at 50% are the same multiplication, and only the unit tells them apart.
            ParamTargets.Gate =>
              new ValueBar.Range(low, high, curve: 2.0f, scale: 100.0f,
                                 unit: "%", digits: 0),

            // Decibels, which the output volume and the limiter's threshold are
            // already in and which the level is alone among the lock targets in being
            // in, for the reason all three share: it is a ratio of amplitude, so a
            // straight bar spends most of its travel inside the top doubling and reads
            // out as a multiplier nobody thinks in.
            //
            // Curved the way the volume is and by the same exponent, since the choosing
            // happens at the same end: the first six decibels down from full scale get a
            // fifth of the travel rather than a twelfth of it, the middle of the bar
            // lands at -10dB, and a pixel is worth about a sixth of a decibel up where a
            // part is balanced against the rest of the piece. The whole of the room above
            // full scale is the top fifth.
            //
            // The bottom is silence and says so, which is the one reading here that is a
            // setting rather than a quantity: a step that takes a channel out is written
            // by dialling the level to the end of its travel, and a bar printing -60.0 dB
            // there would be telling the truth about the number and lying about the
            // sound. Told outright that it is not bipolar, because its ends straddle zero
            // without zero being anywhere near the middle of it — see ValueBar.Range.
            ParamTargets.Level =>
              new ValueBar.Range(low, high, curve: 0.4f, digits: 1, unit: "dB",
                                 bipolar: false, display: Quiet),

            // A side and a distance, which is how a position is read: "L 50" says both
            // and "-0.50" says neither, and the centre is a place with a name rather
            // than a number that happens to be zero. The bar itself already grows out
            // of that centre towards the side being named, since the range straddles
            // it. Typing goes through the number, so the readout counts in the same
            // hundredths the field would take.
            ParamTargets.Pan =>
              new ValueBar.Range(low, high, scale: 100.0f, digits: 0, display: Side),

            // Whole semitones, since half of one is not a transposition of anything
            // written on the plane. Read with its sign, which pan is the other
            // parameter to spell out: what a transpose is is a direction and a
            // distance, and "3" alone says only half of that. The bar grows out of the
            // centre on its own, the range straddling zero.
            ParamTargets.Transpose => ValueBar.Integer(low, high, display: Signed),

            _ => ValueBar.Amount(low, high)
        };
    }

    static string Side(float value)
    {
        var amount = Mathf.RoundToInt(Mathf.Clamp(value, -1.0f, 1.0f) * 100.0f);
        return amount == 0 ? "C" : (amount < 0 ? "L " : "R ") + Mathf.Abs(amount);
    }

    static string Quiet(float value)
      => value <= FmPatch.MinLevel ? "off"
         : value.ToString("F1", CultureInfo.InvariantCulture) + " dB";

    static string Signed(float value)
    {
        var amount = Mathf.RoundToInt(value);
        return amount > 0 ? "+" + amount : amount.ToString();
    }

    // What a relative lock shifts that value by: the same reading over a bar that
    // reaches as far in either direction as the parameter itself does, and which
    // therefore grows out of the middle.
    //
    // The one thing deliberately dropped is a display of its own, which pan is the
    // only parameter to have. A shift is a distance and has no side to be on, so it
    // reads as the plain number the scale already puts it in.
    //
    // A geometric bar carries its floor across as well, which is what makes a shift on
    // a time worth having: the ratios count out from the middle in both directions, so
    // the small shifts sit around the centre where a lock is usually set and the whole
    // range is still at the ends.
    public static ValueBar.Range Relative(int target)
    {
        // The level is the one target whose shift is not measured across its own span. A
        // level runs from silence to over full scale, which is sixty-six decibels, and a
        // bar reaching that far either way would spend almost all of its travel on shifts
        // nothing in a piece asks for. Twenty-four is the end of what does: six is a firm
        // accent, twelve is a different dynamic, and past twenty-four a note is gone
        // rather than quiet — and silence, which is the one thing that wants the whole
        // range, is what an absolute lock is for. Straight, because decibels are already
        // the curve.
        if (target == ParamTargets.Level)
            return new ValueBar.Range(-24.0f, 24.0f, digits: 1, unit: "dB");

        var range = Of(target);
        var span = range.High - range.Low;

        return new ValueBar.Range(-span, span, range.Curve, range.Snap,
                                  range.Scale, range.Unit, range.Digits,
                                  floor: range.Floor);
    }
}

} // namespace Jacquard.App
