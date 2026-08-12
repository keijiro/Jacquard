using Unity.Collections;
using Unity.Mathematics;

namespace Jacquard.App {

// The limiter on the finished mix.
//
// It is a gain and nothing else: one number multiplying both sides, worked out from the
// loudest of the two so that the image cannot be pulled about by a peak on one side.
// The gain is what carries the attack and the release, rather than an envelope
// follower ahead of it — which is the difference between a limiter that lets the front
// of a kick through and one that does not. A detector smoothed on the way in reaches
// the ceiling late and then holds the whole note down; a gain smoothed on the way out
// is at full scale when the transient arrives and takes exactly as long as the attack
// says to arrive at where it should have been.
//
// So a slow attack is a hole punched in the limiting for the length of the attack, and
// that hole is the punch. What comes through it is over the ceiling, which is what the
// soft clip after this is for: a few samples of a peak rounded off is a sound records
// have made for fifty years, and it is bounded — the clip is the reason nothing here
// needs a lookahead, and a lookahead is the one thing that would have cost latency.
//
// There is no ratio and no knee. Above the ceiling the gain is exactly what holds the
// output at it, which is an infinite ratio and a hard knee, and everything a knee would
// have softened is softened by the attack instead. What is played is the drive.
//
// State is one float in a NativeArray for the reason the other two buses keep theirs
// there: this struct is copied into the render job, so anything written to a field of
// it is written to the copy and lost at the end of the buffer.

public struct LimiterBus
{
    public NativeArray<float> state; // The peak and the gain, carried between buffers

    const int Peak = 0, Gain = 1;

    public static LimiterBus Create()
      => new LimiterBus { state = new NativeArray<float>(2, Allocator.Persistent) };

    public void Dispose()
    {
        if (state.IsCreated) state.Dispose();
    }

    // Drives the mix into the ceiling and holds it there, in place.
    public void Process(NativeArray<float> left, NativeArray<float> right,
                        int frameCount, in LimiterRuntime settings)
    {
        // A fresh bus opens all the way rather than fading up from silence, which is
        // what a zeroed array would otherwise mean.
        var gain = state[Gain];
        if (gain <= 0.0f) gain = 1.0f;

        var held = state[Peak];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var l = left[frame] * settings.drive;
            var r = right[frame] * settings.drive;

            // The loudest of the two sides, so the two are held down together — and
            // held rather than followed: a peak is taken the instant it arrives and
            // let go of at the release.
            //
            // The hold is what makes the ceiling a ceiling. Read sample by sample, the
            // loudness of a tone goes to nothing twice a cycle, so the gain would climb
            // back between the peaks and arrive at each one too high: at 220Hz a cycle
            // is 4.5ms against an attack of 5, and the output sat a fifth over the
            // ceiling however long it was given to settle. What holds the peak leaves
            // the gain a constant to converge on, so the only thing over the ceiling is
            // what the attack deliberately lets past.
            var peak = math.max(math.abs(l), math.abs(r));

            held = peak > held ? peak : held + (peak - held) * settings.release;

            // What the gain would have to be for that peak to land on the ceiling.
            // Anything under it asks for no reduction at all.
            var target = held > settings.ceiling ? settings.ceiling / held : 1.0f;

            // Down at the attack and up at the release, which is the whole of the
            // shape: a gain that is above where it should be is late, and a gain that
            // is below is holding something down that has already gone.
            gain += (target - gain) *
                    (target < gain ? settings.attack : settings.release);

            left[frame] = l * gain;
            right[frame] = r * gain;
        }

        state[Peak] = held;
        state[Gain] = gain;
    }
}

} // namespace Jacquard.App
