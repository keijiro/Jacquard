namespace Jacquard {

// One partial: a two operator FM pair (modulator into carrier) with modulator
// self-feedback, which is the whole oscillator and none of the decisions about it.
//
// Everything that says how the tone is made is handed in per sample, because the two
// partials of a unison voice differ in nothing but the rate they are stepped at — so
// what a partial is, in the end, is the phases a pair of sines has to remember
// between one sample and the next.
//
// What is deliberately not shared between two of these is the feedback memory. The
// two modulators run at different frequencies, and one loop fed from both would
// couple them into something that is neither.

struct FmPartial
{
    const float TwoPi = FastMath.TwoPi;

    public void Reset()
    {
        (_carrierPhase, _modulatorPhase) = (0.0f, 0.0f);
        (_feedback1, _feedback2) = (0.0f, 0.0f);
    }

    public float Next(float increment, float ratio, float feedback,
                      float index, float amplitude)
    {
        // Feeding back the average of the last two modulator outputs keeps the
        // loop from breaking into noise.
        var mod = FastMath.Sin(TwoPi * _modulatorPhase +
                            feedback * (_feedback1 + _feedback2) * 0.5f);
        (_feedback2, _feedback1) = (_feedback1, mod);

        var output = FastMath.Sin(TwoPi * _carrierPhase + mod * index) * amplitude;

        _carrierPhase = FastMath.Frac(_carrierPhase + increment);
        _modulatorPhase = FastMath.Frac(_modulatorPhase + increment * ratio);

        return output;
    }

    float _carrierPhase;    // Normalized phase [0,1)
    float _modulatorPhase;
    float _feedback1;       // The last two modulator outputs
    float _feedback2;
}

// Runtime state of one voice: one partial, or a detuned pair of them when the note
// asks for unison.
//
// The note is the voice's only source of timbre information; everything else here
// exists because a sine wave has to remember its phase. Buffer handling is not
// this type's business — the caller walks the frames and decides which ones fall
// inside the note — so nothing here depends on a container type and the whole
// oscillator lives on the engine-free side.
//
// A pair is one voice and not two, which is the decision the rest of this file
// follows from. Two voices would be two slots out of twenty-four, so unison would
// halve the polyphony; they would be two events, so all three places that make one
// would have to make them in step; and stealing, which knows nothing about pairs,
// would take one half and leave the other sounding on its own — a note that goes
// half out of tune under pressure. What it costs instead is that a voice now has to
// hand back two numbers rather than one.

public struct FmVoiceState
{
    public FmNoteEvent Note => _note;
    public bool Active => _active != 0;

    // All oscillator state is reset, so a given event always produces exactly the
    // same waveform. Both partials start at the same phase rather than one of them
    // being offset: an offset would hollow out the onset, where the pair has not yet
    // drifted apart and is still adding to itself, and a percussive patch is mostly
    // onset.
    public void Trigger(in FmNoteEvent note, float sampleRate)
    {
        _note = note;
        _active = 1;
        _lower.Reset();
        _upper.Reset();

        // The carrier is always 1:1, so this is its own increment and the
        // modulator's is this times its ratio. A note asking for unison is two of
        // them, the same interval either side of what was written; a note that is
        // not divides and multiplies by exactly one, and comes out exactly where it
        // was before there was a second partial to be beside.
        var increment = note.frequency / sampleRate;
        var detune = note.DetuneRatio;

        _increment = increment / detune;
        _incrementUpper = increment * detune;

        _paired = (byte)(note.unison > 0.0f ? 1 : 0);

        // Held here rather than read per sample: what a pair is shared at is settled
        // at note-on like every other gain on this event. It rides on the level so
        // that the two send buses inherit it without a multiply of their own.
        _level = note.level * note.UnisonGain;
    }

    public void Release() => _active = 0;

    // Sample position at which this voice goes silent and frees its slot.
    public long EndSample(float sampleRate)
      => _note.startSample + (long)(_note.TotalDuration * sampleRate);

    // Renders one sample, as the one partial the note asks for or as the detuned pair
    // it asks for instead. time is the elapsed note time in seconds, which the caller
    // derives from the absolute sample position so that a note can start in the middle
    // of a buffer.
    //
    // The two come back separately rather than summed, because they are rendered at
    // two different places across the image and the caller is what knows about sides:
    // a pair added up here would be a pair that could not be spread. What wants them
    // summed — the two send buses, and anything drawing the pool — can add them.
    //
    // Everything but the phase is shared. Both partials follow the one pitch envelope,
    // so the interval between them holds through a sweep instead of closing up as the
    // note lands; both take the same ratio, index and envelopes, because this is one
    // note sounded twice rather than two sounds.
    public void Next(float time, out float lower, out float upper)
    {
        // The pitch envelope moves the frequency while the note sounds, so the
        // increment is per sample rather than settled at trigger time. Both
        // operators follow it: the modulator is a ratio of the carrier, and
        // keeping the two locked is what makes a sweep sound like one voice
        // bending rather than two drifting apart.
        var scale = _note.PitchScale(time);

        var index = _note.modulationIndex * _note.ModulatorLevel(time);
        var amplitude = _level * _note.CarrierLevel(time);

        lower = _lower.Next(_increment * scale, _note.modulatorRatio,
                            _note.feedback, index, amplitude);

        // A single voice stops here, and stops before it has paid for anything: the
        // second half is skipped rather than rendered and multiplied by a zero.
        upper = _paired == 0 ? 0.0f
                : _upper.Next(_incrementUpper * scale, _note.modulatorRatio,
                              _note.feedback, index, amplitude);
    }

    // Private members

    FmNoteEvent _note;
    byte _active;
    byte _paired;            // Whether the note asked for a second partial

    FmPartial _lower;        // The half tuned below the note, and the only one a
    FmPartial _upper;        // note without unison ever runs

    float _increment;        // Carrier phase per sample for each half, before the
    float _incrementUpper;   // pitch envelope; the same number twice without unison
    float _level;            // The note's level, less what a pair is shared at
}

} // namespace Jacquard
