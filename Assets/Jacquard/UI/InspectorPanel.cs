using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The detail window for whatever the cursor is on.
//
// sequencer.md draws the line here: a cell carries the kind of tile and the figure
// you need in order to read the score, and everything else — which parameter a lock
// points at, how far it moves it, the exact percentage behind a pie chart — is set
// in a window of its own rather than crammed into thirty pixels.
//
// It is also where a tile is put down and taken away, since the cursor is already
// the answer to where: a cell that will take a tile offers the tiles instead of a
// description of nothing, ground with no lane on it offers a lane, and everything
// else offers what it is and a way to remove it. There is no palette elsewhere on
// the screen, so a button that is not on this panel cannot apply to this cell.

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

        if (!force && tile == _tile && lane == _lane && place == _place)
        {
            // The same tile, but its values may have changed from elsewhere — a
            // transpose from the keys, a load — so the bars still have to be pulled
            // back in line with it.
            ValueBar.SyncAll(_body);
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

    void Build(Tile tile, Lane lane)
    {
        _title.text = Title(tile);

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

    // Adds a section under a rule of its own, unless it turned out to hold nothing:
    // a lock keeps everything it owns on the Lock panel, and a rule with a gap under
    // it would be a line with nothing to separate.
    void Section(VisualElement content)
    {
        if (content.childCount == 0) return;
        _body.Add(Controls.Divider());
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

    // The controls belonging to the tile itself. A lock has none: which parameters
    // it holds is a list of every target rather than a row or two, so it is the Lock
    // panel's, and a jump has nothing to set at all.
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

        var row = Controls.Row();
        row.Add(Controls.Push(head ? "Delete lane" : "Delete",
                              () => Act(_editor.Delete), head ? 74 : 54));
        return row;
    }

    void BuildNote(VisualElement body, NoteTile note)
    {
        // The bar spells the pitch as well as numbering it, so there is no longer a
        // row beside it saying what 60 means.
        //
        // The note is heard where the drag ends rather than at every semitone it
        // crosses: a scrub over an octave is twelve notes on top of each other and
        // none of them the one being chosen. A typed pitch sounds straight away, since
        // it never passed through the eleven others.
        body.Add(Controls.Bar("Pitch", PitchRange, () => note.Note,
                              value => { note.Note = Mathf.Clamp(Mathf.RoundToInt(value),
                                                                 Pitch.Lowest, Pitch.Highest);
                                         _editor.RememberNote(note);
                                         Touch(); },
                              () => _editor.Preview(note.Note)));

        // Length is in steps, so what it means in real time depends on the
        // channel's division, its gate ratio and the project tempo.
        body.Add(Controls.Bar("Length", LengthRange, () => note.Length,
                              value => { note.Length = Mathf.Clamp(value, 0.25f, 64.0f);
                                         _editor.RememberNote(note);
                                         Touch(); }));
    }

    void BuildCycle(VisualElement body, CycleGateTile cycle)
    {
        body.Add(Controls.Bar("Period", PeriodRange, () => cycle.Period,
                              value => { cycle.Period = Mathf.RoundToInt(value);
                                         Touch(); }));

        body.Add(Controls.Bar("Fires on", FiresOnRange, () => cycle.Index,
                              value => { cycle.Index = Mathf.RoundToInt(value);
                                         Touch(); }));
    }

    void BuildProb(VisualElement body, ProbGateTile prob)
      => body.Add(Controls.Bar("Chance", ChanceRange, () => prob.Percent,
                               value => { prob.Percent = value; Touch(); }));

    void BuildChannel(VisualElement body, ChannelTile channel)
    {
        body.Add(Controls.Bar("Channel", ChannelRange, () => channel.Channel,
                              value => { channel.Channel = Mathf.RoundToInt(value);
                                         Touch(); }));

        var divisions = new List<string>();
        foreach (var d in ChannelTile.Divisions) divisions.Add("1/" + d);

        body.Add(Controls.Chooser("Step", divisions,
                                  () => System.Array.IndexOf(ChannelTile.Divisions,
                                                             channel.Division),
                                  index => { channel.Division = ChannelTile.Divisions[index];
                                             Touch(); }));
    }

    VisualElement BuildLane(Lane lane)
    {
        var body = new VisualElement();

        body.Add(Controls.Heading("Lane"));

        // The one number here that is still stepped rather than scrubbed: a step is a
        // cell, growing only happens where there is free ground for one, and a refused
        // step is something to see one at a time rather than to drag through.
        body.Add(Controls.Stepper("Steps", () => lane.Steps.Count,
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

    // A MIDI note number, read out as the name it spells so that a pitch is legible
    // without counting semitones. Dragging covers the octaves music is actually
    // written in, which is what makes a semitone a couple of pixels of travel rather
    // than one; the rest can still be typed, as far as the plane's own ends.
    static readonly ValueBar.Range PitchRange =
      ValueBar.Integer(24.0f, 108.0f,
                       value => Mathf.RoundToInt(value) + " " +
                                Pitch.ToName(Mathf.RoundToInt(value)));

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

    static readonly ValueBar.Range ChannelRange = ValueBar.Integer(1.0f, PatchBank.Channels);

    static readonly ValueBar.Range PeriodRange =
      ValueBar.Integer(CycleGateTile.MinPeriod, CycleGateTile.MaxPeriod);

    // The lap the gate fires on. Its top end is the longest period there can be rather
    // than the one this tile is on, since the tile clamps an index its own period
    // cannot reach and the bar is pulled back to whatever it took.
    static readonly ValueBar.Range FiresOnRange =
      ValueBar.Integer(1.0f, CycleGateTile.MaxPeriod);

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
}

} // namespace Jacquard.App
