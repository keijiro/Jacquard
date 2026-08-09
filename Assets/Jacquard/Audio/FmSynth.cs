using UnityEngine;
using UnityEngine.Audio;

using CreationParameters = UnityEngine.Audio.ProcessorInstance.CreationParameters;
using UpdateSetting = UnityEngine.Audio.ProcessorInstance.UpdateSetting;
using Response = UnityEngine.Audio.ProcessorInstance.Response;

namespace Jacquard.App {

// Application facing handle for the FM synth.
//
// The synth has no concept of a patch or of a currently selected sound: the only
// thing you can do with it is schedule an FmNoteEvent, which carries its own
// timbre and the exact sample position at which it should start. That is what lets
// a parameter lock alter one note without any state having to be set beforehand.
//
// The one exception is the send effects, which are shared by every note and so have
// nowhere else to live. Even they are held rather than sequenced: SetFx replaces what
// the audio thread is using, with no position and no schedule.

public sealed class FmSynth : System.IDisposable
{
    public int MaxVoices { get; }
    public int SampleRate { get; }

    // Current position of the audio clock in samples. Scheduling is expressed
    // against this rather than against frame time.
    public long CurrentSample => (long)(AudioSettings.dspTime * SampleRate);

    public FmSynth(int maxVoices, float masterGain = 0.8f, int queueCapacity = 512)
    {
        (MaxVoices, SampleRate) = (maxVoices, AudioSettings.outputSampleRate);
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

    // Schedules a note. startSample may be in the future; the synth starts it on
    // that exact sample.
    public bool Schedule(in FmNoteEvent note)
    {
        var message = note;
        return _context.SendMessage(_rootOutput, ref message) == Response.Handled;
    }

    // Hands over the send effect settings. Not scheduled and not queued: it replaces
    // whatever the audio thread was using, from the next mix cycle.
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

    public void Dispose()
    {
        if (_context.Exists(_rootOutput)) _context.Destroy(_rootOutput);
    }

    // Private members

    ControlContext _context;
    RootOutputInstance _rootOutput;
}

} // namespace Jacquard.App
