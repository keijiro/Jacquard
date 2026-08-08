using System;
using System.Globalization;

namespace Jacquard {

// The tile hierarchy.
//
// Four categories, as laid out in sequencer.md: notes sound, parameter locks
// operate on the timbre, gates decide whether what hangs below them fires, and
// flow tiles steer the sequence. A category word only appears in a concrete name
// when the modifier alone would not carry the meaning, so there is an
// AbsoluteParamTile and a CycleGateTile but a plain JumpTile.
//
// Token is the four character display code, not the data: it is what the cell
// (or an inspector caption) shows, and the file format has its own spellings.

public abstract class Tile
{
    public abstract string Token { get; }
}

// Notes

// Length is measured in steps, defaulting to one. What a step is worth in real
// time comes from the channel's division and the project tempo.

public sealed class NoteTile : Tile
{
    public int Note { get; set; } = 60;
    public float Length { get; set; } = 1.0f;

    public bool HasDefaultLength => MathF.Abs(Length - 1.0f) < 1e-4f;

    public override string Token
      => HasDefaultLength ? Pitch.ToName(Note)
         : Pitch.ToName(Note) + "/" +
           Length.ToString("0.###", CultureInfo.InvariantCulture);
}

// Parameter locks

// Which parameters a lock takes hold of and how far it moves them are not shown
// on the cell; the icon only says which kind of lock it is. A lock carries a slot
// per ParamTargets index, whose contents belong to the synth side.
//
// One tile reaches as many parameters as it likes. Stacking a tile per parameter
// would work — locks chain downward — but it costs a cell each and pushes the
// notes it colours further from the step they belong to, and there is nothing a
// stack of them can say that one tile cannot.
//
// A parameter nothing has engaged is left entirely alone, which is why a lock
// that engages nothing does nothing at all. That is allowed, the same way a lock
// with nothing below it to colour is: it is inert rather than wrong, and it is
// what a lock looks like the moment it is placed.
//
// A lock always reaches the whole channel and never outlives the instant it sits
// in, so there is no scope to record: what it actually colours is whatever is
// processed after it, which the position on the plane decides and the tile does
// not need to know.

public abstract class ParamTile : Tile
{
    public bool IsEngaged(int target)
      => InRange(target) && _engaged[target];

    // What the lock moves the target to, or by. Zero when it has not taken hold
    // of that one, which is nothing to add and is never read as a value to set.
    public float this[int target]
      => InRange(target) && _engaged[target] ? _amounts[target] : 0.0f;

    public void Engage(int target, float amount)
    {
        if (!InRange(target)) return;
        (_engaged[target], _amounts[target]) = (true, amount);
    }

    // Letting go forgets the amount as well. A released slot shows what the
    // channel does without it, so a number kept behind the panel would only be a
    // second value with a claim on the same row.
    public void Release(int target)
    {
        if (!InRange(target)) return;
        (_engaged[target], _amounts[target]) = (false, 0.0f);
    }

    public bool IsEmpty
    {
        get
        {
            foreach (var engaged in _engaged) if (engaged) return false;
            return true;
        }
    }

    readonly bool[] _engaged = new bool[ParamTargets.Count];
    readonly float[] _amounts = new float[ParamTargets.Count];

    static bool InRange(int target) => target >= 0 && target < ParamTargets.Count;
}

public sealed class AbsoluteParamTile : ParamTile
{
    public override string Token => "PABS";
}

public sealed class RelativeParamTile : ParamTile
{
    public override string Token => "PREL";
}

// Gates

// A gate ends the walk down its stack when it does not fire, so it governs
// everything below it in the step and nothing above it.

public abstract class GateTile : Tile
{
    // pass counts how many times the runner has been round its own channel, so
    // a cycle gate can pick a lap.
    public abstract bool Evaluate(int pass, Random random);
}

// Fires on one lap out of Period. Index is one based, matching the GCYC4:3
// spelling, and the period stays within 2..8 because that is how many boxes fit
// across a cell.

public sealed class CycleGateTile : GateTile
{
    public const int MinPeriod = 2, MaxPeriod = 8;

    public int Period { get => _period; set => _period = Clamp(value); }
    public int Index { get => _index; set => _index = Math.Clamp(value, 1, _period); }

    public override bool Evaluate(int pass, Random random)
      => ((pass % _period) + _period) % _period == _index - 1;

    public override string Token => "GCYC" + _period + ":" + _index;

    int _period = 4;
    int _index = 1;

    static int Clamp(int value) => Math.Clamp(value, MinPeriod, MaxPeriod);
}

// Fires with the given chance. Any percentage is allowed: the pie chart shows
// whatever fraction it is given, so there is no reason to quantize it.

public sealed class ProbGateTile : GateTile
{
    public float Percent
    {
        get => _percent;
        set => _percent = Math.Clamp(value, 0.0f, 100.0f);
    }

    public override bool Evaluate(int pass, Random random)
      => random.NextDouble() * 100.0 < _percent;

    public override string Token
      => "GPRB:" + _percent.ToString("0.#", CultureInfo.InvariantCulture);

    float _percent = 50.0f;
}

// Flow

public abstract class FlowTile : Tile {}

// Start of a channel's stream. Division is the note value of one step as a
// denominator, so 16 means a sixteenth note.
//
// The channel number picks the timbre as well as the stream: the patch bank holds
// one patch per channel, which is why the number is bounded by the bank rather
// than free to run away.

public sealed class ChannelTile : FlowTile
{
    public int Channel { get => _channel; set => _channel = PatchBank.Clamp(value); }
    public int Division { get => _division; set => _division = Clamp(value); }

    public override string Token => "CHAN:" + _channel;

    // Seconds taken by one step at the given tempo.
    public float StepSeconds(float tempo)
      => 60.0f / MathF.Max(tempo, 1.0f) * 4.0f / _division;

    int _channel = 1;
    int _division = 16;

    // Powers of two from a whole note to a sixty-fourth, plus the triplet
    // denominators that make a lane swing against the others.
    public static readonly int[] Divisions = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64 };

    static int Clamp(int value)
    {
        var best = 16;
        foreach (var d in Divisions) if (Math.Abs(d - value) < Math.Abs(best - value)) best = d;
        return best;
    }
}

// End of a lane. Never stored: it is implied one column past the last step, and
// reaching it sends the runner back to the channel it started from.

public sealed class TerminatorTile : FlowTile
{
    public override string Token => "TERM";
}

// Leaves this lane for the one lane that answers to it. On its own it only
// duplicates a longer lane, so it earns its keep when a gate sits above it.

public sealed class JumpTile : FlowTile
{
    public override string Token => "JUMP";
}

// Where a jump lands, and the head of a branch lane. Exactly one jump reaches
// it, which the lane records rather than the tile.

public sealed class JumpDestTile : FlowTile
{
    public override string Token => "JDST";
}

} // namespace Jacquard
