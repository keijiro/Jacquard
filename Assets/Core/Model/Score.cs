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

    public bool IsFree(GridPoint point, Lane except = null)
    {
        foreach (var lane in Lanes)
        {
            if (lane == except) continue;
            foreach (var cell in lane.OccupiedCells()) if (cell == point) return false;
        }
        return true;
    }

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

    // Extent of the used area, which is what the view sizes its plane from.
    public int Width => Lanes.Count == 0 ? 0 : Lanes.Max(lane => lane.TermX) + 1;

    public int Height => Lanes.Count == 0 ? 0 : Lanes.Max(BottomOf) + 1;

    static int BottomOf(Lane lane)
    {
        var depth = 1;
        foreach (var step in lane.Steps) depth = System.Math.Max(depth, step.Depth);
        return lane.Y + depth;
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

    // Inserts a tile at this point, pushing whatever was there and everything
    // below it one row down. This is how a gate gets above a note that is already
    // written: a stack is read from the top, so adding to it means making room
    // rather than overwriting.
    public bool Insert(GridPoint point, Tile tile)
    {
        var cell = At(point);
        if (cell.Kind != CellKind.Tile) return Place(point, tile);

        var step = cell.Lane.Steps[cell.Step];

        // The stack grows by one, so the cell past its current end has to be free.
        if (!IsFree(cell.Lane.CellPoint(cell.Step, step.Depth), cell.Lane)) return false;

        step.Tiles.Insert(cell.Depth, tile);
        return true;
    }

    // The lane that would take a tile at this point, if any.
    public Lane PlacementLane(GridPoint point, out int step, out int depth)
    {
        (step, depth) = (0, 0);

        foreach (var lane in Lanes)
        {
            var sx = point.X - lane.X;
            var sy = point.Y - lane.Y;

            if (sx < 0 || sx > lane.Steps.Count || sy < 0) continue;

            // The terminator column only takes a tile on the rail row, where it
            // becomes a new step.
            if (sx == lane.Steps.Count)
            {
                if (sy != 0) continue;
                if (!IsFree(point, lane)) continue;
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
