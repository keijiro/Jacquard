using System.Collections.Generic;
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
// The Swap group under the eight rows is the one thing on this panel that is not a row,
// and the one thing on it that writes the score. It is here because this is the single
// place in the interface where a channel is a subject in its own right rather than
// something a cell names: an exchange of two channels answers to no cell, so there is no
// cursor to hang it off — the same argument that keeps the send effects off the tile
// panel and on one of their own. What the operation is and what it costs are argued at
// Project.SwapChannels.
//
// What it costs here is the premise the rest of the panel rests on. Everything else on
// it is played rather than set — a mute writes nothing that a file would notice — so the
// panel as a whole stayed live while a load waited on the lap line, and that is now true
// of every control here except one. The group alone is put out of reach; see SetLocked.
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

        Root.Add(BuildSwap());

        Refresh();
    }

    // Called when the score changes, which is what can move a row without the row being
    // touched: a lane arriving or leaving is a channel becoming reachable or not, and a
    // load is eight rows arriving at once, since the mutes come with the file.
    public void Refresh()
    {
        foreach (var row in _rows) row.Sync();
    }

    // Takes the group out of reach while a load waits on the lap line, and gives it back
    // at the seam. The rest of the panel stays live: a mute is played across the seam and
    // writes nothing a file would notice, and this is the one control here that does.
    //
    // The group and not the panel, and that is not only about what is being held. A
    // shield is found by name from wherever it is asked for downwards, so a SetLocked on
    // the whole panel would find this group's shield already standing, take it for its
    // own and cover nothing — see Controls.SetLocked.
    public void SetLocked(bool locked) => Controls.SetLocked(_swap, locked);

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

    // Swap
    //
    // Two channels to exchange and a button to do it with, standing under the eight rows
    // and parted from them by the heading — which carries the rule and the air above it,
    // and is what a group in this UI is announced by. Not a foot: that is for the one
    // button a panel has and is not the point of it, and would put a second gap under a
    // rule that has nothing to head.
    //
    // Stepped through with arrows rather than scrubbed, for the reason the Tile panel
    // gives about the same choice: a channel is not a quantity, so it is picked off a
    // written-down list. Captionless, since this panel has no caption column — its rows
    // are built against a single character instead, and a caption's width here would be
    // air taken off the two things being chosen.
    //
    // The two numbers are deliberately left standing after a press. What is showing is
    // the way back: a swap is its own inverse, and pressing again is this app's only
    // undo, so a reset to 1 and 2 would tidy away the one thing on screen that says how
    // to take the press back.
    VisualElement BuildSwap()
    {
        _swap = new VisualElement();
        _swap.style.flexShrink = 0;

        _swap.Add(Controls.Heading("Swap", follows: true));

        var numbers = new List<string>();
        for (var i = 1; i <= PatchBank.Channels; i++) numbers.Add(i.ToString());

        var row = Controls.Row();
        row.Add(Stepper(numbers, () => _a, value => _a = value));
        row.Add(Controls.SwapMark());
        row.Add(Stepper(numbers, () => _b, value => _b = value));
        _swap.Add(row);

        // The width the panel has left, the way Select takes it: this is the button the
        // group is for, so nothing beside it has a claim on the row.
        _swapButton = Controls.Push("Swap", Swap);
        _swapButton.style.flexGrow = 1;
        _swapButton.style.marginRight = 0;

        var press = Controls.Row();
        press.Add(_swapButton);
        _swap.Add(press);

        SyncSwap();

        return _swap;
    }

    // One of the two, sized to half of what the row has. A Chooser is itself a row, so
    // it carries a row's gap under it and lays out to its content: inside a row of its
    // own that leaves a gap where there is nothing to part, and — at the mouse panel's
    // width — dead air to the right of each pair of arrows.
    VisualElement Stepper(List<string> numbers, System.Func<int> get,
                          System.Action<int> set)
    {
        var stepper = Controls.Chooser(numbers, () => get() - 1,
                                       index => { set(index + 1); SyncSwap(); });
        stepper.style.marginBottom = 0;
        stepper.style.flexGrow = 1;
        return stepper;
    }

    // The same number on both sides is a no-op, and says so the way a mute under a solo
    // and a Select with nowhere to go say it: dimmed whole, and the press not acted on.
    //
    // Only the button, and only off the two numbers here. Everything a swap moves is
    // read off the project by the eight rows above, which Commit brings back into line
    // through Refresh — so there is nothing for this to sync but its own dim.
    void SyncSwap()
      => _swapButton.style.opacity = _a == _b ? Style.DimmedOpacity : 1.0f;

    void Swap()
    {
        if (_a == _b) return;

        _editor.SwapChannels(_a, _b);

        // The keys belong to the plane, and the press took the focus off it — the same
        // hand-back a mute does.
        _editor.View.Focus();
    }

    // The two numbers being exchanged. Held here rather than read off anything, since
    // they are a question the hand is asking and not a fact about the score.
    VisualElement _swap;
    Button _swapButton;
    int _a = 1, _b = 2;

    // A channel
    //
    // An element of its own for the reason a lock's row is one: what a row shows is
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
