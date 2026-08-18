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
// A lock lasts as long as the step it sits on. Inside a pass that is the rule above:
// it colours whatever is processed after it. Across passes it is what lets a lane whose
// steps are longer than the ones under it hold the channel through the instants between
// its own steps — every lane is asked at its own place in every pass, and it either
// reads the step it has come to or puts back the one it is still on. Nothing outlives
// its own step, so a channel no lane is holding starts each instant from its patch
// again.
//
// One score can be played out and another begun without stopping, and the seam is the
// turn of the master lane — see Score.MasterLane, which is where a piece says how long
// it is. A lap line cannot be worked out in advance, since a gate decides how far a lap
// goes and one of them throws dice, so it is found as it happens: a project waiting to
// come in makes the loop watch the master's lap count across each slice, and the sample
// the next lap starts on becomes a nearer horizon that the outgoing score is run out to.
// The takeover then happens inside the same pass, which is what leaves nothing between
// the two scores and nothing over them.
//
// The turn of the master lane is also when a lane starts. A CHAN switched off stops when
// it reaches the end of its lane, and a CHAN switched on runs from the sample the next
// master lap begins on — the same sample, so a lane comes back in step with the rest
// rather than wherever the hand happened to land. A lane drawn while the sequence plays
// waits for that moment too, since there is nothing else for it to be in step with.
//
// The master lane itself is never off. It is what hands out that moment, so a silent
// master would leave every other lane with nothing to start on; its switch is taken and
// kept, and ignored while it is the master. Everything here therefore leans on one
// invariant: while the transport runs, the master runner is running, so a non-empty
// runner list has at least one runner with a sample in it and time always advances.

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

    // Puts a runner on every CHAN lane of the project, and starts the ones that are
    // switched on — all on the same sample, since this is a lap beginning. A standing
    // start and a score taking over from another one are the same act; what differs is
    // only where that sample comes from.
    //
    // A lane switched off gets its seat and no sample, so it sits out until a lap turns
    // over with it switched on. The master lane runs whatever its switch says.
    //
    // The master is read once rather than per lane, both because finding it sorts the
    // lanes and because _master is not assigned until the seats exist.
    void Populate(double startSample)
    {
        _runners.Clear();

        var master = Project.Score.MasterLane;
        var order = 0;

        foreach (var lane in Project.Score.ChannelLanes)
        {
            var running = lane == master || lane.Channel.Enabled;
            _runners.Add(new Runner(lane, order++,
                                    running ? startSample : Runner.Never));
        }

        _master = FindMaster();
        _playing = _runners.Count > 0;
    }

    // Puts a runner at the top of its own lane on the given sample. Everything about
    // where it was is dropped, which is the whole difference between this and a lane
    // that never stopped: a stopped runner's position is a sample in the past, so
    // resuming from it would emit steps behind the clock, and its lap count belongs to
    // a run that has ended. A lane coming back and a lane drawn a moment ago both
    // arrive here, and they have to sound the same.
    //
    // The locks it was holding are among the things dropped, for the same reason: they
    // were the reading of a step in that ended run.
    static void Start(Runner runner, double sample)
    {
        (runner.Lane, runner.StepIndex, runner.Pass, runner.NextSample) =
          (runner.OriginLane, 0, 0, sample);

        runner.BeginHold(0.0);
    }

    // Starts every switched-on lane that is sitting out, on the sample a lap begins.
    void StartPending(double sample)
    {
        foreach (var runner in _runners)
            if (!runner.Running && runner.OriginLane.Channel.Enabled)
                Start(runner, sample);
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
    // CHAN lane waits for a lap to turn over, since the turn of the piece is what a
    // lane starts on and there is nothing else for a new one to be in step with.
    //
    // A lane switched off or on here is not touched at all. Off takes effect when the
    // runner reaches the end of its lane and on when the master comes round, and both
    // of those are read where they happen rather than mirrored onto the runner, so a
    // hand that switches a lane off and back on inside one lap has changed nothing.
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
                // Never, so the new lane sits out until a lap begins. The one case
                // that reads a sample here is a lane that is the master the moment it
                // arrives — a channel one lane dropped above the others — and the
                // repair in Schedule is what gives it one.
                runner = new Runner(lane, order, Runner.Never);
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

        // The master runner has to be running, since it is what every other lane starts
        // on, and two edits can leave one that is not: deleting the master lane hands the
        // title to whichever lane is now topmost, which may be one that has stopped, and
        // putting a project in outright reassigns it over the runners of the score going
        // out. The repair is here rather than in Resync because it needs a sample and
        // Resync has no clock — this is the same standing start Play uses, one lookahead
        // ahead of the audio position so that the first step is not part way through.
        if (!_master.Running) Start(_master, currentSample + lookaheadSamples);

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
                // The lap count is read across every slice now rather than only while a
                // score waits, since the turn of the piece is also what starts a lane.
                var watching = _incoming != null && !HasBoundary;
                var lap = _master.Pass;

                RunSlice(next, sampleRate, output);

                // The lap the piece is measured in has turned over, and the sample the
                // next one starts on is now known. It could not have been worked out
                // ahead of time: a gate decides how long a lap is, and one of them
                // throws dice.
                //
                // Two things want that sample and only one of them gets it. A score
                // waiting to come in takes it as the line to stop on, and the lanes
                // waiting to start would be started on the far side of that line — where
                // the incoming score's own Populate is about to seat them anyway, so
                // starting them here is work that is thrown away a moment later.
                if (_master.Pass != lap)
                {
                    if (watching) _boundary = _master.NextSample;
                    else if (!HasBoundary) StartPending(_master.NextSample);
                }

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

        // Whoever has something to say at this instant. That is the runners due on it,
        // and also the ones part way through a step whose locks are still standing: a
        // lane in the middle of a step reads nothing and sounds nothing, but the hold it
        // opened has not run out and the channel is still coloured by it.
        foreach (var runner in _runners)
            if (Lands(runner, time) || Holding(runner, time)) _slice.Add(runner);

        // Upper CHAN tiles go first, which is what puts an accent lane placed above
        // the main one in a position to colour it.
        _slice.Sort((a, b) => a.Order.CompareTo(b.Order));

        // A lock reaches no further than the step it sits on, so the working bank is the
        // patch bank again at the top of every slice; what is still standing is put back
        // by the lane holding it, in the place in the pass that lane occupies.
        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            _working[channel] = Project.Patches[channel];

        foreach (var runner in _slice)
        {
            if (Lands(runner, time))
                Execute(runner, startSample, sampleRate, output);
            else
                Reapply(runner);
        }
    }

    // Whether this instant is the one the runner is due on.
    //
    // Half a sample of tolerance: two runners on different divisions can land on the
    // same instant with the accumulated position differing in the last bit.
    static bool Lands(Runner runner, double time)
      => runner.NextSample < time + Tolerance;

    // Whether the step the runner is on is still standing here, which is what keeps its
    // locks on the channel.
    //
    // The same tolerance read from the other side, and deliberately the complement of
    // Lands: a step's hold runs to exactly where the next step begins, so a running lane
    // is either due or in the middle of a step and never neither. What the two questions
    // do not answer alike is a lane that has stopped — its position is out past every
    // comparison while the last step it played is still sounding, and the hold is what
    // carries its locks to the end of it.
    static bool Holding(Runner runner, double time)
      => time + Tolerance <= runner.HoldUntil;

    // Reads the step the runner is sitting on, then moves the runner along.
    void Execute(Runner runner, long startSample, int sampleRate,
                 List<FmNoteEvent> output)
    {
        var stepSeconds = runner.StepSeconds(Project.Tempo);
        var lane = runner.Lane;
        var step = lane.StepAt(runner.StepIndex);

        // Where the step after this one falls, which is everything that wants to know
        // how long this one lasts: the position to move on to, the moment a lane that
        // ends here falls silent, and the end of the window this step's locks hold for.
        var after = runner.NextSample + stepSeconds * sampleRate;

        runner.Record(startSample, lane, runner.StepIndex);

        // Opened before the descent and whether or not there is a step to descend, so an
        // empty cell releases what the cell before it was holding.
        runner.BeginHold(after);

        var destination = step == null ? null
          : Descend(step, runner, startSample, stepSeconds, output);

        // A jump is taken here rather than in the descent, since the rest of the stack
        // still belonged to this instant.
        if (destination != null)
        {
            (runner.Lane, runner.StepIndex) = (destination, 0);
            runner.NextSample = after;
            return;
        }

        var stopped = !Advance(runner);

        if (!stopped)
        {
            runner.NextSample = after;
            return;
        }

        // The playhead is told where the lane ends rather than cleared, so that it goes
        // out exactly when the last step is heard. Clearing would empty the queue of
        // everything already scheduled and unheard, which is a lookahead of steps that
        // still sound — the drawing would stop before the music did. The sample is the
        // one after the last step and not this slice's own: a marker sharing a sample
        // with the last step is dequeued in the same breath as it, and the final cell of
        // the lane would never light.
        runner.Record((long)after, null, -1);
        runner.NextSample = Runner.Never;
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

                    // Kept as well as applied, so the steps of the lanes below that fall
                    // inside this one meet it too.
                    runner.Hold(param);
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
    //
    // False when the lane the runner was born from is switched off, which is where that
    // switch takes effect: a lane plays out the lap it is on and stops at the end of it,
    // never part way through. The reading is taken here rather than kept on the runner so
    // that a switch thrown twice inside one lap has done nothing.
    //
    // The master is asked for by identity and not by position. Finding the master lane
    // sorts every lane in the score, which is not a thing to do once a step, and between
    // an edit and its Resync the answer would not be the runner the lap is actually
    // counted on.
    //
    // A runner that stops is still wound back to the top of its own lane. A lap can end
    // on a branch lane, and one left parked there would come back to a lane it was only
    // visiting.
    bool Advance(Runner runner)
    {
        runner.StepIndex++;

        if (runner.StepIndex < runner.Lane.Steps.Count) return true;

        runner.Lane = runner.OriginLane;
        runner.StepIndex = 0;

        if (runner != _master && !runner.OriginLane.Channel.Enabled) return false;

        runner.Pass++;
        return true;
    }

    // Puts back the locks of a step that is still standing, at the place in the pass
    // the lane holding them occupies.
    //
    // Read there rather than at the top of the slice, so a held lock reaches exactly
    // what a fresh one would — the lanes below it and nothing above — and the rule
    // that a lock goes above the sounds it colours holds whether or not the instant
    // being played is the one it was placed on.
    void Reapply(Runner runner)
    {
        var channel = runner.Channel;

        foreach (var tile in runner.HeldLocks) Apply(tile, channel);
    }

    // A lock always reaches the whole channel and never outlasts the step it sits on, so
    // there is nothing to resolve about where it applies: it writes the working patch,
    // and whoever comes later in the pass reads it.
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

    // How wide one instant is here. Gathering a slice, telling a lane that is due from
    // one part way through a step, and deciding which side of the lap line a runner
    // falls on are the same question asked three times, so they are asked with the same
    // number.
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
