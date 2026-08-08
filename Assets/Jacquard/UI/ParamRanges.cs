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
// The pitch sweep needs no case of its own: its range straddles zero, so the bar
// draws itself from where zero sits and shows the direction of the offset along with
// its size.

static class ParamRanges
{
    // The value a lock target holds, which is what an absolute lock and the sound
    // panel both set.
    public static ValueBar.Range Of(int target)
    {
        var (low, high) = (ParamTargets.Min(target), ParamTargets.Max(target));

        return target switch
        {
            ParamTargets.ModDecay or ParamTargets.CarAttack or
            ParamTargets.CarRelease or ParamTargets.PitchDecay
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

            _ => ValueBar.Amount(low, high)
        };
    }

    // What a relative lock shifts that value by: the same reading over a bar that
    // reaches as far in either direction as the parameter itself does, and which
    // therefore grows out of the middle.
    public static ValueBar.Range Relative(int target)
    {
        var range = Of(target);
        var span = range.High - range.Low;

        return new ValueBar.Range(-span, span, range.Curve, range.Snap,
                                  range.Scale, range.Unit, range.Digits);
    }
}

} // namespace Jacquard.App
