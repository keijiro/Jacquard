namespace Jacquard {

// Runtime state of one voice: a two operator FM pair (modulator into carrier)
// with modulator self-feedback.
//
// The note is the voice's only source of timbre information; everything else here
// exists because a sine wave has to remember its phase. Buffer handling is not
// this type's business — the caller walks the frames and decides which ones fall
// inside the note — so nothing here depends on a container type and the whole
// oscillator lives on the engine-free side.

public struct FmVoiceState
{
    const float TwoPi = FastMath.TwoPi;

    public FmNoteEvent Note => _note;
    public bool Active => _active != 0;

    // All oscillator state is reset, so a given event always produces exactly the
    // same waveform.
    public void Trigger(in FmNoteEvent note, float sampleRate)
    {
        _note = note;
        _active = 1;
        (_carrierPhase, _modulatorPhase) = (0.0f, 0.0f);
        (_feedback1, _feedback2) = (0.0f, 0.0f);
        _carrierIncrement = note.frequency * note.carrierRatio / sampleRate;
        _modulatorIncrement = note.frequency * note.modulatorRatio / sampleRate;
    }

    public void Release() => _active = 0;

    // Sample position at which this voice goes silent and frees its slot.
    public long EndSample(float sampleRate)
      => _note.startSample + (long)(_note.TotalDuration * sampleRate);

    // Renders one sample. time is the elapsed note time in seconds, which the
    // caller derives from the absolute sample position so that a note can start
    // in the middle of a buffer.
    public float Next(float time)
    {
        // Feeding back the average of the last two modulator outputs keeps the
        // loop from breaking into noise.
        var mod = FastMath.Sin(TwoPi * _modulatorPhase +
                            _note.feedback * (_feedback1 + _feedback2) * 0.5f);
        (_feedback2, _feedback1) = (_feedback1, mod);

        var index = _note.modulationIndex * _note.modulator.Level(time, _note.duration);
        var level = _note.velocity * _note.carrier.Level(time, _note.duration);

        var output = FastMath.Sin(TwoPi * _carrierPhase + mod * index) * level;

        _carrierPhase = Frac(_carrierPhase + _carrierIncrement);
        _modulatorPhase = Frac(_modulatorPhase + _modulatorIncrement);

        return output;
    }

    // Private members

    FmNoteEvent _note;
    byte _active;

    float _carrierPhase;    // Normalized phase [0,1)
    float _modulatorPhase;
    float _carrierIncrement;
    float _modulatorIncrement;
    float _feedback1;       // The last two modulator outputs
    float _feedback2;

    static float Frac(float x) => FastMath.Frac(x);
}

} // namespace Jacquard
