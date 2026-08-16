using Unity.Collections;
using Unity.Mathematics;

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

    const int Count = 0, Dropped = 1, Stolen = 2, Cancelled = 3, Late = 4, Started = 5;
    internal const int CounterCount = 6;

    public int dropped => counters[Dropped];
    public int stolen => counters[Stolen];
    public int cancelled => counters[Cancelled];

    // The worst a note has been late by, in samples, since this was last cleared.
    //
    // Late means the buffer a note was started in had already begun when the note was
    // still to come, so the front of it was never rendered: what a pitch sweep does
    // then is start part way down, since everything about a voice is read off the time
    // since its own start. It is the one fault the audio side can see and the main
    // thread cannot — a note is stamped in the render job's clock, and whether that
    // stamp arrived in time is only known where the stamp is read. See
    // FmSynthPipeline.FollowTheRenderClock, which grows the lead by exactly this.
    public int late => counters[Late];

    // How many notes were started over the same stretch, which is what says whether a
    // lateness of zero means they were all on time or that there were none. A piece
    // with a note every half second spends most of its windows empty, and an empty one
    // is not evidence of a lead that is long enough.
    public int started => counters[Started];

    public void ClearLateness() => (counters[Late], counters[Started]) = (0, 0);

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
    // side its note came from would be two answers to one question. A voice that is a
    // unison pair goes in as the sum of its two halves for the same reason and not a
    // new one: what the effect wants is the note, and where the note's halves were
    // stood is a fact about the dry mix.
    //
    // Which is also why the dry sum has two terms now and the sends still have one.
    // The two halves of a pair are rendered at two positions, so each needs its own
    // pair of gains; everything downstream of that is the arrangement there always
    // was, with a gain per destination fixed for the life of the voice.
    //
    // levels is the one thing written here that nothing downstream reads: how loud each
    // slot was over this buffer, for whoever is drawing the pool. It is a level and not
    // a flag because a voice is not on or off — it is somewhere in its envelope — and
    // it is taken from the samples themselves rather than from the envelope, so what is
    // drawn is what came out.
    public void Render(NativeArray<float> dryL, NativeArray<float> dryR,
                       NativeArray<float> reverbIn, NativeArray<float> delayIn,
                       NativeArray<float> levels, int frameCount, long bufferStart,
                       float sampleRate)
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

            // Before the trigger, since the note is about to be swapped out of the
            // queue. Anything at or before the start of this buffer is time the note
            // will never be rendered in.
            var late = bufferStart - queue[next].startSample;
            if (late > counters[Late]) counters[Late] = (int)math.min(late, int.MaxValue);
            counters[Started]++;

            Trigger(queue[next], sampleRate);

            // Swap-remove, which is why the queue needs no ordering.
            counters[Count]--;
            queue[next] = queue[counters[Count]];
        }

        var dt = 1.0f / sampleRate;

        var watched = levels.IsCreated;

        for (var i = 0; i < voices.Length; i++)
        {
            if (!voices[i].Active)
            {
                // A slot that has finished has to say so, or the last level it was
                // seen at would stand there for good.
                if (watched) levels[i] = 0.0f;
                continue;
            }

            // NativeArray hands out copies, so read, render and write back.
            var voice = voices[i];
            var note = voice.Note;
            var total = note.TotalDuration;

            // Once per voice per buffer, not per sample: the note holds still, so
            // the gains it renders at do too. A note asking for unison is two
            // positions rather than one — its own, thrown out to either side by the
            // spread — and one without gets back the pair of gains it always had
            // beside a second pair of zeroes for a half that is never rendered.
            note.UnisonGains(out var lowerL, out var lowerR,
                             out var upperL, out var upperR);

            var loudest = 0.0f;

            for (var frame = 0; frame < frameCount; frame++)
            {
                // Elapsed note time. The subtraction stays small even for a long
                // running DSP clock, so float precision is fine here.
                var time = (bufferStart + frame - note.startSample) * dt;

                if (time < 0.0f) continue;              // Starts later in this buffer
                if (time >= total) { voice.Release(); break; }

                voice.Next(time, out var lower, out var upper);

                // What the voice is worth as one signal, which is what everything
                // with nowhere to put a position takes.
                var sample = lower + upper;

                if (watched) loudest = math.max(loudest, math.abs(sample));

                dryL[frame] += lower * lowerL + upper * upperL;
                dryR[frame] += lower * lowerR + upper * upperR;
                reverbIn[frame] += sample * note.reverbSend;
                delayIn[frame] += sample * note.delaySend;
            }

            voices[i] = voice;

            if (watched) levels[i] = loudest;
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
