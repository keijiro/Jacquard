using Unity.Collections;
using Unity.Mathematics;

namespace Jacquard.App {

// The delay the notes are sent to, in time with the sequence.
//
// Two lines, one per side of the stereo wet path, read at a tap the tempo decides.
// What arrives here is already a number of samples: the project holds the time as a
// note value and the main thread turns it into a distance, because the tempo is
// something the score knows about and the audio thread is not.
//
// Two things this has to survive that nothing else in the project does.
//
// The tap moves while the delay is running — the tempo bar is a bar like any other,
// and the note value is a chooser that can be stepped mid-bar. A tap is a position
// in the signal, unlike the reverb's coefficients, so moving it by an eighth of a
// second is a splice: the read pointer lands somewhere unrelated to where it was and
// the seam is a click. So the tap is never set, only approached, and the approach is
// **rate limited** rather than exponential. A limit is a constant speed, which is a
// constant interval of pitch for as long as the glide lasts and then nothing — the
// sound a tape delay makes when its head is moved, which is a sound rather than a
// fault. An exponential would start the glide at whatever speed the jump was wide,
// which for a rung of the ladder is an audible screech at the front of it.
//
// The other is that the tap is fractional. A tempo of 132 does not divide the sample
// rate, so rounding the tap to a whole sample would quantize the delay time and,
// worse, make a slow tempo drag arrive in steps of one sample each — which is
// exactly the ticking the rate limit is there to avoid. So the two samples either
// side of the tap are mixed in proportion.
//
// If the glide is ever unwanted, the thing to replace it with is a pair of taps and
// a short crossfade between them: that jumps cleanly with no pitch at all, at the
// cost of a second read per sample and a fade to keep track of. The glide is the
// choice here because it can be played.

public struct DelayBus
{
    public NativeArray<float> lines;  // Two lines end to end, capacity frames each
    public NativeArray<int> cursors;  // One entry: where the write head is
    public NativeArray<float> state;  // The tap in samples, then the two lowpasses
    public int capacity;              // A line's length, and the second line's origin

    const int Tap = 0, LowpassL = 1, LowpassR = 2;

    // How fast the tap is allowed to travel, in samples per sample. A quarter means
    // the read pointer moves at three quarters or five quarters of the write
    // pointer's speed while it is catching up — about five semitones of bend — and
    // that a rung of the ladder is arrived at in a second or so.
    const float MaxTapRate = 0.25f;

    // Never closer than this to the write head, so that the interpolation always has
    // two samples of history to read and the loop can never feed itself instantly.
    const float MinTap = 2.0f;

    public static DelayBus Create(float sampleRate)
    {
        // Sized for the longest the ladder can ask for: one beat at the slowest
        // tempo the transport offers. Anything past that is a setting that cannot be
        // reached from the panel, so it is clamped rather than allocated for.
        var frames = (int)(DelayTime.LongestSeconds * sampleRate) + 4;

        return new DelayBus
          { capacity = frames,
            lines = new NativeArray<float>(frames * 2, Allocator.Persistent),
            cursors = new NativeArray<int>(1, Allocator.Persistent),
            state = new NativeArray<float>(3, Allocator.Persistent) };
    }

    public void Dispose()
    {
        if (lines.IsCreated) lines.Dispose();
        if (cursors.IsCreated) cursors.Dispose();
        if (state.IsCreated) state.Dispose();
    }

    // Adds the repeats of what is in input to the two wet buffers.
    //
    // tapSamples is where the delay wants to be read from; feedback, tone and spread
    // are the panel's three numbers. Unlike the reverb the tap is stepped per sample,
    // since the whole point of the limit is that it is a speed.
    public void Process(NativeArray<float> input, NativeArray<float> wetL,
                        NativeArray<float> wetR, int frameCount, float tapSamples,
                        float feedback, float tone, float spread)
    {
        var target = math.clamp(tapSamples, MinTap, capacity - MinTap);

        // A fresh bus starts at the tap rather than gliding out to it.
        if (state[Tap] <= 0.0f) state[Tap] = target;

        feedback = math.clamp(feedback, 0.0f, SendFx.MaxFeedback);
        spread = math.saturate(spread);

        // The tone is how much of a repeat's top survives, so it is the brightness
        // itself: at one the filter runs with a coefficient of one, which is a repeat
        // passed straight through, and the bar darkens as it comes down. Squared, so
        // that the darkening is spread over the bar instead of happening all at once
        // near the bottom of it.
        //
        // The floor is what the bottom of the bar comes to, and it is a measured
        // number rather than a small one. It used to be 0.02 — about 154Hz — from when
        // the heard tap was read ahead of the filter and the bottom of the bar only
        // ever thinned the tail. Now that the tap is filtered too, a floor that low
        // spends the bottom fifth of the bar on a repeat already 10dB down and a tail
        // 28dB down, which is a stretch of travel that turns the delay off rather than
        // darkening it. At 0.06, about 473Hz, the bottom of the bar is a repeat 3.4dB
        // down with a tail 15dB down: dark, short, and still there. Measured on the
        // voice a new score opens in, an eighth note apart at the default feedback.
        var bright = math.saturate(tone);
        var cutoff = bright * bright * 0.94f + 0.06f;

        var write = cursors[0];
        var tap = state[Tap];

        for (var frame = 0; frame < frameCount; frame++)
        {
            tap += math.clamp(target - tap, -MaxTapRate, MaxTapRate);

            var read = write - tap;
            if (read < 0.0f) read += capacity;

            var left = Read(0, read);
            var right = Read(capacity, read);

            // Each repeat darker than the one before it, because the filter is inside
            // the loop and every lap passes through it again.
            state[LowpassL] += (left - state[LowpassL]) * cutoff;
            state[LowpassR] += (right - state[LowpassR]) * cutoff;

            // And what is heard is taken after the filter rather than before it, so
            // the first repeat is darkened along with the rest. Read ahead of it — as
            // it was until now — the first repeat is whatever went in, untouched: with
            // the tone at the bottom of the bar that is a full scale slap followed by
            // a tail 43dB below it, which is one crisp echo and no tone control at
            // all. What the change costs is the level of that first repeat, and on a
            // note rather than an impulse it is small: 0.2dB at the default setting,
            // 3.4dB at the bottom of the bar.
            wetL[frame] += state[LowpassL];
            wetR[frame] += state[LowpassR];

            var backL = state[LowpassL] * feedback;
            var backR = state[LowpassR] * feedback;

            // Spread does two things at once, which is what lets one number cover the
            // span: it takes the input off the right hand line, and it crosses the
            // feedback over. At zero both lines carry the same thing and the repeats
            // sit in the middle; at one only the left is fed and each lap swaps sides,
            // which is a ping-pong.
            var dry = input[frame];

            lines[write] = dry + backL * (1.0f - spread) + backR * spread;
            lines[capacity + write] =
              dry * (1.0f - spread) + backR * (1.0f - spread) + backL * spread;

            if (++write >= capacity) write = 0;
        }

        cursors[0] = write;
        state[Tap] = tap;
    }

    // The tap falls between two samples, so it reads both.
    float Read(int origin, float position)
    {
        var index = (int)position;
        var frac = position - index;

        var next = index + 1;
        if (next >= capacity) next -= capacity;

        return lines[origin + index] * (1.0f - frac) + lines[origin + next] * frac;
    }
}

} // namespace Jacquard.App
