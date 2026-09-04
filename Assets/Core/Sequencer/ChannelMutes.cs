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
// This is saved with the piece, which is why it hangs off the project. A hand held over
// a channel is played rather than set — that argument once kept it out of the format
// altogether — but what a hand leaves behind is a decision about how the thing is
// heard, and it is the one such decision the app used to forget between one session and
// the next. A file carries one line for it, and a load puts the switches back where the
// hands that wrote it left them.
//
// Both sets are saved, not only the one being consulted. The mutes under a solo are
// exactly what dropping the last solo gives back, so a file holding only the audible
// answer would come back with them cleared — which is the one thing the rule above
// promises does not happen.

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

    // Exchanges the hand held over two channels, both switches at once.
    //
    // Here rather than as four calls from the caller, for the reason this class exists
    // at all: solo and mute are one object because what a hand holds over a channel is
    // two sets, and a caller spelling the exchange out itself is a caller that will go
    // on swapping two of the three the day a third switch arrives.
    //
    // All four flags are read before any is written, which is the whole of the trick a
    // swap is: written as it goes, the second pair would be read back off the first.
    public void Swap(int a, int b)
    {
        var (mutedA, mutedB) = (IsMuted(a), IsMuted(b));
        var (soloedA, soloedB) = (IsSoloed(a), IsSoloed(b));

        SetMuted(a, mutedB);
        SetMuted(b, mutedA);
        SetSoloed(a, soloedB);
        SetSoloed(b, soloedA);
    }

    // Private members

    // Channel numbers are one based and folded into the bank the way every other
    // reader of one folds them, so a hand edited file cannot index past the end here.
    static int Index(int channel) => PatchBank.Clamp(channel) - 1;

    readonly bool[] _muted = new bool[PatchBank.Channels];
    readonly bool[] _soloed = new bool[PatchBank.Channels];
}

} // namespace Jacquard
