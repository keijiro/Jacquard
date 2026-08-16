using UnityEngine;
using UnityEngine.Audio;

using CreationParameters = UnityEngine.Audio.ProcessorInstance.CreationParameters;
using UpdateSetting = UnityEngine.Audio.ProcessorInstance.UpdateSetting;
using Response = UnityEngine.Audio.ProcessorInstance.Response;

namespace Jacquard.App {

// The scriptable audio pipeline driver, which is the one every platform but the Web
// uses.
//
// There is almost nothing here: the pipeline owns the audio thread, calls the control
// and realtime parts on its own schedule, and carries messages between them. What is
// left for this class is to allocate the root output once and to turn each of the
// application's three verbs into a message.

sealed class FmSynthPipeline : IFmSynthBackend
{
    public int SampleRate { get; }

    // Where the audio clock stands, in the reckoning the render job uses.
    //
    // Two clocks count the same samples here and they are not the same number. What
    // the main thread can read is AudioSettings.dspTime; what a note is stamped against
    // is the dspTime the pipeline hands the render job, which only reaches this side as
    // part of the status report. At boot the two agree, and the offset below is zero.
    //
    // An interruption is where they part. Measured on an iPad: thirteen seconds in the
    // background left the render job's clock 2.40s ahead of AudioSettings.dspTime, and
    // it stayed exactly that far ahead for as long as the app was watched afterwards.
    // Scheduling against the wrong one of the two puts every note two and a half
    // seconds into the render job's past, where it is triggered and — being long past
    // its own length — released in the same buffer it was started in. What that sounds
    // like is the app never making another sound: the notes are all there, all on time
    // by their own reckoning, and all of them over before anybody looks at them. The
    // one thing heard is the moment of the return, where the notes that were in flight
    // release together and the reverb answers them.
    public long CurrentSample => RawSample + _clockOffset;

    // How far past CurrentSample a note has to be placed to be heard from its first
    // sample — measured on this machine rather than assumed, because nothing about it
    // can be worked out from here.
    //
    // It used to be nought, on the reasoning that the pipeline renders when asked and
    // so can always start a note in the very next buffer. The buffer, yes; the note,
    // no. A note goes main thread, control part, realtime part, queue, and each of
    // those hops is served once a mix cycle; on top of that the two clocks do not read
    // alike. Measured on an iPad it came to four cycles, and a lead shorter than that
    // does not delay a note — the pool starts it anyway, from wherever the buffer being
    // rendered has got to, which takes the front off it. A pitch sweep so started
    // begins part way down.
    //
    // Everything above this reads it: JacquardApp adds it to both of its windows and
    // ScoreEditor to its audition, so this one number is the whole of the correction.
    public long MinimumLead => _lead;

    public FmSynthScope Scope => _scope;

    public FmSynthPipeline(int maxVoices, float masterGain, int queueCapacity)
    {
        SampleRate = AudioSettings.outputSampleRate;
        _context = ControlContext.builtIn;

        // Here rather than in the core's Allocate, which runs on the other side of the
        // pipeline: this is memory the main thread reads, so the main thread is what
        // owns it. A NativeArray is a handle, so the copy the audio side is given is
        // the same memory.
        _scope = FmSynthScope.Create(ScopeFrames, maxVoices);

        // Half of whatever a mix buffer is worth, which is the sharpest the slip below
        // can usefully be held to. Read rather than written down, so that raising the
        // buffer — which is the answer the warning gives — raises what it takes to trip
        // it by the same amount.
        //
        // It is also the finest a note can be placed by anything up here, and so the
        // grain everything about the lead below is measured in.
        AudioSettings.GetDSPBufferSize(out var bufferFrames, out _);
        _slipTolerance = 0.5 * bufferFrames / SampleRate;
        _bufferFrames = bufferFrames;

        // What the lead is until the first measurement lands, which is the first couple
        // of seconds of a launch and nothing else. Generous rather than accurate: the
        // cost of too much lead is that a live effect reaches a little further ahead,
        // and the cost of too little is the front of every note.
        _lead = UncalibratedBuffers * bufferFrames;

        _rootOutput = _context.AllocateRootOutput(
          new FmSynthRealtime(),
          new FmSynthControl
            { maxVoices = maxVoices,
              queueCapacity = queueCapacity,
              masterGain = masterGain,
              scope = _scope },
          new CreationParameters
            { controlUpdateSetting = UpdateSetting.UpdateIfDataIsAvailable,
              // Always, rather than only when notes arrive: voices are stolen and
              // freed inside the render job, so the diagnostics change on cycles
              // where nothing was sent.
              realtimeUpdateSetting = UpdateSetting.UpdateAlways });
    }

    public bool Schedule(in FmNoteEvent note)
    {
        var message = note;
        return _context.SendMessage(_rootOutput, ref message) == Response.Handled;
    }

    public bool SetFx(in MixFxRuntime fx)
    {
        var message = fx;
        return _context.SendMessage(_rootOutput, ref message) == Response.Handled;
    }

    // The report as it stood when this frame began, and the same one however many
    // times it is asked for.
    //
    // Asking is not free of consequence any more: two of the figures in it are gathered
    // on the control side and handed over once, so a second ask in the same frame would
    // come back with the lateness of a note the first ask had already taken away. There
    // is one ask a frame, in Pump, and this is what it left.
    public FmSynthStatus GetStatus() => _status;

    FmSynthStatus AskForStatus()
    {
        var message = default(FmSynthStatus);
        _context.SendMessage(_rootOutput, ref message);
        return message;
    }

    // Throws away what was measured about the path and measures it again.
    //
    // Called when something has happened that the measurement cannot have survived: a
    // spell in the background, or the audio system renegotiating its format. What is
    // not thrown away is the figure itself, which stays in force while the new one is
    // gathered — the path is unlikely to have changed by much, and a hand that presses
    // Play in the tenth of a second this takes should get the old number rather than a
    // guess.
    public void Recalibrate() => (_leadSamplesTaken, _leadWorst) = (0, 0);

    // Once a frame, and none of what it does is rendering: take the one report this
    // frame gets, follow where the render job's clock has gone, measure how far ahead a
    // note has to be placed, and notice when the audio thread has missed a deadline.
    //
    // Nothing here renders — the pipeline asks for what it needs on a thread of its own
    // — but that thread has one buffer's worth of time to answer in and no way at all of
    // saying when it did not. What happens then is that those samples are never mixed:
    // the device is handed whatever was already in front of it, and the music has a hole
    // in it with a hard edge at either end, which is heard as a bang and has nothing to
    // do with what was played.
    //
    // The measurement is one subtraction. The DSP clock counts samples the audio system
    // has actually processed, and the device consumes them at a rate its own crystal
    // decides, so a stretch that was never mixed leaves the two permanently that much
    // further apart. What is watched for is the step, not the offset.
    //
    // This lives in the driver rather than in the app because it is only true of this
    // one. AudioSettings.dspTime is Unity's audio system, and on the Web the synth does
    // not go through Unity's audio system at all — the same reading there would be
    // about a mixer nobody is listening to.
    public void Pump()
    {
        // First of all and once only: everything below reads this, and so does the app
        // after them, and the report is handed over rather than shown. See GetStatus.
        _status = AskForStatus();

        FollowTheRenderClock();
        MeasureTheLead();

        // A single reading says nothing: the clock stands still between one mix cycle
        // and the next, so what is read is the true offset less however far into a
        // buffer this frame happens to sit — measured here, a spread of a whole 5.3ms
        // against the 5.3ms one lost buffer is worth. What is not quantised is the
        // highest reading over a stretch of frames, since one of them does land just
        // after a cycle begins. At 120 frames a second a window of half a second puts
        // that within half a millisecond of the truth, which is sharp enough to see a
        // single buffer go missing.
        var slip = AudioSettings.dspTime - Time.realtimeSinceStartupAsDouble;
        if (slip > _slipPeak) _slipPeak = slip;

        if (Time.unscaledTime - _slipWindowAt < SlipWindow) return;

        var peak = _slipPeak;
        (_slipPeak, _slipWindowAt) = (double.NegativeInfinity, Time.unscaledTime);

        // The first window is what every window after it is measured against.
        if (!_slipMarked)
        {
            (_slipMark, _slipMarked) = (peak, true);
            return;
        }

        var moved = peak - _slipMark;
        _slipMark = peak;

        // Half a buffer is under what one lost buffer moves this and far over both what
        // the peak is uncertain by and what two clocks off different crystals drift
        // apart in half a second, which is a twentieth of a millisecond. Measured over
        // forty windows of a healthy stream the worst this moved was 0.44ms.
        if (moved > -_slipTolerance && moved < _slipTolerance) return;

        Debug.LogWarning(
          moved < 0.0
          ? $"Jacquard: {-moved * 1000.0:0.0}ms of audio was never mixed. The audio " +
            "thread missed its deadline and the device played something else for that " +
            "long, which is heard as a bang. What buys tolerance for it is a longer " +
            "buffer: raise Buffer size on the System panel, which the next launch takes up."
          : $"Jacquard: the audio clock ran {moved * 1000.0:0.0}ms ahead of real time, " +
            "so the stream was cut and restarted somewhere else.");
    }

    public void Dispose()
    {
        // The scope is not freed here, and that is the whole of the fix for a crash at
        // teardown that this reproduced about a third of the times the player was quit.
        // Destroy only queues the processor's disposal — the audio thread may be part
        // way through a mix when this returns, and the render job writes the scope. So
        // the free belongs on the far side of that queue, and FmSynthControl.Dispose is
        // where the audio side lets go of everything else for the same reason.
        if (_context.Exists(_rootOutput)) _context.Destroy(_rootOutput);
    }

    // Private members

    // Asks the audio thread how far ahead a note has to be placed, and takes the answer
    // once enough of them agree.
    //
    // One question a frame, and the answer to the one before it comes back on the same
    // message. A single answer is quantised to a mix cycle and can land a cycle short,
    // so the worst of a handful of them is what is taken, plus a buffer against the
    // jitter between measuring this and playing on it.
    //
    // Two things are refused. An answer from before the clock had settled — anything
    // further out than a quarter of a second — is a divergence rather than a path, and
    // belongs to FollowTheRenderClock above, which will have it inside half a second;
    // the run simply starts again until the answers are plausible. And a format that
    // has changed underneath the measurement throws the run away, since every hop it
    // timed belongs to an audio system that is no longer there.
    void MeasureTheLead()
    {
        if (_leadSamplesTaken >= LeadSamples) return;

        var probe = new FmClockProbe { id = ++_probeId, sentAtSample = CurrentSample };
        _context.SendMessage(_rootOutput, ref probe);

        // Nothing answered yet, or the same answer as last frame.
        if (probe.id == 0 || probe.id == _probeAnswered) return;
        _probeAnswered = probe.id;

        var lead = (long)probe.earliest - probe.sentAtSample;

        if (lead < 0 || lead > SampleRate / 4)
        {
            (_leadSamplesTaken, _leadWorst) = (0, 0);
            return;
        }

        if (lead > _leadWorst) _leadWorst = lead;

        if (++_leadSamplesTaken < LeadSamples) return;

        var measured = _leadWorst + _bufferFrames;
        _leadWorst = 0;

        if (measured == _lead) return;

        Debug.Log($"Jacquard: a note has to be placed {measured * 1000.0 / SampleRate:0.0}ms " +
                  $"ahead of the clock to be heard whole, which is " +
                  $"{(double)measured / _bufferFrames:0.0} buffers. That is what the " +
                  "sequencer will hand over by.");

        _lead = measured;
    }

    // What Unity's audio system says, which is the reading available at any moment and
    // the one an interruption can leave behind. See CurrentSample, which is this plus
    // however far the render job's own clock has parted from it.
    long RawSample => (long)(AudioSettings.dspTime * SampleRate);

    // Puts CurrentSample back onto the render job's clock after the two have parted,
    // and grows the lead when a note reaches the mix late even so.
    //
    // The difference between the clocks is read off the status report, which is a mix
    // cycle old at best, so a single reading is always a little too small. What is not
    // is the largest reading over a stretch of frames — the same trick the slip
    // measurement is built on, over the same window, and for the same reason.
    //
    // Nothing moves until the peak stands a tenth of a second from the offset in force.
    // Under that there is nothing to see: the reading is steady to within a buffer, and
    // what it is steady at is a property of the machine rather than a fault. Over it
    // there is no doubt — the interruption measured moved it by 2.40s — and a session
    // that is never interrupted never crosses it, which is what leaves an ordinary run
    // on exactly the clock it has always run on.
    void FollowTheRenderClock()
    {
        var status = _status;

        // The audio system has renegotiated its format — a device plugged in, a route
        // moved — so the path is to be measured again and the difference between the
        // two clocks is no longer the one this machine was showing before. Both start
        // over; until the baseline is back, nothing is corrected.
        if (status.formatGeneration != _generation)
        {
            _generation = status.formatGeneration;
            (_clockBaseline, _baselineWindows) = (long.MinValue, 0);
            Recalibrate();
        }

        // Nothing has been rendered yet, so there is nothing to be behind.
        if (status.dspSample == 0) return;

        // Only a report that has moved says anything new. An unchanged one is the same
        // reading grown staler, and its distance from a clock that has gone on without
        // it is a lateness rather than a divergence — which is also what keeps the
        // first second quiet, where the reports have not started arriving and the
        // difference reads as most of a second.
        if (status.dspSample == _lastReported) return;
        _lastReported = status.dspSample;

        var difference = (long)status.dspSample - RawSample;
        if (difference > _clockPeak) _clockPeak = difference;

        if (status.lateSamples > _latePeak) _latePeak = status.lateSamples;
        _startedInWindow += status.startedNotes;

        if (Time.unscaledTime - _clockWindowAt < SlipWindow) return;

        var peak = _clockPeak;
        var (late, started) = (_latePeak, _startedInWindow);
        (_clockPeak, _latePeak, _startedInWindow, _clockWindowAt) =
          (long.MinValue, 0, 0, Time.unscaledTime);

        var band = SampleRate / 10;

        // The first windows are what every window after them is measured against.
        //
        // The two clocks do not read alike even when nothing has gone wrong: measured
        // on the iPad, the render job's stood a steady three buffers under the audio
        // system's. That difference is margin — a note is stamped against the one and
        // rendered against the other, so those three buffers are three buffers of head
        // start on top of LiveLead — and putting the clock back to a difference of zero
        // rather than back to this is what took the front off every note once the first
        // interruption had been answered: 11ms of each of them, measured, which on a
        // patch with a pitch sweep is heard as the sweep starting part way down.
        //
        // Four windows and not one, because the first report of all is most of a second
        // stale and a peak taken across two seconds cannot be that one.
        if (_baselineWindows < BaselineWindows)
        {
            if (peak > _clockBaseline) _clockBaseline = peak;
            _baselineWindows++;
            return;
        }

        var moved = peak - _clockBaseline - _clockOffset;

        // The coarse pass. Nothing gathered under the offset being replaced says
        // anything about the one replacing it, so the lateness of this window goes with
        // it and the trim waits for evidence taken on the new clock.
        if (System.Math.Abs(moved) >= band)
        {
            Debug.LogWarning(
              $"Jacquard: the render clock stands {(double)moved / SampleRate:0.00}s " +
              "from where the audio system says it does, which is what an interruption " +
              "leaves behind it. Scheduling has been moved back onto the render clock.");

            (_clockOffset, _lateBefore) = (peak - _clockBaseline, 0);
            return;
        }

        // No note started in this window, so nothing here was late — which is not the
        // same as anything here being on time. A piece with a note every half second
        // spends half its windows empty, and breaking the pair below on one of those is
        // what left a clock 12ms out standing for nine seconds when this was measured.
        if (started == 0) return;

        // Past the dead band it is the transport that is behind rather than the clock —
        // the seconds after an interruption, where the sequencer is catching up — and
        // shifting the clock for that would only take the sound further from the score.
        // Nothing about it says anything either way, so the pair below starts again.
        if (late >= band)
        {
            _lateBefore = 0;
            return;
        }

        // The trim, and what it moves is the lead rather than the clock. A note that
        // arrives after its own start says the path took longer than the lead allows
        // for; it says nothing about where the render job thinks it is. Measuring the
        // lead is MeasureTheLead's job and this is only what catches what that missed,
        // but the two have to answer in the same currency or one will be found undoing
        // the other.
        //
        // It asks for the fault in two windows running. A frame that took too long is
        // late in exactly this way and is over by the next window — the sequencer
        // already answers that one, by taking the head off a note rather than moving it
        // — where a lead that is short puts every note after its own start for as long
        // as it stands. What is taken is the smaller of the two readings, which is the
        // part that is standing rather than the part that was a hitch.
        //
        // A note inside the buffer it was due in is as close as anything up here can
        // put it, so that is the floor as well as the margin added on top.
        var standing = System.Math.Min(late, _lateBefore);
        _lateBefore = late;

        if (standing <= _bufferFrames) return;

        Debug.LogWarning(
          $"Jacquard: notes were reaching the mix {standing * 1000.0 / SampleRate:0.0}ms " +
          "after their own start, so the front of each of them was never rendered. " +
          "The lead has been grown by that much.");

        _lead += standing + _bufferFrames;
        _lateBefore = 0;
    }

    // A fifteenth of a second at the rates a device hands out, which is longer than
    // any one frame will draw and short enough that what is on screen is what was
    // just heard.
    const int ScopeFrames = 4096;

    // How long a window the slip is judged over, and how far the DSP clock has stood
    // above the wall clock — which a stream that has lost nothing holds steady. The
    // clock follower's window as well, which takes its peak over the same stretch for
    // the same reason. See Pump.
    const float SlipWindow = 0.5f;

    // How many of those windows the difference between the two clocks is watched over
    // before it is taken as the difference this machine has when nothing is wrong.
    const int BaselineWindows = 4;

    // How many answers the lead is taken from — one a frame, so a tenth of a second of
    // asking — and what it stands at until the first of them lands. See MeasureTheLead.
    const int LeadSamples = 8;
    const int UncalibratedBuffers = 6;

    readonly double _slipTolerance;
    readonly int _bufferFrames;

    // How far the render job's clock stands ahead of the audio system's, in samples,
    // the highest difference seen in the window being gathered, and the difference
    // this machine shows when nothing has gone wrong — which is what the offset is
    // measured from rather than from zero.
    long _clockOffset;
    long _clockPeak = long.MinValue;
    long _clockBaseline = long.MinValue;
    int _baselineWindows;

    // What MinimumLead answers, the worst answer of the run being gathered, how far
    // that run has got, and the numbering that pairs a probe with its answer.
    long _lead, _leadWorst;
    int _leadSamplesTaken, _probeId, _probeAnswered;

    // The format the measurements above belong to.
    int _generation;

    // This frame's report, taken once at the top of Pump. See GetStatus.
    FmSynthStatus _status;
    int _latePeak, _lateBefore, _startedInWindow;
    float _clockWindowAt;
    ulong _lastReported;

    double _slipPeak = double.NegativeInfinity;
    double _slipMark;
    bool _slipMarked;
    float _slipWindowAt;

    ControlContext _context;
    RootOutputInstance _rootOutput;
    FmSynthScope _scope;
}

} // namespace Jacquard.App
