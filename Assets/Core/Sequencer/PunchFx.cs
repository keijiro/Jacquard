using System;
using System.Collections.Generic;

namespace Jacquard {

// The twelve punch-in effects, in the order the panel stands them in: each pair is a
// column, and the columns read as sends, gate, octave, ramp, and then two columns of
// roll.
//
// The number on a roll is how many sixteenths long it is, which is the only thing
// that separates the four of them — one class with one number different — so it is
// the name as well.

public enum PunchEffect
{
    Reverb, Delay,
    Stab, Sustain,
    OctaveDown, OctaveUp,
    Fall, Rise,
    Roll1, Roll2,
    Roll3, Roll4
}

// What a hand does to the score on its way to the synth.
//
// Everything else that colours a note is written on the plane: a lock is a tile, a
// gate is a tile, and what a channel sounds like is a patch the score carries. None
// of that can be played, because none of it can be held for two beats and let go.
// This is the layer that can. It sits between the sequencer and the synth, takes the
// events the runners produced and hands over something else for as long as a button
// is down.
//
// It reaches only what has not been handed over yet, which is also the whole of the
// promise it makes: a voice reads its event once and never again, so a note already
// sounding is not retuned, not shortened and not thrown into the reverb by anything
// pressed after it began. What a punch changes is the next note.
//
// Nothing here is saved. A press is a gesture rather than a setting, so there is no
// file key, no version bump and no lock target — which is what keeps a feature this
// wide out of the format and off the panel budget entirely.
//
// The grid it counts in is the project's sixteenth, from the sample the transport
// started on, and not the step of whichever lane a note came from. A ramp that rose
// faster under a lane running in eighths would be two answers to how far up the
// ramp is, and a roll is a length of time rather than a lane's idea of one.

public sealed class PunchFx
{
    // Held effects

    public bool IsHeld(PunchEffect fx) => _held[(int)fx];

    // The sample is only the moment the hand arrived: what it means depends on the
    // tempo and the grid, neither of which this is told until the next handover, so
    // it is kept and resolved there.
    public void Press(PunchEffect fx, long sample)
    {
        _held[(int)fx] = true;
        _pressed[(int)fx] = sample;

        // Which press came last, which is the only thing that decides between two
        // rolls. Counted rather than read off the sample, so that two arriving in the
        // same frame — or restamped together by Start — still have an order.
        _sequence[(int)fx] = ++_presses;
    }

    public void Release(PunchEffect fx)
    {
        _held[(int)fx] = false;
        _rolls[(int)fx] = null;
    }

    // Transport

    // The sample the first step of the sequence lands on, which is what the sixteenth
    // grid is counted from. Anything already held is stamped again, so a button held
    // across a stop starts its ramp and its roll where the music starts.
    public void Start(long originSample)
    {
        Stop();

        _origin = originSample;
        _handedTo = originSample;

        for (var i = 0; i < Count; i++)
            if (_held[i]) _pressed[i] = originSample;
    }

    public void Stop()
    {
        _queue.Clear();
        _history.Clear();
        _sounding.Clear();

        for (var i = 0; i < Count; i++) _rolls[i] = null;
    }

    // Handover

    // Takes what the sequencer produced. Nothing is decided here: an event parked now
    // may be handed over under a punch that has not been pressed yet.
    public void Enqueue(IReadOnlyList<FmNoteEvent> notes)
    {
        for (var i = 0; i < notes.Count; i++) _queue.Add(notes[i]);
    }

    // Hands over everything due before the horizon, coloured by whatever is held at
    // this moment rather than by whatever was held when the sequencer ran.
    //
    // With nothing held and no roll running this is a copy: an event comes out of the
    // queue and goes into the output untouched, which is what makes the whole feature
    // inert while the panel is down.
    public void HandOver(long horizon, float tempo, int sampleRate,
                         List<FmNoteEvent> output)
    {
        var sixteenth = Sixteenth(tempo, sampleRate);
        if (sixteenth <= 0.0) return;

        Arm(sixteenth);

        _sounding.Clear();

        var roll = Owner();

        // Straight from the score, unless a roll has taken the score's place. A roll
        // that is still recording has not: it plays the sequence through once and
        // stands in for it only from the far end of what it recorded.
        var kept = 0;

        for (var i = 0; i < _queue.Count; i++)
        {
            var note = _queue[i];

            if (note.startSample >= horizon)
            {
                _queue[kept++] = note;
                continue;
            }

            if (roll == null || note.startSample < roll.End) _sounding.Add(note);
        }

        _queue.RemoveRange(kept, _queue.Count - kept);

        if (roll != null) Repeat(roll, horizon);

        // Recording, remembering and colouring all read the event as the score wrote
        // it, so a roll caught under an octave holds the plain note and rises with the
        // hand rather than being stamped with where the hand was.
        foreach (var note in _sounding)
        {
            Record(note);
            _history.Add(note);
            output.Add(Colour(note, sixteenth, sampleRate));
        }

        Forget(horizon - (long)(sixteenth * HistoryLaps));

        _handedTo = horizon;
    }

    // Private members

    const int Count = 12;

    // What Stab leaves of a sixteenth, and the floor FromPatch already holds every
    // gate to.
    const float StabGate = 0.1f;
    const float MinimumGate = 0.005f;

    // What Stab leaves of the release. A gate cut to a tenth of a step is not a short
    // note if the tail behind it is a quarter of a second: the note is let go early
    // and then takes exactly as long as it ever did to go quiet, so what a stab would
    // sound like is a stab and a wash. Ten milliseconds is short enough to be an edge
    // and long enough not to click.
    const float StabRelease = 0.01f;

    // Two bars of sixteenths, which is where a ramp turns over.
    const int RampLaps = 32;

    // How far back the record of what has sounded reaches. A roll window is four
    // sixteenths at the longest and is claimed from within itself, so twice that is
    // already slack.
    const int HistoryLaps = 8;

    readonly bool[] _held = new bool[Count];
    readonly long[] _pressed = new long[Count];
    readonly int[] _sequence = new int[Count];
    readonly Roll[] _rolls = new Roll[Count];

    readonly List<FmNoteEvent> _queue = new();
    readonly List<FmNoteEvent> _history = new();
    readonly List<FmNoteEvent> _sounding = new();

    int _presses;

    long _origin;
    long _handedTo;

    // A window of the sequence, caught once and then played in place of it.
    //
    // Start is a grid point at or before the press, so what is caught is the step the
    // hand was on rather than the one after it — half of which is already in the past
    // by the time a press is seen, which is what the record of what has sounded is
    // for. End is where the catching stops and the standing in begins: the shortest
    // roll is full the moment it is asked for, and a longer one spends the rest of its
    // own length letting the sequence through and writing it down.
    sealed class Roll
    {
        public long Start;
        public long End;
        public long Length => End - Start;

        // Where the record of what has sounded gives out, which is everything already
        // handed over. Past this the window fills from what is handed over next, and
        // the split is what keeps a note from being caught twice.
        public long Caught;

        public long EmittedTo;

        public readonly List<FmNoteEvent> Notes = new();
    }

    // How many sixteenths a roll is long, and zero for everything that is not one.
    static int RollSteps(PunchEffect fx)
      => fx switch { PunchEffect.Roll1 => 1,
                     PunchEffect.Roll2 => 2,
                     PunchEffect.Roll3 => 3,
                     PunchEffect.Roll4 => 4,
                     _ => 0 };

    static double Sixteenth(float tempo, int sampleRate)
      => 60.0 / Math.Max(tempo, 1.0f) / 4.0 * sampleRate;

    long GridIndex(long sample, double sixteenth)
      => (long)Math.Floor((sample - _origin) / sixteenth);

    long GridSample(long index, double sixteenth)
      => _origin + (long)Math.Round(index * sixteenth);

    // The roll that is standing in for the score, which is the one pressed last: they
    // all answer the same question and there is one answer. Letting that one go leaves
    // whichever is still down and was pressed most recently before it, so a finger
    // rolled across the four lands on the one it is still holding.
    Roll Owner()
    {
        Roll owner = null;
        var latest = 0;

        for (var i = 0; i < Count; i++)
        {
            if (_rolls[i] == null || _sequence[i] <= latest) continue;
            (owner, latest) = (_rolls[i], _sequence[i]);
        }

        return owner;
    }

    // Gives every roll that has been pressed but not yet caught its window. All four
    // catch, whether or not they are the one standing in for the score, so that
    // letting go of the one on top hands over to a window that is already full.
    void Arm(double sixteenth)
    {
        for (var i = 0; i < Count; i++)
        {
            var steps = RollSteps((PunchEffect)i);
            if (steps > 0) Arm((PunchEffect)i, steps, sixteenth);
        }
    }

    void Arm(PunchEffect fx, int steps, double sixteenth)
    {
        var slot = (int)fx;
        if (!_held[slot] || _rolls[slot] != null) return;

        var index = GridIndex(_pressed[slot], sixteenth);

        var roll = new Roll
          { Start = GridSample(index, sixteenth),
            End = GridSample(index + steps, sixteenth) };

        roll.Caught = Math.Min(roll.End, _handedTo);

        // A window claimed late enough that its own far end is already behind the
        // handover has nothing to say about the past, so it stands in from here.
        roll.EmittedTo = Math.Max(roll.End, _handedTo);

        // Whatever of the window is already behind us, out of what has sounded.
        foreach (var note in _history)
            if (note.startSample >= roll.Start && note.startSample < roll.Caught)
                roll.Notes.Add(note);

        _rolls[slot] = roll;
    }

    // Writes a note into whichever windows are still open on it.
    void Record(in FmNoteEvent note)
    {
        for (var i = 0; i < Count; i++) Record(_rolls[i], note);
    }

    static void Record(Roll roll, in FmNoteEvent note)
    {
        if (roll == null) return;
        if (note.startSample < roll.Caught || note.startSample >= roll.End) return;

        roll.Notes.Add(note);
    }

    // Lays the window down again and again from the far end of itself. Each pass is
    // the recorded note with a new sample to start on and nothing else changed, so
    // what a roll sounds like is decided by whatever is held when it is handed over
    // rather than by what was held when it was caught.
    void Repeat(Roll roll, long horizon)
    {
        var length = roll.Length;
        if (length <= 0 || roll.Notes.Count == 0) return;

        // Never behind the handover. A roll that was covered by one pressed over the
        // top of it stops being laid down while that lasts, so when it is handed back
        // its own mark is wherever it was left — and every pass it missed in between
        // would come out at once, in the past, as a handful of notes with their heads
        // already cut off.
        var from = Math.Max(Math.Max(roll.EmittedTo, roll.End), _handedTo);
        if (from >= horizon) return;

        var pass = Math.Max(0, (from - roll.End) / length);

        for (var start = roll.End + pass * length; start < horizon; start += length)
        {
            foreach (var note in roll.Notes)
            {
                var at = start + (note.startSample - roll.Start);
                if (at < from || at >= horizon) continue;

                var copy = note;
                copy.startSample = at;
                _sounding.Add(copy);
            }
        }

        roll.EmittedTo = horizon;
    }

    void Forget(long before)
    {
        var kept = 0;

        for (var i = 0; i < _history.Count; i++)
            if (_history[i].startSample >= before) _history[kept++] = _history[i];

        _history.RemoveRange(kept, _history.Count - kept);
    }

    // Everything held, in one order, so that two that meet compose the same way every
    // time. Stab sets the gate and Sustain doubles whatever it finds, which is why the
    // two held together come out at a fifth of a sixteenth rather than at odds.
    //
    // Both of them reach the release as well as the gate, because how long a note
    // lasts is the two of them and not the gate alone. Stab shortens the release to
    // the point where it stops being a tail — and only shortens, so a patch already
    // clipped stays where it is rather than being lengthened by a button that means
    // *shorter*. Sustain doubles it, since a note held twice as long wants a tail in
    // proportion; doubling the gate alone would leave the same fixed tail on a note of
    // a different length, which is a change of envelope rather than of length.
    FmNoteEvent Colour(FmNoteEvent note, double sixteenth, int sampleRate)
    {
        if (_held[(int)PunchEffect.Reverb]) note.reverbSend = 1.0f;
        if (_held[(int)PunchEffect.Delay]) note.delaySend = 1.0f;

        if (_held[(int)PunchEffect.Stab])
        {
            note.duration = MathF.Max((float)(sixteenth / sampleRate) * StabGate,
                                      MinimumGate);
            note.carrierRelease = MathF.Min(note.carrierRelease, StabRelease);
        }

        if (_held[(int)PunchEffect.Sustain])
        {
            note.duration *= 2.0f;
            note.carrierRelease *= 2.0f;
        }

        var semitones = 0;

        if (_held[(int)PunchEffect.OctaveUp]) semitones += 12;
        if (_held[(int)PunchEffect.OctaveDown]) semitones -= 12;

        semitones += Ramp(PunchEffect.Rise, note.startSample, sixteenth);
        semitones -= Ramp(PunchEffect.Fall, note.startSample, sixteenth);

        // Once, from the total, so that an octave up against an octave down is silence
        // about the pitch rather than two multiplications that nearly cancel.
        if (semitones != 0)
            note.frequency *= MathF.Pow(2.0f, semitones / 12.0f);

        return note;
    }

    // A semitone per sixteenth from the step the hand arrived on, turning over after
    // two bars. Counted from the press rather than from the bar line: what a ramp is
    // for is the shape of the rise, and a rise that started halfway up because the
    // hand was late is not one.
    int Ramp(PunchEffect fx, long sample, double sixteenth)
    {
        if (!_held[(int)fx]) return 0;

        var anchor = GridSample(GridIndex(_pressed[(int)fx], sixteenth), sixteenth);
        var laps = (long)Math.Floor((sample - anchor) / sixteenth);

        return (int)(((laps % RampLaps) + RampLaps) % RampLaps);
    }
}

} // namespace Jacquard
