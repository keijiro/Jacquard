using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Editing operations, one place for every change the user can make to a score.
//
// The cursor is also the selection: there is no separate notion of a selected
// tile, so what the detail panel shows is whatever the cursor is standing on.
//
// Placing a tile on an occupied cell inserts rather than overwrites, because a
// stack is written from the top down and a gate usually arrives after the note it
// is going to govern. Typing a note onto an existing note is the one case that
// edits in place, since that plainly means "this pitch, not that one".

public sealed class ScoreEditor
{
    public Project Project { get; set; }
    public Sequencer Sequencer { get; set; }
    public FmSynth Synth { get; set; }
    public ScoreView View { get; set; }

    // Raised after anything that changes the score, so the view can rebuild and
    // the runners can be reconciled.
    public event Action Changed;

    public int Octave { get; set; } = 4;

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

    public void PutNote(int note)
    {
        if (Selected is NoteTile existing)
            existing.Note = note;
        else if (!Put(new NoteTile { Note = note }))
            return;

        Octave = Pitch.ToOctave(note);
        Preview(note);
        Commit();
    }

    // Places whatever the palette hands over. A jump brings its branch lane along,
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
            _ => new NoteTile { Note = (Octave + 1) * 12 }
        };

        if (!Put(tile)) return;

        if (tile is JumpTile jump)
        {
            var below = new GridPoint(Math.Max(1, View.Cursor.X - 4), Score.Height + 1);
            Score.AddBranchLane(jump, below, 4);
        }

        Commit();
    }

    bool Put(Tile tile)
    {
        var cell = Cell;

        if (cell.IsFlowCell && cell.Kind == CellKind.Head) return false;

        return cell.Kind == CellKind.Tile
          ? Score.Insert(View.Cursor, tile)
          : Score.Place(View.Cursor, tile);
    }

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

    public void Transpose(int semitones)
    {
        if (Selected is not NoteTile note) return;

        note.Note = Math.Clamp(note.Note + semitones, Pitch.Lowest, Pitch.Highest);
        Octave = Pitch.ToOctave(note.Note);
        Preview(note.Note);
        Commit();
    }

    public void SetOctave(int octave)
    {
        Octave = Math.Clamp(octave, 0, 8);

        if (Selected is not NoteTile note) return;

        note.Note = (Octave + 1) * 12 + note.Note % 12;
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

    // Moves a lane bodily, which is also how the execution order is changed: the
    // runner of a lane sitting lower down runs later and can overwrite what the
    // ones above it did.
    public void MoveLane(int dx, int dy)
    {
        var lane = SelectedLane;
        if (lane == null) return;

        var (x, y) = (lane.X + dx, lane.Y + dy);
        if (x < 1 || y < 0) return;

        lane.X = x;
        lane.Y = y;

        // The cursor travels with the lane, so repeated nudges stay on target.
        View.SetCursor(View.Cursor.Offset(dx, dy));
        Commit();
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

    // Note entry is the fast path: the letter keys write a note at the cursor and
    // step to the right, the way a tracker behaves.
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

        var character = evt.character;

        if (character >= 'a' && character <= 'g' || character >= 'A' && character <= 'G')
        {
            var name = char.ToUpperInvariant(character).ToString() + Octave;
            if (Pitch.TryParse(name, out var pitch))
            {
                PutNote(pitch);
                View.MoveCursor(1, 0);
            }
            return true;
        }

        if (character >= '0' && character <= '8')
        {
            SetOctave(character - '0');
            return true;
        }

        switch (character)
        {
            case '#': case '+': Transpose(1); return true;
            case '-': Transpose(-1); return true;
        }

        return false;
    }
}

} // namespace Jacquard.App
