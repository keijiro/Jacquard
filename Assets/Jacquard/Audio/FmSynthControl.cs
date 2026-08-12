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
    // connection and so on. Allocate handles being called a second time.
    public JobHandle Configure(ControlContext context, ref FmSynthRealtime realtime,
                               in AudioFormat format)
    {
        realtime.core.masterGain = masterGain;
        realtime.core.Allocate(format.sampleRate, format.bufferFrameCount,
                               maxVoices, queueCapacity);
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
        if (message.Is<MixFxRuntime>())
        {
            ref var fx = ref message.Get<MixFxRuntime>();
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
      => realtime.core.Release();
}

} // namespace Jacquard.App
