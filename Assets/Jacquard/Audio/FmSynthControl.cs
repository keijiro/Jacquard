using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Audio;

using Pipe = UnityEngine.Audio.ProcessorInstance.Pipe;
using Message = UnityEngine.Audio.ProcessorInstance.Message;
using Response = UnityEngine.Audio.ProcessorInstance.Response;

namespace Jacquard.App {

// Control part: owns the buffer lifetime and relays events between the main thread
// and the audio thread.

struct FmSynthControl : RootOutputInstance.IControl<FmSynthRealtime>
{
    internal int maxVoices;
    internal int queueCapacity;
    internal float masterGain;

    FmSynthStatus _status; // Latest report received from the realtime part

    // Format negotiation, also invoked on output device changes, headphone
    // connection and so on.
    public JobHandle Configure(ControlContext context, ref FmSynthRealtime realtime,
                               in AudioFormat format)
    {
        realtime.format = format;
        realtime.masterGain = masterGain;

        // Configure may run again on a format change, so release any existing
        // buffers first. Notes sounding at that moment are simply cut off.
        Release(ref realtime);

        realtime.pool = new FmVoicePool
          { voices = new NativeArray<FmVoiceState>(maxVoices, Allocator.Persistent),
            queue = new NativeArray<FmNoteEvent>(queueCapacity, Allocator.Persistent),
            counters = new NativeArray<int>(FmVoicePool.CounterCount, Allocator.Persistent) };

        // The dry bus, the two send buses and the two sides of the finished mix.
        var frames = format.bufferFrameCount;
        realtime.dry = new NativeArray<float>(frames, Allocator.Persistent);
        realtime.reverbIn = new NativeArray<float>(frames, Allocator.Persistent);
        realtime.delayIn = new NativeArray<float>(frames, Allocator.Persistent);
        realtime.outL = new NativeArray<float>(frames, Allocator.Persistent);
        realtime.outR = new NativeArray<float>(frames, Allocator.Persistent);

        // Both buses size their lines from the sample rate, so a device change
        // rebuilds them rather than resampling what is in them. Whatever tail was
        // sounding is lost with the notes that fed it.
        realtime.reverb = ReverbBus.Create(format.sampleRate);
        realtime.delay = DelayBus.Create(format.sampleRate);

        return default;
    }

    public void Update(ControlContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
            if (element.TryGetData(out FmSynthStatus status))
                _status = status;
    }

    public Response OnMessage(ControlContext context, Pipe pipe, Message message)
    {
        // A note to schedule: hand it over to the audio thread.
        if (message.Is<FmNoteEvent>())
        {
            ref var note = ref message.Get<FmNoteEvent>();
            return pipe.SendData(context, in note) ? Response.Handled : Response.Unhandled;
        }

        // The effect settings, which take the same route for want of a note to ride
        // on. Unlike a note there is no queue behind this: the audio thread keeps the
        // latest one it was given and nothing is lost by a message that never lands,
        // since the next change sends the whole struct again.
        if (message.Is<SendFxRuntime>())
        {
            ref var fx = ref message.Get<SendFxRuntime>();
            return pipe.SendData(context, in fx) ? Response.Handled : Response.Unhandled;
        }

        // A status query. Message.Get returns a reference into the sender's own
        // struct, so writing to it is how the answer gets back.
        if (message.Is<FmSynthStatus>())
        {
            message.Get<FmSynthStatus>() = _status;
            return Response.Handled;
        }

        return Response.Unhandled;
    }

    public void Dispose(ControlContext context, ref FmSynthRealtime realtime)
      => Release(ref realtime);

    static void Release(ref FmSynthRealtime realtime)
    {
        if (realtime.pool.voices.IsCreated) realtime.pool.voices.Dispose();
        if (realtime.pool.queue.IsCreated) realtime.pool.queue.Dispose();
        if (realtime.pool.counters.IsCreated) realtime.pool.counters.Dispose();

        if (realtime.dry.IsCreated) realtime.dry.Dispose();
        if (realtime.reverbIn.IsCreated) realtime.reverbIn.Dispose();
        if (realtime.delayIn.IsCreated) realtime.delayIn.Dispose();
        if (realtime.outL.IsCreated) realtime.outL.Dispose();
        if (realtime.outR.IsCreated) realtime.outR.Dispose();

        realtime.reverb.Dispose();
        realtime.delay.Dispose();
    }
}

} // namespace Jacquard.App
