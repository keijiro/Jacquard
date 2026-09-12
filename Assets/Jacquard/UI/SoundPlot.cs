using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// One note of the patch being dialled, drawn.
//
// It is the real DSP and not a drawing of one. FmNoteEvent.FromPatch builds the event
// the sequencer would have built and FmVoiceState.Trigger/Next renders it exactly as a
// voice will, which is the idiom SelfTest already measures the synth through — so what
// is on screen rasps where the sound rasps rather than where a curve fitted to the
// parameters says it should. Nothing may be decimated on the way in either: FmPartial
// feeds back the average of its last two modulator outputs, so the samples have to be
// stepped in order at a fixed rate or the loop is a different loop.
//
// The two clocks
//
// The picture needs the oscillator run at the sample rate while the envelopes are read
// off an axis of its own, and FmVoiceState already separates the two in its signature:
// Trigger settles the phase increment from the sample rate, and Next takes the elapsed
// note time as an argument, read by PitchScale, ModulatorLevel and CarrierLevel and by
// nothing else. So the loop below hands one clock to each and is otherwise the plain
// loop. That separation is a property of FmVoiceState to keep rather than an accident —
// deriving time from a counter inside the voice would take this with it — and it is
// written down in Docs/impl-synth.md for that reason.
//
// The axis
//
// The width is the whole note, and it spends its width where the bars spend their
// travel. A single linear axis was tried first and rejected on a trial render: the
// release reaches four seconds against a gate of a sixth of one, so a patch with any
// tail at all spent two thirds of the picture on a flat line, and a five millisecond
// release vanished. The bars already solved that problem — ValueBar.Seconds is
// geometric for exactly this reason — so the picture borrows their answer, and the two
// readings then agree: a hand that carries the Release bar halfway along sees the tail
// take about half the width a tail can take.
//
// Three segments, each given a share of the width read off its own bar, with time
// running linearly in real seconds inside each one. What that costs is one sentence:
// the FM decay runs at a different rate per pixel in the release than in the gate. By
// then the modulation is gone, which is why the seam is not visible and why this is a
// note rather than a defect.
//
// The pitch
//
// Pinned to A4 at 48kHz, which is 109.09 samples to the carrier's cycle and
// deliberately not a round number of cycles across the plot. The reason is the feedback
// loop: what FmPartial feeds back is the average of its last *two samples*, which is a
// lowpass whose corner is a fraction of the sample rate and of nothing musical. At any
// other samples-per-cycle the Feedback bar would be drawing a filter nobody can hear,
// and the aliasing at ratio 8 and amount 12 would be a different amount of aliasing
// from the one the voice actually has.
//
// So the cycle count falls out rather than being chosen: N / 109.09, which at four
// samples to the pixel is a dozen or so cycles across the middle column — thirteen on a
// touch screen and eleven under a mouse. A narrower plot shows fewer cycles, which is
// right.
//
// What reaches it
//
// Six parameters and no others: FM ratio, FM amount, Feedback, FM decay, Amp attack,
// Amp release. Everything else in the patch is forced to a stated constant, and each of
// them for a reason — see Event. A scrub on any of the other nine must not repaint, and
// that is a signature compare rather than a timer: Show keeps the six values and the
// element's own size and returns at once when none of them has moved. So a Level scrub
// costs one struct compare a frame and an Amount scrub costs one render a frame, which
// is what the picture is for.
//
// Which is also the whole of why this does not join the one moving thing in this
// interface. Docs/impl-style.md keeps that inside OnboardingShade, and a waveform
// display is the classic thing to break the rule: this one is repainted when a value it
// depends on changes and at no other time — no clock, no scheduler, no per-frame
// callback — and the guard above is what makes that structural rather than a promise.
// The time axis is across the picture, not through it.
//
// The cost is roughly 60 to 100 flops a sample — two FastMath.Sin in FmPartial.Next, an
// Exp in ModulatorLevel which early-outs entirely at a decay of 0 or 1 and a fresh patch
// sits at 1, an Exp in the release only, and the pitch compare, which is the one the
// forced pitchDecay of zero turns into a compare and a return. At the touch profile's
// 1456 samples that is about 145k flops a redraw, against the 0.4 ms a channel-start
// showing already costs.
//
// Measured at the mouse profile's 1232, in the editor under Mono: a drag on one of the
// six costs 0.14 ms a frame, and a drag on one of the other nine costs twenty
// nanoseconds — a struct compare and a return, which is the guard doing the whole of
// what it is for.

public sealed class SoundPlot : VisualElement
{
    public SoundPlot()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Paint;

        // The points are in the element's own coordinates, so a panel that has just
        // been narrowed into a safe area has to fill them again. Nothing about the
        // sound has changed, so this goes straight to the build rather than through
        // the guard in Show, which would refuse it.
        RegisterCallback<GeometryChangedEvent>(_ => Rebuild());
    }

    // The patch to draw, which for a lock is the channel's patch with the lock applied
    // — see SoundPanel, and ParamTile.ApplyTo for the one statement of what that means.
    //
    // Filled here rather than inside generateVisualContent, which UI Toolkit calls back
    // for reasons of its own: a repaint of a layer is not a change of sound.
    public void Show(in FmPatch patch)
    {
        var shape = new Shape(patch);
        if (_drawn && shape.Equals(_shape)) return;

        (_shape, _drawn) = (shape, true);
        Rebuild();
    }

    // The note the picture is of, which is also the note the self test renders.
    //
    // What that test checks — that six parameters reach the picture and the other nine
    // do not, that the trace is bounded by its envelope and lands exactly on silence —
    // is a claim about the forcing below, so a second copy of the forcing in the test
    // would be a second claim rather than a check of this one. Public for that, and the
    // element with it.
    public static FmNoteEvent Event(in FmPatch patch) => Event(new Shape(patch));

    // Private members

    // The six the picture is a function of, kept so that a scrub on any of the other
    // nine can be answered with a compare. It is also everything Event needs, so
    // nothing else about the patch is held here for a redraw to read.
    readonly struct Shape : System.IEquatable<Shape>
    {
        public readonly float Ratio, Index, Feedback, Decay, Attack, Release;

        public Shape(in FmPatch patch)
          => (Ratio, Index, Feedback, Decay, Attack, Release) =
             (patch.modulatorRatio, patch.modulationIndex, patch.feedback,
              patch.modulatorDecay, patch.carrierAttack, patch.carrierRelease);

        public bool Equals(Shape other)
          => Ratio == other.Ratio && Index == other.Index &&
             Feedback == other.Feedback && Decay == other.Decay &&
             Attack == other.Attack && Release == other.Release;
    }

    Shape _shape;
    bool _drawn;

    readonly List<Vector2> _trace = new();
    readonly List<Vector2> _envelope = new();

    // The rate the phase clock is pinned to and the note it sounds. A4 and 48kHz
    // together are what fix the samples per cycle; see the header for why that number
    // and not a round count of cycles is the thing being held.
    const float SampleRate = 48000.0f;
    const int PlotNote = 69;

    // Samples to the pixel. One pixel is one signed peak of the four, which is what
    // Visualizer.BuildTrace chose and argues for: a peak keeps the jaggedness a dense
    // FM tone actually has, where an average would flatten it to a hum. Four is what
    // read well on a trial render of seven patches — the default, the opening voice, a
    // hard bite, feedback at 2, ratio at 8, a half-second pad and a sub-unity wobble —
    // every one of them filling the width with the FM decay visible as the shape
    // relaxing into a sine.
    const int Oversample = 4;

    // The gate the picture draws, which is a written-down constant and deliberately not
    // the project tempo: read off the tempo, a tempo scrub would redraw the plot. A
    // hundred and fifty milliseconds is long enough that a two-operator patch's bite is
    // over inside it — the FM decay's own unit is a tenth of a second, so the middle of
    // that bar has most of a sweep spent by here — and short enough that a five
    // millisecond release is still a visible corner rather than a pixel.
    const float NominalGate = 0.15f;

    // What the attack and the release can take of the width at the very top of their
    // bars. The two and the gate's floor are one arithmetic: they sum to 1, so the gate
    // keeps a fifth of the picture whatever the other two are dialled to and nothing
    // ever has to be scaled back. A fifth is what a gate needs to read as a held note
    // rather than as the join between two envelopes.
    //
    // The release takes more than the attack because it is the longer parameter and the
    // one more often listened to: four seconds against two, and a tail is the half of an
    // envelope a hand dials by ear.
    const float AttackShare = 0.35f;
    const float ReleaseShare = 0.45f;

    // The event, built from a copy of the patch with everything but the six turned off.
    //
    // Each of the forced values has a reason. level is 0dB, which fixes the vertical
    // scale at plus or minus one and so needs no normalising — a quiet channel would
    // otherwise draw nothing. pan and the two sends are left where they are because
    // Next never reads them. unison is off: sixty cents is invisible at a dozen cycles,
    // and it halves the oscillator cost. gateScale is one, since the gate is the stated
    // constant above. pitchSweep is zero because it is not one of the six — and
    // pitchDecay is zeroed *as well*, which is what makes PitchScale take its
    // time >= pitchDecay early return; zeroing only the sweep would leave a Pow2 over an
    // Exp running per sample for a result of exactly 1.
    //
    // The gate is passed as attack + NominalGate rather than as NominalGate.
    // CarrierLevel holds at the attack's own level all the way to duration and releases
    // only after it, so the attack happens *inside* the gate rather than before it. Get
    // that wrong and a two second attack — the top of the bar — eats the whole picture
    // and the release never appears.
    static FmNoteEvent Event(in Shape shape)
    {
        var patch = FmPatch.Default;

        patch.level = 0.0f;
        patch.unison = 0.0f;
        patch.gateScale = 1.0f;
        patch.pitchSweep = 0.0f;
        patch.pitchDecay = 0.0f;

        patch.modulatorRatio = shape.Ratio;
        patch.modulationIndex = shape.Index;
        patch.feedback = shape.Feedback;
        patch.modulatorDecay = shape.Decay;
        patch.carrierAttack = shape.Attack;
        patch.carrierRelease = shape.Release;

        return FmNoteEvent.FromPatch(patch, PlotNote, shape.Attack + NominalGate, 0);
    }

    // Renders the note and keeps one point per pixel of each curve.
    void Rebuild()
    {
        _trace.Clear();
        _envelope.Clear();

        var size = contentRect.size;
        if (!_drawn || size.x < 2.0f || size.y < 2.0f) return;

        var note = Event(_shape);

        // Where the three segments sit across the width, each read off the same bar the
        // hand that set it was looking at. The gate takes what is left, which by the
        // arithmetic above is never less than a fifth.
        var head = ParamRanges.Of(ParamTargets.CarAttack)
                     .ToPosition(note.carrierAttack) * AttackShare;
        var tail = ParamRanges.Of(ParamTargets.CarRelease)
                     .ToPosition(note.carrierRelease) * ReleaseShare;
        var body = 1.0f - head - tail;

        // Half a pixel in from each edge, so a trace at full scale is stroked inside the
        // box rather than half outside it.
        var middle = size.y * 0.5f;
        var reach = middle - 1.0f;

        var columns = Mathf.RoundToInt(size.x);
        var samples = columns * Oversample;
        var peak = 0.0f;

        var voice = new FmVoiceState();
        voice.Trigger(note, SampleRate);

        for (var i = 0; i < samples; i++)
        {
            // The envelopes' clock. The oscillator's is the Trigger above and the step
            // this loop takes, and neither of them is this number.
            var time = TimeAt(i / (samples - 1.0f), note, head, body, tail);

            voice.Next(time, out var lower, out var upper);

            var value = lower + upper;
            if (Mathf.Abs(value) > Mathf.Abs(peak)) peak = value;

            if ((i + 1) % Oversample != 0) continue;

            var column = i / Oversample;
            var x = size.x * column / (columns - 1.0f);

            _trace.Add(new Vector2(x, middle - peak * reach));
            // Read off the same event on the same axis, so the two curves cannot drift
            // apart and the trace sits inside the envelope by construction rather than
            // by a second calculation.
            _envelope.Add(new Vector2(x, middle - note.CarrierLevel(time) * reach));

            peak = 0.0f;
        }

        MarkDirtyRepaint();
    }

    // Bar position across the width to elapsed note time. Linear in real seconds inside
    // each segment; see the header for why the segments are not all the same scale.
    //
    // The last pixel is the end of the note and not a step past it: position 1 maps to
    // the gate plus the whole of the release, which is where CarrierLevel has run out. A
    // plot running past it would draw the silence after the note instead of the end of
    // it.
    //
    // To a rounding rather than to the bit, on both counts — the shares are taken off 1
    // and added back to it, and a duration plus a release less that duration is not
    // always the release again. What that leaves is a last column standing within a few
    // parts in a hundred thousand of the release of the end, where the envelope is
    // either the exact zero of CarrierLevel's early return or a ten-millionth of full
    // scale short of it. Neither is a pixel, and the self test holds the end of the
    // trace to the millionth to keep it that way.
    //
    // A release dialled all the way down leaves no third segment to run through, and the
    // branch below is what keeps that case landing on the same number rather than on the
    // held gate.
    static float TimeAt(float position, in FmNoteEvent note,
                        float head, float body, float tail)
    {
        if (position < head) return note.carrierAttack * (position / head);

        if (position < head + body)
            return note.carrierAttack + NominalGate * ((position - head) / body);

        return tail > 0.0f
               ? note.duration + note.carrierRelease * ((position - head - body) / tail)
               : note.duration + note.carrierRelease;
    }

    // Two polylines, one subpath each, so the tessellation ceiling ScoreView warns about
    // is nowhere in reach: what costs there is the number of subpaths a filled path
    // holds, and this is a stroke of one.
    void Paint(MeshGenerationContext context)
    {
        if (_trace.Count < 2) return;

        var painter = context.painter2D;
        painter.lineWidth = 1.0f;

        // The envelope first and one step further back on the ramp, which is the rule
        // the rest of this interface is dimmed by: it is the same content, behind.
        painter.strokeColor = Style.Marker;
        Polyline(painter, _envelope);

        painter.strokeColor = Style.NoteLine;
        Polyline(painter, _trace);
    }

    static void Polyline(Painter2D painter, List<Vector2> points)
    {
        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++) painter.LineTo(points[i]);
        painter.Stroke();
    }
}

} // namespace Jacquard.App
