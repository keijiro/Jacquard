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
//
// Behind this there are two drivers, and which one is in use is the only thing about
// this class that is platform specific. Everywhere the scriptable audio pipeline
// exists, that is what runs the synth. On the Web it does not exist, and neither does
// any other way of being called back for samples, so there the same DSP is driven
// from Update and pushed at the browser — see FmSynthWeb, which is also where the
// latency that costs is explained.

public sealed class FmSynth : System.IDisposable
{
    public int MaxVoices { get; }

    public int SampleRate => _backend.SampleRate;

    // Current position of the audio clock in samples. Scheduling is expressed
    // against this rather than against frame time.
    public long CurrentSample => _backend.CurrentSample;

    // How far past CurrentSample the earliest schedulable note lies. Zero under the
    // pipeline, which renders on demand and can start a note in the very next buffer.
    // A driver that renders ahead of the clock has already committed the samples in
    // between, so a note placed inside them would lose its front.
    public long MinimumLead => _backend.MinimumLead;

    public FmSynth(int maxVoices, float masterGain = 0.8f, int queueCapacity = 512)
    {
        MaxVoices = maxVoices;
#if UNITY_WEBGL && !UNITY_EDITOR
        _backend = new FmSynthWeb(maxVoices, masterGain, queueCapacity);
#else
        _backend = new FmSynthPipeline(maxVoices, masterGain, queueCapacity);
#endif
    }

    // Schedules a note. startSample may be in the future; the synth starts it on
    // that exact sample.
    public bool Schedule(in FmNoteEvent note) => _backend.Schedule(note);

    // Hands over the send effect settings. Not scheduled and not queued: it replaces
    // whatever the audio thread was using, from the next mix cycle.
    public bool SetFx(in SendFxRuntime fx) => _backend.SetFx(fx);

    public FmSynthStatus GetStatus() => _backend.GetStatus();

    // Called once a frame. Nothing under the pipeline, where the audio thread asks
    // for what it needs; on the Web it is the entire engine.
    public void Pump() => _backend.Pump();

    public void Dispose() => _backend.Dispose();

    // Private members

    readonly IFmSynthBackend _backend;
}

// What a driver has to provide for the above to be a synth.

interface IFmSynthBackend : System.IDisposable
{
    int SampleRate { get; }
    long CurrentSample { get; }
    long MinimumLead { get; }

    bool Schedule(in FmNoteEvent note);
    bool SetFx(in SendFxRuntime fx);
    FmSynthStatus GetStatus();
    void Pump();
}

} // namespace Jacquard.App
