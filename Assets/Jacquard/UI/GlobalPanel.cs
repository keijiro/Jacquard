using UnityEngine.UIElements;

namespace Jacquard.App {

// What is set for the whole thing and belongs to no cell, no channel and no effect.
//
// Two groups: which semitones the whole piece is allowed to land on, and the limiter
// across the finished mix. The alternative was a Limiter panel, and it would have been
// the right name for exactly as long as the limiter was the only thing of its kind:
// what a mix is driven into, what a meter is reading, what the master level is, none of
// these has a cell to hang from either, and a panel per setting is a row of switches on
// the transport for what is really one question — *what is set for the whole thing?*
// The scale is the first thing to arrive and prove it.
//
// So the panel is named for the answer to that and each group inside it is headed.
// Every other panel here holds one kind of thing and needs no heading, which is the
// argument that split the send effects into two panels; this one is the exception by
// design, since being the place where the odd settings live is the whole of what it is.
//
// The scale comes first because that is the order a note meets the two: it is decided
// as the note is made, and the limiter is what the sum of every note is held under.
//
// It comes up in the middle of the screen rather than in a column. The columns are read
// against the score — the cursor's panels say what a cell holds, the sends say what a
// channel's amounts feed into — and nothing here is read against anything: a limiter is
// set while listening to the whole mix, with the eye nowhere in particular. The middle
// is also the one place a panel can be put that says it is not part of the arrangement
// around the plane, which is what a setting nobody visits twice a session should say.

sealed class GlobalPanel
{
    public VisualElement Root { get; }

    public GlobalPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Global");

        _body = new VisualElement();
        Root.Add(_body);

        Build();
    }

    // Called when the score changes, which is where a load arrives with a limiter of
    // its own. Nothing here answers to the cursor.
    public void Refresh()
    {
        if (!ReferenceEquals(_project, _editor.Project))
        {
            Build();
            return;
        }

        ValueBar.SyncAll(_body);

        // Which knows about bars and nothing else, so the keyboard is pulled back in
        // line by hand. A load arrives on the branch above and rebuilds the lot.
        _keys?.Sync();
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly VisualElement _body;

    Project _project;
    ScaleKeys _keys;

    void Build()
    {
        _project = _editor.Project;

        _body.Clear();

        // Under the header, where every panel here puts one.
        _body.Add(Controls.Divider());

        // Ahead of the limiter because that is the order a note meets them: what it
        // is allowed to be, then what the sum of everything is held under.
        _body.Add(Controls.Heading("Scale"));

        _keys = new ScaleKeys(Scale, degree =>
        {
            Scale.SetAllowed(degree, !Scale.Allows(degree));
            _keys.Sync();

            // Nothing is committed. The score is not touched — what is written on the
            // plane keeps the pitch it was written with — and the sequencer reads this
            // as it makes each note, so the next one to be made is the first one moved.
            _editor.View.Focus();
        });

        _body.Add(_keys);

        _body.Add(Controls.Divider());
        _body.Add(Controls.Heading("Limiter"));

        // Threshold first, because it is the one that is played. The other two are the
        // shape of what it does.
        //
        // The field behind it is called ceiling and the label is not, which is the one
        // place in this project where the two disagree on purpose. A ceiling is where an
        // output lands, and with the make-up automatic this output always lands at full
        // scale; what the hand on this bar is choosing is where limiting begins. The model
        // keeps the other name because down there it is still the level the gain holds the
        // mix under, and renaming it would be a format bump for a word.
        _body.Add(Controls.Bar("Threshold", ThresholdRange,
                               () => Limiter.ceiling, v => Limiter.ceiling = v));
        _body.Add(Controls.Bar("Attack", AttackRange,
                               () => Limiter.attack, v => Limiter.attack = v));
        _body.Add(Controls.Bar("Release", ReleaseRange,
                               () => Limiter.release, v => Limiter.release = v));
    }

    // By reference, so that a setter writes the project's own struct rather than a copy
    // of it. The synth is not told: JacquardApp compares what it last sent against what
    // this holds, every frame.
    ref Limiter Limiter => ref _editor.Project.Limiter;

    // Spelled out because UI Toolkit has a Scale of its own, which is the same reason
    // the limiter's own constants are reached through the namespace below.
    Jacquard.Scale Scale => _editor.Project.Scale;

    // Twelve switches laid out as a keyboard: seven across the bottom and five sitting
    // in the gaps above them, with the two gaps a keyboard does not have — E to F and B
    // to C — left empty.
    //
    // That is the whole of the shape, and deliberately. What a player needs from it is
    // to find a semitone without counting, which the two missing blacks give: they are
    // what turn a run of twelve boxes into somewhere a hand already knows. Anything
    // further — narrower blacks, a black overlapping the whites it sits between, a
    // drawn key — would be a picture of a keyboard rather than a set of switches, and
    // these are switches: what a press does is allow a note, not play one.
    //
    // No captions, for the reason a lap switch has none. Position is what a switch in a
    // run means, and here the position is a pitch.
    sealed class ScaleKeys : VisualElement
    {
        public ScaleKeys(Jacquard.Scale scale, System.Action<int> toggle)
        {
            _scale = scale;

            var size = Controls.SwitchSize(WhiteKeys);
            var stride = size + Controls.Gap;

            style.height = size * 2.0f + Controls.Gap;

            // The blacks first in the tree and absolutely placed, so that the whites
            // below them lay themselves out in a plain row and neither has to know
            // where the other is standing.
            var blacks = new VisualElement { style = { height = size } };
            blacks.style.position = Position.Absolute;
            blacks.style.left = 0;
            blacks.style.right = 0;
            blacks.style.top = 0;
            Add(blacks);

            var whites = new VisualElement();
            whites.style.flexDirection = FlexDirection.Row;
            whites.style.marginTop = size + Controls.Gap;
            Add(whites);

            for (var i = 0; i < WhiteKeys; i++)
            {
                var degree = White[i];
                _switches[degree] = Controls.Switch(WhiteKeys, () => toggle(degree));
                whites.Add(_switches[degree]);
            }

            for (var i = 0; i < BlackKeys; i++)
            {
                var degree = Black[i];
                var key = Controls.Switch(WhiteKeys, () => toggle(degree));

                // Half a step along from the white it follows, which is where the gap
                // between two of them is.
                key.style.position = Position.Absolute;
                key.style.left = stride * (BlackAfter[i] + 0.5f);
                key.style.top = 0;
                key.style.marginRight = 0;
                key.style.marginBottom = 0;

                blacks.Add(key);
                _switches[degree] = key;
            }

            Sync();
        }

        public void Sync()
        {
            for (var degree = 0; degree < Jacquard.Scale.Degrees; degree++)
                Controls.SetActive(_switches[degree], _scale.Allows(degree));
        }

        const int WhiteKeys = 7;
        const int BlackKeys = 5;

        static readonly int[] White = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] Black = { 1, 3, 6, 8, 10 };

        // Which white key each black one stands after, counted across the row: the
        // fourth and the seventh gaps are the ones nothing goes in.
        static readonly int[] BlackAfter = { 0, 1, 3, 4, 5 };

        readonly Jacquard.Scale _scale;
        readonly Button[] _switches = new Button[Jacquard.Scale.Degrees];
    }

    // Down from full scale, and read as how far the mix is squeezed rather than as where
    // the output lands: the make-up gain gives back whatever this takes off, so pulling
    // the bar down makes the thing harder without making it quieter. That is what leaves
    // it as the one number here that is played, and why there is no second bar beside it
    // — a make-up to be set by hand would only ever be set to this, negated.
    //
    // It runs to 48dB down, which is most of the bar spent somewhere no limiter is meant
    // to be taken and is the point: this is an instrument, and the bottom of this bar is
    // the soft clip playing the whole mix.
    //
    // Decibels, which nothing else on screen is in, for the reason the setting itself is:
    // what a squeeze does is halve the signal a few times over, and a bar counting
    // multipliers spends most of its travel on the first of them. A dB is already a
    // logarithm, so the bar over one is straight and a pixel is worth the same amount
    // wherever it is taken.
    static readonly ValueBar.Range ThresholdRange =
      new ValueBar.Range(Jacquard.Limiter.MinCeiling, 0.0f, digits: 1, unit: "dB");

    // The same geometric bar an envelope time gets, and for the same reason — the
    // useful part of an attack here runs from a fraction of a millisecond, where the
    // limiter is holding a transient down, to fifty, where it is letting one through.
    static readonly ValueBar.Range AttackRange =
      ValueBar.Seconds(Jacquard.Limiter.MinAttack, Jacquard.Limiter.MaxAttack);

    static readonly ValueBar.Range ReleaseRange =
      ValueBar.Seconds(Jacquard.Limiter.MinRelease, Jacquard.Limiter.MaxRelease);
}

} // namespace Jacquard.App
