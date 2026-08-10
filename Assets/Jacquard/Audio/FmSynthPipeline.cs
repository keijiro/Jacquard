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

    public FmSynthPipeline(int maxVoices, float masterGain, int queueCapacity)
    {
        SampleRate = AudioSettings.outputSampleRate;
        _context = ControlContext.builtIn;

        _rootOutput = _context.AllocateRootOutput(
          new FmSynthRealtime(),
          new FmSynthControl
            { maxVoices = maxVoices,
              queueCapacity = queueCapacity,
              masterGain = masterGain },
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

    public bool SetFx(in SendFxRuntime fx)
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
    }

    // Private members

    ControlContext _context;
    RootOutputInstance _rootOutput;
}

} // namespace Jacquard.App
