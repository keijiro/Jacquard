using UnityEngine;

namespace Jacquard.App {

// How long a buffer the audio system hands the synth.
//
// It is the one number on this machine that decides whether the mix arrives in time.
// The audio thread has exactly one buffer's worth of time to render one, and what
// happens when it does not is not a slower frame but a hole in the music with a hard
// edge at either end — see FmSynthPipeline.Pump, which watches for exactly that and
// tells whoever is listening to come here. A longer buffer buys tolerance for it and
// costs latency: 256 frames is 5.3ms at the rate a device usually hands out, and 1024
// is 21.3ms, which is the whole distance between a live effect that answers the hand
// and one that answers a moment later.
//
// So it belongs to the machine and not to the piece — a fast laptop and a busy one
// want different numbers out of the same score — which puts it in PlayerPrefs beside
// the other things the System panel keeps.
//
// It is applied once, before the synth is built, and not again. Unity reads its own
// figure out of the project settings at boot, so a stored number does nothing until
// something asks for it: AudioSettings.Reset is that ask, and the moment for it is
// before anything has been allocated against the old figure. Which is also why the
// panel says a restart is needed. Reset does work with the pipeline running — the
// audio system renegotiates the format, FmSynthControl.Configure runs again and the
// mix buffer is reallocated to the new size, all of it measured — but two numbers
// read once in FmSynthPipeline's constructor would still be about the old buffer,
// and a setting that is honest about when it lands beats one that is nearly right.
//
// That same Reset is where the output rate is settled, because it is the same call and
// one reinitialization of the output is enough for both. What the rate is, and why it is
// not a setting the way this is, is DspOutputRate's own argument; what belongs here is
// only that the two numbers travel together.

static class DspBuffer
{
    // Everywhere the synth goes through Unity's audio system, which is everywhere but
    // a Web build: there the browser owns the block size and hands the synth a fixed
    // one. Written as a property with the platform inside it so that nothing else has
    // to know the spelling — a compile time constant either way, and the panel simply
    // does not build the row where it is false.
    public static bool Supported =>
#if UNITY_WEBGL && !UNITY_EDITOR
      false;
#else
      true;
#endif

    // The span the bar covers, in frames. Below 256 is where this machine already is
    // and where the trouble was; above 1024 the latency is louder than any dropout it
    // would prevent.
    //
    // The step is a choice about resolution and nothing else. A block size is a plain
    // number to the audio system — the three names in Project Settings are an editor
    // inspector's enum over an int, and 768, 400, 333 and 300 were each granted exactly
    // and rendered in lockstep here — so what the stops have to be is useful rather than
    // permitted. Half a buffer is 2.7ms of deadline at the rate a device usually hands
    // out, which is the smallest move worth making, and it puts seven stops on the bar:
    // enough that it is read as a bar rather than as four positions, few enough that
    // every one of them is a different answer.
    public const int Min = 256;
    public const int Max = 1024;
    public const int Step = 128;

    // What the panel reads before anybody has chosen anything, which is two figures: 512
    // frames everywhere the audio thread is what decides it, and 1024 on iOS, where it is
    // decided by something outside this app altogether.
    //
    // **Two stops up from the bottom of the bar.** 256 frames is 5.3ms of audio thread,
    // and 5.3ms is a deadline this project has one machine's proof of missing: read off
    // the iPad, five minutes of the sample score each, six and a half thousand notes each
    // and nothing else changed between the runs — at 48000 with 256 frames the stream
    // reported seven restarts, at 48000 with 512 none, and at 24000 with 256, which is the
    // same 10.7ms of deadline that 512 is now, none. What it minded was the deadline, and
    // neither the rate nor the frame count on its own.
    //
    // **The same figure on every platform that reading covers, and that half of it is a
    // judgement rather than a reading.** Nothing about the fault is particular to a tablet
    // — a desktop that is slow, or busy with something else, has the same audio thread and
    // the same one buffer's worth of time to fill it — and no measurement here says that a
    // desktop holds 5.3ms under load, only that this one has not been caught failing to. A
    // default is what a machine nobody has measured is handed, so it is set where both of
    // the machines that have been measured are safe.
    //
    // What that costs is 5.3ms between a Live FX button and what comes out. 256 stays on
    // the bar for the machine that holds it: which end of that trade to take is a
    // machine's own bargain to make, and the warning a missed deadline prints is what
    // sends a hand back up.
    //
    // **iOS is handed 1024 instead, and that stop is not about this machine at all.** What
    // a frame count reaches the device as is an IO buffer duration — 512 frames at 48000Hz
    // is 10.7ms — and iOS breaks the audio track of its own screen recording when an app
    // asks for a buffer shorter than the system's own default, which stands around 20ms.
    // The fault is not a dropout here and there: the recorded track is wrong from end to
    // end, drifting and grating for the whole of its length, while the app is heard
    // playing perfectly out of the speaker beside it. That last part is what says whose
    // fault it is, since nothing here is failing to render, and Apple has it as a
    // regression of iOS 26 that iOS 18 did not have — FB22245447, open, with no workaround
    // offered. The one lever this side of it is to stop asking for a buffer under the
    // default. Read off the device: at 512 the recording is broken every time, at 1024 —
    // 21.3ms, which is about where the default begins — it is clean.
    //
    // **Which is why Android stays at 512.** Everything above is a machine holding a
    // deadline; this is one platform's framework mishandling what it is handed, a phone
    // being slow has nothing to do with it, and a platform without the fault should not
    // pay for it. It is also why this is a default and not a floor: the bar still reaches
    // 256 on iOS, because a person who is not recording the screen is owed the response,
    // and the cost of finding out the wrong way is a recording to make again.
    //
    // What the longer stop costs on iOS is the same trade read from the other end, and
    // half of it is not the plain half. 10.7ms more between the mix and the speaker is the
    // plain half. The other is that every hop a note takes down the pipe is served once a
    // mix cycle, so the lead DspClock measures grows with the buffer, and what that
    // reaches is how far ahead of itself a live effect has to be read — see
    // JacquardApp.HandoverSeconds, where the window it is added to is measured in frames
    // rather than in milliseconds. The figure in force is printed at every launch rather
    // than guessed at here.
    //
    // AudioManager.asset holds 512, which is the desktop figure and no longer this one for
    // every platform, since that is what Unity boots with and a launch nobody has told
    // otherwise then costs no Reset at all. The two mobile platforms reset anyway, because
    // they ask for a rate as well and the asset carries one figure for all of them — see
    // DspOutputRate — so iOS's own frame count rides along on a Reset already being made.
    public const int Default =
#if UNITY_IOS && !UNITY_EDITOR
      1024;
#else
      512;
#endif

    // What the setting says, which is not the same question as what is in force.
    //
    // The write does not flush. A drag crosses every stop between two of them and a
    // flush is a write to disk, so the hand coming off is what commits it — see Flush.
    public static int Requested
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Key, Default), Min, Max);
        set => PlayerPrefs.SetInt(Key, Mathf.Clamp(value, Min, Max));
    }

    public static void Flush() => PlayerPrefs.Save();

    // What the audio system is running on, which is not always what was asked for: a
    // driver is free to round a block size to something it can do.
    public static int Current
    {
        get
        {
            AudioSettings.GetDSPBufferSize(out var frames, out _);
            return frames;
        }
    }

    // What this launch was asked for, which is what a restart being owed is measured
    // against — the setting having moved since the app started, and nothing else.
    //
    // Not Current, which would be the same question only on a device that gives exactly
    // what it is handed. One that rounds would leave a note reading *applies at the next
    // launch* standing after that launch had already happened and applied it, which is
    // the one thing a note about restarting must never say.
    public static int Applied { get; private set; }

    // Called once at startup, before the synth is built.
    //
    // Nothing happens where the stored number is the one Unity booted with and no rate is
    // wanted: the audio system is left exactly as it was found, which is the ordinary case
    // on a desktop. The two mobile platforms reset on every launch, since the rate they
    // want is one AudioManager.asset cannot carry for them alone — see DspOutputRate.
    public static void Apply()
    {
        if (!Supported) return;

        var frames = Requested;
        Applied = frames;

        var rate = DspOutputRate.Desired;

        // Nothing to ask for: the stored buffer is the one Unity booted with, and the
        // rate is either already the one wanted or this platform's own business.
        if (frames == Current && (rate == 0 || rate == DspOutputRate.Current)) return;

        var config = AudioSettings.GetConfiguration();
        config.dspBufferSize = frames;
        if (rate != 0) config.sampleRate = rate;

        // None of this stops anything: the mix is rendered to whatever length and at
        // whatever rate the audio system reports, so a device that would not take a
        // figure is a device running on its own and playing perfectly well. What it
        // costs is the one thing that figure was for — the audio thread's deadline is
        // not the one that was chosen, or the fold is where DspOutputRate says it must
        // not be — so it is said once, where the rest of what the audio has to say goes.
        if (!AudioSettings.Reset(config))
            Debug.LogWarning($"Jacquard: the audio system refused {frames} frames at " +
                             $"{config.sampleRate}Hz and kept {Current} at " +
                             $"{DspOutputRate.Current}Hz.");
        else
        {
            if (Current != frames)
                Debug.LogWarning($"Jacquard: a buffer of {frames} frames was rounded " +
                                 $"to {Current} by the device.");

            if (rate != 0 && DspOutputRate.Current != rate)
                Debug.LogWarning($"Jacquard: an output rate of {rate}Hz was rounded to " +
                                 $"{DspOutputRate.Current}Hz by the device.");
        }

        // And once, plainly, what is in force. Only on the path where something was
        // asked for, since the other path changed nothing and has nothing to report.
        //
        // This is the whole of what a Reset has over the figure in AudioManager.asset —
        // it can be asked what happened — and a claim of that kind is worth nothing if
        // the answer is only ever printed when it is bad. On a device this is also the
        // one place the format is written down at all.
        Debug.Log($"Jacquard: the audio output is {DspOutputRate.Current}Hz in buffers " +
                  $"of {Current} frames, which is " +
                  $"{Current * 1000.0 / DspOutputRate.Current:0.0}ms of audio thread.");
    }

    const string Key = "Jacquard.DspBuffer";
}

} // namespace Jacquard.App
