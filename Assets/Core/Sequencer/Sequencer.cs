using System;
using System.Collections.Generic;

namespace Jacquard {

// Drives the runners and turns the tiles they meet into note events.
//
// Timing is expressed against the audio clock: every step lands on an absolute
// sample position computed by looking ahead of the current audio position, so a
// dropped frame delays when notes are handed over but never when they sound.
//
// Runners that fall on the same instant are executed as one slice, lowest order
// first. Parameter locks written during a slice are collected before any note of
// that slice is stamped, which is what lets an accent lane placed below the main
// lane overwrite what the main lane just played.

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
        _channels.Clear();
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

    // Takes up an edited timbre, for the one channel it belongs to. Absolute locks
    // that had already moved that channel away from its patch are forgotten, which
    // is the only sane reading of "the patch changed under you".
    public void RefreshPatch(int channel)
    {
        if (_channels.TryGetValue(channel, out var state))
            state.Patch = Project.Patches[channel];
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

        _slice.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var runner in _slice) ChannelOf(runner.Channel).ClearSlice();

        _pending.Clear();

        foreach (var runner in _slice) Execute(runner, startSample, sampleRate);

        foreach (var step in _pending) Emit(step, output);
    }

    // Executes the step a runner is sitting on, then moves it along.
    void Execute(Runner runner, long startSample, int sampleRate)
    {
        var tempo = Project.Tempo;
        var stepSeconds = runner.StepSeconds(tempo);
        var lane = runner.Lane;
        var step = lane.StepAt(runner.StepIndex);

        runner.Record(startSample, lane, runner.StepIndex);

        var jumped = false;

        if (step != null && Passes(step, runner))
        {
            // Both scopes are collected: a stack can hold a lock on the rail and
            // another one under a note, and each reaches only what it sits with.
            ApplyChannelLocks(step, runner);

            var notes = CollectNotes(step);

            if (notes != null)
                _pending.Add(new PendingStep
                  { Channel = runner.Channel,
                    StartSample = startSample,
                    StepSeconds = stepSeconds,
                    Notes = notes,
                    Locks = CollectLocks(step, runner, true) });

            // A jump only counts once the gates above it have let it through,
            // which is the whole reason a jump is worth placing.
            var jump = step.Find<JumpTile>();

            if (jump != null)
            {
                var destination = Project.Score.DestinationOf(jump);

                if (destination != null && destination.Steps.Count > 0)
                {
                    runner.Lane = destination;
                    runner.StepIndex = 0;
                    jumped = true;
                }
            }
        }

        if (!jumped) Advance(runner);

        runner.NextSample += stepSeconds * sampleRate;
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

    // Gates sit above what they govern, and they always sit at the top of a
    // stack, so every gate in the step has to agree before anything below fires.
    bool Passes(Step step, Runner runner)
    {
        foreach (var tile in step.Tiles)
            if (tile is GateTile gate && !gate.Evaluate(runner.Pass, _random))
                return false;

        return true;
    }

    static List<NoteTile> CollectNotes(Step step)
    {
        List<NoteTile> notes = null;

        foreach (var tile in step.Tiles)
            if (tile is NoteTile note) (notes ??= new List<NoteTile>()).Add(note);

        return notes;
    }

    // Where a lock reaches is decided by where it sits: with a note above it in
    // the stack it belongs to that note, and on its own it belongs to the
    // channel. noteScope picks which half of that split to collect.
    List<LockOp> CollectLocks(Step step, Runner runner, bool noteScope)
    {
        List<LockOp> ops = null;
        var seenNote = false;

        foreach (var tile in step.Tiles)
        {
            if (tile is NoteTile) { seenNote = true; continue; }
            if (tile is not ParamTile param) continue;
            if (seenNote != noteScope) continue;

            var op = param switch
            {
                AbsoluteParamTile => new LockOp(true, param.Target, param.Amount),
                RelativeParamTile => new LockOp(false, param.Target, param.Amount),
                // The running total, not the increment: it is added to the base
                // value every lap, so the ramp keeps climbing.
                _ => new LockOp(false, param.Target,
                                runner.Accumulate(param, param.Amount))
            };

            (ops ??= new List<LockOp>()).Add(op);
        }

        return ops;
    }

    // Channel scope. An absolute lock changes the channel's standing value, while
    // relative and accumulating ones only tilt this instant.
    void ApplyChannelLocks(Step step, Runner runner)
    {
        var ops = CollectLocks(step, runner, false);
        if (ops == null) return;

        var channel = ChannelOf(runner.Channel);

        foreach (var op in ops)
            if (op.Absolute)
                ParamTargets.Set(ref channel.Patch, op.Target, op.Amount);
            else
                channel.SliceDelta[op.Target] += op.Amount;
    }

    // Stamps the notes of one step, after every runner in the slice has had its
    // say about the channel parameters.
    void Emit(in PendingStep step, List<FmNoteEvent> output)
    {
        var channel = ChannelOf(step.Channel);
        var patch = channel.Patch;

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var delta = channel.SliceDelta[target];
            if (delta != 0.0f) ParamTargets.Add(ref patch, target, delta);
        }

        if (step.Locks != null)
            foreach (var op in step.Locks)
                if (op.Absolute)
                    ParamTargets.Set(ref patch, op.Target, op.Amount);
                else
                    ParamTargets.Add(ref patch, op.Target, op.Amount);

        // Every note of a chord takes the same parameters. sequencer.md settles
        // for this: a linear stack has no way to hang a lock off one voice of a
        // chord without it reading as belonging to the note directly above.
        foreach (var note in step.Notes)
            output.Add(FmNoteEvent.FromPatch(patch, note.Note,
                                             note.Length * step.StepSeconds,
                                             step.StartSample));
    }

    ChannelState ChannelOf(int channel)
    {
        if (_channels.TryGetValue(channel, out var state)) return state;

        state = new ChannelState { Patch = Project.Patches[channel] };
        _channels.Add(channel, state);
        return state;
    }

    // Private types

    readonly struct LockOp
    {
        public readonly bool Absolute;
        public readonly int Target;
        public readonly float Amount;

        public LockOp(bool absolute, int target, float amount)
          => (Absolute, Target, Amount) = (absolute, target, amount);
    }

    struct PendingStep
    {
        public int Channel;
        public long StartSample;
        public float StepSeconds;
        public List<NoteTile> Notes;
        public List<LockOp> Locks;
    }

    // Per channel parameter state. Patch starts as the channel's own patch out of
    // the bank and then carries what absolute locks have set, surviving between
    // steps; SliceDelta is the tilt applied by the relative and accumulating locks
    // of the current instant only.
    sealed class ChannelState
    {
        public FmPatch Patch;
        public readonly float[] SliceDelta = new float[ParamTargets.Count];

        public void ClearSlice() => Array.Clear(SliceDelta, 0, SliceDelta.Length);
    }

    // Private members

    readonly List<Runner> _runners = new();
    readonly List<Runner> _previous = new();
    readonly List<Runner> _slice = new();
    readonly List<PendingStep> _pending = new();
    readonly Dictionary<int, ChannelState> _channels = new();
    readonly Random _random = new();

    bool _playing;
}

} // namespace Jacquard
