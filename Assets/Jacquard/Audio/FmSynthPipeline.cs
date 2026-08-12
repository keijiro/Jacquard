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

    public void Pump() {}

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

    ControlContext _context;
    RootOutputInstance _rootOutput;
    FmSynthScope _scope;
}

} // namespace Jacquard.App
