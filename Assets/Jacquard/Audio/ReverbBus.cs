using Unity.Collections;
using Unity.Mathematics;

namespace Jacquard.App {

// The reverb the notes are sent to.
//
// A Schroeder network in the arrangement Freeverb settled on: eight comb filters in
// parallel, each with a one pole lowpass inside its feedback path, then four
// allpasses in series to smear what comes out of them. Two of everything, since the
// wet path is stereo, with the right hand set of lines a little longer so that the
// two sides decorrelate.
//
// It is here rather than a feedback delay network because of what the panel asks
// for: a usable tail from two controls. Size is one number that every comb reads as
// its feedback and damping is one number that every lowpass reads as its cutoff, and
// between them they cover a tiled room and a hall without anything else having to be
// dialled in. An FDN would sound better and would want a matrix, a diffusion amount
// and a modulation depth to get there.
//
// Nothing here changes the length of a line, which is what makes the whole thing
// safe to sweep: size and damping are coefficients, so moving one alters how the
// signal already in the lines decays rather than where it is read from. Only the
// width can step the output, since it is a gain, and the smoothing below covers all
// three rather than singling it out.
//
// The tunings are Freeverb's, measured at 44.1 kHz and scaled to whatever the device
// is running at. They are a set of mutually prime lengths, which is what keeps the
// combs from agreeing with each other and ringing.

public struct ReverbBus
{
    // Every delay line laid end to end, indexed through starts.
    public NativeArray<float> lines;
    public NativeArray<int> starts;   // Lines + 1 entries, so a length is a subtraction
    public NativeArray<int> cursors;  // Where each line is being read and written
    public NativeArray<float> stores; // The lowpass inside each comb
    public NativeArray<float> smooth; // Size, damping and width, on their way to target

    public const int CombCount = 8;
    public const int AllpassCount = 4;
    const int PerChannel = CombCount + AllpassCount;
    const int Lines = PerChannel * 2;

    // Freeverb's lengths in samples at 44.1 kHz, and the stretch the right channel
    // adds to each of them.
    static readonly int[] CombTuning =
      { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };

    static readonly int[] AllpassTuning = { 556, 441, 341, 225 };

    const int StereoSpread = 23;
    const float ReferenceRate = 44100.0f;

    // What the two normalized controls come to as coefficients. A room that never
    // quite stops and one that stops immediately are both useless, so the feedback
    // covers the span between them rather than reaching either end.
    const float MinFeedback = 0.70f;
    const float FeedbackSpan = 0.28f;
    const float DampSpan = 0.4f;

    const float AllpassFeedback = 0.5f;

    // Freeverb's input trim and its matching output scale. Together they come to
    // roughly unity, which is what makes a send of one a wet signal about as loud as
    // the dry note that fed it.
    const float InputGain = 0.015f;
    const float OutputGain = 3.0f;

    // How fast a moved control arrives, as a time constant. Long enough that a bar
    // dragged across its whole travel does not step, short enough that it is not a
    // control with a lag on it.
    const float SmoothingSeconds = 0.03f;

    public static ReverbBus Create(float sampleRate)
    {
        var bus = new ReverbBus();

        bus.starts = new NativeArray<int>(Lines + 1, Allocator.Persistent);
        bus.cursors = new NativeArray<int>(Lines, Allocator.Persistent);
        bus.stores = new NativeArray<float>(CombCount * 2, Allocator.Persistent);
        bus.smooth = new NativeArray<float>(3, Allocator.Persistent);

        var total = 0;

        for (var line = 0; line < Lines; line++)
        {
            bus.starts[line] = total;
            total += Length(line, sampleRate);
        }

        bus.starts[Lines] = total;
        bus.lines = new NativeArray<float>(total, Allocator.Persistent);

        // The controls start where they are rather than sliding up from nothing, so
        // the first note into a fresh bus already has the tail the panel shows.
        bus.smooth[0] = -1.0f;

        return bus;
    }

    // Line order is the eight combs of the left channel, then its four allpasses,
    // then the same again for the right.
    static int Length(int line, float sampleRate)
    {
        var channel = line / PerChannel;
        var index = line % PerChannel;

        var tuning = index < CombCount ? CombTuning[index]
                     : AllpassTuning[index - CombCount];

        var scaled = (int)(tuning * sampleRate / ReferenceRate) + channel * StereoSpread;
        return math.max(scaled, 1);
    }

    public void Dispose()
    {
        if (lines.IsCreated) lines.Dispose();
        if (starts.IsCreated) starts.Dispose();
        if (cursors.IsCreated) cursors.Dispose();
        if (stores.IsCreated) stores.Dispose();
        if (smooth.IsCreated) smooth.Dispose();
    }

    // Adds the tail of what is in input to the two wet buffers. The controls are
    // read once for the block and then held: a block is a few milliseconds, and a
    // coefficient that settles over thirty of them cannot be heard arriving in
    // steps that size.
    public void Process(NativeArray<float> input, NativeArray<float> wetL,
                        NativeArray<float> wetR, int frameCount, float sampleRate,
                        float size, float damp, float width)
    {
        Approach(size, damp, width, frameCount / sampleRate);

        var feedback = MinFeedback + FeedbackSpan * math.saturate(smooth[0]);
        var damping = DampSpan * math.saturate(smooth[1]);
        var spread = math.saturate(smooth[2]);

        // The pair is turned from two independent channels into one image: at a
        // width of zero both sides carry the mean and the tail sits in the middle,
        // and at one each side is entirely its own.
        var direct = OutputGain * (spread * 0.5f + 0.5f);
        var crossed = OutputGain * (1.0f - spread) * 0.5f;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var x = input[frame] * InputGain;

            var left = 0.0f;
            var right = 0.0f;

            for (var i = 0; i < CombCount; i++)
            {
                left += Comb(i, x, feedback, damping);
                right += Comb(PerChannel + i, x, feedback, damping);
            }

            for (var i = 0; i < AllpassCount; i++)
            {
                left = Allpass(CombCount + i, left);
                right = Allpass(PerChannel + CombCount + i, right);
            }

            wetL[frame] += left * direct + right * crossed;
            wetR[frame] += right * direct + left * crossed;
        }
    }

    // One pole per control, toward whatever the panel last said. The first block
    // after a Create jumps instead, which is what the sentinel in smooth[0] marks.
    void Approach(float size, float damp, float width, float blockSeconds)
    {
        if (smooth[0] < 0.0f)
        {
            (smooth[0], smooth[1], smooth[2]) = (size, damp, width);
            return;
        }

        var rate = 1.0f - math.exp(-blockSeconds / SmoothingSeconds);

        smooth[0] += (size - smooth[0]) * rate;
        smooth[1] += (damp - smooth[1]) * rate;
        smooth[2] += (width - smooth[2]) * rate;
    }

    // A comb whose feedback path is dulled by a one pole, which is what makes the
    // tail lose its top as it decays rather than ringing on unchanged.
    float Comb(int line, float input, float feedback, float damping)
    {
        var index = starts[line] + cursors[line];
        var output = lines[index];

        var store = output * (1.0f - damping) + stores[Store(line)] * damping;
        stores[Store(line)] = store;

        lines[index] = input + store * feedback;
        Advance(line);

        return output;
    }

    // Passes everything and delays nothing on average, which is how a comb's output
    // is smeared into something without a pitch of its own.
    float Allpass(int line, float input)
    {
        var index = starts[line] + cursors[line];
        var buffered = lines[index];

        lines[index] = input + buffered * AllpassFeedback;
        Advance(line);

        return buffered - input;
    }

    // The combs are the first eight lines of each channel, so their stores pack down
    // into an array of their own rather than leaving four holes per channel.
    static int Store(int line) => line / PerChannel * CombCount + line % PerChannel;

    void Advance(int line)
    {
        var cursor = cursors[line] + 1;
        cursors[line] = cursor >= starts[line + 1] - starts[line] ? 0 : cursor;
    }
}

} // namespace Jacquard.App
