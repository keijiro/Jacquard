using Unity.Burst;
using Unity.Collections;
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

// Realtime part: runs the voices on the audio thread.
//
// It owns no timbre state. Notes arrive through the pipe carrying their own patch
// and an absolute sample position, and are rendered at exactly that position.

[BurstCompile(CompileSynchronously = true)]
struct FmSynthRealtime : RootOutputInstance.IRealtime
{
    internal FmVoicePool pool;
    internal NativeArray<float> mono; // Mono mix, duplicated to all channels
    internal AudioFormat format;
    internal float masterGain;

    JobHandle _job;
    ulong _dspSample;
    FmSynthStatus _lastReported;

    [BurstCompile(DisableSafetyChecks = true)]
    struct RenderJob : IJob
    {
        public FmVoicePool pool;
        public NativeArray<float> mono;
        public long bufferStart;
        public int frameCount;
        public float sampleRate;
        public float masterGain;

        public void Execute()
        {
            for (var frame = 0; frame < frameCount; frame++) mono[frame] = 0.0f;

            pool.Render(mono, frameCount, bufferStart, sampleRate);

            // Soft clip, so that a dense chord cannot blow past 0dBFS.
            for (var frame = 0; frame < frameCount; frame++)
                mono[frame] = SoftClip(mono[frame] * masterGain);
        }

        // A Pade approximant of tanh. The library function would read better, but
        // it is an extern that Burst declines to resolve, and that quietly drops the
        // whole job back to managed execution on the audio thread.
        static float SoftClip(float x)
        {
            var s = math.min(x * x, 9.0f);
            return math.clamp(x * (27.0f + s) / (27.0f + 9.0f * s), -1.0f, 1.0f);
        }
    }

    // Receives scheduled notes from the control part and reports diagnostics back.
    public void Update(UpdatedDataContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
            if (element.TryGetData(out FmNoteEvent note))
                pool.Enqueue(note);

        var status = new FmSynthStatus
          { dspSample = _dspSample,
            activeVoices = pool.ActiveVoiceCount(),
            queuedNotes = pool.QueuedCount(),
            droppedNotes = pool.dropped,
            stolenNotes = pool.stolen,
            cancelledNotes = pool.cancelled };

        // Report whenever a count changes, and otherwise a few times a second to
        // keep the reported clock fresh. The periodic report also covers start-up,
        // where the all-zero state would match _lastReported and never be sent.
        var interval = (ulong)(format.sampleRate / 20);

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

        _job = new RenderJob
          { pool = pool,
            mono = mono,
            bufferStart = (long)_dspSample,
            frameCount = format.bufferFrameCount,
            sampleRate = format.sampleRate,
            masterGain = masterGain }.Schedule(input);
    }

    public void EndProcessing(in RealtimeContext context, Pipe pipe, ChannelBuffer output)
    {
        _job.Complete();

        var frameCount = math.min(output.frameCount, mono.Length);

        for (var frame = 0; frame < frameCount; frame++)
            for (var channel = 0; channel < output.channelCount; channel++)
                output[channel, frame] = mono[frame];
    }

    // Deallocation happens in Control.Dispose or on re-Configure.
    public void RemovedFromProcessing() {}
}

} // namespace Jacquard.App
