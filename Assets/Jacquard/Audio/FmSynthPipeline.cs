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

    public long CurrentSample => (long)(AudioSettings.dspTime * SampleRate);

    // The pipeline renders when asked, so the next buffer is always available.
    public long MinimumLead => 0;

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
        AudioSettings.GetDSPBufferSize(out var bufferFrames, out _);
        _slipTolerance = 0.5 * bufferFrames / SampleRate;

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

    public FmSynthStatus GetStatus()
    {
        var message = default(FmSynthStatus);
        _context.SendMessage(_rootOutput, ref message);
        return message;
    }

    // Once a frame, and under this driver the only thing it has to do is notice when
    // the audio thread has missed a deadline.
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
            "buffer: raise DSP Buffer Size in Project Settings > Audio."
          : $"Jacquard: the audio clock ran {moved * 1000.0:0.0}ms ahead of real time, " +
            "so the stream was cut and restarted somewhere else.");
    }

    public void Dispose()
    {
        if (_context.Exists(_rootOutput)) _context.Destroy(_rootOutput);
        _scope.Dispose();
    }

    // Private members

    // A fifteenth of a second at the rates a device hands out, which is longer than
    // any one frame will draw and short enough that what is on screen is what was
    // just heard.
    const int ScopeFrames = 4096;

    // How long a window the slip is judged over, and how far the DSP clock has stood
    // above the wall clock — which a stream that has lost nothing holds steady. See
    // Pump.
    const float SlipWindow = 0.5f;

    readonly double _slipTolerance;

    double _slipPeak = double.NegativeInfinity;
    double _slipMark;
    bool _slipMarked;
    float _slipWindowAt;

    ControlContext _context;
    RootOutputInstance _rootOutput;
    FmSynthScope _scope;
}

} // namespace Jacquard.App
