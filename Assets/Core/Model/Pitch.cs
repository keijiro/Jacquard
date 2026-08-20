using System;

namespace Jacquard {

// Note names and frequencies.
//
// Pitches are MIDI note numbers throughout, with 60 spelled C4. Only sharps are
// used: sequencer-spec.md drops flats so that one pitch has exactly one spelling and
// a cell therefore has exactly one look.

public static class Pitch
{
    public const int Lowest = 12;   // C0
    public const int Highest = 120; // C9

    static readonly string[] Names =
      { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string ToName(int note)
      => Names[Mod(note, 12)] + (note / 12 - 1);

    // Name without the octave, for the cell label where the two are typeset
    // separately.
    public static string ToClassName(int note) => Names[Mod(note, 12)];

    // The two halves a pitch is set in, since a bar covering the whole of one is a
    // bar where a semitone is two pixels of travel. Which half a note belongs to is
    // the only thing either of them knows: the class is the letter and the octave is
    // the number the cell already typesets separately.
    //
    // ToOctave divides rather than floors, so it is only right from note 0 up. That is
    // every note this program can hold — Lowest is C0 — and the panel clamps before it
    // ever asks.
    public static int ToOctave(int note) => note / 12 - 1;

    public static int ToClass(int note) => Mod(note, 12);

    // And back. 60 is C4, so an octave starts one below where its number would put it.
    public static int FromParts(int octave, int pitchClass)
      => (octave + 1) * 12 + pitchClass;

    public static bool IsSharp(int note) => Names[Mod(note, 12)].Length > 1;

    // Equal temperament, A4 = 440Hz. The note is a float rather than an int so that
    // anything bending a pitch can ask for a frequency between two semitones.
    public static float ToFrequency(float note)
      => 440.0f * MathF.Pow(2.0f, (note - 69.0f) / 12.0f);

    // Parses a name such as "C4", "F#4" or "G#-1". Flats are rejected.
    public static bool TryParse(string text, out int note)
    {
        note = 0;
        if (string.IsNullOrEmpty(text)) return false;

        var index = Array.IndexOf(Names, char.ToUpperInvariant(text[0]).ToString());
        if (index < 0) return false;

        var i = 1;
        if (i < text.Length && (text[i] == '#' || text[i] == 's'))
        {
            index++;
            i++;
        }

        if (i >= text.Length) return false;

        if (!int.TryParse(text.Substring(i),
                          System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture,
                          out var octave)) return false;

        note = (octave + 1) * 12 + index;
        return note >= 0 && note < 128;
    }

    // Floor modulo, so that negative note numbers still name correctly.
    static int Mod(int a, int b) => (a % b + b) % b;
}

} // namespace Jacquard
