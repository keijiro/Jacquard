using Unity.Collections;

namespace Jacquard.App {

// The volume on the finished mix, applied last of all.
//
// One multiplication a sample, and everything here is about the other half of that:
// what happens while the number is moving. A gain read once a block and held for the
// length of it steps at every block boundary — a finger dragging the bar across its
// travel in half a second moves it by about a decibel a block, which is a tenth of the
// waveform's height arriving between two samples, and that is a click. Every other
// setting on this mix is spared that by what kind of quantity it is: a level is baked
// into a note at its start, the reverb's controls are coefficients that colour a tail
// rather than scale it, and the limiter's own gain is smoothed by the attack and the
// release it exists to have. A master volume has none of those excuses.
//
// So the target is approached across the block rather than jumped to at the top of it:
// the gain is walked from where the last block left it to where this one asks for it,
// in equal steps, arriving on the final sample. That is a ramp and not a filter, which
// is the point — there is no time constant to pick and nothing lags. Whatever the panel
// says is what the block ends on, and the ten milliseconds in between are spent getting
// there quietly.
//
// State is one float in a NativeArray for the reason the other buses keep theirs there:
// this struct is copied into the render job, so a field written down there is written to
// the copy and lost at the end of the buffer.

public struct OutputBus
{
    public NativeArray<float> state; // The gain, carried between buffers

    // Negative marks a bus nothing has been played through yet, so the first block sits
    // at whatever the project says instead of fading up to it from silence. A ramp from
    // zero would be inaudible and harmless; a ramp from zero on a project loaded at full
    // volume is a fade-in on the first note of a session, and there is no reason for one.
    public static OutputBus Create()
    {
        var bus = new OutputBus
          { state = new NativeArray<float>(1, Allocator.Persistent) };

        bus.state[0] = -1.0f;
        return bus;
    }

    public void Dispose()
    {
        if (state.IsCreated) state.Dispose();
    }

    // Scales both sides in place, arriving at gain on the last frame of the block.
    public void Process(NativeArray<float> left, NativeArray<float> right,
                        int frameCount, float gain)
    {
        var current = state[0];

        if (current < 0.0f) current = gain;

        // Held at the target when there is nothing to travel, which is what this is
        // doing on all but the handful of blocks a hand is on the bar for.
        if (current == gain)
        {
            if (gain != 1.0f)
                for (var frame = 0; frame < frameCount; frame++)
                {
                    left[frame] *= gain;
                    right[frame] *= gain;
                }
        }
        else
        {
            var step = (gain - current) / frameCount;

            for (var frame = 0; frame < frameCount; frame++)
            {
                current += step;
                left[frame] *= current;
                right[frame] *= current;
            }

            // Rather than whatever the additions came to, so that a bar left alone is
            // exactly at its number and the branch above takes over next block.
            current = gain;
        }

        state[0] = current;
    }
}

} // namespace Jacquard.App
