namespace Jacquard {

// One timbre per channel.
//
// A patch belongs to the channel a runner sounds on rather than to the project, so
// a CHAN tile's number now picks the sound as well as the timing: two lanes on the
// same channel share a timbre because they share a channel, which is the same rule
// that already governs what a channel scoped lock reaches.
//
// A fixed array rather than a dictionary, for two reasons. A lock is applied
// through ParamTargets, which works on a ref, and only an array can hand one out;
// and the channel field of a CHAN tile has to offer a bounded set of numbers
// anyway, so there is nothing sparse to represent.

public sealed class PatchBank
{
    // What a CHAN tile accepts. Nothing in the synth or the sequencer cares about
    // the ceiling — the constant exists so that the channel field, the bank and
    // the file all agree on one.
    public const int Channels = 8;

    public ref FmPatch this[int channel] => ref _patches[Clamp(channel) - 1];

    // Channel numbers are one based, and one from outside the bank is folded in
    // rather than rejected: the editor cannot produce one, but a hand edited file
    // can.
    public static int Clamp(int channel) => System.Math.Clamp(channel, 1, Channels);

    public PatchBank()
    {
        for (var i = 0; i < Channels; i++) _patches[i] = FmPatch.Default;
    }

    // Private members

    readonly FmPatch[] _patches = new FmPatch[Channels];
}

} // namespace Jacquard
