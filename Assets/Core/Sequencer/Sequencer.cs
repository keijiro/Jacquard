using System;
using System.Collections.Generic;

namespace Jacquard {

// Drives the runners and turns the tiles they meet into note events.
//
// Timing is expressed against the audio clock: every step lands on an absolute
// sample position computed by looking ahead of the current audio position, so a
// dropped frame delays when notes are handed over but never when they sound.
//
// One instant of the timeline is a slice, and a slice is processed as a single
// downward pass: the runners that land on it go in the order of their CHAN tiles,
// topmost first, and each one reads its step from the rail row down. Everything a
// tile does reaches what is processed after it and nothing before it, which is the
// one rule behind gates, locks and notes alike.
//
// Locks last exactly as long as that pass, so every channel starts each slice from
// its own patch again.
//
// One score can be played out and another begun without stopping, and the seam is the
// turn of the master lane — see Score.MasterLane, which is where a piece says how long
// it is. A lap line cannot be worked out in advance, since a gate decides how far a lap
// goes and one of them throws dice, so it is found as it happens: a project waiting to
// come in makes the loop watch the master's lap count across each slice, and the sample
// the next lap starts on becomes a nearer horizon that the outgoing score is run out to.
// The takeover then happens inside the same pass, which is what leaves nothing between
// the two scores and nothing over them.

public sealed class Sequencer
{
    // Public state

    // Which channels are heard comes with it, in Project.Mutes. Nothing about the run
    // depends on that — see the note on the note tile in Descend — and it is read off
    // the project rather than held here so that a project loaded over the top of this
    // one brings its own, which is the whole of the wiring a load needs.
    //
    // Assigning one is an abrupt swap, which is what a project arriving before there is
    // anything to play means. It also drops a line a switch was waiting on, since that
    // line was measured on the score being replaced here. SwitchTo is the door that
    // waits.
    public Project Project
    {
        get => _project;

        set
        {
            _project = value;
            (_incoming, _boundary) = (null, NoBoundary);
            _master = FindMaster();
        }
    }

    public bool IsPlaying => _playing;

    public IReadOnlyList<Runner> Runners => _runners;

    // The runner the piece's period is counted on, and null while stopped. Its Pass is
    // the lap count and its PlayingStep is how far through the lap the music has got —
    // there is no second name here for either, since two names for one number is how
    // the two get to disagree.
    public Runner MasterRunner => _master;

    public bool IsSwitchPending => _incoming != null;

    // Raised once an incoming project has taken over, with the sample it took over on,
    // or zero when there was no run to take over from. It is raised after the window
    // has been run out rather than at the seam itself, so that a view rebuilt by a
    // listener cannot land in the middle of the loop that is still emitting notes.
    public event Action<long> Switched;

    // Puts a project in.
    //
    // While stopped it takes effect at once. While playing it waits for the master lane
    // to come round, and the two scores then read as one: the incoming runners start on
    // the sample the outgoing lap ended on, with nothing between them and nothing over
    // them. There is one next, so asking twice is asking once.
    public void SwitchTo(Project project)
    {
        _incoming = project;
        SettleIfIdle();
    }

    // Transport

    // The first step is placed one lookahead ahead of the current audio position
    // so that playback starts cleanly rather than part way through a step.
    public void Play(long currentSample, long lookaheadSamples)
    {
        Stop();
        Populate(currentSample + lookaheadSamples);
    }

    public void Stop()
    {
        _playing = false;
        _runners.Clear();
        (_master, _boundary) = (null, NoBoundary);

        SettleIfIdle();
    }

    // Puts a runner on every CHAN lane of the project, all starting on the same sample.
    // A standing start and a score taking over from another one are the same act; what
    // differs is only where that sample comes from.
    void Populate(double startSample)
    {
        _runners.Clear();

        var order = 0;

        foreach (var lane in Project.Score.ChannelLanes)
            _runners.Add(new Runner(lane, order++, startSample));

        _master = FindMaster();
        _playing = _runners.Count > 0;
    }

    // The runner the lap is counted on: the one born from the master lane, and whoever
    // runs first if the score has nothing to say about it.
    Runner FindMaster()
    {
        var lane = _project?.Score.MasterLane;

        foreach (var runner in _runners)
            if (runner.OriginLane == lane) return runner;

        return _runners.Count > 0 ? _runners[0] : null;
    }

    // A request waits for the lap line, and only for as long as there is a run to wait
    // through. When the run ends first — the transport stopped, or the last CHAN lane
    // edited away — there is nothing left to wait for and the project arrives. A load
    // that evaporated because the transport was stopped under it would be worse than
    // either of the two things that could happen instead.
    void SettleIfIdle()
    {
        if (_playing || _incoming == null) return;

        _project = _incoming;
        (_incoming, _boundary) = (null, NoBoundary);
        _master = FindMaster();

        Switched?.Invoke(0);
    }

    // Reconciles the runners with an edited score without interrupting the sound.
    // Runners whose CHAN lane survives keep their position and lap count; a new
    // CHAN lane joins in step with whoever is already running.
    public void Resync()
    {
        if (!_playing) return;

        _previous.Clear();
        _previous.AddRange(_runners);
        _runners.Clear();

        var order = 0;

        foreach (var lane in Project.Score.ChannelLanes)
        {
            var runner = _previous.Find(r => r.OriginLane == lane);

            if (runner == null)
            {
                var start = _previous.Count > 0 ? _previous[0].NextSample : 0.0;
                runner = new Runner(lane, order, start);
            }

            // The lane the runner was visiting may be gone, or may have been
            // shortened under its feet.
            if (!Project.Score.Lanes.Contains(runner.Lane)) runner.Lane = lane;
            if (runner.StepIndex >= runner.Lane.Steps.Count) runner.StepIndex = 0;

            runner.Order = order++;
            _runners.Add(runner);
        }

        _master = FindMaster();
        _playing = _runners.Count > 0;

        // A line already found is not un-found by an edit: it is a sample, and the lap
        // it was measured on is the one still being played out. What an edit can do is
        // take the run away altogether, and then a project waiting on it arrives.
        SettleIfIdle();
    }

    // Scheduling

    // Emits every note that starts within the lookahead window. Safe to call at
    // any rate: nothing here depends on the frame time.
    public void Schedule(long currentSample, long lookaheadSamples, int sampleRate,
                         List<FmNoteEvent> output)
    {
        foreach (var runner in _runners) runner.AdvancePlayhead(currentSample);

        if (!_playing) return;

        var horizon = (double)(currentSample + lookaheadSamples);

        // A slice can only ever consume time, so the bound is a safety net for a
        // degenerate score rather than an expected limit. A takeover spends one turn of
        // it without running a slice, and there is at most one of those.
        for (var guard = 0; guard < 1024; guard++)
        {
            // Where the outgoing score stops. The lap line belongs to the score
            // arriving, whose runners all start exactly on it, so nothing within half a
            // sample of it is this one's to play. A lane that divides the lap evenly
            // lands on that instant to the bit, and letting it run there would sweep the
            // master itself into the slice and play the first step of the new lap twice.
            var limit = _boundary < horizon ? _boundary - Tolerance : horizon;

            var next = double.MaxValue;

            foreach (var runner in _runners)
                if (runner.NextSample < next) next = runner.NextSample;

            if (next < limit)
            {
                // Watched only while something is waiting on it, so a run with nothing
                // pending is the run that was here before.
                var watching = _incoming != null && !HasBoundary && _master != null;
                var lap = watching ? _master.Pass : 0;

                RunSlice(next, sampleRate, output);

                // The lap the piece is measured in has turned over, and the sample the
                // next one starts on is now known. It could not have been worked out
                // ahead of time: a gate decides how long a lap is, and one of them
                // throws dice.
                if (watching && _master.Pass != lap) _boundary = _master.NextSample;

                continue;
            }

            // Either the window is spent, or the lap line lies past the end of it and
            // waits for a later call.
            if (_boundary >= horizon) break;

            TakeOver();
        }

        // After the loop and never inside it.
        if (!_switched) return;

        _switched = false;
        Switched?.Invoke(_switchedAt);
    }

    // The seam: everything the outgoing score had to say has been said, and nothing of
    // the incoming one has been said yet. The project is put in before the runners are,
    // so that every step of the new score reads its own tempo, patches and mutes.
    void TakeOver()
    {
        var at = _boundary;

        _project = _incoming;
        (_incoming, _boundary) = (null, NoBoundary);

        Populate(at);

        (_switched, _switchedAt) = (true, (long)at);
    }

    // One instant of the timeline.
    void RunSlice(double time, int sampleRate, List<FmNoteEvent> output)
    {
        var startSample = (long)time;

        _slice.Clear();

        // Half a sample of tolerance: two runners on different divisions can land
        // on the same instant with the accumulated position differing in the last
        // bit.
        foreach (var runner in _runners)
            if (runner.NextSample < time + Tolerance) _slice.Add(runner);

        // Upper CHAN tiles go first, which is what puts an accent lane placed above
        // the main one in a position to colour it.
        _slice.Sort((a, b) => a.Order.CompareTo(b.Order));

        // A lock reaches no further than the instant it sits in, so nothing carries
        // over: the working bank is the patch bank again at the top of every slice.
        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            _working[channel] = Project.Patches[channel];

        foreach (var runner in _slice)
            Execute(runner, startSample, sampleRate, output);
    }

    // Reads the step the runner is sitting on, then moves the runner along.
    void Execute(Runner runner, long startSample, int sampleRate,
                 List<FmNoteEvent> output)
    {
        var stepSeconds = runner.StepSeconds(Project.Tempo);
        var lane = runner.Lane;
        var step = lane.StepAt(runner.StepIndex);

        runner.Record(startSample, lane, runner.StepIndex);

        var destination = step == null ? null
          : Descend(step, runner, startSample, stepSeconds, output);

        if (destination != null)
            (runner.Lane, runner.StepIndex) = (destination, 0);
        else
            Advance(runner);

        runner.NextSample += stepSeconds * sampleRate;
    }

    // Walks one stack from the rail row down, which is the whole of a step's
    // meaning. A gate ends the walk, so what sits above one is already done and
    // what sits below it never happens; a lock colours the notes that follow,
    // whether further down this stack or in a lane below on the same channel; a
    // note is stamped with the channel as it stands at that depth.
    //
    // Returns the lane the runner should leave for, if it met a jump.
    Lane Descend(Step step, Runner runner, long startSample, float stepSeconds,
                 List<FmNoteEvent> output)
    {
        var channel = runner.Channel;

        Lane destination = null;

        foreach (var tile in step.Tiles)
        {
            if (tile is GateTile gate && !gate.Evaluate(runner.Pass, _random)) break;

            switch (tile)
            {
                case ParamTile param:
                    Apply(param, channel);
                    break;

                case NoteTile note:
                    // A muted channel is read exactly as an unmuted one and drops its
                    // notes on the way out, which is the last thing that happens to
                    // one. Nothing above this line asks: the gates have already turned
                    // over, the locks have already coloured the working patch and the
                    // jump below is still taken, so a channel let back in is heard
                    // from where the sequence has got to rather than from a lap that
                    // was never run.
                    if (!Project.Mutes.Sounds(channel)) break;

                    // Every note of a chord takes the channel as it stands where it
                    // sits, so a lock between two of them separates the two. That
                    // reaches the pitch as well as the timbre: the working patch is
                    // what SoundingPitch is asked about, so a lock on the transpose
                    // moves the notes under it and no others.
                    output.Add(FmNoteEvent.FromPatch(
                      _working[channel],
                      Project.SoundingPitch(_working[channel], note.Note),
                      note.Length * stepSeconds, startSample));
                    break;

                // Where the runner goes next, decided here but taken afterwards:
                // the rest of the stack still belongs to this instant. A stack with
                // two reachable jumps in it hands the runner to the lower one.
                case JumpTile jump:
                    var branch = Project.Score.DestinationOf(jump);
                    if (branch != null && branch.Steps.Count > 0) destination = branch;
                    break;
            }
        }

        return destination;
    }

    // Moves one step right, or back to the origin channel when the terminator is
    // reached. The terminator takes no time of its own, so a lap lasts exactly as
    // many steps as the lane has.
    static void Advance(Runner runner)
    {
        runner.StepIndex++;

        if (runner.StepIndex < runner.Lane.Steps.Count) return;

        runner.Lane = runner.OriginLane;
        runner.StepIndex = 0;
        runner.Pass++;
    }

    // A lock always reaches the whole channel and never more than this instant, so
    // there is nothing to resolve about where it applies: it writes the working
    // patch, and whoever comes later in the pass reads it.
    //
    // Every parameter it has taken hold of is written, in target order; the rest of
    // the patch is not touched, so two locks in the same stack only disagree where
    // they engage the same parameter, and there the lower one wins by being read
    // later.
    void Apply(ParamTile param, int channel)
    {
        var absolute = param is AbsoluteParamTile;

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            if (!param.IsEngaged(target)) continue;

            if (absolute)
                ParamTargets.Set(ref _working[channel], target, param[target]);
            else
                ParamTargets.Add(ref _working[channel], target, param[target]);
        }
    }

    // Private members

    // How wide one instant is here. Gathering a slice and deciding which side of the
    // lap line a runner falls on are the same question asked twice, so they are asked
    // with the same number.
    const double Tolerance = 0.5;

    // Where the lap line sits once the master lane has said, and out of the way of
    // every comparison until it has.
    const double NoBoundary = double.MaxValue;

    readonly List<Runner> _runners = new();
    readonly List<Runner> _previous = new();
    readonly List<Runner> _slice = new();
    readonly PatchBank _working = new();
    readonly Random _random = new();

    Project _project;
    Project _incoming;
    Runner _master;

    double _boundary = NoBoundary;

    bool HasBoundary => _boundary < NoBoundary;

    bool _switched;
    long _switchedAt;

    bool _playing;
}

} // namespace Jacquard
