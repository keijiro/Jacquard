namespace Jacquard {

// Which channels are heard, held apart from the score.
//
// A mute is not an edit. The runners go on running whatever this says — a muted lane
// keeps its place in the bar, its laps go on counting, its cycle gates go on turning
// over and its jumps are still taken — and the one thing that changes is that the notes
// it reaches are not handed to the synth. So letting a channel back in is hearing it
// from where the sequence has got to, which is the whole point of a mute on a running
// sequence and is not something a delete and an undo could do.
//
// Solo is a mute of everything else, which is why the two live in one object rather
// than two: with anything soloed the question a channel is asked is whether it is one
// of them, and the mutes are simply not consulted. They are kept rather than cleared,
// so that dropping the last solo gives back the mix that was there before it.
//
// None of this is saved. What a file holds is the piece, and a hand held over one
// channel of it is a performance, the same argument the live effects are kept out of
// the format by: there is no version bump here, no key on any line, and a load leaves
// the mutes exactly where the hands left them.

public sealed class ChannelMutes
{
    public bool IsMuted(int channel) => _muted[Index(channel)];

    public void SetMuted(int channel, bool muted) => _muted[Index(channel)] = muted;

    public bool IsSoloed(int channel) => _soloed[Index(channel)];

    public void SetSoloed(int channel, bool soloed) => _soloed[Index(channel)] = soloed;

    // Whether anything at all is soloed, which is what decides which of the two sets
    // is being read.
    public bool AnySoloed
    {
        get
        {
            foreach (var soloed in _soloed) if (soloed) return true;
            return false;
        }
    }

    // The one question the sequencer asks.
    public bool Sounds(int channel)
      => AnySoloed ? IsSoloed(channel) : !IsMuted(channel);

    // Private members

    // Channel numbers are one based and folded into the bank the way every other
    // reader of one folds them, so a hand edited file cannot index past the end here.
    static int Index(int channel) => PatchBank.Clamp(channel) - 1;

    readonly bool[] _muted = new bool[PatchBank.Channels];
    readonly bool[] _soloed = new bool[PatchBank.Channels];
}

} // namespace Jacquard
