using UnityEngine.UIElements;

namespace Jacquard.App {

// What is set for the whole thing and belongs to no cell, no channel and no effect.
//
// One group of rows so far, which is the limiter across the finished mix, and that is
// enough to say what this panel is for. The alternative was a Limiter panel, and it
// would have been the right name for exactly as long as the limiter was the only thing
// of its kind: what a mix is driven into, what a meter is reading, what the master
// level is, none of these has a cell to hang from either, and a panel per setting is a
// row of switches on the transport for what is really one question — *what is set for
// the whole thing?*
//
// So the panel is named for the answer to that and the group inside it is headed
// Limiter. Every other panel here holds one kind of thing and needs no heading, which
// is the argument that split the send effects into two panels; this one is the
// exception by design, since being the place where the odd settings live is the whole
// of what it is.
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
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly VisualElement _body;

    Project _project;

    void Build()
    {
        _project = _editor.Project;

        _body.Clear();

        // Under the header, where every panel here puts one.
        _body.Add(Controls.Divider());
        _body.Add(Controls.Heading("Limiter"));

        // Drive first, because it is the one that is played. The other three are the
        // shape of what it runs into.
        _body.Add(Controls.Bar("Drive", DriveRange,
                               () => Limiter.drive, v => Limiter.drive = v));
        _body.Add(Controls.Bar("Ceiling", CeilingRange,
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

    // Decibels, which nothing else on screen is in, for the reason the settings
    // themselves are: what a drive does is double the signal a few times over, and a
    // bar counting multipliers spends most of its travel on the first of them. A dB is
    // already a logarithm, so the bar over one is straight and a pixel is worth the
    // same amount of push wherever it is taken.
    static readonly ValueBar.Range DriveRange =
      new ValueBar.Range(0.0f, Jacquard.Limiter.MaxDrive, digits: 1, unit: "dB");

    // Down from full scale, and no further than the drive reaches up: past that the
    // two are simply fighting each other with the output quieter for it.
    static readonly ValueBar.Range CeilingRange =
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
