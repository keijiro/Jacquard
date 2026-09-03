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
// left for this class is to allocate the root output once, to turn each of the
// application's three verbs into a message, and to carry the questions DspClock wants
// asked — everything this driver has to know about where the clock is and how far ahead
// of it a note has to be placed is measured there.

sealed class FmSynthPipeline : IFmSynthBackend
{
    public int SampleRate { get; }

    public long CurrentSample => _clock.CurrentSample;
    public long MinimumLead => _clock.MinimumLead;

    public FmSynthScope Scope => _scope;

    public FmSynthPipeline(int maxVoices, float masterGain, int queueCapacity)
    {
#if UNITY_IOS && !UNITY_EDITOR
        // First, and from the driver rather than from the app: on a device in silent
        // mode nothing below this line is heard until it is said. What is said, and why
        // it has to be said more than once, is in JacquardAudioSession.mm.
        IosAudioSession.Apply();
#endif

        SampleRate = AudioSettings.outputSampleRate;
        _context = ControlContext.builtIn;

        // Here rather than in the core's Allocate, which runs on the other side of the
        // pipeline: this is memory the main thread reads, so the main thread is what
        // owns it. A NativeArray is a handle, so the copy the audio side is given is
        // the same memory.
        _scope = FmSynthScope.Create(ScopeFrames, maxVoices);

        // The buffer is the grain everything about timing is measured in up here — the
        // finest a note can be placed, and what one lost buffer moves the clock by — so
        // it is read rather than written down.
        AudioSettings.GetDSPBufferSize(out var bufferFrames, out _);
        _clock = new DspClock(SampleRate, bufferFrames);

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

    // The report as it stood when this frame began, and the same one however many times
    // it is asked for.
    //
    // Asking is not free of consequence: two of the figures in it are gathered on the
    // control side and handed over once, so a second ask in the same frame would come
    // back without the lateness the first ask had already taken away. There is one ask a
    // frame, in Pump, and this is what it left.
    public FmSynthStatus GetStatus() => _status;

    public void Recalibrate() => _clock.Recalibrate();

    // Once a frame, and none of what it does is rendering: take the one report this
    // frame gets, and hand it and the pipe to the clock, which is where every reading
    // taken off them is made sense of.
    public void Pump()
    {
        _status = AskForStatus();

        _clock.Follow(_status);

        // The lead is asked about down the same pipe a note takes, since what is being
        // measured is exactly what a note meets on the way. One question a frame, and
        // the answer to the one before it comes back on the same message.
        if (_clock.WantsProbe)
        {
            var probe = _clock.Ask();
            _context.SendMessage(_rootOutput, ref probe);
            _clock.Answer(probe);
        }

        _clock.WatchForDropouts();
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

    // A fifteenth of a second at the rates a device hands out, which is longer than
    // any one frame will draw and short enough that what is on screen is what was
    // just heard.
    const int ScopeFrames = 4096;

    FmSynthStatus AskForStatus()
    {
        var message = default(FmSynthStatus);
        _context.SendMessage(_rootOutput, ref message);
        return message;
    }

    // This frame's report, taken once at the top of Pump. See GetStatus.
    FmSynthStatus _status;

    readonly DspClock _clock;

    ControlContext _context;
    RootOutputInstance _rootOutput;
    FmSynthScope _scope;
}

} // namespace Jacquard.App
