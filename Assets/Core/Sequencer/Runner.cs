using System.Collections.Generic;

namespace Jacquard {

// A runner scans a lane and executes the tiles it meets.
//
// The score is static data; a runner exists only while playing. One is born from
// each CHAN lane, and a JUMP never makes another — it only sends the one it has
// somewhere else. So the number of runners equals the number of CHAN lanes:
// running side by side adds runners, branching redirects them.
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
    public double NextSample { get; set; }

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
}

} // namespace Jacquard
