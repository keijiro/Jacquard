using System.Collections.Generic;

namespace Jacquard {

// A grid coordinate. Cells are addressed in whole steps and rows; nothing in the
// model knows about pixels.

public readonly struct GridPoint : System.IEquatable<GridPoint>
{
    public readonly int X, Y;

    public GridPoint(int x, int y) => (X, Y) = (x, y);

    public GridPoint Offset(int dx, int dy) => new GridPoint(X + dx, Y + dy);

    public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object other) => other is GridPoint p && Equals(p);
    public override int GetHashCode() => X * 397 ^ Y;
    public override string ToString() => X + "," + Y;

    public static bool operator ==(GridPoint a, GridPoint b) => a.Equals(b);
    public static bool operator !=(GridPoint a, GridPoint b) => !a.Equals(b);
}

// One column of a lane: everything that happens at the same instant, stacked
// downwards. The stack has no fixed depth — a step simply holds as many tiles as
// it needs, so a lane occupies only the cells it fills and not a rectangle.

public sealed class Step
{
    public List<Tile> Tiles { get; } = new();

    public int Depth => Tiles.Count;
    public bool IsEmpty => Tiles.Count == 0;

    public Tile At(int depth)
      => depth >= 0 && depth < Tiles.Count ? Tiles[depth] : null;

    public T Find<T>() where T : Tile
    {
        foreach (var tile in Tiles) if (tile is T match) return match;
        return null;
    }
}

// A row of steps placed anywhere on the plane. What kind of lane it is comes from
// its head cell and never from where it sits; the one thing position decides is
// the order the runners execute in, which reads off the vertical position of the
// CHAN tile.

public sealed class Lane
{
    // Grid position of the first step. The head sits one column to the left and
    // the terminator one column past the last step.
    public int X { get; set; }
    public int Y { get; set; }

    public FlowTile Head { get; set; }

    public List<Step> Steps { get; } = new();

    // For a branch lane, the jump that reaches it. The pairing lives here rather
    // than in a separate table so that one to one holds by construction: there
    // is nowhere to write a second jump, and a branch lane cannot exist without
    // one.
    public JumpTile JumpSource { get; set; }

    public ChannelTile Channel => Head as ChannelTile;
    public bool IsBranch => Head is JumpDestTile;

    public int HeadX => X - 1;
    public int TermX => X + Steps.Count;

    public GridPoint HeadPoint => new GridPoint(HeadX, Y);
    public GridPoint TermPoint => new GridPoint(TermX, Y);

    public GridPoint CellPoint(int step, int depth)
      => new GridPoint(X + step, Y + depth);

    public Step StepAt(int index)
      => index >= 0 && index < Steps.Count ? Steps[index] : null;

    public Step AddStep()
    {
        var step = new Step();
        Steps.Add(step);
        return step;
    }

    // Every cell this lane owns: the whole rail row, and whatever hangs under it.
    //
    // A step it owns even while empty. What a lane occupies is the run it plays
    // through rather than the tiles that happen to be written on it, so a step
    // nothing has been written on yet is still this lane's to write on — an empty
    // cell is where a lane is going, not ground going spare. Anything else would
    // let a stack from the lane above grow across a rail that is plainly drawn,
    // and leave whichever lane came second in the list unreachable there.
    public IEnumerable<GridPoint> OccupiedCells()
    {
        for (var x = HeadX; x <= TermX; x++) yield return new GridPoint(x, Y);

        for (var i = 0; i < Steps.Count; i++)
            for (var d = 1; d < Steps[i].Depth; d++)
                yield return CellPoint(i, d);
    }

    // The same question asked of one cell. Overlap checks run this per lane rather
    // than walking the cells, which is what keeps a scan for free ground cheap
    // however long the lanes are.
    public bool Owns(GridPoint point)
    {
        if (IsOnRail(point)) return true;

        var step = point.X - X;
        var depth = point.Y - Y;

        return step >= 0 && step < Steps.Count &&
               depth >= 1 && depth < Steps[step].Depth;
    }

    // The row the rail runs along, from the head to the terminator inclusive.
    public bool IsOnRail(GridPoint point)
      => point.Y == Y && point.X >= HeadX && point.X <= TermX;
}

} // namespace Jacquard
