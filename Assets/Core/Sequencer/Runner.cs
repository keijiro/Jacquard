using System.Collections.Generic;

namespace Jacquard {

// A runner scans a lane and executes the tiles it meets.
//
// The score is static data; a runner exists only while playing. There is one per
// CHAN lane, and a JUMP never makes another — it only sends the one it has
// somewhere else. So the number of runners equals the number of CHAN lanes:
// running side by side adds runners, branching redirects them.
//
// What that count does not say is how many are running. A CHAN switched off keeps
// its place in the list and stops moving, so the seat is per lane and whether it
// is occupied is a second question — asked of NextSample, below.
//
// Order comes from the vertical position of the CHAN tile it was born from, and
// it travels with the runner: moving to a branch lane placed anywhere on the
// plane does not change when this runner gets its turn.

public sealed class Runner
{
    public Lane OriginLane { get; }

    // Reassigned when the score is edited: a CHAN tile can be moved above or
    // below another one while the sequence keeps playing.
    public int Order { get; set; }

    public int Channel => OriginLane.Channel?.Channel ?? 1;

    // Where the runner is about to execute.
    public Lane Lane { get; set; }
    public int StepIndex { get; set; }

    // Laps completed around the origin channel, which is what a cycle gate picks
    // from.
    public int Pass { get; set; }

    // Absolute sample position of the next step, kept in double so that a long
    // session cannot drift off the grid.
    //
    // Never when this runner is not running at all, which is how a lane that has
    // been switched off says so. A flag beside this number could disagree with it,
    // and the one that decides is always this one: the scheduler picks the earliest
    // sample and gathers whatever falls within half a sample of it, so a position
    // out past every comparison is already excluded from both, with nothing in
    // either loop to say it. NoBoundary is the same trick on the lap line.
    public double NextSample { get; set; }

    // Far enough out that no window reaches it and no arithmetic here can bring it
    // back: adding a step to it leaves it where it is.
    public const double Never = double.MaxValue;

    public bool Running => NextSample < Never;

    // What is audible right now, as opposed to what has been scheduled. Lags the
    // scheduling position by the lookahead, so a highlight matches what is heard.
    public Lane PlayingLane { get; private set; }
    public int PlayingStep { get; private set; } = -1;

    public Runner(Lane origin, int order, double startSample)
    {
        (OriginLane, Order, NextSample) = (origin, order, startSample);
        Lane = origin;
    }

    public float StepSeconds(float tempo)
      => OriginLane.Channel?.StepSeconds(tempo) ?? 0.125f;

    // Held locks

    // The locks the step being played put on the channel, and the sample the step after
    // it begins on.
    //
    // A lock lasts as long as the step it sits on, and the step is this lane's — so a
    // lane whose division is coarser than the ones below it goes on holding the channel
    // through the instants between its own steps. Sequencer.Reapply is where a step
    // still standing is put back into the pass.
    //
    // What is kept is the tiles that were reached and not the numbers they arrived at.
    // Reading them again writes the same working patch as the pass that placed them —
    // the relative ones stack and clamp in the same order — and no gate is asked twice,
    // since a tile a gate cut off never got in here to begin with.
    public IReadOnlyList<ParamTile> HeldLocks => _held;

    public double HoldUntil { get; private set; }

    // Takes the locks of the step being read now, in place of whatever the step before
    // it left. A step with no lock on it therefore lets the channel go, which is what
    // an empty cell on a lock lane is good for.
    //
    // Zero is a hold that is already over, which is what a runner put back at the top
    // of its lane is left with: what it was holding belonged to a run that has ended.
    public void BeginHold(double until)
    {
        _held.Clear();
        HoldUntil = until;
    }

    public void Hold(ParamTile tile) => _held.Add(tile);

    // Playhead tracking

    public void Record(long sample, Lane lane, int step)
      => _scheduled.Enqueue(new Marker { Sample = sample, Lane = lane, Step = step });

    public void AdvancePlayhead(long currentSample)
    {
        while (_scheduled.Count > 0 && _scheduled.Peek().Sample <= currentSample)
        {
            var marker = _scheduled.Dequeue();
            (PlayingLane, PlayingStep) = (marker.Lane, marker.Step);
        }
    }

    public void ClearPlayhead()
    {
        _scheduled.Clear();
        (PlayingLane, PlayingStep) = (null, -1);
    }

    // Private members

    struct Marker
    {
        public long Sample;
        public Lane Lane;
        public int Step;
    }

    readonly Queue<Marker> _scheduled = new();
    readonly List<ParamTile> _held = new();
}

} // namespace Jacquard
