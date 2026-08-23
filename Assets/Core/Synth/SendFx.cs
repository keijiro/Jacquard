using System;

namespace Jacquard {

// The note values a delay time is chosen from.
//
// A delay that is not in time with the sequence is a delay nobody reaches for, so
// the time is never a number of milliseconds: it is a note value, and what it comes
// to in seconds is whatever the project's tempo says. Which makes it the same kind
// of quantity as a lane's step, and it is spelled the same way — 1/8 is an eighth
// note whichever of the two is reading it.
//
// The rungs run from shortest to longest rather than being grouped by note value,
// because the control is a pair of arrows: a list that reads 1/4, 1/4T, 1/8D would
// go long, shorter, longer as it is stepped through, and there is nothing to be
// gained from a chooser that does not move in one direction.
//
// Dotted values are here and the sequencer's divisions do not have them. A dotted
// eighth against a straight sequence is most of what a delay is used for, which is
// worth a table of its own; a lane, which has to divide a bar exactly, is not.

public static class DelayTime
{
    public static readonly string[] Names =
      { "1/32", "1/16T", "1/16", "1/8T", "1/16D", "1/8", "1/4T", "1/8D", "1/4" };

    // In beats, so a rung times 60/tempo is a time in seconds.
    public static readonly float[] Beats =
      { 0.125f, 1.0f / 6.0f, 0.25f, 1.0f / 3.0f, 0.375f,
        0.5f, 2.0f / 3.0f, 0.75f, 1.0f };

    public const int Default = 5; // 1/8

    // The longest a rung can ask for, which is one beat at the slowest tempo the
    // transport offers. What it is for is the size of the delay line: a buffer that
    // covers this covers every setting that can actually be dialled in.
    public const float LongestSeconds = 3.0f;

    // Which rung a stored time is on. The time is kept as a number of beats rather
    // than as an index into this table, so that the value in a file still means what
    // it says if the table is ever re-cut; the cost is that reading it back is a
    // search for the nearest rung rather than a lookup.
    public static int Nearest(float beats)
    {
        var (nearest, distance) = (Default, float.MaxValue);

        for (var i = 0; i < Beats.Length; i++)
        {
            var d = MathF.Abs(Beats[i] - beats);
            if (d >= distance) continue;
            (nearest, distance) = (i, d);
        }

        return nearest;
    }
}

// The two send effects, as the project holds them.
//
// Seven numbers for two effects, which is the whole of the design brief: what is
// wanted first is a pair of controls that can be swept while the sequence plays, not
// a studio's worth of them. So the reverb is a size and a damping, the delay is a
// time, a feedback and a tone, and each has one number for how wide it sits in the
// stereo field. Everything else about either — the comb tunings, the interpolation —
// is a decision the bus makes rather than one this offers.
//
// These belong to the project and not to a patch, unlike the two send amounts that
// feed them. One reverb serves every channel, which is the point of a send: eight
// channels each with a reverb of its own would be eight tails where a record has
// one, and eight times the work.
//
// Every field is normalized to [0,1] except the time, which is in beats. Nothing
// here is in seconds or in samples: what those come to depends on the tempo and on
// the device, and neither is the project's business.

public struct SendFx
{
    public float reverbSize;  // Tail length, short room to long hall
    public float reverbDamp;  // How fast the tail loses its top
    public float reverbWidth; // Correlated pair to fully spread

    public float delayBeats;    // Time as a multiple of one beat
    public float delayFeedback; // How much of a repeat comes back, up to MaxFeedback
    public float delayTone;     // How much of a repeat's top survives a lap
    public float delaySpread;   // Straight stereo to full ping-pong

    // Short of one, so that the loop cannot run away however hard the bar is pushed.
    public const float MaxFeedback = 0.95f;

    // A default that is audible the moment a send is raised, rather than one that
    // needs the panel visited first: a medium room, and an eighth note delay with
    // enough feedback for two or three repeats, each one a little duller than the
    // one before it — a tone wide open would hand back three copies of the note.
    public static SendFx Default => new SendFx
      { reverbSize = 0.5f,
        reverbDamp = 0.5f,
        reverbWidth = 1.0f,
        delayBeats = DelayTime.Beats[DelayTime.Default],
        delayFeedback = 0.35f,
        delayTone = 0.6f,
        delaySpread = 0.0f };

    // The delay time at a tempo. The same arithmetic a lane's step uses, since a
    // beat is a beat: ChannelTile.StepSeconds is this times four over its division.
    public float DelaySeconds(float tempo)
      => delayBeats * 60.0f / MathF.Max(tempo, 1.0f);
}

} // namespace Jacquard
