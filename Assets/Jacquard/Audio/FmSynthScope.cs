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

public struct FmSynthScope
{
    // The mix, mono, as a ring. The two sides are summed here rather than kept apart:
    // what is being drawn is what is being heard, and a scope of two lines almost on
    // top of each other says nothing the one line does not.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float> wave;

    // Where the next sample goes, which is also where the oldest one currently is.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<int> cursor;

    // How loud each voice slot was over the last buffer, or nothing at all if it is
    // free. A level rather than a flag: a voice is not on or off, it is somewhere in
    // its envelope, and the whole reason to draw the pool is to see that happen.
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<float> levels;

    public bool IsCreated => wave.IsCreated;

    public int Length => wave.Length;

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

    public float Level(int slot) => levels[slot];

    public int Slots => levels.Length;

    public static FmSynthScope Create(int frames, int voices)
      => new FmSynthScope
        { wave = new NativeArray<float>(frames, Allocator.Persistent),
          cursor = new NativeArray<int>(1, Allocator.Persistent),
          levels = new NativeArray<float>(voices, Allocator.Persistent) };

    public void Dispose()
    {
        if (wave.IsCreated) wave.Dispose();
        if (cursor.IsCreated) cursor.Dispose();
        if (levels.IsCreated) levels.Dispose();
    }

    // Called at the end of a render, with the mix as it will be heard.
    public void Write(NativeArray<float> left, NativeArray<float> right, int frameCount)
    {
        if (!wave.IsCreated) return;

        var length = wave.Length;
        var at = cursor[0];

        for (var frame = 0; frame < frameCount; frame++)
        {
            wave[at] = (left[frame] + right[frame]) * 0.5f;
            if (++at >= length) at = 0;
        }

        cursor[0] = at;
    }
}

} // namespace Jacquard.App
