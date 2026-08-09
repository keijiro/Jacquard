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
// The mix is a dry bus and two send buses. A voice goes into the dry one at full
// strength and into each of the others at whatever its note asked for, so the two
// effects hear a sum of exactly the notes that were sent to them and nothing else.
// The wet path is stereo where the dry one is not: a reverb with no width and a
// delay that cannot cross sides would be most of the two effects thrown away, while
// the notes themselves have nowhere for a position to come from — a score has no
// pan, and inventing one for it is a different decision than this.

[BurstCompile(CompileSynchronously = true)]
struct FmSynthRealtime : RootOutputInstance.IRealtime
{
    internal FmVoicePool pool;
    internal ReverbBus reverb;
    internal DelayBus delay;

    internal NativeArray<float> dry;      // Every voice at full strength
    internal NativeArray<float> reverbIn; // What the notes sent to the reverb
    internal NativeArray<float> delayIn;  // What they sent to the delay
    internal NativeArray<float> outL;     // Wet, then the finished mix
    internal NativeArray<float> outR;

    internal AudioFormat format;
    internal float masterGain;

    JobHandle _job;
    ulong _dspSample;
    FmSynthStatus _lastReported;
    SendFxRuntime _fx;

    [BurstCompile(DisableSafetyChecks = true)]
    struct RenderJob : IJob
    {
        public FmVoicePool pool;
        public ReverbBus reverb;
        public DelayBus delay;

        public NativeArray<float> dry;
        public NativeArray<float> reverbIn;
        public NativeArray<float> delayIn;
        public NativeArray<float> outL;
        public NativeArray<float> outR;

        public long bufferStart;
        public int frameCount;
        public float sampleRate;
        public float masterGain;
        public SendFxRuntime fx;

        public void Execute()
        {
            for (var frame = 0; frame < frameCount; frame++)
            {
                dry[frame] = 0.0f;
                reverbIn[frame] = 0.0f;
                delayIn[frame] = 0.0f;
                outL[frame] = 0.0f;
                outR[frame] = 0.0f;
            }

            pool.Render(dry, reverbIn, delayIn, frameCount, bufferStart, sampleRate);

            // In parallel rather than in series. Feeding the delay's repeats into the
            // reverb is a good sound and would be one line, but it is also a decision
            // about how the two are wired that the panel would then have to offer a
            // number for, and the brief was the fewest controls that carry.
            delay.Process(delayIn, outL, outR, frameCount, fx.delaySamples,
                          fx.delayFeedback, fx.delayTone, fx.delaySpread);

            reverb.Process(reverbIn, outL, outR, frameCount, sampleRate,
                           fx.reverbSize, fx.reverbDamp, fx.reverbWidth);

            // Soft clip, so that a dense chord cannot blow past 0dBFS. The dry mix
            // joins here, which is also where the two sides stop being wet only.
            for (var frame = 0; frame < frameCount; frame++)
            {
                var centre = dry[frame];
                outL[frame] = SoftClip((centre + outL[frame]) * masterGain);
                outR[frame] = SoftClip((centre + outR[frame]) * masterGain);
            }
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

    // Receives scheduled notes and effect settings from the control part, and reports
    // diagnostics back.
    public void Update(UpdatedDataContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
        {
            if (element.TryGetData(out FmNoteEvent note)) { pool.Enqueue(note); continue; }
            if (element.TryGetData(out SendFxRuntime fx)) _fx = fx;
        }

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
            reverb = reverb,
            delay = delay,
            dry = dry,
            reverbIn = reverbIn,
            delayIn = delayIn,
            outL = outL,
            outR = outR,
            bufferStart = (long)_dspSample,
            frameCount = format.bufferFrameCount,
            sampleRate = format.sampleRate,
            masterGain = masterGain,
            fx = _fx }.Schedule(input);
    }

    // The one place the output stops being a single buffer copied across every
    // channel. A device with one channel hears the two sides summed, and anything
    // past the second gets the same sum rather than a copy of one side.
    public void EndProcessing(in RealtimeContext context, Pipe pipe, ChannelBuffer output)
    {
        _job.Complete();

        var frameCount = math.min(output.frameCount, outL.Length);
        var stereo = output.channelCount > 1;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var (left, right) = (outL[frame], outR[frame]);
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
