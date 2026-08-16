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
    internal FmSynthScope scope;

    FmSynthStatus _status;  // Latest report received from the realtime part
    FmClockProbe _probe;    // And the latest probe it has answered

    // Format negotiation, also invoked on output device changes, headphone
    // connection and so on. Allocate handles being called a second time.
    public JobHandle Configure(ControlContext context, ref FmSynthRealtime realtime,
                               in AudioFormat format)
    {
        // Said out loud, because everything the driver has measured about the path down
        // here was measured against a format that is no longer the one in force.
        realtime.generation++;

        realtime.core.masterGain = masterGain;
        realtime.core.Allocate(format.sampleRate, format.bufferFrameCount,
                               maxVoices, queueCapacity);
        // After Allocate, which releases what it is about to replace: the scope is the
        // driver's and outlives a format change.
        realtime.core.scope = scope;
        return default;
    }

    public void Update(ControlContext context, Pipe pipe)
    {
        foreach (var element in pipe.GetAvailableData(context))
        {
            if (element.TryGetData(out FmSynthStatus status))
            {
                // Most of a report is a state and the newest one wins. Two of them are
                // not: they cover the stretch since the last report and are cleared by
                // it, so they have to be gathered here rather than overwritten.
                //
                // What is on the other side of this is a main thread that reads once a
                // frame, and reports arrive once a mix cycle — six of them to a frame
                // at fifteen frames a second, which is a rate this app is put into by
                // the device getting warm. Overwriting threw five in six away, and the
                // one thing being counted is a fault that shows up on individual notes.
                var (late, started) = (_status.lateSamples, _status.startedNotes);

                _status = status;
                _status.lateSamples = System.Math.Max(late, status.lateSamples);
                _status.startedNotes = started + status.startedNotes;
                continue;
            }

            if (element.TryGetData(out FmClockProbe probe)) _probe = probe;
        }
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

        // A clock probe, which is a question and a collection in one message: the
        // question goes down the pipe, and what comes back up the caller's own struct
        // is the last question the realtime part answered. Numbered from one, so a
        // nought collects without asking again.
        if (message.Is<FmClockProbe>())
        {
            ref var probe = ref message.Get<FmClockProbe>();
            if (probe.id != 0) pipe.SendData(context, in probe);
            probe = _probe;
            return Response.Handled;
        }

        // A status query. Message.Get returns a reference into the sender's own
        // struct, so writing to it is how the answer gets back.
        if (message.Is<FmSynthStatus>())
        {
            message.Get<FmSynthStatus>() = _status;

            // The two gathered above are handed over rather than shown, so that what
            // the far side reads is the worst and the whole of what has happened since
            // it last read and never the same note twice.
            (_status.lateSamples, _status.startedNotes) = (0, 0);
            return Response.Handled;
        }

        return Response.Unhandled;
    }

    // The scope goes with the rest of it, even though the driver is what allocated it.
    // What matters is not who owns the memory but when it stops being written, and this
    // is the one place the audio side has said it is finished — see FmSynthPipeline's
    // Dispose, where freeing it on the main thread instead was crashing the mix.
    public void Dispose(ControlContext context, ref FmSynthRealtime realtime)
    {
        realtime.core.Release();
        scope.Dispose();
    }
}

} // namespace Jacquard.App
