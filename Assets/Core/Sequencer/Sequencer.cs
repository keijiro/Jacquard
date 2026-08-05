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

public sealed class Sequencer
{
    // Public state

    public Project Project { get; set; }

    public bool IsPlaying => _playing;

    public IReadOnlyList<Runner> Runners => _runners;

    // Transport

    // The first step is placed one lookahead ahead of the current audio position
    // so that playback starts cleanly rather than part way through a step.
    public void Play(long currentSample, long lookaheadSamples)
    {
        Stop();

        var start = currentSample + lookaheadSamples;
        var order = 0;

        foreach (var lane in Project.Score.ChannelLanes)
            _runners.Add(new Runner(lane, order++, start));

        _playing = _runners.Count > 0;
    }

    public void Stop()
    {
        _playing = false;
        _runners.Clear();
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

        _playing = _runners.Count > 0;
    }

    // Scheduling

    // Emits every note that starts within the lookahead window. Safe to call at
    // any rate: nothing here depends on the frame time.
    public void Schedule(long currentSample, long lookaheadSamples, int sampleRate,
                         List<FmNoteEvent> output)
    {
        foreach (var runner in _runners) runner.AdvancePlayhead(currentSample);

        if (!_playing) return;

        var horizon = currentSample + lookaheadSamples;

        // A slice can only ever consume time, so the bound is a safety net for a
        // degenerate score rather than an expected limit.
        for (var guard = 0; guard < 1024; guard++)
        {
            var next = double.MaxValue;

            foreach (var runner in _runners)
                if (runner.NextSample < next) next = runner.NextSample;

            if (next >= horizon) break;

            RunSlice(next, sampleRate, output);
        }
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
            if (runner.NextSample < time + 0.5) _slice.Add(runner);

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
                    // Every note of a chord takes the channel as it stands where it
                    // sits, so a lock between two of them separates the two.
                    output.Add(FmNoteEvent.FromPatch(_working[channel], note.Note,
                                                     note.Length * stepSeconds,
                                                     startSample));
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
    void Apply(ParamTile param, int channel)
    {
        if (param is AbsoluteParamTile)
            ParamTargets.Set(ref _working[channel], param.Target, param.Amount);
        else
            ParamTargets.Add(ref _working[channel], param.Target, param.Amount);
    }

    // Private members

    readonly List<Runner> _runners = new();
    readonly List<Runner> _previous = new();
    readonly List<Runner> _slice = new();
    readonly PatchBank _working = new();
    readonly Random _random = new();

    bool _playing;
}

} // namespace Jacquard
