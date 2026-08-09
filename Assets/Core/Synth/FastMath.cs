namespace Jacquard {

// The handful of functions the synth needs, written out longhand.
//
// This exists for a specific reason: Burst cannot resolve the extern methods
// behind System.MathF, so a render job that calls MathF.Sin quietly falls back to
// managed execution on the audio thread. Unity.Mathematics would solve that, but it
// references the engine, and this assembly is deliberately built with no engine
// references at all — so the arithmetic is spelled out here instead, which keeps
// the DSP both portable and compilable.
//
// Accuracy is around 4e-6 for the sine, which is some 100dB below the signal.

public static class FastMath
{
    public const float Pi = 3.14159265f;
    public const float TwoPi = 6.28318531f;
    public const float HalfPi = 1.57079633f;

    public static float Floor(float x)
    {
        var i = (int)x;
        return x < 0.0f && x != i ? i - 1 : i;
    }

    public static float Frac(float x) => x - Floor(x);

    // Reduced to a half turn, folded into a quarter, then a ninth order odd
    // polynomial — the usual way a sine is built when there is no library.
    public static float Sin(float x)
    {
        var turns = x * (1.0f / TwoPi);
        turns -= Floor(turns + 0.5f);

        var r = turns * TwoPi;

        if (r > HalfPi) r = Pi - r;
        else if (r < -HalfPi) r = -Pi - r;

        var s = r * r;

        return r * (1.0f + s * (-0.166666667f +
                    s * (0.00833333333f +
                    s * (-0.000198412698f +
                    s * 2.75573192e-6f))));
    }

    // The other half of the pan law's circle, which is a quarter turn ahead of the
    // sine and nothing else: nothing here needs a cosine accurate anywhere the sine
    // above is not.
    public static float Cos(float x) => Sin(x + HalfPi);

    // Split into a power of two and a fraction. The envelopes only ever ask for a
    // decay, so the exponent stays small and the loop is a few multiplies.
    public static float Exp(float x)
    {
        var t = x * 1.44269504f; // log2(e)
        var i = (int)Floor(t);
        var f = t - i;

        var p = 1.0f + f * (0.693147181f +
                f * (0.240226507f +
                f * (0.0555041087f +
                f * (0.00961812911f +
                f * 0.00133335581f))));

        return p * Exp2(i);
    }

    // A static table would be quicker, but a managed array cannot be touched from
    // Burst compiled code, and the loop runs at most a dozen times.
    static float Exp2(int exponent)
    {
        var result = 1.0f;

        if (exponent > 64) return float.MaxValue;
        if (exponent < -64) return 0.0f;

        while (exponent > 0) { result *= 2.0f; exponent--; }
        while (exponent < 0) { result *= 0.5f; exponent++; }

        return result;
    }

    // Equal temperament pitch, which is an exp2 of a twelfth.
    public static float Pow2(float x)
      => Exp(x * 0.693147181f);
}

} // namespace Jacquard
