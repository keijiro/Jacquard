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
// So the panel is named for the answer to that and each group inside it is headed. This
// was the one panel built that way, against an argument that a panel already says what a
// heading would; the send effects are now grouped the same way, and the shape — a
// heading, the rule under it, its rows — is the one every panel here groups by. What is
// left of the distinction is that the groups here have nothing in common but being
// global, while a reverb and a delay are two of a kind.
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
    Keys _keys;

    void Build()
    {
        _project = _editor.Project;

        _body.Clear();

        // Ahead of the limiter because that is the order a note meets them: what it
        // is allowed to be, then what the sum of everything is held under.
        _body.Add(Controls.Heading("Scale"));

        _keys = new Keys(degree => Scale.Allows(degree), degree =>
        {
            Scale.SetAllowed(degree, !Scale.Allows(degree));
            _keys.Sync();

            // Nothing is committed. The score is not touched — what is written on the
            // plane keeps the pitch it was written with — and the sequencer reads this
            // as it makes each note, so the next one to be made is the first one moved.
            _editor.View.Focus();
        });

        _body.Add(_keys);

        _body.Add(Controls.Heading("Limiter", follows: true));

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
