using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Jacquard.App {

// The synth with nothing attached to its output: the voices, the two send buses, the
// buffers they render into, and the job that fills them.
//
// Everything here is said in samples and frame counts, and nothing in it knows who is
// asking. That is what lets one piece of DSP serve two drivers that could hardly be
// less alike — the scriptable audio pipeline, which calls into it on the audio thread
// and hands it the clock to render against, and the Web build, where nothing will
// call it at all and the main thread has to render blocks and push them at the
// browser. Both arrive here as the same question: fill this many frames, starting at
// this sample.
//
// The mix is a dry bus and two send buses. A voice goes into the dry one at the pair
// of gains its pan asks for and into each of the others at whatever its note asked
// for, so the two effects hear a sum of exactly the notes that were sent to them and
// nothing else.
//
// Every path here is stereo, and each becomes so for its own reason. The wet one,
// because a reverb with no width and a delay that cannot cross sides would be most of
// the two effects thrown away — the image is the effect. The dry one, because the pan
// in the patch is a position per note, which is a finer thing than either bus can say
// and the one place a chord can be spread out at all.

[BurstCompile(CompileSynchronously = true)]
struct FmSynthCore
{
    public FmVoicePool pool;
    public ReverbBus reverb;
    public DelayBus delay;

    public NativeArray<float> dryL;     // Every voice, placed by its own pan
    public NativeArray<float> dryR;
    public NativeArray<float> reverbIn; // What the notes sent to the reverb
    public NativeArray<float> delayIn;  // What they sent to the delay
    public NativeArray<float> outL;     // Wet, then the finished mix
    public NativeArray<float> outR;

    public float sampleRate;
    public int frameCount;              // What one Render fills
    public float masterGain;

    // Builds the voices, the queue behind them and every buffer the mix needs.
    //
    // Safe to call again on a format change, which is what a device change or a pair
    // of headphones arriving looks like from here: whatever was allocated is released
    // first, and the notes sounding at that moment are simply cut off.
    //
    // Both buses size their lines from the sample rate, so a change in it rebuilds
    // them rather than resampling what is in them. Whatever tail was sounding is lost
    // with the notes that fed it.
    public void Allocate(float rate, int frames, int maxVoices, int queueCapacity)
    {
        Release();

        (sampleRate, frameCount) = (rate, frames);

        pool = new FmVoicePool
          { voices = new NativeArray<FmVoiceState>(maxVoices, Allocator.Persistent),
            queue = new NativeArray<FmNoteEvent>(queueCapacity, Allocator.Persistent),
            counters = new NativeArray<int>(FmVoicePool.CounterCount, Allocator.Persistent) };

        dryL = new NativeArray<float>(frames, Allocator.Persistent);
        dryR = new NativeArray<float>(frames, Allocator.Persistent);
        reverbIn = new NativeArray<float>(frames, Allocator.Persistent);
        delayIn = new NativeArray<float>(frames, Allocator.Persistent);
        outL = new NativeArray<float>(frames, Allocator.Persistent);
        outR = new NativeArray<float>(frames, Allocator.Persistent);

        reverb = ReverbBus.Create(rate);
        delay = DelayBus.Create(rate);
    }

    public void Release()
    {
        if (pool.voices.IsCreated) pool.voices.Dispose();
        if (pool.queue.IsCreated) pool.queue.Dispose();
        if (pool.counters.IsCreated) pool.counters.Dispose();

        if (dryL.IsCreated) dryL.Dispose();
        if (dryR.IsCreated) dryR.Dispose();
        if (reverbIn.IsCreated) reverbIn.Dispose();
        if (delayIn.IsCreated) delayIn.Dispose();
        if (outL.IsCreated) outL.Dispose();
        if (outR.IsCreated) outR.Dispose();

        reverb.Dispose();
        delay.Dispose();
    }

    // Fills outL and outR with the frames beginning at bufferStart. The two ways in
    // differ only in who runs the job: the pipeline wants a handle to hang off its
    // own, and the Web build has no worker thread to hand it to and so runs it where
    // it stands.
    public JobHandle Schedule(long bufferStart, in SendFxRuntime fx, JobHandle input)
      => MakeJob(bufferStart, fx).Schedule(input);

    public void Run(long bufferStart, in SendFxRuntime fx)
      => MakeJob(bufferStart, fx).Run();

    RenderJob MakeJob(long bufferStart, in SendFxRuntime fx)
      => new RenderJob
        { pool = pool,
          reverb = reverb,
          delay = delay,
          dryL = dryL,
          dryR = dryR,
          reverbIn = reverbIn,
          delayIn = delayIn,
          outL = outL,
          outR = outR,
          bufferStart = bufferStart,
          frameCount = frameCount,
          sampleRate = sampleRate,
          masterGain = masterGain,
          fx = fx };

    // The diagnostics as they stand, against whatever sample position the driver
    // believes it is at.
    public FmSynthStatus Status(ulong dspSample)
      => new FmSynthStatus
        { dspSample = dspSample,
          activeVoices = pool.ActiveVoiceCount(),
          queuedNotes = pool.QueuedCount(),
          droppedNotes = pool.dropped,
          stolenNotes = pool.stolen,
          cancelledNotes = pool.cancelled };

    [BurstCompile(DisableSafetyChecks = true)]
    struct RenderJob : IJob
    {
        public FmVoicePool pool;
        public ReverbBus reverb;
        public DelayBus delay;

        public NativeArray<float> dryL;
        public NativeArray<float> dryR;
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
                dryL[frame] = 0.0f;
                dryR[frame] = 0.0f;
                reverbIn[frame] = 0.0f;
                delayIn[frame] = 0.0f;
                outL[frame] = 0.0f;
                outR[frame] = 0.0f;
            }

            pool.Render(dryL, dryR, reverbIn, delayIn, frameCount, bufferStart,
                        sampleRate);

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
                outL[frame] = SoftClip((dryL[frame] + outL[frame]) * masterGain);
                outR[frame] = SoftClip((dryR[frame] + outR[frame]) * masterGain);
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
}

} // namespace Jacquard.App
