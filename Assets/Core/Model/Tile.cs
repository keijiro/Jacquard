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
// Token is the four character code a tile is spelled with, not the data. It is
// what a saved file writes and what this codebase calls a tile by; it is not what
// the UI shows, since a panel names a tile in words and a cell draws an icon. The
// note is the exception at both ends: its token is the pitch name itself, so it is
// the one token a caption is happy to print.

public abstract class Tile
{
    public abstract string Token { get; }

    // A tile of the same kind holding the same thing, or nothing for a tile that
    // cannot be had twice. Having no copy is what keeps a tile out of a copied
    // stack, so which tiles take part in one is said here and nowhere else.
    //
    // The flow tiles are the ones with no copy, which is a fact about them rather
    // than a rule laid over them: a CHAN names a lane, a JUMP is the identity its
    // branch lane answers to, and a TERM is implied one column past the last step
    // and never stored. None of them means anything a cell away from where it
    // stands.
    //
    // Written out by hand rather than round-tripped through ProjectFormat, which
    // has a text for every tile already. That text is a file's spelling: reading it
    // back is private, throws on anything it cannot parse, wants the file version
    // to go with it, and forgets a cycle gate's laps above its period. A copy has
    // to be exactly what it came from.
    public virtual Tile Copy() => null;
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

    public override Tile Copy() => new NoteTile { Note = Note, Length = Length };
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

    // Fills in a lock of whichever kind the caller made, since what a lock holds is
    // kept here and the kind is all a subclass adds.
    protected ParamTile CopyInto(ParamTile copy)
    {
        for (var target = 0; target < ParamTargets.Count; target++)
            if (_engaged[target]) copy.Engage(target, _amounts[target]);

        return copy;
    }

    readonly bool[] _engaged = new bool[ParamTargets.Count];
    readonly float[] _amounts = new float[ParamTargets.Count];

    static bool InRange(int target) => target >= 0 && target < ParamTargets.Count;
}

public sealed class AbsoluteParamTile : ParamTile
{
    public override string Token => "PABS";

    public override Tile Copy() => CopyInto(new AbsoluteParamTile());
}

public sealed class RelativeParamTile : ParamTile
{
    public override string Token => "PREL";

    public override Tile Copy() => CopyInto(new RelativeParamTile());
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

// Fires on whichever laps of the cycle are switched on, a lap being one time round
// the channel the gate stands on.
//
// A lap is a switch and not a number. One lap out of the period was all this could
// say to begin with, so a gate that wanted the first and the third of four had to
// be two gates in two cells, and a pattern of any interest could not be written at
// all. Every lap having a switch of its own costs the tile nothing — the whole
// cycle is one word — and it is what turns the period into a bar rather than a
// pointer.
//
// The period reaches 32, which is two bars of sixteen steps and as far as a cycle
// this is worth reading off a cell goes. The laps live in the bits of one mask, so
// laps above the period are kept rather than cleared: a period pulled in and let
// back out finds its switches where it left them, and only a save forgets them.

public sealed class CycleGateTile : GateTile
{
    public const int MinPeriod = 2, MaxPeriod = 32;

    public int Period { get => _period; set => _period = Clamp(value); }

    public bool Fires(int lap) => (_mask & Bit(lap)) != 0;

    public void SetFires(int lap, bool fires)
      => _mask = fires ? _mask | Bit(lap) : _mask & ~Bit(lap);

    // The laps of the current period as one digit each, the first lap leftmost:
    // the order the cell draws them in and the order a file writes them in.
    public string Pattern
    {
        get
        {
            var digits = new char[_period];
            for (var lap = 1; lap <= _period; lap++)
                digits[lap - 1] = Fires(lap) ? '1' : '0';
            return new string(digits);
        }

        set
        {
            _mask = 0;
            for (var lap = 1; lap <= value.Length; lap++)
                SetFires(lap, value[lap - 1] == '1');
        }
    }

    // A gate switched on nowhere never fires, which is inert rather than wrong: it
    // is what one switch off from a single lap looks like, and the panel is the
    // only place it can be seen either way.
    public override bool Evaluate(int pass, Random random)
      => Fires(((pass % _period) + _period) % _period + 1);

    public override string Token => "GCYC" + _period + ":" + Pattern;

    // The whole mask and not the pattern, so that the laps outside the period come
    // across as well: a period pulled in on the original is pulled in on the copy
    // and finds the same switches when it is let back out.
    public override Tile Copy()
      => new CycleGateTile { _period = _period, _mask = _mask };

    int _period = 4;
    uint _mask = 1;

    static int Clamp(int value) => Math.Clamp(value, MinPeriod, MaxPeriod);

    static uint Bit(int lap)
      => lap < 1 || lap > MaxPeriod ? 0u : 1u << (lap - 1);
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

    public override Tile Copy() => new ProbGateTile { Percent = _percent };

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
