namespace Jacquard {

// Which of the twelve pitch classes are allowed to sound.
//
// A switch per semitone rather than a key and a mode. A mode is a name for a set of
// switches, and the set is the thing that acts: naming it would put a table of modes
// between the hand and the twelve boxes it is really setting, and any scale outside
// the table — a blues sixth, a raga, one note borrowed — could not be asked for at
// all. The keyboard on the panel is what says which switch is which, so nothing is
// lost by not having the word.
//
// This is not an edit. What is written on the plane is untouched: a note tile keeps
// the pitch it was given and shows it, and this decides what that pitch sounds as.
// So a scale can be tried against a piece and taken off again, which is the whole
// reason it is a setting rather than a pass over the notes — and it is why what it
// does is snap rather than drop. A note that does not sound is a hole in the music,
// and there is no arrangement of a stack that fills one.
//
// Everything on, which is every semitone allowed, is the same thing as no scale at
// all and is the default. Nothing on has no answer of its own: with nowhere for a
// note to go, every note stays where it is. That is inert rather than wrong, the same
// way a cycle gate switched on nowhere is.

public sealed class Scale
{
    public const int Degrees = 12;

    // Asked with a note number rather than a degree, since a scale is about the
    // twelve and a pitch is what the caller is holding. A degree is a note number
    // that happens to be one of the first twelve, so the panel asks the same way.
    public bool Allows(int note) => _allowed[Degree(note)];

    public void SetAllowed(int note, bool allowed)
      => _allowed[Degree(note)] = allowed;

    // The nearest note this scale will take, which is the note itself when it takes
    // it already.
    //
    // Down before up at each distance, so a note exactly between two of them takes
    // the lower — a rule rather than a preference, since something has to answer and
    // an arbitrary side would sound different in two places for no reason anybody
    // could hear. Six steps reaches every degree from any other, so the walk either
    // finds a home or there is none to find.
    public int Snap(int note)
    {
        if (Allows(note)) return note;

        for (var distance = 1; distance <= Degrees / 2; distance++)
        {
            if (Allows(note - distance)) return note - distance;
            if (Allows(note + distance)) return note + distance;
        }

        return note;
    }

    // Private members

    // Floor modulo, so that a note below C0 still names a degree. The same arithmetic
    // Pitch uses, and for the same reason.
    static int Degree(int note) => (note % Degrees + Degrees) % Degrees;

    // Chromatic, which is the scale that does nothing: a project that has never been
    // told about this sounds exactly as it did before there was one.
    readonly bool[] _allowed =
      { true, true, true, true, true, true, true, true, true, true, true, true };
}

} // namespace Jacquard
