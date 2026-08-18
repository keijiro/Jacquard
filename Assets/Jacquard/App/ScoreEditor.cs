using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// What can be asked for by name, which is only the tiles a user puts down: the
// terminator is implied and a jump destination arrives with the jump that reaches
// it, so neither is ever asked for.
//
// This is deliberately not the file format's four character token. A token is a
// spelling for a file, and the panel that offers these has to name them in words
// instead — so the two are kept apart rather than one standing in for the other.
public enum TileKind
{
    Note, AbsoluteLock, RelativeLock, CycleGate, ChanceGate, Jump
}

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

    // Whether the score is being held still.
    //
    // It is while a load is waiting for the lap line: the switch is measured on the lane
    // the runners are playing, so an edit that moved a lane or took one away would move
    // the line under it. Nothing about the mix is held — the sound panel, the sends, the
    // channels and the live effects all go on working, since what they change is how the
    // piece is heard rather than where it ends.
    //
    // Everything that writes the score asks this. The panels that draw the score dim
    // themselves and stop taking presses, and this stands behind them so that no path
    // reaches the score by another route.
    public bool Locked { get; set; }

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
    public void Put(TileKind kind)
    {
        if (Locked) return;

        Tile tile = kind switch
        {
            // Holding nothing yet. Which parameters a lock takes is the whole of
            // what there is to say about one, so it is said on the panel rather
            // than guessed at here: a lock that arrived already moving the level
            // would be a choice nobody made.
            TileKind.AbsoluteLock => new AbsoluteParamTile(),
            TileKind.RelativeLock => new RelativeParamTile(),
            TileKind.CycleGate => new CycleGateTile { Period = 4, Pattern = "1000" },
            TileKind.ChanceGate => new ProbGateTile { Percent = 50 },
            TileKind.Jump => new JumpTile(),
            _ => new NoteTile { Note = _notePitch, Length = _noteLength }
        };

        if (!Put(tile)) return;

        if (tile is JumpTile jump)
        {
            // Below everything, and back a little from the jump so the link has
            // somewhere to travel. Held at the score's own left edge rather than at the
            // plane's: the margin out there is what a lane is carried into by hand, and
            // a branch lane dropped in it would push the whole score aside to make
            // room it never asked for.
            var below = new GridPoint(Math.Max(Score.MinX + 1, View.Cursor.X - 4),
                                      Score.Height + 1);
            Score.AddBranchLane(jump, below, 4);
        }

        if (tile is NoteTile note) Preview(note.Note);

        Commit();
    }

    bool Put(Tile tile) => CanPlace && Score.Place(View.Cursor, tile);

    public void Delete()
    {
        if (Locked) return;

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

    // Copying
    //
    // A double click on the plane means one of two things, and the cell says which:
    // on a tile it takes a copy of that tile and the stack under it, on ground that
    // would take a tile it puts the last copy down. So the gesture that used to
    // write one note now writes the shape that was hardest to write — a chord, or a
    // gate with what it governs — and the Tile panel is left as the one way of
    // asking for a tile by name.
    //
    // Nothing is offered on the chrome to go with it. What is copied is a position
    // on the plane and what is copied onto is another one, which is a thing to point
    // at rather than a button to press, and the plane already answers positions.
    //
    // An empty copy is not a paste of nothing: with nothing taken yet the gesture
    // does nothing at all, rather than falling back on the note it used to write.
    // One gesture reading two ways depending on what was done ten minutes ago is a
    // gesture nobody can aim.

    // A third reading on the one cell that could not take either of the other two, for
    // which see ToggleChannel. A JDST head has no channel and falls through to the paste,
    // which refuses a head cell, so it goes on doing nothing.
    public void DoubleClick()
    {
        if (Cell.Kind == CellKind.Head && Cell.Lane?.Channel != null) ToggleChannel();
        else if (Cell.Kind == CellKind.Tile) CopyStack();
        else PasteStack();
    }

    // Starts or stops a lane, which is the one thing on the plane that is played rather
    // than written, and the reason this gesture reads a third way on the CHAN cell.
    //
    // The consistency being spent is real and it is being spent knowingly. What the
    // gesture asks everywhere else is "this cell, then that one" — a copy and the place
    // it lands — and a CHAN head can be neither end of that: a flow tile has no copy, and
    // a head is not ground a tile can go on. So the gesture does nothing at all here
    // today, and what goes in the empty hand is the one control worth reaching for
    // without looking, at the speed a hand works while a piece is playing.
    //
    // Nothing about when it takes effect is decided here. The switch is written and the
    // sequencer reads it where it matters — at the end of the lane for a stop, on the turn
    // of the piece for a start — so this is only ever the writing down.
    public void ToggleChannel()
    {
        var channel = Cell.Lane?.Channel;
        if (Locked || channel == null) return;

        channel.Enabled = !channel.Enabled;
        Commit();
    }

    public void CopyStack()
    {
        _copiedCells.Clear();

        var copies = Score.CopyStack(View.Cursor, _copiedCells);
        if (copies == null) return;

        _copied = copies;

        // The score is unchanged, so there is nothing to commit and nothing for the
        // runners to be reconciled with. What the plane shows is the cells that were
        // taken, which is also what says a jump in the stack was left behind.
        View.Flash(_copiedCells);
    }

    public void PasteStack()
    {
        if (Locked || _copied == null || !CanPlace) return;

        // Copied again on the way out as well as on the way in: what is held has to
        // be untouched by an edit of the tiles it came from, and two pastes have to
        // be two stacks rather than one stack written in two places.
        var tiles = new List<Tile>();
        foreach (var tile in _copied) tiles.Add(tile.Copy());

        var point = View.Cursor;
        var lane = Score.PlacementLane(point, out var step, out _);
        if (!Score.PlaceStack(point, tiles)) return;

        Commit();

        // Asked of the lane after the commit, the way a drop is: committing can move
        // the score bodily, so the cell worked out before it is a cell on the score
        // as it used to sit.
        View.SetCursor(lane.CellPoint(step, lane.Steps[step].Depth - tiles.Count));

        // A step is one instant, so the notes in a stack are a chord and are
        // previewed as one.
        foreach (var tile in tiles) if (tile is NoteTile note) Preview(note.Note);
    }

    List<Tile> _copied;
    readonly List<GridPoint> _copiedCells = new();

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
        if (Locked || Selected is not NoteTile note) return;

        note.Note = Math.Clamp(note.Note + semitones, Pitch.Lowest, Pitch.Highest);
        RememberNote(note);
        Preview(note.Note);
        Commit();
    }

    // Lanes

    public void NewChannelLane()
    {
        if (Locked) return;

        // The row is searched for one column right of the cursor, because a row's
        // position is that of its first step and the head sits to the left of it:
        // asked at the cursor, the CHAN lands a column short of where it was asked
        // for, and the cell a hand pointed at is the cell it means.
        var point = Score.FindFreeRow(View.Cursor.Offset(1, 0), 16);
        var lane = Score.AddLane(point.X, point.Y, new ChannelTile { Channel = Channel }, 16);
        Commit();

        // The cursor follows the lane wherever the search had to put it. Asked for a
        // row that is taken, the lane lands further down, and a cursor left behind on
        // the old cell would say the new lane is somewhere it is not — the next thing
        // typed would go into whatever the cursor is still standing on. Read after the
        // commit, since committing can move the score bodily, the way a paste does.
        View.SetCursor(lane.HeadPoint);
    }

    public void ResizeLane(int delta)
    {
        var lane = SelectedLane;
        if (Locked || lane == null) return;

        if (delta > 0)
        {
            // Only grow into free ground, so that lanes cannot be made to overlap.
            if (!Score.HasRoomToGrow(lane)) return;
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
    //
    // Both follow the cursor by asking a lane where it now is, and neither by reusing
    // the cell the drop was aimed at. Committing can move the score bodily — a lane put
    // down to the left of the others takes the plane with it — so a coordinate worked
    // out before the commit is a coordinate about the score as it used to sit.

    public void DropTiles(CellRef source, GridPoint target)
    {
        if (Locked) return;

        var move = Score.PlanMove(source, target);
        if (!Score.ApplyMove(source, move)) return;

        Commit();
        View.SetCursor(move.Lane.CellPoint(move.Step, move.Depth));
    }

    public void DropLane(Lane lane, GridPoint head)
    {
        if (Locked || !Score.MoveLane(lane, head)) return;

        Commit();
        View.SetCursor(lane.HeadPoint);
    }

    // Playback

    // Sounds a note straight away, so that editing is audible. It goes out with the
    // timbre of the channel the cursor is on, so what a note sounds like here is
    // what it will sound like when the sequence reaches it.
    //
    // Which is why the pitch goes through the same two steps the sequencer puts it
    // through — the channel's transpose, then the scale — and why a note written
    // outside the scale is heard where it will land rather than where it was typed.
    // The locks and the live effects are still not here: those belong to a step and
    // to a hand on a button, and neither is what a cell is being asked about.
    //
    // Two ways in, and the difference is who asked. An edit sounding a note about itself
    // is the auditioning the System panel switches off, and goes through Preview. A note
    // asked for outright — the Return key, which does nothing else — is not a remark
    // about an edit and is not a setting, so it goes straight to Sound and comes out
    // whatever the switch says. A player who has turned the auditioning off still has a
    // way to hear the cell under the cursor, which is the whole reason the two are told
    // apart.
    public void Preview(int note) => Preview(note, Channel);

    public void Preview(int note, int channel)
    {
        if (Audition.On) Sound(note, channel);
    }

    public void Sound(int note) => Sound(note, Channel);

    public void Sound(int note, int channel)
    {
        if (Synth == null) return;

        var patch = Project.Patches[channel];
        var start = Synth.CurrentSample + Synth.MinimumLead + Synth.SampleRate / 20;
        var length = 60.0f / Math.Max(Project.Tempo, 1.0f) / 4.0f;

        Synth.Schedule(FmNoteEvent.FromPatch(patch, Project.SoundingPitch(patch, note),
                                             length, start));
    }

    public void Commit()
    {
        Sequencer?.Resync();
        Changed?.Invoke();
    }

    // A whole new project rather than an edit of the one that was here.
    //
    // Nothing is reconciled, which is the whole difference from a commit: the runners
    // are already playing what this brings — they were handed it at the lap line, ahead
    // of the clock — so there is no score to reconcile them with and Resync would only
    // find them matching what they already hold. What is left is to point the plane and
    // the panels at it, which is what Changed does.
    public void Adopt(Project project)
    {
        Project = project;
        View.Score = project.Score;
        Changed?.Invoke();
    }

    // Keyboard

    // What is left to the keys is moving about and the two edits worth repeating:
    // deleting, and walking a note up or down. Putting a tile down is the panel's,
    // so that there is one way of doing it and it is the one on screen.
    public bool HandleKey(KeyDownEvent evt)
    {
        // Every key here either edits the score or moves the cursor the panels that
        // edit it are reading, so a held score takes none of them. Play and stop are
        // settled before this is asked and go on working.
        if (Locked) return false;

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
                // Sound and not Preview: this key does nothing but ask for the note, so
                // it is the one audition the System panel's switch does not govern.
                if (Selected is NoteTile note) Sound(note.Note);
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
