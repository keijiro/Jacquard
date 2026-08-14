using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// One row per channel, and the whole bank on one panel.
//
// The other panels here show one thing at a time — the tile under the cursor, the sound
// of the channel it names, the hold one lock has. This one shows all eight at once and
// has to, because what it is for is the comparison: a mute is only ever pressed against
// what the rest of the mix is doing, and a solo says nothing at all except in relation
// to the channels it is silencing.
//
// A row is a number, two switches and a button, in that order: which channel, whether
// it is the only one, whether it is silent, and a way to go to it. Solo before Mute
// because solo overrules it — with anything soloed the mutes are not consulted, so they
// grey out, keeping what they hold rather than being cleared. Dropping the last solo
// gives back the mix that was underneath it.
//
// Select is the row's way onto the plane. A channel's timbre is edited on the Sound
// panel, which comes up for the CHAN tile that names the channel and for nothing else,
// so this moves the cursor to that tile: the panel stays the one thing that says what
// the cursor is on, and this is a way of moving the cursor rather than a second way of
// opening a panel. A channel with no lane of its own has nowhere to go and greys out,
// which is also the one place on screen that says which of the eight are in use.
//
// It stands in the top left, which is the one corner the cursor's panels never reach,
// and it is raised by a switch on the transport row like everything else there. A mute
// is played rather than set, which is an argument for having it up already — but it is
// eight rows of chrome over the plane, and a hand that is muting is a hand that can
// press the switch first. What it covers while it is up is a corner the plane can be
// panned out from under.

sealed class ChannelsPanel
{
    public VisualElement Root { get; }

    public ChannelsPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Channels");

        _rows = new Row[PatchBank.Channels];

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
        {
            var row = new Row(this, channel);
            _rows[channel - 1] = row;
            Root.Add(row);
        }

        Refresh();
    }

    // Called when the score changes, which is what can move a row without the row being
    // touched: a lane arriving or leaving is a channel becoming reachable or not, and a
    // load is eight rows arriving at once, since the mutes come with the file.
    public void Refresh()
    {
        foreach (var row in _rows) row.Sync();
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly Row[] _rows;

    // Read off the project every time rather than held, the same way the score is: a
    // load brings mutes of its own, and a reference taken when this panel was built
    // would leave the switches pressing on the file that was closed.
    ChannelMutes Mutes => _editor.Project.Mutes;

    // Where Select goes: the head of the first lane that names this channel, in the
    // order the runners are born in, so that a channel with two lanes takes the cursor
    // to the one that runs first.
    GridPoint? HeadOf(int channel)
    {
        foreach (var lane in _editor.Score.ChannelLanes)
            if (lane.Channel.Channel == channel) return lane.HeadPoint;

        return null;
    }

    void Select(int channel)
    {
        var head = HeadOf(channel);
        if (head.HasValue) _editor.View.SetCursor(head.Value);

        // The cursor is where the keys belong, and a press moved the focus to the
        // button. SetCursor also brings the head into view, so the plane may well have
        // moved under the hand that pressed this.
        _editor.View.Focus();
    }

    // A channel
    //
    // An element of its own for the reason LockPanel's row is one: what a row shows is
    // read off the model every time it is asked, so there is nothing to keep in step by
    // hand and no list of closures standing in for the tree.

    sealed class Row : VisualElement
    {
        public Row(ChannelsPanel panel, int channel)
        {
            (_panel, _channel) = (panel, channel);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.marginBottom = Controls.Gap;

            // A digit and the air after it. The caption column every other panel uses
            // is as wide as a parameter's name, and what stands here is one character.
            var number = Controls.Text(channel.ToString(), Controls.FontSize,
                                       Style.Label);
            number.style.width = Controls.FontSize;
            number.style.height = Controls.RowHeight;
            number.style.marginRight = Controls.Gap;
            number.style.unityTextAlign = TextAnchor.MiddleLeft;
            Add(number);

            _solo = Controls.Push("Solo", ToggleSolo, 40);
            Add(_solo);

            _mute = Controls.Push("Mute", ToggleMute, 40);
            Add(_mute);

            // Whatever is left of the row, which is what makes the panel's own width
            // the thing that decides it rather than a number written here.
            _select = Controls.Push("Select", () => _panel.Select(_channel));
            _select.style.flexGrow = 1;
            _select.style.marginRight = 0;
            Add(_select);

            Sync();
        }

        // Pulls the row back in line with the mutes and with the score.
        public void Sync()
        {
            var mutes = _panel.Mutes;
            var soloing = mutes.AnySoloed;

            Controls.SetActive(_solo, mutes.IsSoloed(_channel));
            Controls.SetActive(_mute, mutes.IsMuted(_channel));

            // A mute that is not being consulted says so, and a Select with nowhere to
            // go says so, in the one way this UI has of saying it: dimmed whole, the
            // way a released lock row is. Neither is disabled in the layout engine's
            // sense — the presses are simply not acted on below — since a disabled
            // control here would inherit the default theme's idea of grey rather than
            // this one's.
            _mute.style.opacity = soloing ? Style.DimmedOpacity : 1.0f;
            _select.style.opacity = _panel.HeadOf(_channel).HasValue
                                    ? 1.0f : Style.DimmedOpacity;
        }

        // Private members

        readonly ChannelsPanel _panel;
        readonly int _channel;
        readonly Button _solo;
        readonly Button _mute;
        readonly Button _select;

        // Every row's mute changes appearance when any solo moves, so the whole panel
        // is synced rather than the row that was pressed.
        void ToggleSolo()
        {
            var mutes = _panel.Mutes;
            mutes.SetSoloed(_channel, !mutes.IsSoloed(_channel));
            _panel.Refresh();
            _panel._editor.View.Focus();
        }

        void ToggleMute()
        {
            var mutes = _panel.Mutes;
            if (mutes.AnySoloed) return;

            mutes.SetMuted(_channel, !mutes.IsMuted(_channel));
            Sync();
            _panel._editor.View.Focus();
        }
    }
}

} // namespace Jacquard.App
