using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Editing operations, one place for every change the user can make to a score.
//
// The cursor is also the selection: there is no separate notion of a selected
// tile, so what the detail panel shows is whatever the cursor is standing on.
//
// A tile goes down on free ground only, which is what the panel offers one on. A
// stack is therefore written from the top down — the gate first, the note it
// governs in the cell underneath it — rather than by pushing a tile in above one
// that is already there.

public sealed class ScoreEditor
{
    public Project Project { get; set; }
    public Sequencer Sequencer { get; set; }
    public FmSynth Synth { get; set; }
    public ScoreView View { get; set; }

    // Raised after anything that changes the score, so the view can rebuild and
    // the runners can be reconciled.
    public event Action Changed;

    public Score Score => Project.Score;
    public CellRef Cell => Score.At(View.Cursor);
    public Tile Selected => Cell.Tile;

    public Lane SelectedLane
    {
        get
        {
            var cell = Cell;
            if (cell.Lane != null) return cell.Lane;
            // Standing next to a lane still counts, so that a lane can be worked
            // on without hunting for one of its cells.
            foreach (var lane in Score.Lanes)
                if (lane.IsOnRail(View.Cursor)) return lane;
            return null;
        }
    }

    // The channel being worked on, which is what picks the timbre the sound panel
    // edits and the one a preview is heard through. Away from any lane it is
    // channel one, since something has to answer.
    public int Channel => Score.ChannelOf(SelectedLane);

    // Tiles

    // Whether the cursor is standing on ground that will take a tile: a lane's
    // empty step, the cell under a stack, or the terminator, which takes one by
    // growing the lane. This is what the panel asks before offering the tiles.
    public bool CanPlace
    {
        get
        {
            var cell = Cell;
            if (cell.Kind == CellKind.Tile || cell.Kind == CellKind.Head) return false;
            return Score.PlacementLane(View.Cursor, out _, out _) != null;
        }
    }

    // Places whatever the panel hands over. A jump brings its branch lane along,
    // so that one jump to one destination holds at every moment of editing rather
    // than being checked afterwards.
    public void Put(string kind)
    {
        Tile tile = kind switch
        {
            // Holding nothing yet. Which parameters a lock takes is the whole of
            // what there is to say about one, so it is said on the panel rather
            // than guessed at here: a lock that arrived already moving the level
            // would be a choice nobody made.
            "PABS" => new AbsoluteParamTile(),
            "PREL" => new RelativeParamTile(),
            "GCYC" => new CycleGateTile { Period = 4, Index = 1 },
            "GPRB" => new ProbGateTile { Percent = 50 },
            "JUMP" => new JumpTile(),
            _ => new NoteTile { Note = _notePitch, Length = _noteLength }
        };

        if (!Put(tile)) return;

        if (tile is JumpTile jump)
        {
            var below = new GridPoint(Math.Max(1, View.Cursor.X - 4), Score.Height + 1);
            Score.AddBranchLane(jump, below, 4);
        }

        if (tile is NoteTile note) Preview(note.Note);

        Commit();
    }

    // The shorthand for the Note button, since a note is what most cells get and a
    // double click is already on the cell that would take one.
    public void PlaceNote() => Put("NOTE");

    bool Put(Tile tile) => CanPlace && Score.Place(View.Cursor, tile);

    public void Delete()
    {
        var cell = Cell;

        // Deleting a lane's head is how a lane is removed, which also takes any
        // branch lanes it fed.
        if (cell.Kind == CellKind.Head && cell.Lane != null)
        {
            Score.RemoveLane(cell.Lane);
            Commit();
            return;
        }

        if (Score.Remove(View.Cursor)) Commit();
    }

    // Notes

    // A new note arrives at the pitch and length of the last one worked on, rather
    // than at a fixed middle C: notes come in runs that stay in a register and
    // usually keep a length, so the note just written is the better guess at the
    // next one. Nothing is remembered across a load, which is what the defaults are.
    public void RememberNote(NoteTile note)
      => (_notePitch, _noteLength) = (note.Note, note.Length);

    int _notePitch = 60;
    float _noteLength = 1.0f;

    public void Transpose(int semitones)
    {
        if (Selected is not NoteTile note) return;

        note.Note = Math.Clamp(note.Note + semitones, Pitch.Lowest, Pitch.Highest);
        RememberNote(note);
        Preview(note.Note);
        Commit();
    }

    // Lanes

    public void NewChannelLane()
    {
        var point = Score.FindFreeRow(View.Cursor, 16);
        Score.AddLane(point.X, point.Y, new ChannelTile { Channel = Channel }, 16);
        Commit();
    }

    public void ResizeLane(int delta)
    {
        var lane = SelectedLane;
        if (lane == null) return;

        if (delta > 0)
        {
            // Only grow into free ground, so that lanes cannot be made to overlap.
            if (!Score.IsFree(lane.TermPoint.Offset(1, 0), lane)) return;
            lane.AddStep();
        }
        else if (lane.Steps.Count > 1)
        {
            lane.Steps.RemoveAt(lane.Steps.Count - 1);
        }

        Commit();
    }

    // Dragging
    //
    // Where a tile goes is a question the plane can answer directly, so it is
    // asked there: a tile is picked up off its cell and put down on another, and a
    // lane is carried by the head cell that names it. That leaves nothing here to
    // do but apply what the drop resolved to and follow it with the cursor, so
    // that the panel goes on showing what was just moved.

    public void DropTiles(CellRef source, GridPoint target)
    {
        var move = Score.PlanMove(source, target);
        if (!Score.ApplyMove(source, move)) return;

        Commit();
        View.SetCursor(move.Lane.CellPoint(move.Step, move.Depth));
    }

    public void DropLane(Lane lane, GridPoint head)
    {
        if (!Score.MoveLane(lane, head)) return;

        Commit();
        View.SetCursor(head);
    }

    // Playback

    // Sounds a note straight away, so that editing is audible. It goes out with the
    // timbre of the channel the cursor is on, so what a note sounds like here is
    // what it will sound like when the sequence reaches it.
    public void Preview(int note) => Preview(note, Channel);

    public void Preview(int note, int channel)
    {
        if (Synth == null) return;

        var patch = Project.Patches[channel];
        var start = Synth.CurrentSample + Synth.SampleRate / 20;
        var length = 60.0f / Math.Max(Project.Tempo, 1.0f) / 4.0f;

        Synth.Schedule(FmNoteEvent.FromPatch(patch, note, length, start));
    }

    public void Commit()
    {
        Sequencer?.Resync();
        Changed?.Invoke();
    }

    // Keyboard

    // What is left to the keys is moving about and the two edits worth repeating:
    // deleting, and walking a note up or down. Putting a tile down is the panel's,
    // so that there is one way of doing it and it is the one on screen.
    public bool HandleKey(KeyDownEvent evt)
    {
        var shift = evt.shiftKey;
        var command = evt.actionKey || evt.commandKey || evt.ctrlKey;

        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow: View.MoveCursor(-1, 0); return true;
            case KeyCode.RightArrow: View.MoveCursor(1, 0); return true;

            case KeyCode.UpArrow:
                if (shift) Transpose(command ? 12 : 1);
                else View.MoveCursor(0, -1);
                return true;

            case KeyCode.DownArrow:
                if (shift) Transpose(command ? -12 : -1);
                else View.MoveCursor(0, 1);
                return true;

            case KeyCode.Delete:
            case KeyCode.Backspace:
                Delete();
                return true;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                if (Selected is NoteTile note) Preview(note.Note);
                return true;
        }

        switch (evt.character)
        {
            case '#': case '+': Transpose(1); return true;
            case '-': Transpose(-1); return true;
        }

        return false;
    }
}

} // namespace Jacquard.App
