using UnityEngine;

namespace Jacquard.App {

// What the main thread is allowed to believe about the audio system's clock.
//
// Three things live here and they are one subject. Where the clock is, which is what a
// note's position is measured from. How far ahead of it a note has to be placed to be
// heard from its first sample. And whether the stream has lost anything, which is the
// one fault down there that leaves no trace up here.
//
// None of the three can be worked out; all of them are measured, and two of them are
// measured against the render job rather than against Unity's audio system, because
// those are not the same clock — see CurrentSample, which is where that begins.
//
// It belongs to the pipeline driver and to nothing else. AudioSettings.dspTime is
// Unity's audio system, and on the Web the synth does not go through Unity's audio
// system at all: the same readings there would be about a mixer nobody is listening to,
// and that driver knows its own numbers by construction.
//
// Nothing here sends a message. The questions it wants asked are handed to the driver
// to carry — see Ask and Answer — since the pipe belongs to the driver and the arithmetic
// belongs here.

sealed class DspClock
{
    public DspClock(int sampleRate, int bufferFrames)
    {
        (_sampleRate, _bufferFrames) = (sampleRate, bufferFrames);

        // Half of whatever a mix buffer is worth, which is the sharpest the slip can
        // usefully be held to. Taken from the buffer rather than written down, so that
        // raising it — which is the answer the warning gives — raises what it takes to
        // trip it by the same amount.
        _slipTolerance = 0.5 * bufferFrames / sampleRate;

        // What the lead is until the first measurement lands, which is the first couple
        // of seconds of a launch and nothing else. Generous rather than accurate: the
        // cost of too much lead is that a live effect reaches a little further ahead,
        // and the cost of too little is the front of every note.
        _lead = UncalibratedBuffers * bufferFrames;
    }

    // Where the audio clock stands, in the reckoning the render job uses.
    //
    // Two clocks count the same samples here and they are not the same number. What the
    // main thread can read is AudioSettings.dspTime; what a note is stamped against is
    // the dspTime the pipeline hands the render job, which only reaches this side as
    // part of the status report. At boot the two agree, and the offset is zero.
    //
    // An interruption is where they part. Measured on an iPad: thirteen seconds in the
    // background left the render job's clock 2.40s ahead of AudioSettings.dspTime, and
    // it stayed exactly that far ahead for as long as the app was watched afterwards.
    // Scheduling against the wrong one of the two puts every note two and a half
    // seconds into the render job's past, where it is triggered and — being long past
    // its own length — released in the same buffer it was started in. What that sounds
    // like is the app never making another sound: the notes are all there, all on time
    // by their own reckoning, and all of them over before anybody looks at them. The one
    // thing heard is the moment of the return, where the notes that were in flight
    // release together and the reverb answers them.
    public long CurrentSample => RawSample + _offset;

    // How far past CurrentSample a note has to be placed to be heard from its first
    // sample — measured on this machine rather than assumed, because nothing about it
    // can be worked out from here.
    //
    // The driver used to answer nought, on the reasoning that the pipeline renders when
    // asked and so can always start a note in the very next buffer. The buffer, yes; the
    // note, no. A note goes main thread, control part, realtime part, queue, and each of
    // those hops is served once a mix cycle; on top of that the two clocks do not read
    // alike. Measured on an iPad it came to between one and three buffers, and a lead
    // shorter than that does not delay a note — the pool starts it anyway, from wherever
    // the buffer being rendered has got to, which takes the front off it. A pitch sweep
    // so started begins part way down.
    //
    // Everything above the driver reads it: JacquardApp adds it to both of its windows
    // and ScoreEditor to its audition, so this one number is the whole of the correction.
    public long MinimumLead => _lead;

    // Throws away what was measured about the path and measures it again.
    //
    // Called when something has happened that the measurement cannot have survived: a
    // spell in the background, or the audio system renegotiating its format. What is not
    // thrown away is the figure itself, which stays in force while the new one is
    // gathered — the path is unlikely to have changed by much, and a hand that presses
    // Play in the tenth of a second this takes should get the old number rather than a
    // guess.
    public void Recalibrate() => (_leadSamplesTaken, _leadWorst) = (0, 0);

    // Puts CurrentSample back onto the render job's clock after the two have parted, and
    // grows the lead when a note reaches the mix late even so. Once a frame, with the
    // one report that frame has.
    //
    // The difference between the clocks is read off that report, which is a mix cycle
    // old at best, so a single reading is always a little too small. What is not is the
    // largest reading over a stretch of frames — the same trick the slip measurement is
    // built on, over the same window, and for the same reason.
    //
    // Nothing moves until the peak stands a tenth of a second from the offset in force.
    // Under that there is nothing to see: the reading is steady to within a buffer, and
    // what it is steady at is a property of the machine rather than a fault. Over it
    // there is no doubt — the interruption measured moved it by 2.40s — and a session
    // that is never interrupted never crosses it, which is what leaves an ordinary run
    // on exactly the clock it has always run on.
    public void Follow(in FmSynthStatus status)
    {
        // The audio system has renegotiated its format — a device plugged in, a route
        // moved — so the path is to be measured again and the difference between the two
        // clocks is no longer the one this machine was showing before. Both start over;
        // until the baseline is back, nothing is corrected.
        if (status.formatGeneration != _generation)
        {
            _generation = status.formatGeneration;
            (_baseline, _baselineWindows) = (long.MinValue, 0);
            Recalibrate();
        }

        // Nothing has been rendered yet, so there is nothing to be behind.
        if (status.dspSample == 0) return;

        // Only a report that has moved says anything new. An unchanged one is the same
        // reading grown staler, and its distance from a clock that has gone on without
        // it is a lateness rather than a divergence — which is also what keeps the first
        // second quiet, where the reports have not started arriving and the difference
        // reads as most of a second.
        if (status.dspSample == _lastReported) return;
        _lastReported = status.dspSample;

        var difference = (long)status.dspSample - RawSample;
        if (difference > _peak) _peak = difference;

        if (status.lateSamples > _latePeak) _latePeak = status.lateSamples;
        _startedInWindow += status.startedNotes;

        if (Time.unscaledTime - _windowAt < Window) return;

        var peak = _peak;
        var (late, started) = (_latePeak, _startedInWindow);
        (_peak, _latePeak, _startedInWindow, _windowAt) =
          (long.MinValue, 0, 0, Time.unscaledTime);

        var band = _sampleRate / 10;

        // The first windows are what every window after them is measured against.
        //
        // The two clocks do not read alike even when nothing has gone wrong: measured on
        // the iPad, the render job's stood a steady three buffers under the audio
        // system's. That difference is margin — a note is stamped against the one and
        // rendered against the other, so those three buffers are three buffers of head
        // start on top of the app's own handover window — and putting the clock back to
        // a difference of zero rather than back to this is what took the front off every
        // note once the first interruption had been answered: 11ms of each of them,
        // measured, which on a patch with a pitch sweep is heard as the sweep starting
        // part way down.
        //
        // Four windows and not one, because the first report of all is most of a second
        // stale and a peak taken across two seconds cannot be that one.
        if (_baselineWindows < BaselineWindows)
        {
            if (peak > _baseline) _baseline = peak;

            // What the baseline may hold is a margin and not a parting. The two clocks
            // reading a few buffers apart is this machine showing its own shape, and it
            // is kept; seconds apart is an interruption that happened before this run
            // began, and adopting *that* as the shape of the machine is a run that can
            // never make a sound — every note is stamped that far into the render job's
            // past, triggered and released in the buffer it arrives in, and the pass
            // below sees a clock that has not moved because the parting was already in
            // the number it measures from.
            //
            // Measured 2026-08-22 in an editor that had been open for hours and cycled
            // in and out of play mode through two audio interruptions: the render job's
            // clock stood 110.21s ahead at the first frame of every new session, the
            // baseline took all of it, and the app was silent for the whole session with
            // nothing on the console. Capped, the first window past the baseline reads
            // the parting as what it is and the coarse pass puts the clock back.
            //
            // The cap is the same band the coarse pass calls no doubt at all, which is
            // where the two statements belong together: past a tenth of a second the
            // clocks have parted, whether that happened while this run watched or
            // before it started.
            if (++_baselineWindows == BaselineWindows)
                _baseline = System.Math.Clamp(_baseline, -(long)band, band);

            return;
        }

        var moved = peak - _baseline - _offset;

        // The coarse pass. Nothing gathered under the offset being replaced says
        // anything about the one replacing it, so the lateness of this window goes with
        // it and the trim waits for evidence taken on the new clock.
        if (System.Math.Abs(moved) >= band)
        {
            Debug.LogWarning(
              $"Jacquard: the render clock stands {(double)moved / _sampleRate:0.00}s " +
              "from where the audio system says it does, which is what an interruption " +
              "leaves behind it. Scheduling has been moved back onto the render clock.");

            (_offset, _lateBefore) = (peak - _baseline, 0);
            return;
        }

        // No note started in this window, so nothing here was late — which is not the
        // same as anything here being on time. A piece with a note every half second
        // spends half its windows empty, and breaking the pair below on one of those is
        // what left a clock 12ms out standing for nine seconds when this was measured.
        if (started == 0) return;

        // Past the dead band it is the transport that is behind rather than the clock —
        // the seconds after an interruption, where the sequencer is catching up — and
        // growing the lead for that would only take the sound further from the score.
        // Nothing about it says anything either way, so the pair below starts again.
        if (late >= band)
        {
            _lateBefore = 0;
            return;
        }

        // The trim, and what it moves is the lead rather than the clock. A note that
        // arrives after its own start says the path took longer than the lead allows
        // for; it says nothing about where the render job thinks it is. Measuring the
        // lead is Ask and Answer's job and this is only what catches what they missed.
        //
        // It asks for the fault in two windows running. A frame that took too long is
        // late in exactly this way and is over by the next window — the sequencer
        // already answers that one, by taking the head off a note rather than moving it
        // — where a lead that is short puts every note after its own start for as long
        // as it stands. What is taken is the smaller of the two readings, which is the
        // part that is standing rather than the part that was a hitch.
        //
        // A note inside the buffer it was due in is as close as anything up here can put
        // it, so that is the floor as well as the margin added on top. What takes this
        // back down is the next measurement, which replaces the figure outright.
        var standing = System.Math.Min(late, _lateBefore);
        _lateBefore = late;

        if (standing <= _bufferFrames) return;

        Debug.LogWarning(
          $"Jacquard: notes were reaching the mix {standing * 1000.0 / _sampleRate:0.0}ms " +
          "after their own start, so the front of each of them was never rendered. " +
          "The lead has been grown by that much.");

        _lead += standing + _bufferFrames;
        _lateBefore = 0;
    }

    // Whether the lead still wants asking about, which is until enough answers have
    // agreed and again from every Recalibrate.
    public bool WantsProbe => _leadSamplesTaken < LeadSamples;

    // The question: where the main thread believes the clock is, numbered so that the
    // answer can be told from the one before it. The driver carries this down the same
    // pipe a note takes, which is the whole point of asking this way.
    public FmClockProbe Ask()
      => new FmClockProbe { id = ++_probeId, sentAtSample = CurrentSample };

    // And the answer, which is the earliest sample the render job could still fill when
    // the question reached it.
    //
    // A single answer is quantised to a mix cycle and can land a cycle short, so the
    // worst of a handful of them is what is taken, plus a buffer against the jitter
    // between measuring this and playing on it.
    //
    // An answer from before the clock had settled — anything further out than a quarter
    // of a second — is a divergence rather than a path, and belongs to Follow, which
    // will have it inside half a second; the run simply starts again until the answers
    // are plausible.
    public void Answer(in FmClockProbe probe)
    {
        // Nothing answered yet, or the same answer as last frame.
        if (probe.id == 0 || probe.id == _probeAnswered) return;
        _probeAnswered = probe.id;

        var lead = (long)probe.earliest - probe.sentAtSample;

        if (lead < 0 || lead > _sampleRate / 4)
        {
            Recalibrate();
            return;
        }

        if (lead > _leadWorst) _leadWorst = lead;

        if (++_leadSamplesTaken < LeadSamples) return;

        var measured = _leadWorst + _bufferFrames;
        _leadWorst = 0;

        if (measured == _lead) return;

        Debug.Log($"Jacquard: a note has to be placed {measured * 1000.0 / _sampleRate:0.0}ms " +
                  $"ahead of the clock to be heard whole, which is " +
                  $"{(double)measured / _bufferFrames:0.0} buffers. That is what the " +
                  "sequencer will hand over by.");

        _lead = measured;
    }

    // Notices when the audio thread has missed its deadline. Once a frame.
    //
    // Nothing up here renders — the pipeline asks for what it needs on a thread of its
    // own — but that thread has one buffer's worth of time to answer in and no way at
    // all of saying when it did not. What happens then is that those samples are never
    // mixed: the device is handed whatever was already in front of it, and the music has
    // a hole in it with a hard edge at either end, which is heard as a bang and has
    // nothing to do with what was played.
    //
    // The measurement is one subtraction. The DSP clock counts samples the audio system
    // has actually processed, and the device consumes them at a rate its own crystal
    // decides, so a stretch that was never mixed leaves the two permanently that much
    // further apart. What is watched for is the step, not the offset.
    public void WatchForDropouts()
    {
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

        if (Time.unscaledTime - _slipWindowAt < Window) return;

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

    // Private members

    // What Unity's audio system says, which is the reading available at any moment and
    // the one an interruption can leave behind. See CurrentSample, which is this plus
    // however far the render job's own clock has parted from it.
    long RawSample => (long)(AudioSettings.dspTime * _sampleRate);

    // How long a window everything here is judged over: how far the DSP clock has stood
    // above the wall clock, which a stream that has lost nothing holds steady, and the
    // difference between the two clocks, whose peak is taken over the same stretch for
    // the same reason.
    const float Window = 0.5f;

    // How many of those windows the difference between the two clocks is watched over
    // before it is taken as the difference this machine has when nothing is wrong.
    const int BaselineWindows = 4;

    // How many answers the lead is taken from — one a frame, so a tenth of a second of
    // asking — and what it stands at until the first of them lands.
    const int LeadSamples = 8;
    const int UncalibratedBuffers = 6;

    readonly int _sampleRate, _bufferFrames;
    readonly double _slipTolerance;

    // How far the render job's clock stands ahead of the audio system's, in samples, the
    // highest difference seen in the window being gathered, and the difference this
    // machine shows when nothing has gone wrong — which is what the offset is measured
    // from rather than from zero.
    long _offset;
    long _peak = long.MinValue;
    long _baseline = long.MinValue;
    int _baselineWindows;

    // What MinimumLead answers, the worst answer of the run being gathered, how far that
    // run has got, and the numbering that pairs a probe with its answer.
    long _lead, _leadWorst;
    int _leadSamplesTaken, _probeId, _probeAnswered;

    // The format everything above was measured against.
    int _generation;

    int _latePeak, _lateBefore, _startedInWindow;
    float _windowAt;
    ulong _lastReported;

    double _slipPeak = double.NegativeInfinity;
    double _slipMark;
    bool _slipMarked;
    float _slipWindowAt;
}

} // namespace Jacquard.App
