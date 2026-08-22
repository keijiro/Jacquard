using System;

namespace Jacquard {

// Exponential fades from 1 to 0 over x in [0,1], ported from the unity-sap-test
// prototype.
//
// Both are normalized so that they reach exactly 0 at x = 1, which is what lets a
// voice end in silence instead of being cut off, and a pitch envelope land on the
// note's own frequency instead of near it. A level is a pure function of the
// elapsed note time and the gate length, so a voice keeps no envelope state and
// needs no stage bookkeeping.
//
// Snap is the same shape stood up steeper: a fifth of the way in it is already down
// to a fifth of its depth. That is too abrupt for a level, which is why the amplitude
// envelopes do not use it, and about what a pitch envelope wants.
//
// How much steeper is a decision about what pitchDecay's dial means rather than about
// the curve's shape, and it cannot be anything else: an exponential scaled in time is
// still that exponential, so the sound depends only on pitchDecay / SnapCurve, and
// doubling the constant is indistinguishable from halving the dial. 16 came across from
// the prototype and made the dial read about four times long — the envelope was over
// inside the first quarter of whatever time it was given, so the top of a range widened
// to two seconds was a sweep that could not be heard sweeping. At 8 the pitch is still
// moving audibly three quarters of the way along and a decay entered as long is long.
// Below that the shape does start to change, and for the worse: past the decay
// PitchScale returns a flat 1, so whatever slope is left at x = 1 is a corner, and by 5
// — Fade's constant — a deep sweep lands with an audible kink. A level can afford that
// corner where a pitch cannot, which is most of why these are two constants and not one.

static class FmCurve
{
    const float Curve = 5.0f;
    const float Tail = 0.006737947f; // exp(-Curve)

    // exp(-SnapCurve), and it has to stay that: it is what buys the exact landing
    // above, and it is wrong the moment the curve is changed on its own.
    const float SnapCurve = 8.0f;
    const float SnapTail = 3.3546263e-4f;

    public static float Fade(float x)
      => (FastMath.Exp(-Curve * x) - Tail) / (1.0f - Tail);

    public static float Snap(float x)
      => (FastMath.Exp(-SnapCurve * x) - SnapTail) / (1.0f - SnapTail);
}

// The timbre, held by the project.
//
// The synth stores no patch of its own: this is stamped into every note event as
// it is scheduled, which is also why a parameter lock can alter one note without
// disturbing anything else. gateScale is not an oscillator setting but lives here
// so that every lock target is a plain field of one struct — and every field is a
// lock target, which is what makes ParamTargets a list of these fifteen and
// nothing else.
//
// One of them the synth never sees. transpose moves the note the sequencer is about
// to make, so it is spent before an event exists and is the one field with nothing
// mirroring it in FmNoteEvent — an event already knows what it sounds, and a number
// saying how far it was moved to get there would be a second answer nobody reads.
// It is in the patch because it answers to a channel, in the way the sends do, and
// because a step that moves one channel's notes and nothing else is exactly what a
// lock is for. Where it is spent, and what happens to the note next, is
// Project.SoundingPitch.
//
// Four of them are not oscillator settings at all, in the sense that they describe
// nothing about how the tone is made: where the note sits across the stereo image,
// how wide it sits there, and how much of it goes to each of the two send effects,
// whose own settings belong to the project rather than to a timbre (SendFx). They
// are in the patch because each is worth locking — a reverb on one note of a chord,
// or that note thrown to one side, is exactly the accent a lock exists for — and
// because a position and a send decided at note-on are a position and a send that
// never have to be smoothed.
//
// Pan moves the dry signal only. What the note sends to the reverb and the delay
// goes in unpanned, since each of those makes a stereo image of its own out of a
// mono feed, and a tail that argued with the note's own position would be two
// answers to one question.
//
// Unison is the width: above zero the note is sounded twice rather than once, the
// two copies tuned a little apart and stood on either side of where the pan puts
// them, so that they beat against each other. It is the one thing here that changes
// how many oscillators a note costs, and it is still a field of the patch rather
// than a property of the synth because a step that widens one note of a line and
// leaves the rest single is the same kind of accent a send is.
//
// The two operators get deliberately different envelope shapes, matching what
// each one actually does. The carrier gates the output, so it is an AR: rise,
// hold for the note, fall. The modulator only colours the tone, so it is a single
// decay from full depth, which is what gives a two operator patch its bite. The
// carrier always runs at the note frequency; only the modulator has a ratio.
//
// The modulator's decay is also the one envelope setting here that is not a time.
// It is a slope, for the reason given at ModulatorLevel: what a player is choosing
// is how hard the tone breaks, and the two ends of that choice — no modulation and
// modulation that never leaves — are settings a time cannot name.
//
// A third envelope moves the pitch itself, which is what turns this patch into a
// kick drum: a steep drop onto the note frequency is most of what a kick is. It
// is kept to two numbers, how far the pitch moves and how long it takes to get
// there, because a percussive sweep is over before any more detail than that
// could be heard.

public struct FmPatch
{
    public float transpose;  // Semitones the channel's notes are moved by

    public float level;      // Output level in dB, full scale at 0
    public float pan;        // Across the image, -1 hard left to +1 hard right
    public float unison;     // How wide the detuned pair sits [0,1]; 0 is one voice
    public float gateScale;  // Multiplies the note's gate length

    public float modulatorRatio;  // Modulator frequency as a ratio of frequency
    public float modulationIndex; // Peak modulation depth in radians
    public float feedback;        // Modulator self-feedback depth in radians
    public float modulatorDecay;  // How steeply the modulation falls away [0,1]:
                                  // 0 is gone at once, 1 never decays at all

    public float carrierAttack;   // Time to reach full level (seconds)
    public float carrierRelease;  // Time to fall to silence after the gate

    public float pitchSweep;      // Depth of the pitch envelope in octaves,
                                  // negative to bend up into the note instead
    public float pitchDecay;      // Time for the pitch to arrive at frequency

    public float reverbSend;      // How much of the note reaches the reverb [0,1]
    public float delaySend;       // How much of it reaches the delay [0,1]

    // Where every parameter rests when nothing has been said about it: the patch a bank
    // is built from, and the value a row of the Sound panel goes back to when its name
    // is double clicked.
    //
    // It is the nothing end of every bar rather than a sound anyone chose. The
    // modulator is at the carrier's own frequency and nought deep, so a fresh patch is
    // a plain sine and the FM is not switched on until a hand asks for it; its decay is
    // laid flat, so the first depth dialled in is heard for the whole note rather than
    // through an envelope nobody set. The release is the attack's five milliseconds,
    // which is the shortest tail that does not click — a gate and no more.
    //
    // Which is deliberately not what a new score sounds like. A piece cannot be started
    // in a bare sine, so the tone a fresh score comes up in is dialled on top of this,
    // in Project.CreateInitial: this is where a parameter goes when it is taken back,
    // and that is where the app begins. Splitting the two is what lets taking a
    // parameter back mean *off* rather than mean some other patch's number.
    //
    // The pitch envelope starts at no depth for the same reason as the FM, and its
    // decay is two hundred milliseconds — the one number here that is not the nothing
    // end, because there is no nothing end to a duration. It is what makes the first
    // depth entered a sweep to be heard rather than the click a kick's snap would give,
    // and the drum is the shorter end of the same bar.
    //
    // The sends start silent, so a project that never opens the Send FX panel sounds
    // exactly as it did before there was one. Pan starts centred, which the gains below
    // are normalized to render exactly as an unpanned note used to, and unison starts
    // at nothing, which is one voice per note and the same debt paid the same way.
    //
    // Two decibels down is the four fifths this used to be spelled as, to within a
    // sixteenth of a decibel: the number is round where the amplitude was, and nothing
    // about a fresh patch is louder or quieter than it has been since there was one.
    public static FmPatch Default => new FmPatch
      { transpose = 0.0f,
        level = -2.0f,
        pan = 0.0f,
        unison = 0.0f,
        gateScale = 1.0f,
        modulatorRatio = 1.0f,
        modulationIndex = 0.0f,
        feedback = 0.0f,
        modulatorDecay = 1.0f,
        carrierAttack = 0.005f,
        carrierRelease = 0.005f,
        pitchSweep = 0.0f,
        pitchDecay = 0.2f,
        reverbSend = 0.0f,
        delaySend = 0.0f };

    // The two ends of the level, kept here rather than with every other target's in
    // ParamTargets because the conversion below has to agree with them: the bottom is
    // the one value that is a switch rather than a quantity, and the top is what the
    // DSP is promised it will never be handed more than.
    //
    // Silence at the bottom rather than sixty decibels down, for the reason OutputVolume
    // gives at its own floor: a level whose lowest setting still lets something through
    // is one nothing can be silenced with, and a step that takes a channel out is worth
    // being able to write. Sixty is where that lands because a thousandth of full scale
    // is already past anything a room gives back.
    //
    // Six over the top, which is twice the amplitude a level could reach when it stopped
    // at one, and it is there for the accent upwards: a channel is mixed at whatever it
    // is worth against the rest of the piece, and a lock that lifts one step of it has
    // to have somewhere to go from there. Nothing clips for it — the mix is staged so
    // that full scale is four notes, see FmSynth.MasterGain — a note up here simply
    // spends two of the four.
    public const float MinLevel = -60.0f;
    public const float MaxLevel = 6.0f;

    // The level as the gain a voice multiplies by, which is the one place the decibels
    // stop being a reading and become arithmetic.
    //
    // Decibels are what the field holds because that is what a *shift* has to be added
    // in: six down is six down wherever it is applied, where a fifth off is nearly
    // nothing at the top of the range and silence near the bottom of it. So the whole of
    // the level is dialled, locked and saved in dB, and this runs once per note event on
    // the way out.
    //
    // Spelled out rather than calling Limiter.Gain, which is the same power of ten, for
    // the reason OutputVolume gives for its own copy: what this has that a threshold has
    // not is the two ends, and folding them in here is what leaves every caller with
    // nothing to clamp.
    public static float Amplitude(float decibels)
      => decibels <= MinLevel ? 0.0f
         : MathF.Pow(10.0f, Math.Min(decibels, MaxLevel) / 20.0f);
}

// A note-on event: the complete patch alongside pitch, timing and the exact
// sample to start on. Nothing about how it sounds is stored anywhere else.
//
// Note that level is an output level, not a velocity: nothing in here describes
// how a note was played, only what comes out. A tracker's velocity column would
// be one of the things that map onto it.

public struct FmNoteEvent
{
    public long startSample;
    public float frequency;
    public float level;    // Peak output amplitude, past one when the patch it
                           // came from was over full scale
    public float pan;      // Across the image, -1 hard left to +1 hard right
    public float unison;   // How wide the detuned pair sits [0,1]; 0 is one voice
    public float duration; // Gate length in seconds; release follows it
    public int priority;   // Higher priority wins when voices are stolen

    public float modulatorRatio;
    public float modulationIndex;
    public float feedback;
    public float modulatorDecay;

    public float carrierAttack;
    public float carrierRelease;

    public float pitchSweep;
    public float pitchDecay;

    // Constant for the life of the voice, which is the whole reason a send needs no
    // smoothing: the gain a note is rendered at never moves under it.
    public float reverbSend;
    public float delaySend;

    // Total time the note occupies a voice, gate plus carrier release.
    public float TotalDuration => duration + carrierRelease;

    // The two gains the dry signal is rendered at. Equal power: the pair is a point
    // on a circle rather than on a line, so a note keeps its weight as it crosses
    // instead of sagging in the middle the way a pair of straight fades does.
    //
    // The circle is scaled so that the centre is unity rather than the ends, which
    // makes a patch that never touches pan render exactly as it did before there was
    // one — the same thing the silent sends were worth. What it costs is 3dB of
    // headroom at the extremes, where a note is on one side only; the soft clip at
    // the end of the mix is already what a dense chord relies on.
    public void PanGains(out float left, out float right)
      => Gains(pan, out left, out right);

    // The same law asked about somewhere other than the note's own position, which is
    // what a unison pair needs: the two halves are read through this at the two places
    // the spread puts them, so nothing about how a position becomes a pair of gains is
    // said twice.
    public static void Gains(float position, out float left, out float right)
    {
        position = position < -1.0f ? -1.0f : position > 1.0f ? 1.0f : position;

        // A quarter turn of travel: hard left at zero and hard right at a right
        // angle, with the centre halfway between at 45 degrees.
        var angle = (position + 1.0f) * (FastMath.HalfPi * 0.5f);

        left = FastMath.Cos(angle) * Root2;
        right = FastMath.Sin(angle) * Root2;
    }

    const float Root2 = 1.41421356f;
    const float Root2Inverse = 0.70710678f;

    // How far each half of a unison pair is moved from the note, as a frequency ratio:
    // one half is multiplied by it and the other divided, so what was written stays in
    // the middle and a chord does not drift as the setting is turned up.
    //
    // An interval and not a number of hertz, for the reason the pitch envelope is in
    // octaves: a fixed offset in hertz is most of a semitone at the bottom of a bass
    // line and nothing at all at the top of a lead, so one setting would mean a
    // different amount of detune on every part it was used on.
    //
    // Sixty cents from end to end at the top of the travel — 15Hz of beating at A4
    // and a little over 2Hz at the bottom of a bass line, which is well past a chorus
    // and into a pair that is audibly arguing about the note. Proportional below it,
    // so the point the spread finishes at is 18 cents and 4.6Hz, which is where the
    // thickening lives; everything above that is the parameter reaching for the edge
    // it is named after.
    //
    // It was thirty when this arrived, chosen as the point a pair stops reading as one
    // note, and thirty turned out to be where that only just begins: the top of the bar
    // sounded like a wide chorus rather than like the edge, and the interesting half of
    // the travel was all crowded into the last inch. Doubling it spends the top of the
    // range on something that is actually a different sound and leaves the useful
    // chorus at 0.2 to 0.3, where the spread is finishing anyway.
    public const float MaxDetuneCents = 60.0f;

    public float DetuneRatio
      => unison <= 0.0f ? 1.0f
         : FastMath.Pow2(unison * MaxDetuneCents / 2400.0f);

    // How far apart the setting asks for the two halves to be thrown, which opens over
    // the first three tenths of the travel and is wide open above them.
    //
    // The spread and the detune finish at different places on purpose. Tying them
    // together would mean no setting where a wide pair is only just detuned, which is
    // most of what this is for; and the image is somewhere a pair can be put, with
    // nowhere further to put it once it is at the sides, while the interval goes on
    // being worth more all the way up.
    public const float SpreadFull = 0.3f;

    public float Spread
      => unison <= 0.0f ? 0.0f
         : unison >= SpreadFull ? 1.0f : unison / SpreadFull;

    // How far each half actually travels, which is the spread cut down by the room the
    // pan has left it. A pair opened at the centre reaches the sides; the same pair on
    // a note already thrown to the right closes up as it goes, and lands on the wall as
    // one.
    //
    // This is what keeps the two parameters out of each other's way. Reaching by the
    // whole spread and clamping was the obvious arrangement and the wrong one: the
    // outer half would stop at the wall while the inner one went on travelling, so a
    // pan at full unison moved the pair half as far as it says and never got it hard
    // over at all — a fully panned note came out 4.8dB to one side where an unpanned
    // one is silent on the other. Which makes pan a control that quietly means less
    // the more unison is used, and two settings that fight over the same wall.
    //
    // Proportional instead, so **pan always reaches the end of its travel and unison
    // spreads by however much is left there.** What it costs is width at the extremes,
    // where a hard panned pair is two copies on one spot — but they are still detuned,
    // so what a note loses out there is the image and not the thickness. And nothing
    // is clamped any more: the clamp inside Gains is a guard rather than the mechanism.
    //
    // Spelled out rather than reaching for MathF.Abs and MathF.Min, since this is read
    // inside the render job and one extern Burst cannot resolve takes the compiled
    // library down for the whole assembly.
    public float Reach
    {
        get
        {
            var distance = pan < 0.0f ? -pan : pan;
            return distance >= 1.0f ? 0.0f : Spread * (1.0f - distance);
        }
    }

    // What each half is rendered at, so that turning unison up is a change of width
    // and not a change of level.
    //
    // Both ends of this are exact rather than a compromise, and they are different
    // numbers because the two halves reach a channel differently at each end. With the
    // pair on top of each other every channel hears both of them in step, and two
    // coherent halves at a half each are the single voice this was — which is what
    // makes the very bottom of the travel continuous with a note that has no unison at
    // all, rather than stepping 3dB the instant the bar leaves zero. Wide open, each
    // channel hears one half only, and a hard panned half is already up by root two,
    // so root two down is unity per side.
    //
    // The crossing between them runs over the spread's travel, and the spread is read
    // here rather than the reach for a reason worth stating, because the obvious guess
    // is the other one. **What decides whether two halves add as one signal or as two
    // is the detune and not where they sit.** A pair sixty cents apart has stopped
    // agreeing with itself long before anything panned it, and squeezing it onto one
    // spot against a wall does not put it back in step — so a gain that fell to a half
    // out there would take a hard panned note down 3dB for no reason an ear could name.
    //
    // The pan cannot pull the level about in any case, which is what makes reading the
    // spread alone sufficient: under the equal power law the four gains of a pair square
    // and sum to four wherever the two are put, so two decorrelated halves carry the
    // same power at every position. Position moves the sound and the spread decides
    // what it weighs, and neither reaches into the other.
    //
    // What it costs is about a decibel around the middle, in whichever direction the
    // note's own pitch and length have left the pair coherent — which is not a number
    // the law can know, and is why the ends are what it is pinned to.
    public float UnisonGain
      => unison <= 0.0f ? 1.0f : 0.5f + (Root2Inverse - 0.5f) * Spread;

    // The dry gains a note renders its two halves at: the note's own position moved
    // out to either side by whatever the reach turned out to be, each read through the
    // pan law. A note with no unison gets back exactly the pair of gains it always
    // had, and a second pair of zeroes for a half that is never rendered.
    public void UnisonGains(out float leftA, out float rightA,
                            out float leftB, out float rightB)
    {
        if (unison <= 0.0f)
        {
            PanGains(out leftA, out rightA);
            (leftB, rightB) = (0.0f, 0.0f);
            return;
        }

        var reach = Reach;

        Gains(pan - reach, out leftA, out rightA);
        Gains(pan + reach, out leftB, out rightB);
    }

    // Carrier level: rise over the attack, hold for the rest of the gate, then
    // release from whatever level was actually reached, so a note shorter than
    // its own attack still fades out without a discontinuity.
    public float CarrierLevel(float time)
    {
        if (time < duration) return AttackLevel(time);

        var t = time - duration;
        if (t >= carrierRelease) return 0.0f;

        return AttackLevel(duration) * FmCurve.Fade(t / carrierRelease);
    }

    float AttackLevel(float time)
      => time < carrierAttack ? time / carrierAttack : 1.0f;

    // Modulation depth: full at the note start, falling away at whatever slope the
    // patch asks for. It ignores the gate, so the tail of a long note settles into a
    // plain sine, which is the classic two operator behaviour.
    //
    // modulatorDecay is the slope rather than a length, which is what makes the whole
    // range of it playable. A time has to be entered against the note it is under —
    // 30ms is a bite on a stab and nothing at all on a pad — and its two ends are both
    // unreachable: an FM patch with no modulation and one whose modulation never
    // leaves are the settings either side of the useful range, and neither is a
    // number of milliseconds. As a slope both are just the ends of the travel: 0
    // stands the decay up vertically and the modulation is gone before the first
    // sample, so the note is a plain sine; 1 lays it flat and the full depth holds
    // for the life of the note. Everything between is an exponential with a time
    // constant of DecayUnit * v / (1 - v), which is a hundredth of a second a tenth
    // of the way along, a tenth of a second halfway, and most of a second at nine
    // tenths — a click, a bite and an audible sweep, spread across the bar in that
    // order.
    //
    // Nothing normalizes this the way FmCurve does. A level has to land exactly on
    // zero so that a voice can end without a step in it, and a depth has no such
    // debt: it is inaudible long before it is zero, and it is never cut off, so there
    // is nothing for the tail subtraction to hide.
    public float ModulatorLevel(float time)
    {
        if (modulatorDecay >= 1.0f) return 1.0f;
        if (modulatorDecay <= 0.0f) return 0.0f;

        return FastMath.Exp(-time * (1.0f - modulatorDecay) /
                            (modulatorDecay * DecayUnit));
    }

    // What half the FM decay's travel is worth, and so where the useful part of that
    // travel sits. A tenth of a second in the middle is what puts a drum's bite in
    // the first third of the bar and leaves the last third for a modulation meant to
    // be heard moving.
    const float DecayUnit = 0.1f;

    // Pitch envelope, as a multiplier on the note frequency. Measured in octaves
    // rather than in Hz, so one setting bends every note by the same interval:
    // transposing a kick down does not flatten its sweep with it.
    //
    // Past the decay it is exactly 1, which also covers a decay of zero: the
    // pitch envelope is then simply off.
    public float PitchScale(float time)
      => time >= pitchDecay ? 1.0f
         : FastMath.Pow2(pitchSweep * FmCurve.Snap(time / pitchDecay));

    // Builds an event from a resolved patch. The patch has already had every
    // parameter lock applied to it by the time this is called.
    public static FmNoteEvent FromPatch(in FmPatch patch, int note,
                                        float gateSeconds, long startSample)
    {
        // Converted once. Both of the things that read a level read the same number:
        // what comes out of the voice, and how hard the pool fights to keep it.
        var level = FmPatch.Amplitude(patch.level);

        return new FmNoteEvent
        { startSample = startSample,
          frequency = Pitch.ToFrequency(note),
          level = level,
          pan = Math.Clamp(patch.pan, -1.0f, 1.0f),
          unison = Math.Clamp(patch.unison, 0.0f, 1.0f),
          duration = MathF.Max(gateSeconds * patch.gateScale, 0.005f),
          // Louder notes outrank quieter ones when the pool runs out of voices,
          // so an accent survives a dense chord. Eight was the whole of the scale
          // while a level stopped at an amplitude of one; a note over full scale
          // ranks above eight and costs nothing for it, since every comparison on
          // this number is against another one of these and never against a figure.
          priority = (int)MathF.Round(level * 8.0f),
          modulatorRatio = patch.modulatorRatio,
          modulationIndex = patch.modulationIndex,
          feedback = patch.feedback,
          modulatorDecay = patch.modulatorDecay,
          carrierAttack = patch.carrierAttack,
          carrierRelease = patch.carrierRelease,
          pitchSweep = patch.pitchSweep,
          pitchDecay = patch.pitchDecay,
          reverbSend = patch.reverbSend,
          delaySend = patch.delaySend };
    }
}

} // namespace Jacquard
