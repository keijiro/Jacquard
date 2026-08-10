using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
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
}

// The send effects as the audio thread wants them, travelling the same pipe in the
// opposite direction to the status.
//
// This is the only mutable state the audio thread reads, and the first: everything
// about a note reaches it stamped into the note itself, but one reverb serving every
// channel cannot be carried by a note. So the settings are pushed whenever they
// change, which the main thread notices by comparing this struct against the last
// one it sent.
//
// The delay time arrives already converted into a distance in samples. The audio
// thread has no business knowing what a tempo or a note value is, and the conversion
// needs the project's tempo, which lives on the other side.

public struct SendFxRuntime
{
    public float reverbSize;
    public float reverbDamp;
    public float reverbWidth;

    public float delaySamples;
    public float delayFeedback;
    public float delayTone;
    public float delaySpread;

    public static SendFxRuntime FromSettings(in SendFx fx, float tempo, float sampleRate)
      => new SendFxRuntime
        { reverbSize = fx.reverbSize,
          reverbDamp = fx.reverbDamp,
          reverbWidth = fx.reverbWidth,
          delaySamples = fx.DelaySeconds(tempo) * sampleRate,
          delayFeedback = fx.delayFeedback,
          delayTone = fx.delayTone,
          delaySpread = fx.delaySpread };

    public bool Equals(in SendFxRuntime other)
      => reverbSize == other.reverbSize && reverbDamp == other.reverbDamp &&
         reverbWidth == other.reverbWidth && delaySamples == other.delaySamples &&
         delayFeedback == other.delayFeedback && delayTone == other.delayTone &&
         delaySpread == other.delaySpread;
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

    JobHandle _job;
    ulong _dspSample;
    FmSynthStatus _lastReported;
    SendFxRuntime _fx;

    // Receives scheduled notes and effect settings from the control part, and reports
    // diagnostics back.
    public void Update(UpdatedDataContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
        {
            if (element.TryGetData(out FmNoteEvent note)) { core.pool.Enqueue(note); continue; }
            if (element.TryGetData(out SendFxRuntime fx)) _fx = fx;
        }

        var status = core.Status(_dspSample);

        // Report whenever a count changes, and otherwise a few times a second to
        // keep the reported clock fresh. The periodic report also covers start-up,
        // where the all-zero state would match _lastReported and never be sent.
        var interval = (ulong)(core.sampleRate / 20);

        if (!Differs(status, _lastReported) &&
            status.dspSample - _lastReported.dspSample < interval) return;

        if (pipe.SendData(context, in status)) _lastReported = status;
    }

    static bool Differs(in FmSynthStatus a, in FmSynthStatus b)
      => a.activeVoices != b.activeVoices || a.queuedNotes != b.queuedNotes ||
         a.droppedNotes != b.droppedNotes || a.stolenNotes != b.stolenNotes ||
         a.cancelledNotes != b.cancelledNotes;

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
