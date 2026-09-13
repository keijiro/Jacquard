using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Jacquard.App {

// What the mix looked like, for anything that wants to draw it.
//
// The synth reports what it is doing through the status pipe, and the status is a
// handful of counters: it is a message, and a message is the wrong shape for a
// waveform. So this is the other direction — memory the driver allocates on the main
// thread and hands to the audio side, written by the render job as it finishes a buffer
// and read by whoever is drawing, at whatever rate that happens to be.
//
// Which means the two ends are not synchronised, and deliberately not. The safety
// system is told so by hand, because it would otherwise refuse a main thread read of an
// array a scheduled job has: what is at stake in that race is one column of a scope
// drawn from a buffer that was half overwritten while it was being read, on a frame
// nobody will see again. Paying a lock or a copy per buffer for that would be paying
// for the audio thread to wait on the drawing.
//
// The ring is short. What a scope wants is the last few milliseconds and nothing
// before that, and a buffer covering a tenth of a second is already longer than
// anything a frame will draw.
//
// There are two rings and one cursor. The second is the part of the mix one channel
// is answerable for, and it shares the first's cursor rather than keeping its own so
// that the two are the same instant at the same index by construction: a reader that
// triggers off the mix can read the tap at the indices the trigger handed it and know
// the two lines are of one moment. Two cursors would be two answers to when now is,
// and nothing above would have any way to reconcile them.
//
// The watch runs the other way — the main thread writes it and the render job reads
// it — and it is the same bargain as everything else here, for the same reason. What
// is at stake in the race is which channel one buffer was tapped for. The worst it
// can come out is the channel that was selected a moment ago, drawn for a twelfth of
// a frame, and the trace is redrawn before anyone could see it. A lock across the
// audio thread costs more than that is worth.

public struct FmSynthScope
{
    // The mix, mono, as a ring. The two sides are summed here rather than kept apart:
    // what is being drawn is what is being heard, and a scope of two lines almost on
    // top of each other says nothing the one line does not.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float> wave;

    // The watched channel's share of that same mix, as a ring of the same length
    // walked by the same cursor. Silent — not absent — while nothing is watched, so
    // the reader has one thing to check rather than two.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float> tap;

    // Where the next sample goes, which is also where the oldest one currently is.
    // One cursor for both rings; see the header.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> cursor;

    // Which channel the tap is to follow, or 0 for none. The one thing here the main
    // thread writes and the audio side reads.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> watch;

    public bool IsCreated => wave.IsCreated;

    public int Length => wave.Length;

    // Asked from both sides: the app sets it as the selection moves, and the render
    // job reads it once a buffer. Zero taps nothing, which is also what it costs.
    public int Watch
    {
        get => watch[0];
        set => watch[0] = value;
    }

    // Where the newest sample sits, so a reader can walk backwards from it.
    public int Head => cursor[0];

    // Anywhere at all, wrapped. Reading past either end of the ring is the normal way
    // to read one, not a mistake to be caught.
    public float At(int index)
    {
        var length = wave.Length;
        index %= length;
        return wave[index < 0 ? index + length : index];
    }

    // The same reading of the other ring, so that one index means one moment on both.
    public float TapAt(int index)
    {
        var length = tap.Length;
        index %= length;
        return tap[index < 0 ? index + length : index];
    }

    public static FmSynthScope Create(int frames)
      => new FmSynthScope
        { wave = new NativeArray<float>(frames, Allocator.Persistent),
          tap = new NativeArray<float>(frames, Allocator.Persistent),
          cursor = new NativeArray<int>(1, Allocator.Persistent),
          watch = new NativeArray<int>(1, Allocator.Persistent) };

    public void Dispose()
    {
        if (wave.IsCreated) wave.Dispose();
        if (tap.IsCreated) tap.Dispose();
        if (cursor.IsCreated) cursor.Dispose();
        if (watch.IsCreated) watch.Dispose();
    }

    // Called at the end of a render, with the mix as it will be heard and the watched
    // channel's dry contribution beside it.
    //
    // The tap arrives unstaged, straight off the voices, and is multiplied by the same
    // master gain the mix was staged with on the way in. That multiply is here and
    // nowhere else, and it is what makes the second line readable against the first:
    // both are on the scale the mix is on, so what the channel's line shows is the
    // share of the mix it is worth rather than how loud it was before the staging.
    public void Write(NativeArray<float> left, NativeArray<float> right,
                      NativeArray<float> tapIn, float tapGain, int frameCount)
    {
        if (!wave.IsCreated) return;

        var length = wave.Length;
        var at = cursor[0];

        for (var frame = 0; frame < frameCount; frame++)
        {
            wave[at] = (left[frame] + right[frame]) * 0.5f;
            tap[at] = tapIn[frame] * tapGain;
            if (++at >= length) at = 0;
        }

        cursor[0] = at;
    }
}

} // namespace Jacquard.App
