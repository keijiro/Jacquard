using System.Collections.Generic;
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

    // Rebuilt only when the cursor has moved onto something else: a stepper that
    // edits the tile in place would otherwise pull itself out from under the click
    // that drove it.
    public void Refresh(bool force = false)
    {
        var tile = _editor.Selected;
        var lane = _editor.SelectedLane;

        if (!force && tile == _tile && lane == _lane) return;

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
        _body.Add(Controls.Stepper("Pitch", () => note.Note,
                                   value => { note.Note = (int)value;
                                              _editor.Preview(note.Note);
                                              Touch(); },
                                   1, "0"));

        var name = Controls.Row();
        name.Add(Controls.Caption("Name"));
        var label = Controls.Value(Pitch.ToName(note.Note));
        label.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
        name.Add(label);
        _body.Add(name);

        // Length is in steps, so what it means in real time depends on the
        // channel's division and the project tempo.
        _body.Add(Controls.Stepper("Length", () => note.Length,
                                   value => { note.Length = UnityEngine.Mathf.Clamp(value, 0.25f, 64.0f);
                                              Touch(); },
                                   0.25f));

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

        _body.Add(Controls.Stepper(param is AbsoluteParamTile ? "Value" : "Amount",
                                   () => param.Amount,
                                   value => { param.Amount = value; Touch(); },
                                   ParamTargets.Increment(param.Target) * 5.0f));

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
        _body.Add(Controls.Stepper("Period", () => cycle.Period,
                                   value => { cycle.Period = (int)value;
                                              Touch(); }, 1, "0"));

        _body.Add(Controls.Stepper("Fires on", () => cycle.Index,
                                   value => { cycle.Index = (int)value;
                                              Touch(); }, 1, "0"));

        _body.Add(Controls.Hint("Fires on one lap out of the period. 2 to 8, which " +
                                "is how many boxes fit across a cell."));
    }

    void BuildProb(ProbGateTile prob)
    {
        _body.Add(Controls.Stepper("Chance", () => prob.Percent,
                                   value => { prob.Percent = value; Touch(); }, 5, "0.#"));

        _body.Add(Controls.Hint("Percent. Whatever it is, the wedge shows it."));
    }

    void BuildChannel(ChannelTile channel)
    {
        _body.Add(Controls.Stepper("Channel", () => channel.Channel,
                                   value => { channel.Channel = (int)value;
                                              Touch(); }, 1, "0"));

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

    const string TerminatorHint =
      "Placed automatically past the last step. Reaching it returns to the CHAN " +
      "the runner started from, even on a branch lane.";

    const string EmptyHint =
      "Type a note letter, or use the palette, to put something here. On a lane's " +
      "TERM cell that adds a step.";
}

} // namespace Jacquard.App
