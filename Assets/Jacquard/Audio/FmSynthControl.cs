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

        realtime.mono = new NativeArray<float>(format.bufferFrameCount, Allocator.Persistent);

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
        if (realtime.mono.IsCreated) realtime.mono.Dispose();
    }
}

} // namespace Jacquard.App
