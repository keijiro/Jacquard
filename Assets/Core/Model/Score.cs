using System.Collections.Generic;
using System.Linq;

namespace Jacquard {

// What a grid cell turns out to be. Rail means a cell on a lane's own rail row
// whose step holds nothing: that is where the pass-through marker shows up.

public enum CellKind { Empty, Rail, Head, Term, Tile }

public readonly struct CellRef
{
    public readonly CellKind Kind;
    public readonly Lane Lane;
    public readonly int Step;
    public readonly int Depth;
    public readonly Tile Tile;

    public CellRef(CellKind kind, Lane lane, int step, int depth, Tile tile)
      => (Kind, Lane, Step, Depth, Tile) = (kind, lane, step, depth, tile);

    public static readonly CellRef Empty = new CellRef(CellKind.Empty, null, 0, 0, null);

    public bool IsFlowCell => Kind == CellKind.Head || Kind == CellKind.Term;
}

// A tile drag resolved against the score: the stack it would land in, where in
// that stack, and how many tiles travel with it. It is worked out apart from the
// move itself so that the answer shown while a drag is in the air and the answer
// the drop acts on are the same one.

public readonly struct TileMove
{
    public readonly Lane Lane;
    public readonly int Step;
    public readonly int Depth;
    public readonly int Count;

    public TileMove(Lane lane, int step, int depth, int count)
      => (Lane, Step, Depth, Count) = (lane, step, depth, count);

    public bool IsValid => Lane != null;

    public static readonly TileMove None = default;
}

// One grid plane holding every lane.
//
// Channels are not split across planes: several CHAN lanes on the same channel
// simply sit next to each other here.

public sealed class Score
{
    public List<Lane> Lanes { get; } = new();

    // Lookup

    // Resolves a cell the same way the mockup does: head, terminator, then the
    // step stack, taking the first lane that claims the position.
    public CellRef At(GridPoint point)
    {
        foreach (var lane in Lanes)
        {
            if (point == lane.HeadPoint)
                return new CellRef(CellKind.Head, lane, -1, 0, lane.Head);

            if (point == lane.TermPoint)
                return new CellRef(CellKind.Term, lane, lane.Steps.Count, 0, Terminator);

            var step = point.X - lane.X;
            var depth = point.Y - lane.Y;

            if (step < 0 || step >= lane.Steps.Count || depth < 0) continue;

            var tile = lane.Steps[step].At(depth);
            if (tile != null)
                return new CellRef(CellKind.Tile, lane, step, depth, tile);

            if (depth == 0)
                return new CellRef(CellKind.Rail, lane, step, 0, null);
        }

        return CellRef.Empty;
    }

    // Ground no lane has a claim on. One lane can be excused, which is what lets a
    // lane be asked about ground it is standing on itself.
    public bool IsFree(GridPoint point, Lane except = null)
    {
        foreach (var lane in Lanes)
            if (lane != except && lane.Owns(point)) return false;

        return true;
    }

    // Whether a lane can take one more step. Growing moves the terminator a column
    // to the right, so what has to be free is the cell it moves into; the cell it
    // vacates is the lane's own rail either way.
    public bool HasRoomToGrow(Lane lane)
      => IsFree(lane.TermPoint.Offset(1, 0), lane);

    // Where a tile currently sits, which is what the jump links need in order to
    // be drawn. Cheap enough to search for: a score holds tens of lanes.
    public GridPoint? Locate(Tile tile)
    {
        foreach (var lane in Lanes)
        {
            if (lane.Head == tile) return lane.HeadPoint;

            for (var i = 0; i < lane.Steps.Count; i++)
            {
                var depth = lane.Steps[i].Tiles.IndexOf(tile);
                if (depth >= 0) return lane.CellPoint(i, depth);
            }
        }
        return null;
    }

    public Lane LaneOf(Tile tile)
    {
        foreach (var lane in Lanes)
        {
            if (lane.Head == tile) return lane;
            foreach (var step in lane.Steps) if (step.Tiles.Contains(tile)) return lane;
        }
        return null;
    }

    // Which channel a lane sounds on, which is what decides its timbre now that
    // each channel has one. A branch lane has no CHAN of its own, so it takes the
    // channel of whatever jumps into it, following the chain until a CHAN lane
    // turns up — the same answer the runner arrives at, since a runner keeps the
    // channel it was born with wherever a jump sends it.
    public int ChannelOf(Lane lane)
    {
        // Bounded so that a file whose links have been edited into a ring cannot
        // hang the editor.
        for (var guard = 0; lane != null && guard < 64; guard++)
        {
            if (lane.Channel != null) return lane.Channel.Channel;
            if (lane.JumpSource == null) break;
            lane = LaneOf(lane.JumpSource);
        }

        return 1;
    }

    // The branch lane a jump hands over to. One to one, so there is never more
    // than one answer.
    public Lane DestinationOf(JumpTile jump)
    {
        foreach (var lane in Lanes) if (lane.JumpSource == jump) return lane;
        return null;
    }

    // Runners are born from CHAN lanes, earliest first, and a runner that sits
    // higher on the plane runs before one that sits lower.
    public IEnumerable<Lane> ChannelLanes
      => Lanes.Where(lane => lane.Channel != null)
              .OrderBy(lane => lane.Y).ThenBy(lane => lane.X);

    // The lane the piece's period is read off: the first channel one lane in the
    // order the runners are born in.
    //
    // Nothing about a lane says that it is this one — it is a position rather than a
    // property, the same way the order a runner takes its turn in is — so a score
    // says where it ends by where its channel one lane is written and not by a flag
    // anybody has to set. Channel one because a lap has to be counted somewhere and
    // that is where it is legible; a score with no channel one lane is read off
    // whoever runs first, since something has to answer.
    //
    // Only the switch between two scores asks this today. It is here rather than
    // there because a period belongs to the score that has one, and the next thing
    // that wants to happen on the turn of a piece should find it already written.
    public Lane MasterLane
    {
        get
        {
            Lane first = null;

            foreach (var lane in ChannelLanes)
            {
                first ??= lane;
                if (lane.Channel.Channel == 1) return lane;
            }

            return first;
        }
    }

    // Extent of the used area, which is what the view sizes its plane from.
    public int Width => Lanes.Count == 0 ? 0 : Lanes.Max(lane => lane.TermX) + 1;

    public int Height => Lanes.Count == 0 ? 0 : Lanes.Max(BottomOf) + 1;

    // The other two corners of the same rectangle. Width and Height are counted from
    // the origin because that is where the plane starts, so a score sitting away from
    // it needs these to say where it begins: the leftmost cell a lane owns is its
    // head, and the topmost is the rail row, since a stack hangs downwards from it.
    public int MinX => Lanes.Count == 0 ? 0 : Lanes.Min(lane => lane.HeadX);

    public int MinY => Lanes.Count == 0 ? 0 : Lanes.Min(lane => lane.Y);

    // The last row this lane occupies, and not the one after it: a lane with nothing
    // stacked on it owns its rail row alone. Height adds the one that turns a row into
    // a count, the same way Width does — this used to return a row too far, which left
    // the plane a row deeper below the score than it was above it.
    static int BottomOf(Lane lane)
    {
        var depth = 1;
        foreach (var step in lane.Steps) depth = System.Math.Max(depth, step.Depth);
        return lane.Y + depth - 1;
    }

    // Moves the whole score across the plane, which is how it keeps free ground on
    // its left and above without a coordinate ever going negative — see
    // ScoreView.Reframe, which is the only caller and where the policy is.
    //
    // A lane is the only thing that holds a position: a tile knows nothing about
    // where it is, a jump reaches its branch lane by reference, and a runner carries
    // a lane and a step index. So this is the whole of it, and it is safe to do while
    // the sequence plays — everything positional that is read of a score is relative,
    // both the order ChannelLanes gives and the MasterLane that falls out of it, and
    // a translation leaves an ordering alone.
    public void Translate(int dx, int dy)
    {
        foreach (var lane in Lanes) (lane.X, lane.Y) = (lane.X + dx, lane.Y + dy);
    }

    // Editing

    // Places a tile, growing the lane by one step when the terminator cell is
    // targeted. A stack has no holes in it, so the only depths that accept a tile
    // are the ones already filled and the one just past the end.
    public bool Place(GridPoint point, Tile tile)
    {
        var lane = PlacementLane(point, out var step, out var depth);
        if (lane == null) return false;

        if (step == lane.Steps.Count) lane.AddStep();

        var tiles = lane.Steps[step].Tiles;

        if (depth < tiles.Count)
            tiles[depth] = tile;
        else
            tiles.Add(tile);

        return true;
    }

    // A copy of the tile at this point and of everything hanging under it, or
    // nothing when that cell holds no tile or holds one that has no copy.
    //
    // What hangs below comes along for the reason a drag takes it along: what a gate
    // or a lock governs is exactly what is under it, so a sub-stack is the unit
    // worth having twice. Tiles with no copy are stepped over rather than ending the
    // walk — a jump in the middle of a stack is not the bottom of it, and what is
    // under the jump is still under everything above it.
    //
    // cells collects where the copied tiles were standing, for a caller that wants
    // to show what it just took. The ones stepped over are not in it.
    public List<Tile> CopyStack(GridPoint point, List<GridPoint> cells = null)
    {
        var cell = At(point);
        if (cell.Kind != CellKind.Tile) return null;

        // Asked of the tile that was aimed at before anything is gathered: a cell
        // that cannot be copied from is not a copy of nothing, it is nothing
        // happening, and what the caller already holds stays untouched.
        if (cell.Tile.Copy() == null) return null;

        var tiles = cell.Lane.Steps[cell.Step].Tiles;
        var copies = new List<Tile>();

        for (var depth = cell.Depth; depth < tiles.Count; depth++)
        {
            var copy = tiles[depth].Copy();
            if (copy == null) continue;

            copies.Add(copy);
            cells?.Add(cell.Lane.CellPoint(cell.Step, depth));
        }

        return copies;
    }

    // Puts a run of tiles down as one stack, keeping their order, and answers
    // whether it happened. The tiles become the score's: a caller handing over what
    // it means to keep has to hand over copies.
    //
    // Not Place called in a loop. A stack that will not fit has to be refused whole
    // — half of one left growing out of a step is not what anybody asked for — so
    // the ground is looked at before a single tile is written.
    public bool PlaceStack(GridPoint point, IReadOnlyList<Tile> tiles)
    {
        if (tiles == null || tiles.Count == 0) return false;

        var lane = PlacementLane(point, out var step, out var depth);
        if (lane == null) return false;

        // Only onto the bottom of a stack, where Place would have appended. Place
        // also takes a depth that is already filled and overwrites it, which for one
        // tile is a tile changing and for a run would be the rest of the stack
        // disappearing under it.
        if (depth != (lane.StepAt(step)?.Depth ?? 0)) return false;

        // The cell aimed at has been answered for by PlacementLane; what is left is
        // the room the rest of the run needs under it. The lane is excused for the
        // reason it is in PlanMove: the cells it is about to fill are its own.
        for (var i = 1; i < tiles.Count; i++)
            if (!IsFree(lane.CellPoint(step, depth + i), lane)) return false;

        if (step == lane.Steps.Count) lane.AddStep();

        lane.Steps[step].Tiles.AddRange(tiles);
        return true;
    }

    // The lane that would take a tile at this point, if any. The editor asks this
    // before offering a tile, so that the only cells offering one are the cells
    // that will take it.
    public Lane PlacementLane(GridPoint point, out int step, out int depth)
    {
        (step, depth) = (0, 0);

        foreach (var lane in Lanes)
        {
            var sx = point.X - lane.X;
            var sy = point.Y - lane.Y;

            if (sx < 0 || sx > lane.Steps.Count || sy < 0) continue;

            // The terminator column only takes a tile on the rail row, where it
            // becomes a new step. The terminator itself has to have somewhere to
            // go as well, which is the same room the Steps control asks for: a
            // lane grows the same amount whichever of the two grew it.
            if (sx == lane.Steps.Count)
            {
                if (sy != 0) continue;
                if (!HasRoomToGrow(lane)) continue;
                (step, depth) = (sx, 0);
                return lane;
            }

            if (sy > lane.Steps[sx].Depth) continue;
            if (sy == lane.Steps[sx].Depth && !IsFree(point, lane)) continue;

            (step, depth) = (sx, sy);
            return lane;
        }

        return null;
    }

    // Removes whatever tile is at this point. Tiles below it move up so that the
    // chain stays unbroken, and a jump takes its branch lane with it.
    public bool Remove(GridPoint point)
    {
        var cell = At(point);
        if (cell.Kind != CellKind.Tile) return false;

        if (cell.Tile is JumpTile jump)
        {
            var branch = DestinationOf(jump);
            if (branch != null) RemoveLane(branch, false);
        }

        cell.Lane.Steps[cell.Step].Tiles.RemoveAt(cell.Depth);
        return true;
    }

    // Dragging

    // The step a dragged tile actually came off, or nothing if it is no longer
    // there. A cell reference is a reading of the score at some earlier moment,
    // and this one has been carried about by a hand since then.
    Step SourceStep(CellRef source)
    {
        var step = source.Lane?.StepAt(source.Step);
        return step != null && step.At(source.Depth) == source.Tile ? step : null;
    }

    // Where a run of dragged tiles would land. This is not PlacementLane: a drop
    // is allowed onto a cell that already holds something, because opening a stack
    // up to take a tile is exactly what reordering one is.
    public Lane DropLane(GridPoint point, out int step, out int depth)
    {
        (step, depth) = (0, 0);

        foreach (var lane in Lanes)
        {
            var sx = point.X - lane.X;
            var sy = point.Y - lane.Y;

            if (sx < 0 || sx > lane.Steps.Count || sy < 0) continue;

            // The terminator column only takes a drop on the rail row, where it
            // becomes a new step, the same way a placed tile grows the lane.
            if (sx == lane.Steps.Count)
            {
                if (sy != 0) continue;
                if (!HasRoomToGrow(lane)) continue;
                (step, depth) = (sx, 0);
                return lane;
            }

            if (sy > lane.Steps[sx].Depth) continue;

            (step, depth) = (sx, sy);
            return lane;
        }

        return null;
    }

    // What dropping the tile at a cell would do, or nothing if that cell will not
    // have it.
    //
    // Inside the step it came from, the one tile moves and the stack closes up
    // behind it: that is what changing the order within a stack is. Anywhere else
    // the tiles hanging below travel with it, since what a gate or a lock governs
    // is precisely what hangs under it, and a sub-stack that stayed behind would
    // be governed by whatever the move left above it.
    public TileMove PlanMove(CellRef source, GridPoint target)
    {
        if (source.Kind != CellKind.Tile) return TileMove.None;

        var from = SourceStep(source);
        if (from == null) return TileMove.None;

        var lane = DropLane(target, out var step, out var depth);
        if (lane == null) return TileMove.None;

        var tiles = from.Tiles;
        var same = lane == source.Lane && step == source.Step;

        if (same && depth == source.Depth) return TileMove.None;

        var count = same ? 1 : tiles.Count - source.Depth;

        if (same)
        {
            // One tile leaves before it comes back, so the stack it lands in is a
            // cell shorter than the one it was picked up from.
            depth = System.Math.Min(depth, tiles.Count - 1);
        }
        else
        {
            // Room for what the target stack grows by, on ground no other lane
            // owns. The lane itself is excused: the cells it is about to vacate
            // are its own.
            var grown = lane.StepAt(step)?.Depth ?? 0;
            for (var i = 0; i < count; i++)
                if (!IsFree(lane.CellPoint(step, grown + i), lane)) return TileMove.None;
        }

        return new TileMove(lane, step, depth, count);
    }

    public bool ApplyMove(CellRef source, TileMove move)
    {
        if (!move.IsValid) return false;

        var from = SourceStep(source);
        if (from == null || source.Depth + move.Count > from.Tiles.Count) return false;

        var tiles = from.Tiles;
        var moved = tiles.GetRange(source.Depth, move.Count);
        tiles.RemoveRange(source.Depth, move.Count);

        if (move.Step == move.Lane.Steps.Count) move.Lane.AddStep();

        var into = move.Lane.Steps[move.Step].Tiles;
        into.InsertRange(System.Math.Min(move.Depth, into.Count), moved);
        return true;
    }

    // Moving a lane bodily is also how the execution order is changed: the runner
    // of a lane sitting lower down runs later and can overwrite what the ones above
    // it did. The position is given as the head cell, since that is the cell a lane
    // is dragged by.
    //
    // Ground another lane owns refuses the move. Nothing stops two lanes from
    // overlapping in the model, but the cells of the loser would be unreachable,
    // and a lane carried across the plane by hand can land anywhere.
    public bool CanMoveLane(Lane lane, GridPoint head)
    {
        if (lane == null || !Lanes.Contains(lane)) return false;
        if (head.X < 0 || head.Y < 0) return false;

        var dx = head.X - lane.HeadX;
        var dy = head.Y - lane.Y;
        if (dx == 0 && dy == 0) return false;

        foreach (var cell in lane.OccupiedCells())
            if (!IsFree(cell.Offset(dx, dy), lane)) return false;

        return true;
    }

    public bool MoveLane(Lane lane, GridPoint head)
    {
        if (!CanMoveLane(lane, head)) return false;
        (lane.X, lane.Y) = (head.X + 1, head.Y);
        return true;
    }

    public Lane AddLane(int x, int y, FlowTile head, int steps)
    {
        var lane = new Lane { X = x, Y = y, Head = head };
        for (var i = 0; i < steps; i++) lane.AddStep();
        Lanes.Add(lane);
        return lane;
    }

    // Creates the branch lane a jump hands over to. Placing the jump and its
    // destination in one action is what keeps the one to one rule true at every
    // moment of editing.
    public Lane AddBranchLane(JumpTile jump, GridPoint near, int steps)
    {
        var point = FindFreeRow(near, steps);
        var lane = AddLane(point.X, point.Y, new JumpDestTile(), steps);
        lane.JumpSource = jump;
        return lane;
    }

    // Drops a lane. Branch lanes reachable from it go too, since a JDST with
    // nothing pointing at it cannot exist; removing a branch lane likewise takes
    // out the jump that fed it.
    public void RemoveLane(Lane lane, bool removeJumpSource = true)
    {
        if (!Lanes.Remove(lane)) return;

        if (removeJumpSource && lane.JumpSource != null)
        {
            var point = Locate(lane.JumpSource);
            if (point.HasValue)
            {
                var cell = At(point.Value);
                if (cell.Kind == CellKind.Tile)
                    cell.Lane.Steps[cell.Step].Tiles.RemoveAt(cell.Depth);
            }
        }

        foreach (var step in lane.Steps)
            foreach (var tile in step.Tiles.ToArray())
                if (tile is JumpTile jump)
                {
                    var branch = DestinationOf(jump);
                    if (branch != null) RemoveLane(branch, false);
                }
    }

    // Somewhere the given lane length fits, searched downwards from a hint. Used
    // when a new lane has to be put down without asking where.
    public GridPoint FindFreeRow(GridPoint hint, int steps)
    {
        var x = System.Math.Max(1, hint.X);

        for (var y = System.Math.Max(1, hint.Y); y < hint.Y + 256; y++)
        {
            var free = true;

            // One clear row above as well, so that an unrelated stack does not
            // end up looking chained to this lane's head.
            for (var i = -1; i <= steps + 1 && free; i++)
                for (var dy = -1; dy <= 0 && free; dy++)
                    free = IsFree(new GridPoint(x - 1 + i, y + dy));

            if (free) return new GridPoint(x, y);
        }

        return new GridPoint(x, hint.Y);
    }

    // A single shared instance is enough: the terminator carries no state and is
    // never stored in a step.
    public static readonly TerminatorTile Terminator = new TerminatorTile();
}

} // namespace Jacquard
