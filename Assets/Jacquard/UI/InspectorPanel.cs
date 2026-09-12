using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The detail window for whatever the cursor is on.
//
// sequencer-spec.md draws the line here: a cell carries the kind of tile and the figure
// you need in order to read the score, and everything else — which parameter a lock
// points at, how far it moves it, the exact percentage behind a pie chart — is set
// in a window of its own rather than crammed into thirty pixels.
//
// It is also where a tile is put down and taken away, since the cursor is already
// the answer to where: a cell that will take a tile offers the tiles instead of a
// description of nothing, ground with no lane on it offers a lane, and everything
// else offers what it is and a way to remove it. There is no palette elsewhere on
// the screen, so a button that is not on this panel cannot apply to this cell.
//
// Everything the cell decides used to be here, which is why the panel ran long: a
// channel start carried the sound that channel is voiced in, and a lock carried a row
// for every parameter it could take hold of. Those two are the Sound panel now — fifteen
// synth parameters read across four columns with a picture of the voice among them
// cannot stand in a 248-unit column, and the frame they got back is a shape rather than
// a second header. What is left here is the tile's own rows, the lane a head carries,
// the palette and Delete, which is thirty elements on the longest showing there is — a
// channel start — and six on a lock, where all that is left is the header and Delete.
//
// The measured argument for keeping a long group between showings went with the groups
// it was about; see SoundPanel. What is left of it here is the shape of Refresh and its
// early return, which are older than either group and are about the drag rather than
// about the allocation: a control that edits the tile in place would otherwise pull
// itself out from under the hand moving it.
//
// One half of that measurement stays, because it is about this file and not about the
// groups. A bar's text field waits for its first edit — see ValueBar.BuildInput — which
// is three elements apiece for fifteen fields that were never opened, and the reason a
// showing here is counted in tens rather than in hundreds.
//
// What the Clear in Refresh buys is that everything under _body is on screen and bound
// to the tile being shown, which is the whole of why the sync path there can sweep the
// tree with no filter and why ValueBar.SyncAll can argue against keeping a list at all.

sealed class InspectorPanel
{
    public VisualElement Root { get; }

    public InspectorPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Tile", out _title);

        _body = new VisualElement();
        Root.Add(_body);

        Refresh(true);
    }

    // Rebuilt only when the cursor has moved onto something else: a control that edits
    // the tile in place would otherwise pull itself out from under the drag that drove
    // it.
    //
    // Everything on this panel — the palette, a note's bars, a channel's steppers, the
    // delete button — is short enough to be made again and is. The two runs that were
    // not are on the Sound panel, where the argument for keeping them is written.
    public void Refresh(bool force = false)
    {
        var tile = _editor.Selected;
        var lane = _editor.SelectedLane;
        // Two empty cells are not the same cell: one may take a tile and the other
        // only a lane, and neither has a tile to tell them apart by.
        var place = _editor.CanPlace;

        if (!force && tile == _tile && lane == _lane && place == _place)
        {
            // The same tile, but its values may have changed from elsewhere — a
            // transpose from the keys, a load — so the bars still have to be pulled
            // back in line with it. The lap switches go the same way, and one of the
            // things that moves them is the Period bar standing over them.
            //
            // The Play switch above all, since the thing that moves it most is a double
            // click on the very cell this panel is showing: the tile is the same tile, so
            // nothing here is rebuilt, and a switch left as it was drawn would read On
            // over a cell that had just gone grey.
            ValueBar.SyncAll(_body);
            _laps?.Sync();
            SyncPlay();
            // And the channel number beside it, for the same reason and from the other
            // side of the screen: the Channels panel can exchange two channels, which
            // leaves this the same ChannelTile object with a different number on it.
            _syncChannel?.Invoke();
            return;
        }

        (_tile, _lane, _place) = (tile, lane, place);

        _body.Clear();
        Build(tile, lane);
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly Label _title;
    readonly VisualElement _body;

    Tile _tile;
    Lane _lane;
    bool _place;

    // The bars are found again by a query over the body; these two are held onto
    // instead, and let go of whenever the body they stood in is cleared — they belong to
    // one tile, and the next showing builds its own.
    LapSwitches _laps;
    Button _play;

    // A chooser paints once and when it is stepped, so the one on a CHAN tile shows a
    // number that can move without this panel being rebuilt — see BuildChannel.
    System.Action _syncChannel;

    void Build(Tile tile, Lane lane)
    {
        _title.text = Title(tile);

        // The two that belong to one tile, and the readout that belongs to one of them.
        (_laps, _play, _syncChannel) = (null, null, null);

        // Free ground, whether that is a lane's own empty step or the terminator it
        // grows from. What such a cell is for is the tile that goes on it.
        if (_place) { Section(BuildPalette()); return; }

        // Off any lane there is nothing to put a tile on, so what is on offer is
        // somewhere to put one.
        if (tile == null) { Section(BuildNewLane()); return; }

        Section(BuildTile(tile));

        // The head is the one cell that is the lane rather than something standing
        // on it, so it is where the lane itself is worked on.
        if (_editor.Cell.Kind == CellKind.Head && lane != null) Section(BuildLane(lane));

        Section(BuildDelete());
    }

    // Adds a section unless it turned out to hold nothing: what would go here is an
    // empty box, and a section that carries air over it would leave that air behind.
    //
    // Which is only about BuildTile, and there about two things: the flow tiles that
    // have nothing to set — a jump, a terminator, a jump target — and a lock, which now
    // falls through that switch with nothing. Everything a lock decides is the fifteen
    // rows and they are on the panel beside this one, so a PABS or a PREL cell leaves
    // this panel with its header, nothing, and Delete. Which is the honest picture, and
    // is the case this guard was written for in the first place.
    //
    // Nothing is drawn between two of them. What ends up on this panel is a list of
    // rows and a button under it, so the only break is the air the foot row carries;
    // a rule between the sections would be marking a seam the eye has no use for, now
    // that a rule belongs to a heading rather than to the seam between two groups.
    void Section(VisualElement content)
    {
        if (content.childCount == 0) return;
        _body.Add(content);
    }

    // The panel's own header, which says what is under the cursor rather than which
    // panel this is. A cell that holds nothing is the only thing here that is not a
    // tile, and the pitch is left out of a note's: the bar underneath spells it, and
    // a header that changed as a pitch was dragged would be a second readout.
    static string Title(Tile tile) => tile == null ? "Empty Cell" : Name(tile) + " Tile";

    // In words, not in tokens. The four character codes are how a tile is spelled in
    // a saved file and how this codebase talks about one, and neither is a reason to
    // make a user learn that PABS is the lock that sets a value: the cell already
    // carries the icon, and what this name owes it is what it does.
    static string Name(Tile tile) => tile switch
    {
        NoteTile => "Note",
        AbsoluteParamTile => "Absolute Lock",
        RelativeParamTile => "Relative Lock",
        CycleGateTile => "Cycle Gate",
        ProbGateTile => "Chance Gate",
        ChannelTile => "Channel Start",
        TerminatorTile => "Lane End",
        JumpTile => "Jump",
        JumpDestTile => "Jump Target",
        _ => "Unknown"
    };

    // The tiles a free cell will take, the note first because that is what most of
    // them get. Nothing here asks where: the cell asked for the list.
    VisualElement BuildPalette()
    {
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;

        foreach (var (label, kind) in Palette)
        {
            var pick = kind;
            var button = Controls.Push(label, () => Act(() => _editor.Put(pick)),
                                       PaletteButtonWidth);
            button.style.marginBottom = Controls.Gap;
            grid.Add(button);
        }

        return grid;
    }

    VisualElement BuildNewLane()
    {
        var row = Controls.Row();
        row.Add(Controls.Push("New lane", () => Act(_editor.NewChannelLane), 66));
        return row;
    }

    // The controls belonging to the tile itself. A jump has none: there is nothing to
    // set on one, since where it goes is drawn on the plane. Neither has a lock, which
    // falls through this switch on purpose — everything it decides is the fifteen rows
    // on the Sound panel, and Section drops what comes back empty.
    VisualElement BuildTile(Tile tile)
    {
        var body = new VisualElement();

        switch (tile)
        {
            case NoteTile note: BuildNote(body, note); break;
            case CycleGateTile cycle: BuildCycle(body, cycle); break;
            case ProbGateTile prob: BuildProb(body, prob); break;
            case ChannelTile channel: BuildChannel(body, channel); break;
        }

        return body;
    }

    VisualElement BuildDelete()
    {
        // Deleting a lane's head is how the whole lane goes, which the button had
        // better say rather than leave to be found out.
        var head = _editor.Cell.Kind == CellKind.Head;

        var row = Controls.Foot();
        row.Add(Controls.Push(head ? "Delete lane" : "Delete",
                              () => Act(_editor.Delete), head ? 74 : 54));
        return row;
    }

    // A pitch is two bars and not one, because the two halves of it are set for
    // different reasons and a single bar serves neither. Eighty-four semitones over the
    // hundred and sixty pixels a drag covers is under two pixels a note, so landing on
    // the note meant was a matter of luck, and moving an octave meant carrying the bar
    // most of the way across the panel. Split, a semitone is thirteen pixels and an
    // octave is eighteen, and the letter can be changed without disturbing the register
    // or the register without disturbing the letter — which is how a pitch is thought
    // about anyway, and how the cell has always drawn one.
    //
    // Neither bar holds anything. They read the two halves off the one note number and
    // write it back together, so the tile is unchanged and so is the file: what is
    // stored is still one MIDI note. The bar being dragged pulls its partner along with
    // it through the Refresh that Touch runs, and a drag survives that because it is
    // measured from where the hand went down rather than from the value.
    //
    // The class bar stops at B rather than turning the octave over. A drag on a bar is
    // clamped to its own travel, so a carry could never come from one anyway, and the
    // octave is the next row down.
    //
    // The note is heard where a drag ends rather than at every semitone it crosses: a
    // scrub over an octave is twelve notes on top of each other and none of them the one
    // being chosen. A typed pitch sounds straight away, since it never passed through
    // the eleven others.
    void BuildNote(VisualElement body, NoteTile note)
    {
        body.Add(Controls.Bar("Note", NoteRange, () => Pitch.ToClass(note.Note),
                              value => SetPitch(note, Pitch.ToOctave(note.Note),
                                                Mathf.RoundToInt(value)),
                              () => _editor.Preview(note.Note)));

        body.Add(Controls.Bar("Octave", OctaveRange, () => Pitch.ToOctave(note.Note),
                              value => SetPitch(note, Mathf.RoundToInt(value),
                                                Pitch.ToClass(note.Note)),
                              () => _editor.Preview(note.Note)));

        // Length is in steps, so what it means in real time depends on the
        // channel's division, its gate ratio and the project tempo.
        body.Add(Controls.Bar("Length", LengthRange, () => note.Length,
                              value => { note.Length = Mathf.Clamp(value, 0.25f, 64.0f);
                                         _editor.RememberNote(note);
                                         Touch(); }));
    }

    // Where the two halves are put back together, so that the clamp and what follows it
    // are written once rather than once per bar.
    void SetPitch(NoteTile note, int octave, int pitchClass)
    {
        note.Note = Mathf.Clamp(Pitch.FromParts(octave, pitchClass),
                                Pitch.Lowest, Pitch.Highest);
        _editor.RememberNote(note);
        Touch();
    }

    // The period, and then a switch per lap of it.
    //
    // Which lap a gate fires on used to be a second bar, which could only ever name
    // one of them: a gate on the first and the third of four was two gates in two
    // cells, and nothing about the tile required that. A switch per lap says any of
    // the patterns the tile can hold, and it says it as the shape the cell then draws
    // — the run under the bar and the boxes on the plane are the same row of laps
    // read at two sizes.
    //
    // It stands under a heading rather than beside a caption because it is a block
    // and not a row: the caption column would leave a hundred pixels for thirty-two
    // switches, which is a target no fingertip could land on.
    void BuildCycle(VisualElement body, CycleGateTile cycle)
    {
        body.Add(Controls.Bar("Period", PeriodRange, () => cycle.Period,
                              value => { cycle.Period = Mathf.RoundToInt(value);
                                         Touch(); }));

        body.Add(Controls.Heading("Fires on", follows: true));

        _laps = new LapSwitches(cycle, lap => Act(() =>
                  { cycle.SetFires(lap, !cycle.Fires(lap)); Touch(); }));
        body.Add(_laps);
    }

    void BuildProb(VisualElement body, ProbGateTile prob)
      => body.Add(Controls.Bar("Chance", ChanceRange, () => prob.Percent,
                               value => { prob.Percent = value; Touch(); }));

    void BuildChannel(VisualElement body, ChannelTile channel)
    {
        // Whether the lane runs at all, which is also what a double click on the cell
        // toggles — this is the same switch written down where a tile's settings are named.
        //
        // On the master lane it can still be thrown and it still saves, and the lane goes
        // on running: which lane is the master is a position and not a property, so the
        // switch belongs to the lane for whenever it stops being the one. The cell is what
        // shows the difference, by staying solid.
        // The state is written on it as well as shown by the fill, the way the step
        // length row prints the division it is set to: one press instead of the chooser's
        // two arrows, and the same reading either way.
        //
        // Pressed, it writes the switch and lets Touch bring the panel back into line, the
        // same road a double click on the cell takes. Two roads into one control is exactly
        // where the two would drift apart if each drew itself.
        _play = Controls.Push("", () => { channel.Enabled = !channel.Enabled; Touch(); }, 44);
        SyncPlay();

        var row = Controls.Row();
        row.Add(Controls.Caption("Play"));
        row.Add(_play);
        body.Add(row);

        // Stepped through with arrows rather than scrubbed on a bar. A channel is not a
        // quantity: eight of them is a list of eight things to sound on, and the
        // distance between the third and the seventh is not four of anything. A bar
        // said otherwise twice over — it drew a fill that grew as if a higher channel
        // were more of something, and it asked a hand to hit one eighth of its length
        // to pick one. The arrows are what this panel already gives a choice from a
        // written-down list, which is what the row under this one is.
        var channels = new List<string>();
        for (var i = 1; i <= PatchBank.Channels; i++) channels.Add(i.ToString());

        // The readout is held onto rather than left to paint itself, which is the Play
        // switch's problem again from the other side of the screen: the Channels panel
        // can exchange two channels, and what that leaves under the cursor is the same
        // ChannelTile object carrying a different number — so Refresh takes its early
        // return and this row would go on showing the number that was there.
        //
        // Widening what Refresh calls the same tile to include the number is not an
        // option, and not merely because it would rebuild more than it has to: stepping
        // this chooser calls set and then paints, set runs Touch and so Refresh, and a
        // rebuild would clear the body out from under the arrow that is mid-press and
        // then write text onto a label that is no longer in the tree.
        body.Add(Controls.Chooser("Channel", channels,
                                  () => channel.Channel - 1,
                                  index => { channel.Channel = index + 1; Touch(); },
                                  out _syncChannel));

        var divisions = new List<string>();
        foreach (var d in ChannelTile.Divisions) divisions.Add("1/" + d);

        // "Step length" and not "Step", which named the thing rather than what is being
        // set about it: what the row holds is how long one step of this lane lasts, and
        // it stands two rows above a count of those steps. One of them says how long and
        // the other says how many, and neither can be read as the other now.
        body.Add(Controls.Chooser("Step length", divisions,
                                  () => System.Array.IndexOf(ChannelTile.Divisions,
                                                             channel.Division),
                                  index => { channel.Division = ChannelTile.Divisions[index];
                                             Touch(); }));
    }

    // Pulls the Play switch back onto whatever the tile now says, for the times the panel
    // is not rebuilt: the cell it is showing is the cell a double click toggles.
    void SyncPlay()
    {
        if (_play == null || _tile is not ChannelTile channel) return;

        _play.text = channel.Enabled ? "On" : "Off";
        Controls.SetActive(_play, channel.Enabled);
    }

    // What the head cell sets about the lane hanging off it, which is one row.
    //
    // No heading over it. A heading is for a group, and a group of one is a line of
    // chrome saying what the row under it could say itself: named "Lane steps" the row
    // is already the lane's, and on a jump target — where there is nothing else on the
    // panel at all — the heading was a title over a single stepper.
    //
    // So it joins the rows above it rather than standing apart from them, which is also
    // the truth of the thing: how long a step lasts is on the head cell and how many of
    // them there are is the lane, and a hand setting one is usually setting the other.
    VisualElement BuildLane(Lane lane)
    {
        var body = new VisualElement();

        // The one number here that is still stepped rather than scrubbed: a step is a
        // cell, growing only happens where there is free ground for one, and a refused
        // step is something to see one at a time rather than to drag through.
        body.Add(Controls.Stepper("Lane steps", () => lane.Steps.Count,
                                  value => _editor.ResizeLane(
                                    value > lane.Steps.Count ? 1 : -1), 1, "0"));

        // Where the lane sits is not set here. A lane further down runs later, so
        // moving one is also how an accent lane gets to overwrite what the lanes
        // above it did — and that is a thing to see happen on the plane, which is
        // what dragging the head cell does.

        return body;
    }

    // Runs an edit and hands the keys back to the grid: a click leaves the focus on
    // the button, and the arrows are supposed to land on the plane.
    void Act(System.Action action)
    {
        action();
        _editor.View?.Focus();
    }

    // Redraws the score for a change made here, without rebuilding this panel.
    void Touch()
    {
        _editor.Commit();
        Refresh();
    }

    // Parameter ranges
    //
    // What a synth parameter's bar covers comes from ParamRanges, which the Sound panel
    // uses. These are the ranges of the sequencer's own numbers, which nothing outside
    // this panel has to know about.

    // The letter half of a pitch, read out as the letter and nothing else: the number
    // behind it is an index into the twelve and says nothing a name does not, which is
    // the one case Range.Display exists for. Typing still goes through that number, the
    // same as the pitch bar these two replaced.
    static readonly ValueBar.Range NoteRange =
      ValueBar.Integer(0.0f, 11.0f,
                       value => Pitch.ToClassName(Mathf.RoundToInt(value)));

    // The register half, which stops one short of the plane's own top. C9 is the highest
    // note there is and it is the only one in its octave, so a bar reaching it would
    // spend a twelfth of its travel on a stop where eleven of the twelve letters are
    // refused and the bar above snaps back to C. Every octave this one covers takes all
    // twelve; the last note is still typed.
    static readonly ValueBar.Range OctaveRange = ValueBar.Integer(0.0f, 8.0f);

    // A length in steps. Dragging lands on quarters of one, since that is where a note
    // either fits the grid or deliberately overlaps the step after it, and it reaches
    // eight where the tile allows sixty-four: a note that long is typed, not scrubbed.
    //
    // The unit is printed because this and the channel's gate ratio are the same
    // multiplication on the step, and the step is what tells them apart: this one
    // counts them, that one takes a percentage of what this one counted.
    static readonly ValueBar.Range LengthRange =
      new ValueBar.Range(0.25f, 8.0f, snap: 0.25f, unit: "steps");

    // Whole percents. The wedge on the cell cannot show a tenth of one anyway, and any
    // percentage at all is still allowed by typing it.
    static readonly ValueBar.Range ChanceRange =
      new ValueBar.Range(0.0f, 100.0f, snap: 1.0f, digits: 0, unit: "%");

    // How many laps the cycle is long, which is also how many switches stand under
    // it. Dragging it covers the whole range, since every value on the way is a
    // pattern the run below can be read at.
    static readonly ValueBar.Range PeriodRange =
      ValueBar.Integer(CycleGateTile.MinPeriod, CycleGateTile.MaxPeriod);

    // What can be put on a free cell, in the order the buttons read: the note first
    // because it is the tile a cell usually wants, then the pairs, then the jump.
    //
    // Each button says what the tile does rather than what it is called in a file. A
    // row of four letter tokens fitted three to a line and told a newcomer nothing,
    // and this panel is the only place a tile is ever chosen, so the words have to
    // carry it. They are the same words the header then shows over the placed tile, so
    // a button and what it made read as the same thing.
    static readonly (string Label, TileKind Kind)[] Palette =
      { ("Note", TileKind.Note),
        ("Jump", TileKind.Jump),
        ("Absolute Lock", TileKind.AbsoluteLock),
        ("Relative Lock", TileKind.RelativeLock),
        ("Cycle Gate", TileKind.CycleGate),
        ("Chance Gate", TileKind.ChanceGate) };

    // Two to a line, which is what the words need and what the panel has room for
    // once the margin between a pair is counted. A wider button would fall to one a
    // line and make a six tile palette six rows tall.
    const float PaletteButtonWidth = 82.0f;

    // Eight laps to a line, which is a bar of sixteenths and puts the longest cycle
    // there can be in four lines. Four to a line would read against the cell, whose
    // boxes go four across, but it would also stand thirty-two switches eight lines
    // deep in a column that has to reach the bottom of the shortest screen this runs
    // on; eight is where a line is still a phrase.
    //
    // It is also where the switch stops being square under a mouse, and this is the
    // one run in the interface that goes past that. Measured at a panel scale of 2:
    // eight to a line comes out 25 by 25 on a touch screen, which is the screen the
    // number was chosen for, and 18 by 22 under a mouse, where the box asks for less
    // height than a button's own padding and border will give it — see Controls.Switch
    // for the floor. A run of slightly tall boxes is what that costs, and it is cheaper
    // than the eight lines the alternative stands in a column that has nowhere to put
    // them.
    const int LapsPerRow = 8;

    // The laps of a cycle gate, one switch each.
    //
    // Every lap the tile could have gets a switch, and the ones past the current
    // period are hidden rather than built and torn down: the period is set on a bar
    // standing directly over them, and a run that rebuilt itself as that bar moved
    // would pull the bar out from under the drag that was moving it. Hiding is also
    // what keeps a switch that goes out of reach and comes back — the tile keeps the
    // bit, so the run shows it again exactly as it was left.
    //
    // This is the older half of the argument the Sound panel's three runs now make at
    // the length of a whole panel: a run of elements that is the same run whatever the
    // number behind it, pulled back into line by a Sync rather than made again.
    sealed class LapSwitches : VisualElement
    {
        public LapSwitches(CycleGateTile cycle, System.Action<int> toggle)
        {
            _cycle = cycle;

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;

            for (var lap = 1; lap <= CycleGateTile.MaxPeriod; lap++)
            {
                var which = lap;
                _switches[lap - 1] = Controls.Switch(LapsPerRow, () => toggle(which));
                Add(_switches[lap - 1]);
            }

            Sync();
        }

        // Pulls the run back in line with the tile, for a switch that was just
        // clicked and for a period that has just moved under it.
        public void Sync()
        {
            for (var lap = 1; lap <= CycleGateTile.MaxPeriod; lap++)
            {
                _switches[lap - 1].style.display =
                  lap <= _cycle.Period ? DisplayStyle.Flex : DisplayStyle.None;
                Controls.SetActive(_switches[lap - 1], _cycle.Fires(lap));
            }
        }

        readonly CycleGateTile _cycle;
        readonly Button[] _switches = new Button[CycleGateTile.MaxPeriod];
    }
}

} // namespace Jacquard.App
