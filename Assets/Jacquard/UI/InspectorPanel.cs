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
// Everything the cell decides is here, which is why the panel runs long: a channel
// start carries the lane it heads and the sound that channel is voiced in as well as
// its own three rows, and a lock carries a row for every parameter it could take hold
// of. Those were panels of their own stacked under this one, each up only while this
// one was showing a particular kind of tile — which is a group of this panel wearing a
// frame, and a second header saying what the cursor had already said. What pays for the
// length is that a column of panels scrolls.

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
    public void Refresh(bool force = false)
    {
        var tile = _editor.Selected;
        var lane = _editor.SelectedLane;
        // Two empty cells are not the same cell: one may take a tile and the other
        // only a lane, and neither has a tile to tell them apart by.
        var place = _editor.CanPlace;
        // And a lock can change channels without moving: renumbering the CHAN above it
        // does that, and so does dragging the lane under a different jump. Asked only
        // for a lock, since nothing else here is built out of it.
        var channel = tile is ParamTile ? _editor.Score.ChannelOf(lane) : 0;

        if (!force && tile == _tile && lane == _lane && place == _place &&
            channel == _channel)
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
            // A lock row shows a bar and whether the lock is holding it, and the second
            // of those is not a bar. Everything it shows can change without the row
            // changing, which is why they are synced rather than built again.
            foreach (var row in _body.Query<LockRow>().Build()) row.Sync();
            return;
        }

        (_tile, _lane, _place, _channel) = (tile, lane, place, channel);

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
    int _channel;

    // The bars are found again by a query over the body; these two are held onto
    // instead, and let go of whenever the body they stood in is cleared.
    LapSwitches _laps;
    Button _play;

    void Build(Tile tile, Lane lane)
    {
        _title.text = Title(tile);
        (_laps, _play) = (null, null);

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

        // And under the lane, for the one head that names a channel, the sound that
        // channel is voiced in.
        if (tile is ChannelTile sound) Section(BuildSound(sound));

        Section(BuildDelete());
    }

    // Adds a section unless it turned out to hold nothing: a lock keeps everything it
    // owns on the Lock panel, so what would go here is an empty box, and a section that
    // carries air over it would leave that air behind.
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
    // set on one, since where it goes is drawn on the plane.
    VisualElement BuildTile(Tile tile)
    {
        var body = new VisualElement();

        switch (tile)
        {
            case NoteTile note: BuildNote(body, note); break;
            case CycleGateTile cycle: BuildCycle(body, cycle); break;
            case ProbGateTile prob: BuildProb(body, prob); break;
            case ChannelTile channel: BuildChannel(body, channel); break;
            case ParamTile param: BuildLock(body, param); break;
        }

        return body;
    }

    // What one parameter lock takes hold of.
    //
    // The same list the sound group shows, in the same order and over the same ranges,
    // because it is the same set: every field of the patch is a lock target. Reading a
    // lock against the timbre it colours only works if the two are laid out alike.
    //
    // A row starts released and greyed, and reads out what the channel does without it.
    // Moving its bar is what takes hold of that parameter — there is no separate step
    // for arming one, since a value nobody set is not a lock — and clicking its name
    // lets go again. Whatever is left grey is untouched by this tile, so a lock holding
    // nothing at all does nothing at all, which is what a freshly placed one is.
    //
    // The heading carries the channel, which is the one thing here a lock cannot say for
    // itself: the tile does not hold a number, and a branch lane borrows one from the
    // jump that reaches it. It was the header of a panel of its own, standing under this
    // one and up only while this one was showing a lock — a group of this panel wearing
    // a frame, the same as the sound was.
    void BuildLock(VisualElement body, ParamTile tile)
    {
        body.Add(Controls.Heading("Channel " + _channel));

        for (var target = 0; target < ParamTargets.Count; target++)
            body.Add(new LockRow(this, tile, target));
    }

    // What a lock row shows while nothing holds it: where the channel already stands for
    // an absolute lock, and no shift at all for a relative one. Either way it is what the
    // parameter does if this tile is left alone, which is also where a drag that takes
    // hold of it starts from.
    float Released(ParamTile tile, int target)
      => tile is AbsoluteParamTile
         ? ParamTargets.Get(_editor.Project.Patches[_channel], target) : 0.0f;

    // One channel's timbre, under the tile that names the channel.
    //
    // It was a panel of its own standing under this one, which is a panel that could
    // only ever be up while this one was showing a CHAN tile, headed with the number
    // the row above it already gives. What it is instead is the last group of the tile
    // it belongs to: the cursor is on the cell, and everything the cell decides is on
    // the one panel, in the order the decisions are made — which lane, how it runs,
    // how long it is, and then what it sounds like.
    //
    // What it lists is the parameter lock targets in their own order, and that is the
    // whole patch: seeing what a lock can reach, and where the channel currently sits
    // inside each range, is what makes a lock's amount mean something.
    //
    // The rows read the channel off the tile rather than off a number of their own, so
    // renumbering the CHAN cell moves the whole group to the other channel with nothing
    // rebuilt. A channel with no lane cannot be edited here, which costs nothing: it has
    // no way of sounding either, and its patch is still saved and loaded with the rest.
    VisualElement BuildSound(ChannelTile channel)
    {
        var body = new VisualElement();

        body.Add(Controls.Heading("Sound", follows: true));

        // Every bar sounds a note on the channel once its value has settled, which is
        // the whole of the auditioning: a drag down a bar is one note rather than a
        // burst of them, and a parameter is heard where it was left. There is no button
        // beside it asking for the same note again — a bar that has just been moved has
        // already played it, and one that has not is one nothing was asked about.
        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var index = target;
            body.Add(Controls.Bar(ParamTargets.Name(index), ParamRanges.Of(index),
                                  () => ParamTargets.Get(Patch(channel), index),
                                  value => Set(channel, index, value),
                                  () => Audition(channel)));
        }

        return body;
    }

    // The bank hands out a reference, which is what lets a field be written in place
    // and a lock target be pointed at.
    ref FmPatch Patch(ChannelTile channel)
      => ref _editor.Project.Patches[channel.Channel];

    // Nothing to tell the sequencer either way: it reads the bank afresh every instant,
    // since a lock never outlives one.
    void Set(ChannelTile channel, int target, float value)
      => ParamTargets.Set(ref Patch(channel), target, value);

    void Audition(ChannelTile channel) => _editor.Preview(60, channel.Channel);

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

        body.Add(Controls.Chooser("Channel", channels,
                                  () => channel.Channel - 1,
                                  index => { channel.Channel = index + 1; Touch(); }));

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
    // What a synth parameter's bar covers comes from ParamRanges, which the Lock and
    // Sound panels use. These are the ranges of the sequencer's own
    // numbers, which nothing outside this panel has to know about.

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
    // on; eight is where a switch is still square and a line is still a phrase.
    const int LapsPerRow = 8;

    // The laps of a cycle gate, one switch each.
    //
    // Every lap the tile could have gets a switch, and the ones past the current
    // period are hidden rather than built and torn down: the period is set on a bar
    // standing directly over them, and a run that rebuilt itself as that bar moved
    // would pull the bar out from under the drag that was moving it. Hiding is also
    // what keeps a switch that goes out of reach and comes back — the tile keeps the
    // bit, so the run shows it again exactly as it was left.
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

    // A parameter of a lock, held or not.
    //
    // An element of its own rather than a row plus a list of closures, for the reason
    // ValueBar.SyncAll gives: the tree already knows what is on screen.
    sealed class LockRow : VisualElement
    {
        public LockRow(InspectorPanel panel, ParamTile tile, int target)
        {
            (_panel, _tile, _target) = (panel, tile, target);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.marginBottom = Controls.Gap;

            _caption = Controls.Caption(ParamTargets.Name(target));

            // Every label this UI builds is transparent to the pointer, so that the
            // text on a cell does not eat the click meant for the cell and the readout
            // on a bar does not eat the drag meant for the bar. This one is the
            // exception: it is the control, not a label on one.
            _caption.pickingMode = PickingMode.Position;

            // And it is as tall as the bar beside it rather than as tall as its own
            // line of text, so that what can be clicked is the row the name sits on
            // and not a twelve pixel strip through the middle of it.
            _caption.style.height = Controls.RowHeight;

            _caption.RegisterCallback<PointerDownEvent>(OnCaptionDown);
            _caption.RegisterCallback<PointerUpEvent>(OnCaptionUp);
            _caption.RegisterCallback<PointerCaptureOutEvent>(_ => ReturnKeyboard());
            _caption.RegisterCallback<PointerEnterEvent>(_ => SetHover(true));
            _caption.RegisterCallback<PointerLeaveEvent>(_ => SetHover(false));
            Add(_caption);

            // An absolute lock holds a value the target could hold itself, so its bar
            // is the target's own; a relative one holds a shift, and reads from the
            // middle. Neither depends on whether the row is held, so taking hold of a
            // parameter never rebuilds anything.
            var range = tile is AbsoluteParamTile
              ? ParamRanges.Of(target) : ParamRanges.Relative(target);

            _bar = Controls.Bar(range, Get, Set);
            _bar.style.flexGrow = 1;
            Add(_bar);

            Sync();
        }

        // Pulls the row back in line with the tile, in both of the things it shows: the
        // number, and whether the lock is holding it.
        public void Sync()
        {
            _bar.Sync();
            UpdateAppearance();
        }

        // Private members

        readonly InspectorPanel _panel;
        readonly ParamTile _tile;
        readonly int _target;
        readonly Label _caption;
        readonly ValueBar _bar;

        bool _hover;

        bool Engaged => _tile.IsEngaged(_target);

        float Get() => Engaged ? _tile[_target] : _panel.Released(_tile, _target);

        // Setting a value is what takes hold of the parameter. Nothing else does, which
        // is what makes an untouched row mean untouched.
        void Set(float value)
        {
            _tile.Engage(_target, value);
            UpdateAppearance();
            _panel._editor.Commit();
        }

        // Nothing is decided on the way down. The name is the one control in a panel
        // that is not a Button, and a Button reports on the release for a reason this
        // row is subject to as much as any of them: a column too tall for the screen is
        // dragged by whatever is on it, and on a lock row the name is the only thing
        // there is to drag — the bar beside it keeps its own gesture, so a hand that
        // means to scroll a phone's panel has nowhere else to land. Decided here, that
        // hand let go of every parameter it happened to start on.
        //
        // The pointer is captured for two things. It is what makes the release arrive
        // here even if the hand slid off the name in between; and it is what lets the
        // column cancel the press by taking the capture away, which is exactly how it
        // cancels a click on a button. A pan then ends in a lost capture and no release
        // ever reaches OnCaptionUp. See ScrollStrip.
        void OnCaptionDown(PointerDownEvent e)
        {
            if (e.button != 0) return;

            _caption.CapturePointer(e.pointerId);

            e.StopPropagation();
        }

        // Letting go is the only thing the name does that the bar cannot. Taking hold
        // from it as well is worth having anyway: a parameter is sometimes wanted
        // exactly where it already is, and there is no drag that says so.
        //
        // The capture is the whole of the test. A press that turned into a pan is a
        // press the column is holding, so its release is delivered there and never
        // seen here at all; one still held here is a press that stayed a press.
        void OnCaptionUp(PointerUpEvent e)
        {
            if (!_caption.HasPointerCapture(e.pointerId)) return;

            if (Engaged) _tile.Release(_target); else _tile.Engage(_target, Get());

            Sync();
            _panel._editor.Commit();

            // Which hands the keyboard back, in ReturnKeyboard.
            _caption.ReleasePointer(e.pointerId);

            e.StopPropagation();
        }

        // The name is not focusable, so the press that reached it took the keyboard away
        // from whatever had it and gave it to nothing. Handing it back is what every
        // button on the toolbar does after being pressed, and for the same reason:
        // letting go of a parameter must not quietly be the end of typing notes on the
        // grid.
        //
        // Off the lost capture rather than off the release, the way Controls.Hold ends:
        // that is the one ending a press and a pan have in common, and a press taken
        // away by the column would otherwise leave the keyboard nowhere. Either way it
        // is after the press, which is what makes the focus stick — the focus controller
        // settles a press itself, after this element has seen it, so a Focus from the
        // down handler is simply undone. ValueBar returns the keyboard at the end of a
        // drag for the same reason.
        void ReturnKeyboard() => _panel._editor.View.Focus();

        void SetHover(bool on)
        {
            _hover = on;
            UpdateAppearance();
        }

        // A released row is dimmed whole, bar and all, the way the rails and a note's
        // length label are dimmed: it is the same content, further back. The name lights
        // under the pointer, since clicking it is the one thing here that a greyed
        // control would otherwise say is not available.
        void UpdateAppearance()
        {
            var engaged = Engaged;

            style.opacity = engaged ? 1.0f : Style.DimmedOpacity;
            _caption.style.color = engaged || _hover ? Style.NoteText : Style.Label;
        }
    }
}

} // namespace Jacquard.App
