using UnityEngine.UIElements;

namespace Jacquard.App {

// The send effects: the reverb and the delay, one panel with a heading over each.
//
// It was a panel each, on the argument that a panel is already the thing that says
// "this group of rows is about that", so a heading inside one was a second answer to a
// question the panel had answered. What that left out is what the second panel costs to
// say it: a header, a rule, an inset above and below and the gap to the panel under it,
// paid over again for something raised by one switch and set in one sitting. Two
// headings come to about what that frame did — four units under it on the mouse profile
// and about as far over it on the touch one, since a heading is a row and both a row and
// a frame grow with the pointer — so the height was never what the split was buying. What
// the merge buys is that the column reads as the one thing the Send FX button raises
// rather than as two boxes that always arrive together and always leave together.
//
// What the old argument was really about is that the first row of a group sat against a
// rule and the last sat against nothing, which made a run of them read as one loose
// list. The answer to that is a heading with the rule under it and air over it, which is
// the shape every panel here now groups by — see Controls.Heading. A rule that stood
// between two groups belonged to neither of them and said only that something changes
// here; under the name it belongs to the name, and the group it heads is everything down
// to the next patch of air.
//
// Grouping inside a panel is affordable in a way it was not, because a panel is about to
// stop being bounded by the shortest screen this runs on: once a panel scrolls, height
// spent on saying what a group is stops competing with the rows underneath it for the
// bottom of the screen. Spending it is what lets a column hold fewer, larger panels,
// which is the direction the whole of this chrome is going.
//
// The panel takes the name of the switch that raises it, the way Global and Channels do.
// It is the pair named by what reaches them: a send is what a channel does, and the
// amounts sending into these are on the Sound panel under the name of the effect each
// one feeds.
//
// This is the only panel whose contents the cursor has nothing to do with. Everything
// else on screen answers to a cell — the Tile panel to the tile under the cursor, the
// Sound panel to the channel a CHAN tile names, the Lock panel to what a lock holds —
// because everything else is a property of something written on the plane. One reverb
// and one delay for the whole project are not: there is no cell that is the reverb, and
// putting one on the plane would be inventing a tile for the sake of a rule.
//
// So this is the exception to "nothing is toggled", and it pays for it by not being up
// unless it has been asked for: a button on the transport row, where the rest of what
// belongs to the project as a whole already is.
//
// It stands in a column of its own beside the cursor's, on the inside of it. A column of
// its own because nothing here answers to a cell, and a panel that does not follow the
// cursor cannot queue up behind panels that do; beside it because the two are read
// together — how much of a channel goes to the reverb is a row of that channel's Sound
// panel, and this is what it goes to, so the amount and the effect are a glance apart.
// What it costs is the plane it covers, and only while it is up.
//
// Seven bars between the two effects and no more. The send amounts are not here: how
// much of a channel goes to the reverb is a property of that channel, so it is in the
// patch and on the Sound panel with the rest of the timbre, and a lock can reach it.

sealed class SendPanel
{
    public VisualElement Root { get; }

    public SendPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Send FX");

        _body = new VisualElement();
        Root.Add(_body);

        Build();
    }

    // Called when the score changes, which is where a load arrives. Nothing here is
    // about the cursor, so the cursor does not call it.
    public void Refresh()
    {
        // A load hands over a whole new project. The bars would follow it by
        // themselves, but the note value is a chooser and a chooser paints its label
        // once, so the body is built again rather than left showing the delay time of
        // the project before this one.
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

        // The reverb first, because that is the order the two are read in on the Sound
        // panel, where the amount feeding each of them is set.
        BuildReverb();
        BuildDelay();
    }

    void BuildReverb()
    {
        _body.Add(Controls.Heading("Reverb"));

        _body.Add(Controls.Bar("Size", Unit,
                               () => Fx.reverbSize, v => Fx.reverbSize = v));
        _body.Add(Controls.Bar("Damp", Unit,
                               () => Fx.reverbDamp, v => Fx.reverbDamp = v));
        _body.Add(Controls.Bar("Width", Unit,
                               () => Fx.reverbWidth, v => Fx.reverbWidth = v));
    }

    void BuildDelay()
    {
        _body.Add(Controls.Heading("Delay", follows: true));

        // The one control here that is not a bar. A delay time is a note value rather
        // than a number, so what it needs is a list to step through: sequencer.md
        // keeps a number on a bar and a choice out of a set on a pair of arrows, and
        // this is the second of those.
        _body.Add(Controls.Chooser("Time", DelayTime.Names,
                                   () => DelayTime.Nearest(Fx.delayBeats),
                                   index => Fx.delayBeats = DelayTime.Beats[index]));

        _body.Add(Controls.Bar("Feedback", FeedbackRange,
                               () => Fx.delayFeedback, v => Fx.delayFeedback = v));
        _body.Add(Controls.Bar("Tone", Unit,
                               () => Fx.delayTone, v => Fx.delayTone = v));
        _body.Add(Controls.Bar("Spread", Unit,
                               () => Fx.delaySpread, v => Fx.delaySpread = v));
    }

    // By reference, so that a setter writes the project's own struct rather than a
    // copy of it. The synth is not told: JacquardApp compares what it last sent
    // against what this holds, every frame.
    ref SendFx Fx => ref _editor.Project.Fx;

    // A plain fraction, which is what six of the seven are. Nothing is in seconds or
    // in radians here, so there is no unit worth printing beside any of them.
    static readonly ValueBar.Range Unit = ValueBar.Amount(0.0f, 1.0f);

    // Stops short of one, since the loop it feeds does. A bar that reached the end of
    // its travel and then had its value clamped would be a bar that lies about where
    // the parameter is.
    static readonly ValueBar.Range FeedbackRange =
      ValueBar.Amount(0.0f, SendFx.MaxFeedback);
}

} // namespace Jacquard.App
