using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The detail window for whatever the cursor is on.
//
// sequencer.md draws the line here: a cell carries the kind of tile and the figure
// you need in order to read the score, and everything else — which parameter a lock
// points at, how far it moves it, the exact percentage behind a pie chart — is set
// in a window of its own rather than crammed into 34 pixels.

sealed class InspectorPanel
{
    public VisualElement Root { get; }

    public InspectorPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Tile", null);
        Root.style.right = 12;
        Root.style.top = 12;

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

        if (!force && tile == _tile && lane == _lane)
        {
            // The same tile, but its values may have changed from the grid — a
            // transpose, a note typed over another — so the bars still have to be
            // pulled back in line with it.
            ValueBar.SyncAll(_body);
            return;
        }

        (_tile, _lane) = (tile, lane);

        _body.Clear();
        Build(tile, lane);
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly VisualElement _body;

    Tile _tile;
    Lane _lane;

    void Build(Tile tile, Lane lane)
    {
        _body.Add(Controls.Caption(Describe(tile)));
        _body.Add(Controls.Divider());

        switch (tile)
        {
            case NoteTile note: BuildNote(note); break;
            case ParamTile param: BuildLock(param); break;
            case CycleGateTile cycle: BuildCycle(cycle); break;
            case ProbGateTile prob: BuildProb(prob); break;
            case ChannelTile channel: BuildChannel(channel); break;
            case JumpTile jump: BuildJump(jump); break;
            case JumpDestTile: BuildJumpDest(lane); break;
            case TerminatorTile: _body.Add(Controls.Hint(TerminatorHint)); break;
            default: _body.Add(Controls.Hint(EmptyHint)); break;
        }

        if (lane == null) return;

        _body.Add(Controls.Divider());
        BuildLane(lane);
    }

    static string Describe(Tile tile) => tile switch
    {
        null => "Empty cell",
        NoteTile note => "Note " + note.Token,
        AbsoluteParamTile => "PABS  absolute lock",
        RelativeParamTile => "PREL  relative lock",
        CycleGateTile => "GCYC  cycle gate",
        ProbGateTile => "GPRB  probability gate",
        ChannelTile => "CHAN  channel start",
        TerminatorTile => "TERM  lane end",
        JumpTile => "JUMP  branch out",
        JumpDestTile => "JDST  branch target",
        _ => tile.Token
    };

    void BuildNote(NoteTile note)
    {
        // The bar spells the pitch as well as numbering it, so there is no longer a
        // row beside it saying what 60 means.
        _body.Add(Controls.Bar("Pitch", PitchRange, () => note.Note,
                               value => { note.Note = Mathf.Clamp(Mathf.RoundToInt(value),
                                                                  Pitch.Lowest, Pitch.Highest);
                                          _editor.Preview(note.Note);
                                          Touch(); }));

        // Length is in steps, so what it means in real time depends on the
        // channel's division and the project tempo.
        _body.Add(Controls.Bar("Length", LengthRange, () => note.Length,
                               value => { note.Length = Mathf.Clamp(value, 0.25f, 64.0f);
                                          Touch(); }));

        _body.Add(Controls.Hint("Length counts steps. Type a letter on the grid to " +
                                "change the pitch, shift+arrows to transpose."));
    }

    void BuildLock(ParamTile param)
    {
        _body.Add(Controls.Chooser("Target", ParamTargets.Names,
                                   () => param.Target,
                                   index => { param.Target = index;
                                              if (param is AbsoluteParamTile)
                                                  param.Amount = ParamTargets.Default(index);
                                              Refresh(true); }));

        // An absolute lock holds a value the target could hold itself, so its bar is
        // the target's own; a relative one holds a shift, and reads from the middle.
        var absolute = param is AbsoluteParamTile;

        _body.Add(Controls.Bar(absolute ? "Value" : "Amount",
                               absolute ? ParamRanges.Of(param.Target)
                                        : ParamRanges.Relative(param.Target),
                               () => param.Amount,
                               value => { param.Amount = value; Touch(); }));

        _body.Add(Controls.Hint(ReachHint(param)));
    }

    // A lock always takes the whole channel and never outlives its own instant, so
    // what is left to say is how far down the reading of this instant has yet to
    // go — which is what the position decides.
    string ReachHint(ParamTile param)
    {
        var cell = _editor.Cell;
        var noteBelow = false;

        if (cell.Kind == CellKind.Tile)
        {
            var tiles = cell.Lane.Steps[cell.Step].Tiles;

            for (var d = cell.Depth + 1; d < tiles.Count; d++)
                if (tiles[d] is NoteTile) noteBelow = true;
        }

        var reach = noteBelow
          ? "The notes below it in this step take it, and so does any lane further " +
            "down the plane sounding on this channel at this instant."
          : "No note below it in this step, so it reaches only the lanes further " +
            "down the plane sounding on this channel at this instant.";

        var kind = param is AbsoluteParamTile
          ? " An absolute lock sets the value." : " A relative lock shifts it.";

        return reach + kind + " Either way it is gone by the next step.";
    }

    void BuildCycle(CycleGateTile cycle)
    {
        _body.Add(Controls.Bar("Period", PeriodRange, () => cycle.Period,
                               value => { cycle.Period = Mathf.RoundToInt(value);
                                          Touch(); }));

        _body.Add(Controls.Bar("Fires on", FiresOnRange, () => cycle.Index,
                               value => { cycle.Index = Mathf.RoundToInt(value);
                                          Touch(); }));

        _body.Add(Controls.Hint("Fires on one lap out of the period. 2 to 8, which " +
                                "is how many boxes fit across a cell."));
    }

    void BuildProb(ProbGateTile prob)
    {
        _body.Add(Controls.Bar("Chance", ChanceRange, () => prob.Percent,
                               value => { prob.Percent = value; Touch(); }));

        _body.Add(Controls.Hint("Percent. Whatever it is, the wedge shows it."));
    }

    void BuildChannel(ChannelTile channel)
    {
        _body.Add(Controls.Bar("Channel", ChannelRange, () => channel.Channel,
                               value => { channel.Channel = Mathf.RoundToInt(value);
                                          Touch(); }));

        var divisions = new List<string>();
        foreach (var d in ChannelTile.Divisions) divisions.Add("1/" + d);

        _body.Add(Controls.Chooser("Step", divisions,
                                   () => System.Array.IndexOf(ChannelTile.Divisions,
                                                              channel.Division),
                                   index => { channel.Division = ChannelTile.Divisions[index];
                                              Touch(); }));

        _body.Add(Controls.Hint("One step is this note value. The channel number also " +
                                "picks the timbre, set in the Sound window. Lanes on " +
                                "the same channel run together and share that sound; " +
                                "the higher CHAN goes first, so a lower one can " +
                                "overwrite it."));
    }

    void BuildJump(JumpTile jump)
    {
        var destination = _editor.Score.DestinationOf(jump);

        _body.Add(Controls.Hint(destination == null
          ? "No destination, which should not happen."
          : "Hands over to the JDST lane at " +
            destination.HeadPoint + ". Put a gate above this cell to make the " +
            "jump conditional; without one it only lengthens the lane."));
    }

    void BuildJumpDest(Lane lane)
    {
        var source = lane?.JumpSource == null
          ? null : _editor.Score.Locate(lane.JumpSource);

        _body.Add(Controls.Hint(source.HasValue
          ? "Entered only from the JUMP at " + source.Value +
            ", and so sounds on channel " + _editor.Score.ChannelOf(lane) +
            " with that channel's timbre and step length. Its own TERM returns to " +
            "the channel start, not to here."
          : "Nothing jumps here, so this lane never sounds. That is allowed."));
    }

    void BuildLane(Lane lane)
    {
        _body.Add(Controls.Caption("Lane at " + lane.HeadPoint));

        // The one number here that is still stepped rather than scrubbed: a step is a
        // cell, growing only happens where there is free ground for one, and a refused
        // step is something to see one at a time rather than to drag through.
        _body.Add(Controls.Stepper("Steps", () => lane.Steps.Count,
                                   value => _editor.ResizeLane(
                                     value > lane.Steps.Count ? 1 : -1), 1, "0"));

        void Move(int dx, int dy)
        {
            _editor.MoveLane(dx, dy);
            Refresh(true);
        }

        var move = Controls.Row();
        move.Add(Controls.Caption("Move"));
        move.Add(Controls.Push("left", () => Move(-1, 0), 40));
        move.Add(Controls.Push("right", () => Move(1, 0), 44));
        _body.Add(move);

        var vertical = Controls.Row();
        vertical.Add(Controls.Caption(""));
        vertical.Add(Controls.Push("up", () => Move(0, -1), 40));
        vertical.Add(Controls.Push("down", () => Move(0, 1), 44));
        _body.Add(vertical);

        _body.Add(Controls.Hint("Moving a lane down makes its runner go later, " +
                                "which is how an accent lane gets to overwrite."));
    }

    // Redraws the score for a change made here, without rebuilding this panel.
    void Touch()
    {
        _editor.Commit();
        Refresh();
    }

    // Parameter ranges
    //
    // What a lock's bar covers comes from ParamRanges, since that is the synth's own
    // idea of where a parameter is useful. These are the ranges of the sequencer's own
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
    static readonly ValueBar.Range LengthRange =
      ValueBar.Amount(0.25f, 8.0f, snap: 0.25f);

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

    const string TerminatorHint =
      "Placed automatically past the last step. Reaching it returns to the CHAN " +
      "the runner started from, even on a branch lane.";

    const string EmptyHint =
      "Type a note letter, or use the palette, to put something here. On a lane's " +
      "TERM cell that adds a step.";
}

} // namespace Jacquard.App
