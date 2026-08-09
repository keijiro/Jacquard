using System;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The two send effects.
//
// The one panel here whose contents the cursor has nothing to do with. Everything
// else on screen answers to a cell — the Tile panel to the tile under the cursor, the
// Sound panel to the channel a CHAN tile names, the Lock panel to what a lock holds —
// because everything else is a property of something written on the plane. One reverb
// and one delay for the whole project are not: there is no cell that is the reverb,
// and putting one on the plane would be inventing a tile for the sake of a rule.
//
// So this is the exception to "nothing is toggled", and it pays for it by not being up
// unless it has been asked for: a button on the transport row, where the rest of what
// belongs to the project as a whole already is. That is also why it takes the close
// button Controls.Panel has always offered and nothing has used — for a panel the
// cursor governs, a close would be undone by the next keypress, and for this one it
// is simply the other half of the switch.
//
// It sits at the top left, the opposite corner to the cursor's column, so that
// reaching for an effect never means covering what the cursor is showing about the
// note the effect is for.
//
// Seven bars and no more, in two named groups. The send amounts are not here: how
// much of a channel goes to the reverb is a property of that channel, so it is in the
// patch and on the Sound panel with the rest of the timbre, and a lock can reach it.

sealed class SendPanel
{
    public VisualElement Root { get; }

    public SendPanel(ScoreEditor editor, Action onClose)
    {
        _editor = editor;

        Root = Controls.Panel("Send", onClose);

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

        _body.Add(Controls.Caption("Reverb"));
        _body.Add(Controls.Bar("Size", Unit,
                               () => Fx.reverbSize, v => Fx.reverbSize = v));
        _body.Add(Controls.Bar("Damp", Unit,
                               () => Fx.reverbDamp, v => Fx.reverbDamp = v));
        _body.Add(Controls.Bar("Width", Unit,
                               () => Fx.reverbWidth, v => Fx.reverbWidth = v));

        _body.Add(Controls.Divider());

        _body.Add(Controls.Caption("Delay"));

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
