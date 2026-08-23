using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

using Pipe = UnityEngine.Audio.ProcessorInstance.Pipe;
using UpdatedDataContext = UnityEngine.Audio.ProcessorInstance.UpdatedDataContext;

namespace Jacquard.App {

// Diagnostics reported from the realtime part back to the control part through the
// pipe, and from there to the application on request.

public struct FmSynthStatus
{
    public ulong dspSample;    // Audio clock position when this was sampled
    public int activeVoices;
    public int queuedNotes;
    public int droppedNotes;   // Lost because the schedule queue was full
    public int stolenNotes;    // Took a voice from a less important note
    public int cancelledNotes; // Rejected because every voice outranked it

    // The worst a note was late by since the last report, in samples, which is the
    // front of a note that was never rendered, and how many notes started over the
    // same stretch — without which a lateness of zero cannot be told from a stretch
    // with nothing in it. See FmVoicePool.late.
    public int lateSamples;
    public int startedNotes;

    // How many times the audio system has renegotiated the format. It changes when a
    // device is plugged in or the route moves, and everything measured about the path
    // to the audio thread was measured on the old one. See FmSynthPipeline.
    public int formatGeneration;
}

// A question about how far ahead of the app's clock a note has to be placed, and the
// answer to it, in one struct travelling the same pipe as everything else.
//
// The main thread stamps the position it believes the clock is at and sends it; the
// realtime part stamps the earliest sample it could still render a note from in full
// and sends it back. The distance between the two stamps is the whole of what the path
// costs — the hops between the parts, each of which is served once a mix cycle, and
// whatever the two clocks disagree by — which is precisely what MinimumLead is for.
//
// It has to be asked rather than reasoned about because none of those terms is written
// down anywhere: the hops belong to the pipeline, the disagreement belongs to the
// device, and on the iPad measured they came to four mix cycles at a sample rate of
// 24kHz, where the same four cycles on a 48kHz device would be half the time.
//
// The id pairs an answer with its question and says which way this one is going: the
// main thread numbers its questions from one, and a nought is the same message asking
// for the last answer without adding a question to the queue.

public struct FmClockProbe
{
    public int id;
    public long sentAtSample; // Where the main thread believed the clock was
    public ulong earliest;    // The first sample the render job could still fill
}

// Everything on the mix that is not carried by a note, as the audio thread wants it,
// travelling the same pipe in the opposite direction to the status.
//
// This is the only mutable state the audio thread reads: everything about a note
// reaches it stamped into the note itself, but one reverb serving every channel and one
// limiter across the finished mix cannot be carried by a note. So the settings are
// pushed whenever they change, which the main thread notices by comparing this struct
// against the last one it sent — one comparison for both halves, since there is one
// message and nothing is lost by sending a little more of it than moved.

public struct MixFxRuntime
{
    public SendFxRuntime sends;
    public LimiterRuntime limiter;

    // The output volume, already a gain. A struct of its own the way the two above have
    // one would be a wrapper around a single multiplication with nothing else to convert
    // — the whole of the conversion is the pow, and it is OutputVolume's own.
    //
    // It is also the one thing in here that is not the project's: the sends and the
    // limiter come off a panel and travel with the file, and this comes out of
    // PlayerPrefs. Nothing downstream cares — what the audio thread is owed is the
    // settings as they now stand, whoever they belong to — and the alternative was a
    // second message on the same pipe carrying one float.
    public float outputGain;

    public static MixFxRuntime FromSettings(in SendFx fx, in Limiter limiter,
                                            float volume, float tempo, float sampleRate)
      => new MixFxRuntime
        { sends = SendFxRuntime.FromSettings(fx, tempo, sampleRate),
          limiter = LimiterRuntime.FromSettings(limiter, sampleRate),
          outputGain = OutputVolume.Gain(volume) };

    public bool Equals(in MixFxRuntime other)
      => sends.Equals(other.sends) && limiter.Equals(other.limiter) &&
         outputGain == other.outputGain;
}

// The send effects, converted.
//
// The delay time arrives already converted into a distance in samples. The audio
// thread has no business knowing what a tempo or a note value is, and the conversion
// needs the project's tempo, which lives on the other side.

public struct SendFxRuntime
{
    public float reverbSize;
    public float reverbTone;
    public float reverbSpread;

    public float delaySamples;
    public float delayFeedback;
    public float delayTone;
    public float delaySpread;

    public static SendFxRuntime FromSettings(in SendFx fx, float tempo, float sampleRate)
      => new SendFxRuntime
        { reverbSize = fx.reverbSize,
          reverbTone = fx.reverbTone,
          reverbSpread = fx.reverbSpread,
          delaySamples = fx.DelaySeconds(tempo) * sampleRate,
          delayFeedback = fx.delayFeedback,
          delayTone = fx.delayTone,
          delaySpread = fx.delaySpread };

    public bool Equals(in SendFxRuntime other)
      => reverbSize == other.reverbSize && reverbTone == other.reverbTone &&
         reverbSpread == other.reverbSpread && delaySamples == other.delaySamples &&
         delayFeedback == other.delayFeedback && delayTone == other.delayTone &&
         delaySpread == other.delaySpread;
}

// The limiter, converted: the ceiling as the gain it stands for and the make-up that
// answers it, and the two times as the coefficients that smooth the gain by one sample.
//
// Every one of those is a pow or a log's worth of work for something that changes when a
// hand moves a bar, so none of them belongs in the loop that runs them forty-eight
// thousand times a second. What the audio thread is handed is four multiplications.
//
// The make-up is the reciprocal of the ceiling and is carried rather than divided out
// down there, which is also why it is spelled as the gain of the negated decibels: the
// two are the same number, and reading it off the bar's own figure is what says it is
// exactly what the ceiling took off rather than approximately so.

public struct LimiterRuntime
{
    public float ceiling; // Linear, what the gain holds the mix under
    public float makeUp;  // Linear, precisely what the ceiling took off
    public float attack;  // One pole coefficient, gain coming down
    public float release; // And going back up

    public static LimiterRuntime FromSettings(in Limiter limiter, float sampleRate)
    {
        // Clamped once, so that the ceiling and the make-up cannot be read off two
        // different numbers.
        var ceiling = Mathf.Clamp(limiter.ceiling, Limiter.MinCeiling, 0.0f);

        return new LimiterRuntime
          { ceiling = Limiter.Gain(ceiling),
            makeUp = Limiter.Gain(-ceiling),
            attack = Coefficient(limiter.attack, sampleRate,
                                 Limiter.MinAttack, Limiter.MaxAttack),
            release = Coefficient(limiter.release, sampleRate,
                                  Limiter.MinRelease, Limiter.MaxRelease) };
    }

    // How much of the way to the target one sample covers. The time is a time
    // constant rather than a distance travelled, which is what makes an attack of a
    // millisecond mean the same thing here as an envelope's does in the voice.
    static float Coefficient(float seconds, float sampleRate, float low, float high)
      => 1.0f - Mathf.Exp(-1.0f / (Mathf.Clamp(seconds, low, high) * sampleRate));

    public bool Equals(in LimiterRuntime other)
      => ceiling == other.ceiling && makeUp == other.makeUp &&
         attack == other.attack && release == other.release;
}

// Realtime part: runs the voices on the audio thread.
//
// It owns no timbre state. Notes arrive through the pipe carrying their own patch
// and an absolute sample position, and are rendered at exactly that position.
//
// The mix itself is FmSynthCore's; what is here is only the part that belongs to the
// pipeline — the pipe at either end of it and the clock it renders against.

[BurstCompile(CompileSynchronously = true)]
struct FmSynthRealtime : RootOutputInstance.IRealtime
{
    internal FmSynthCore core;

    // Bumped by Configure, carried out on every status. See FmSynthStatus.
    internal int generation;

    JobHandle _job;
    ulong _dspSample;
    MixFxRuntime _fx;

    // Receives scheduled notes and effect settings from the control part, and reports
    // diagnostics back.
    public void Update(UpdatedDataContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
        {
            if (element.TryGetData(out FmNoteEvent note)) { core.pool.Enqueue(note); continue; }
            if (element.TryGetData(out MixFxRuntime fx)) { _fx = fx; continue; }

            // Answered from here rather than from anywhere earlier, because here is
            // where a note arriving now would have been put into the queue: whatever
            // held this message up held a note up by the same amount.
            //
            // The buffer about to be rendered is the earliest one this can still be
            // part of — Update runs ahead of Process in the same cycle, and _dspSample
            // is where the last one began — so the first sample a note arriving now
            // can be heard from in full is one buffer past that.
            if (element.TryGetData(out FmClockProbe probe))
            {
                probe.earliest = _dspSample + (ulong)core.outL.Length;
                pipe.SendData(context, in probe);
            }
        }

        var status = core.Status(_dspSample);
        status.formatGeneration = generation;

        // Every cycle, with nothing held back.
        //
        // It used to go out a few times a second unless a count had moved, which is
        // what a diagnostic is worth and no more. Two things in it are worth more than
        // that now. The clock in it is what FmSynthPipeline schedules against, and the
        // difference it takes from a report is the report's own age too small — a
        // fiftieth of a second of staleness would be a fiftieth of a second off the
        // front of every note. And the lateness in it is cleared by the report that
        // carries it, so one that is never sent is a note nobody hears about.
        if (!pipe.SendData(context, in status)) return;

        // What has been said has been said: the peak belongs to the stretch between
        // two reports, so that the far side reads a lateness once rather than for as
        // long as the worst note is remembered.
        core.pool.ClearLateness();
    }

    public JobHandle EarlyProcessing(in RealtimeContext context, Pipe pipe) => default;

    // dspTime is the sample position at which this mix cycle begins, which is what
    // lets a note start on an exact sample rather than on a buffer boundary.
    public void Process(in RealtimeContext context, Pipe pipe, JobHandle input)
    {
        _dspSample = context.dspTime;
        _job = core.Schedule((long)_dspSample, _fx, input);
    }

    // The one place the output stops being a single buffer copied across every
    // channel. A device with one channel hears the two sides summed, and anything
    // past the second gets the same sum rather than a copy of one side.
    public void EndProcessing(in RealtimeContext context, Pipe pipe, ChannelBuffer output)
    {
        _job.Complete();

        var frameCount = math.min(output.frameCount, core.outL.Length);
        var stereo = output.channelCount > 1;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var (left, right) = (core.outL[frame], core.outR[frame]);
            var centre = (left + right) * 0.5f;

            for (var channel = 0; channel < output.channelCount; channel++)
                output[channel, frame] = channel == 0 && stereo ? left
                                        : channel == 1 ? right : centre;
        }
    }

    // Deallocation happens in Control.Dispose or on re-Configure.
    public void RemovedFromProcessing() {}
}

} // namespace Jacquard.App
