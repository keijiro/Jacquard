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

    // How far past CurrentSample the earliest schedulable note lies. A driver that
    // renders ahead of the clock has already committed the samples in between, so a
    // note placed inside them would lose its front.
    //
    // On the Web it is what the driver holds queued, which it knows by construction.
    // Under the pipeline it is measured on the machine — see FmSynthPipeline, where
    // both what it is made of and why it cannot be reasoned about are set out.
    public long MinimumLead => _backend.MinimumLead;

    // Says that whatever MinimumLead was measured against is no longer true: the app
    // has been away, and the audio system it left behind is not the one it came back
    // to. The figure in force stays in force until a new one has been taken.
    public void Recalibrate() => _backend.Recalibrate();

    // What the sum of every voice is scaled by on the way out, which is the one number
    // that says how much of the mix is headroom.
    //
    // A quarter, which is to say **full scale is four notes**. The pan law is unity at
    // the centre rather than at the ends, so a note at level 1 already arrives at the mix
    // at full scale on both sides — the budget is therefore literally counted in notes,
    // and a gain anywhere near a whole is a budget of one. At four fifths it was: two
    // notes measured +4.1dBFS and a triad +7.6, and everything over the top of that was
    // rounded off by the soft clip at the end of the mix, which is why a plain fifth at
    // level 1 sounded dirty. It was not the chord that was wrong. Four is a chord with a
    // bass under it.
    //
    // What it costs is 10.1dB, and the **threshold is where that comes back**: the
    // limiter's make-up is the inverse of it, so pulling the bar down hands the level
    // back and hardens the mix on the way. That bar could not do this before. A mix
    // arriving already at full scale left it nothing to sit under — a threshold below
    // the mix squeezed everything at once and one at the mix caught nothing — so what
    // this gain really buys is a range of levels for it to mean something in.
    //
    // Older projects do not pay the 10.1dB. ProjectFormat shifts a saved threshold by
    // exactly it, which leaves a version 16 piece sounding as it did, note for note.
    public const float MasterGain = 0.25f;

    public FmSynth(int maxVoices, float masterGain = MasterGain, int queueCapacity = 512)
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
    public bool SetFx(in MixFxRuntime fx) => _backend.SetFx(fx);

    public FmSynthStatus GetStatus() => _backend.GetStatus();

    // What the mix looked like, for anything drawing it. Written by the render job as
    // it goes and read whenever a frame happens to want it — see FmSynthScope, which is
    // where the lack of any handshake between the two is argued.
    public FmSynthScope Scope => _backend.Scope;

    // Called once a frame. On the Web it is the entire engine; under the pipeline the
    // audio thread asks for what it needs and this is left watching the clock for a
    // deadline that thread missed, which is the one thing it cannot report itself.
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

    FmSynthScope Scope { get; }

    bool Schedule(in FmNoteEvent note);
    bool SetFx(in MixFxRuntime fx);
    FmSynthStatus GetStatus();
    void Pump();
    void Recalibrate();
}

} // namespace Jacquard.App
