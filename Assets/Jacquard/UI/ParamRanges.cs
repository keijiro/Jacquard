using UnityEngine;

namespace Jacquard.App {

// What a synth parameter's bar looks like.
//
// Where a parameter is useful belongs to the synth, and comes from ParamTargets:
// this only decides how one is read. A time reads out in milliseconds over a curved
// bar, because the useful part of an envelope time is all inside the first tenth of
// its range and a linear bar would resolve to nothing there. The rest are bare
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
// its size. Pan is drawn the same way and only reads out differently.

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

            // A side and a distance, which is how a position is read: "L 50" says both
            // and "-0.50" says neither, and the centre is a place with a name rather
            // than a number that happens to be zero. The bar itself already grows out
            // of that centre towards the side being named, since the range straddles
            // it. Typing goes through the number, so the readout counts in the same
            // hundredths the field would take.
            ParamTargets.Pan =>
              new ValueBar.Range(low, high, scale: 100.0f, digits: 0, display: Side),

            _ => ValueBar.Amount(low, high)
        };
    }

    static string Side(float value)
    {
        var amount = Mathf.RoundToInt(Mathf.Clamp(value, -1.0f, 1.0f) * 100.0f);
        return amount == 0 ? "C" : (amount < 0 ? "L " : "R ") + Mathf.Abs(amount);
    }

    // What a relative lock shifts that value by: the same reading over a bar that
    // reaches as far in either direction as the parameter itself does, and which
    // therefore grows out of the middle.
    //
    // The one thing deliberately dropped is a display of its own, which pan is the
    // only parameter to have. A shift is a distance and has no side to be on, so it
    // reads as the plain number the scale already puts it in.
    public static ValueBar.Range Relative(int target)
    {
        var range = Of(target);
        var span = range.High - range.Low;

        return new ValueBar.Range(-span, span, range.Curve, range.Snap,
                                  range.Scale, range.Unit, range.Digits);
    }
}

} // namespace Jacquard.App
