using Unity.Collections;

namespace Jacquard.App {

// A fixed set of voices plus the queue of notes waiting to start.
//
// Everything lives in NativeArrays so that a copy of this struct handed to a
// Burst job still mutates the same memory. The oscillator itself is not here: the
// per sample maths is FmVoiceState's job, on the engine-free side, and what this
// adds is the part that genuinely needs a container — which buffer frames a note
// covers, and which voice it gets.

struct FmVoicePool
{
    public NativeArray<FmVoiceState> voices;
    public NativeArray<FmNoteEvent> queue; // Scheduled notes, in no order
    public NativeArray<int> counters;

    const int Count = 0, Dropped = 1, Stolen = 2, Cancelled = 3;
    internal const int CounterCount = 4;

    public int dropped => counters[Dropped];
    public int stolen => counters[Stolen];
    public int cancelled => counters[Cancelled];

    public int ActiveVoiceCount()
    {
        var count = 0;
        for (var i = 0; i < voices.Length; i++) if (voices[i].Active) count++;
        return count;
    }

    public int QueuedCount() => counters[Count];

    public void Enqueue(in FmNoteEvent note)
    {
        if (counters[Count] >= queue.Length)
        {
            counters[Dropped]++;
            return;
        }

        queue[counters[Count]++] = note;
    }

    // Starts every queued note that falls inside this buffer, then renders all
    // active voices into the two sides of the dry bus, at the pair of gains their pan
    // asks for, and — in the proportion each note asks for — into the two send buses.
    //
    // A voice is rendered once and split four ways rather than being rendered again
    // per destination, and every gain it is split at is read off the note, which means
    // all of them are fixed for the life of the voice. That is the whole reason
    // neither a pan nor a send needs smoothing: what moves when the Sound panel moves
    // is the next note, never this one.
    //
    // The sends take the voice unpanned. Each of those buses is a mono feed into an
    // effect that builds a stereo image of its own, so a tail that also leaned to the
    // side its note came from would be two answers to one question.
    public void Render(NativeArray<float> dryL, NativeArray<float> dryR,
                       NativeArray<float> reverbIn, NativeArray<float> delayIn,
                       int frameCount, long bufferStart, float sampleRate)
    {
        var bufferEnd = bufferStart + frameCount;

        // Earliest first, so that the priority decisions come out the same
        // regardless of the order the notes arrived in.
        while (true)
        {
            var next = -1;

            for (var i = 0; i < counters[Count]; i++)
            {
                if (queue[i].startSample >= bufferEnd) continue;
                if (next < 0 || queue[i].startSample < queue[next].startSample) next = i;
            }

            if (next < 0) break;

            Trigger(queue[next], sampleRate);

            // Swap-remove, which is why the queue needs no ordering.
            counters[Count]--;
            queue[next] = queue[counters[Count]];
        }

        var dt = 1.0f / sampleRate;

        for (var i = 0; i < voices.Length; i++)
        {
            if (!voices[i].Active) continue;

            // NativeArray hands out copies, so read, render and write back.
            var voice = voices[i];
            var note = voice.Note;
            var total = note.TotalDuration;

            // Once per voice per buffer, not per sample: the note holds still, so
            // the pair of gains it renders at does too.
            note.PanGains(out var left, out var right);

            for (var frame = 0; frame < frameCount; frame++)
            {
                // Elapsed note time. The subtraction stays small even for a long
                // running DSP clock, so float precision is fine here.
                var time = (bufferStart + frame - note.startSample) * dt;

                if (time < 0.0f) continue;              // Starts later in this buffer
                if (time >= total) { voice.Release(); break; }

                var sample = voice.Next(time);

                dryL[frame] += sample * left;
                dryR[frame] += sample * right;
                reverbIn[frame] += sample * note.reverbSend;
                delayIn[frame] += sample * note.delaySend;
            }

            voices[i] = voice;
        }
    }

    // Assigns a voice to a note: a free slot if there is one, otherwise the least
    // important voice is stolen, and a note less important than everything
    // playing is cancelled instead.
    void Trigger(in FmNoteEvent note, float sampleRate)
    {
        var target = -1;

        for (var i = 0; i < voices.Length; i++)
            if (!voices[i].Active) { target = i; break; }

        if (target < 0)
        {
            var lowest = int.MaxValue;
            var earliestEnd = long.MaxValue;

            for (var i = 0; i < voices.Length; i++)
            {
                var priority = voices[i].Note.priority;
                var end = voices[i].EndSample(sampleRate);
                if (priority > lowest) continue;
                if (priority == lowest && end >= earliestEnd) continue;
                (target, lowest, earliestEnd) = (i, priority, end);
            }

            // Equal priority still steals, otherwise a full pool would reject
            // every following note.
            if (note.priority < lowest)
            {
                counters[Cancelled]++;
                return;
            }

            counters[Stolen]++;
        }

        var voice = voices[target];
        voice.Trigger(note, sampleRate);
        voices[target] = voice;
    }
}

} // namespace Jacquard.App
