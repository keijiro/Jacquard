using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

using CoreProject = Jacquard.Project;

namespace Jacquard.App {

// The promotional reel: one lane, played over and over, that grows into the next
// example between laps.
//
// An empty rail takes a note, the note is joined by two more along the rail, the three
// gather into one stack, a gate slides in above them, and the gate becomes the other
// kind. Six pictures of what the plane is for, in the order a person meets them, with
// nothing on the screen but the plane they are drawn on.
//
// The pictures are not written here. They are the lanes of a saved score, read top to
// bottom, so the reel is edited in the app itself and not in this file: draw the lanes,
// save, press P. What this file decides is only how long each one is left running and
// how the one becomes the next.
//
// Two clocks and one line. What the sequencer plays has to change a lookahead before
// the lap it belongs to, and what the screen shows has to arrive on the beat it is
// heard on — so the score is rewritten the moment the master runner's scheduling head
// reaches the lap line, and the tiles are moved over a window that ends on that same
// line measured against the audio clock. The two are one moment seen from either side
// of the lookahead, which is why a tile lands exactly as the loop turns over.
//
// None of this is meant to survive. It is a camera rig: it takes the app apart — the
// chrome goes, the plane is pinned and doubled in size, the score is replaced — and
// puts it back on the way out.

public sealed class PromoDirector
{
    // Public

    public PromoDirector(JacquardApp app) => _app = app;

    public bool Running { get; private set; }

    // Which file the pictures come out of. A save slot rather than an asset, because it
    // is meant to be redrawn between takes.
    public const string Slot = "score5";

    // Laps per example, in the order the lanes are written down the plane.
    //
    // Eight for everything that sounds. Four was enough to hear what a figure is and not
    // enough to sit with it — a reel is watched rather than read, and the eye wants the
    // pattern to come round again after the ear has finished placing it. The gates take
    // eight for a second reason as well: what a gate does is a pattern across laps, and
    // a pattern has to come round twice before it is one.
    //
    // The empty rail keeps four. What it has to show is that nothing is happening yet,
    // and eight laps of nothing is a wait rather than a statement.
    //
    // A lane the file has and this list does not is left running for the last figure
    // here, so an example added to the score plays without this having to be touched.
    static readonly int[] Laps = { 4, 8, 8, 8, 8, 8 };

    // One answer per frame, because two paths watch for the same key: the plane's own
    // key handler and the app's poll. Both fire in the frame the key goes down, and a
    // reel put up and taken down again inside one frame is a reel nobody saw.
    public void Toggle()
    {
        if (_toggled == Time.frameCount) return;
        _toggled = Time.frameCount;

        if (Running) Stop(); else Start();
    }

    // Puts the reel up. Answers nothing: a file that will not read says so on the
    // console and leaves the app as it was.
    public void Start()
    {
        if (Running) return;

        var source = ReadTheReel();
        if (source == null) return;

        if (!Cut(source)) return;

        _returning = _app.Project;
        Running = true;

        // Before the app is taken apart, since taking it apart rebuilds the plane and
        // this is what the plane is about to be sized from.
        _app.View.Plane = _plane;

        // The app is taken apart before the transport is started, since the plane the
        // first lap is drawn on is the one this puts up.
        _app.EnterPromo(_project);

        _start = _app.RestartTransport();
        MarkTheLapLines();

        (_stage, _shown) = (0, 0);
        _animating = false;

        Settle(0);
    }

    // Puts it back down. The score that was being edited comes back, unplayed: the reel
    // replaced it while it stood, and there is nothing about where the reel had got to
    // that is worth carrying into it.
    public void Stop()
    {
        if (!Running) return;

        Running = false;
        _items.Clear();
        _animating = false;

        _app.View.Plane = Vector2Int.zero;
        _app.LeavePromo(_returning);
        _returning = null;
    }

    // Called every frame from the app, ahead of the scheduler: what this may do is
    // rewrite the score the scheduler is about to read, and the whole point is that it
    // does so in the frame before the lap line rather than the frame after it.
    public void Tick()
    {
        if (!Running) return;

        var now = _app.Synth.CurrentSample;

        FollowTheRunner();
        Animate(now);
        Centre();
    }

    // Reading the reel

    // The saved file, read straight off the disk rather than through the store: the
    // store remembers the name it was last asked for, and the reel is not what the app
    // should come up in next time it is opened.
    static CoreProject ReadTheReel()
    {
        var path = Path.Combine(
          Application.persistentDataPath, "Scores", Slot + ".jacquard");

        if (!File.Exists(path))
        {
            Debug.LogWarning("Promo: no " + Slot + " to read — " + path);
            return null;
        }

        try
        {
            return ProjectFormat.Read(File.ReadAllText(path));
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            return null;
        }
    }

    // Takes the score to pieces and keeps what the reel is made of: the tiles of each
    // lane, top to bottom, and one lane of the app's own to play them on.
    //
    // The lane is built here rather than borrowed from the file because every picture
    // has to be drawn on the same rail. A lane per example would move the whole figure
    // across the screen between examples, and what the reel is showing is one lane being
    // written — so the file's lanes are read as what to write on it and thrown away.
    bool Cut(CoreProject source)
    {
        var lanes = new List<Lane>(source.Score.Lanes);
        lanes.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

        _keyframes.Clear();
        _depth = 1;

        var steps = 0;

        foreach (var lane in lanes)
        {
            var frame = new List<List<Tile>>();

            foreach (var step in lane.Steps)
            {
                var tiles = new List<Tile>();

                foreach (var tile in step.Tiles)
                {
                    // A tile with no copy is a jump, which has a lane of its own hanging
                    // off it and so is not something one rail can be made to show.
                    var copy = tile.Copy();
                    if (copy != null) tiles.Add(copy);
                }

                frame.Add(tiles);
                _depth = Mathf.Max(_depth, tiles.Count);
            }

            _keyframes.Add(frame);

            // The longest lane in the file, since the rail has to be the same length
            // throughout: a terminator that moved between pictures would read as the
            // reel cutting rather than as one lane being written on.
            steps = Mathf.Max(steps, lane.Steps.Count);
        }

        if (_keyframes.Count == 0 || steps == 0)
        {
            Debug.LogWarning("Promo: " + Slot + " has no lane to read");
            return false;
        }

        // The channel and the division of the topmost lane, which is what the whole reel
        // is played on: the pictures are one lane's worth of writing and the sound they
        // are written for is the sound that lane names.
        var first = lanes[0].Channel;
        var head = new ChannelTile
          { Channel = first?.Channel ?? 1, Division = first?.Division ?? 16 };

        _plane = PlaneFor(steps);

        // In the middle of the plane, which is what lets the scroll area put it in the
        // middle of the screen: an offset it will not take is a negative one, so half a
        // plane has to lie above and to the left of the picture.
        var score = new Score();
        _lane = score.AddLane((_plane.x - steps - 2) / 2 + 1,
                              (_plane.y - _depth) / 2, head, steps);

        // The file's own project, with its tempo and its patches: what the reel sounds
        // like is set where the pictures are drawn.
        source.Score = score;
        _project = source;

        Write(0);

        _frame = Frame();

        return true;
    }

    // The score

    // Writes one picture into the lane the runners are playing.
    //
    // Tiles are handed over as copies, since the score now owns them and the picture has
    // to be good for the next take. The steps themselves are kept and refilled rather
    // than replaced: a runner holds a step index into this lane, and the lane it is
    // walking must be the same lane on the far side of this.
    void Write(int keyframe)
    {
        var frame = _keyframes[keyframe];

        for (var i = 0; i < _lane.Steps.Count; i++)
        {
            var tiles = _lane.Steps[i].Tiles;
            tiles.Clear();

            if (i >= frame.Count) continue;

            foreach (var tile in frame[i]) tiles.Add(tile.Copy());
        }
    }

    // Where each example ends, in absolute samples. Worked out ahead of time because
    // nothing here can change it: every lap is the same run of steps, and a gate in a
    // stack decides what sounds rather than how far the lane goes.
    void MarkTheLapLines()
    {
        var lap = _lane.Steps.Count *
                  _lane.Channel.StepSeconds(_project.Tempo) * _app.Synth.SampleRate;

        _lines = new double[_keyframes.Count];

        var line = (double)_start;

        for (var i = 0; i < _keyframes.Count; i++)
        {
            line += LapsOf(i) * lap;
            _lines[i] = line;
        }
    }

    static int LapsOf(int keyframe)
      => Laps[Mathf.Min(keyframe, Laps.Length - 1)];

    // Brings the next picture in, at the one moment it can be brought in cleanly: the
    // master runner has just handed the last step of the lap to the scheduler and its
    // next step is the lap line itself. Everything before the line has already been
    // emitted from the picture going out, and everything from the line on is emitted
    // from the one coming in, so the seam falls exactly on the beat.
    void FollowTheRunner()
    {
        if (_stage >= _keyframes.Count - 1) return;

        var runner = _app.Sequencer.MasterRunner;
        if (runner == null || !runner.Running) return;

        // Half a sample, because a lane that divides the lap evenly lands on that
        // instant to the bit.
        if (runner.NextSample < _lines[_stage] - 0.5) return;

        _stage++;
        Write(_stage);
    }

    // The screen

    // The window the tiles move over ends on the lap line and is measured against the
    // audio clock, so the motion resolves on the beat the new picture is first heard on.
    void Animate(long now)
    {
        if (_shown >= _keyframes.Count - 1) return;

        var end = _lines[_shown];
        var span = Mathf.Max(0.01f, _app.PromoTransition) * _app.Synth.SampleRate;
        var begin = end - span;

        if (now < begin) return;

        if (!_animating) Begin(_shown, _shown + 1);

        // Where in the window this frame falls, which is not always the top of it: the
        // window opens between two frames and a frame that ran long opens it part way
        // through. Everything is placed from this rather than stepped along, so a
        // dropped frame costs a picture of the movement and never its landing.
        var t = Mathf.Clamp01((float)((now - begin) / span));

        foreach (var item in _items) Apply(item, t);

        if (t < 1.0f) return;

        _shown++;
        _animating = false;
        Settle(_shown);
    }

    // What moves, what arrives and what leaves, between two pictures.
    //
    // Tiles are matched by what they say — a note by its name, a gate by its pattern —
    // and in reading order, so the same C4 written in both pictures is the same tile on
    // the screen however far it has to travel. What is left over on either side is a
    // tile arriving or a tile leaving, which is also how a gate becoming the other kind
    // reads: two tiles trading places on one cell rather than one tile changing its
    // mind, because that is what has happened.
    void Begin(int from, int to)
    {
        var layer = _app.View.TileLayer;
        layer.Clear();
        _items.Clear();

        Rail(layer);

        var leaving = Placed(_keyframes[from]);
        var arriving = Placed(_keyframes[to]);

        var taken = new bool[leaving.Count];
        var order = 0;

        foreach (var target in arriving)
        {
            var match = -1;

            for (var i = 0; i < leaving.Count; i++)
            {
                if (taken[i] || leaving[i].Tile.Token != target.Tile.Token) continue;
                match = i;
                break;
            }

            var element = new TileElement(target.Tile, Cell(target));
            layer.Add(element);

            if (match < 0)
            {
                _items.Add(new Item { Element = element, Kind = Motion.Arriving,
                                      To = Origin(target), Delay = Stagger(order++) });
                continue;
            }

            taken[match] = true;

            var start = Origin(leaving[match]);
            var finish = Origin(target);

            // A tile written on the same cell in both pictures is not moving, and an
            // item that is not moving is an item that has nothing to say every frame.
            if ((start - finish).sqrMagnitude < 0.01f) continue;

            _items.Add(new Item { Element = element, Kind = Motion.Moving,
                                  From = start, To = finish, Delay = Stagger(order++) });
        }

        for (var i = 0; i < leaving.Count; i++)
        {
            if (taken[i]) continue;

            var element = new TileElement(leaving[i].Tile, Cell(leaving[i]));
            layer.Add(element);

            _items.Add(new Item { Element = element, Kind = Motion.Leaving,
                                  From = Origin(leaving[i]) });
        }

        _animating = true;
    }

    // The picture at rest: the tiles where the score says they are, and the painted
    // layers redrawn, which is where the lattice and the pass-through markers catch up
    // with the score that was rewritten under them a moment ago.
    void Settle(int keyframe)
    {
        var layer = _app.View.TileLayer;
        layer.Clear();
        _items.Clear();

        Rail(layer);

        foreach (var placed in Placed(_keyframes[keyframe]))
            layer.Add(new TileElement(placed.Tile, Cell(placed)));

        _app.View.Repaint();
    }

    // The two cells that are in every picture.
    void Rail(VisualElement layer)
    {
        layer.Add(new TileElement(_lane.Head, _lane.HeadPoint));
        layer.Add(new TileElement(Score.Terminator, _lane.TermPoint));
    }

    static void Apply(Item item, float t)
    {
        var element = item.Element;
        var q = item.Delay >= 1.0f ? 1.0f
              : Mathf.Clamp01((t - item.Delay) / (1.0f - item.Delay));

        switch (item.Kind)
        {
            case Motion.Moving:
                Place(element, Vector2.LerpUnclamped(item.From, item.To, Spring(q)));
                break;

            case Motion.Arriving:
                Place(element, item.To);
                element.style.opacity = Mathf.Clamp01(q * 2.0f);
                Size(element, Mathf.LerpUnclamped(0.35f, 1.0f, Spring(q)));
                break;

            case Motion.Leaving:
                // Gone well before the beat, so that the cell it stood on is clear
                // while whatever is arriving there is still on its way in.
                var away = Mathf.Clamp01(q * 1.8f);
                Place(element, item.From);
                element.style.opacity = 1.0f - away;
                Size(element, 1.0f - 0.35f * away);
                break;
        }
    }

    static void Place(VisualElement element, Vector2 origin)
    {
        element.style.left = origin.x;
        element.style.top = origin.y;
    }

    // Spelled out, because Jacquard has a Scale of its own and it is the musical one.
    static void Size(VisualElement element, float scale)
      => element.style.scale =
           new UnityEngine.UIElements.Scale(new Vector3(scale, scale, 1.0f));

    // Out to the mark and a little past it, which is the whole of what makes a tile
    // arrive rather than appear. Ten per cent of a cell is three pixels at the size the
    // plane is drawn and six at the size the reel is filmed: enough to read as a
    // movement stopping and not enough to read as a bounce.
    static float Spring(float t)
    {
        const float overshoot = 1.1f;
        var u = t - 1.0f;
        return 1.0f + u * u * ((overshoot + 1.0f) * u + overshoot);
    }

    // What each tile waits for its turn. The stack is written top down, so a run of
    // tiles arriving reads as one thing being written rather than as several things
    // happening at once — and the last of them still lands on the beat, since the delay
    // is spent inside the window rather than added to it.
    static float Stagger(int order) => Mathf.Min(0.36f, order * 0.12f);

    // Keeps the lane in the middle of whatever the screen turns out to be. The plane is
    // far larger than the picture on it and the scroll area is what is looking at it, so
    // this is a scroll offset rather than anything the plane knows about.
    void Centre()
    {
        var scroll = _app.Scroll;
        if (scroll == null) return;

        var size = scroll.contentRect.size;
        if (size.x <= 1.0f || size.y <= 1.0f) return;

        var offset = _frame.center - size / 2.0f;
        if ((offset - _asked).sqrMagnitude < 0.01f) return;

        _asked = offset;
        scroll.Offset = offset;
    }

    // The rectangle every picture is drawn inside: the rail from the head to the
    // terminator, and as deep as the deepest stack the reel ever has. Fixed for the
    // whole reel, so that a picture growing downwards does not pull the screen after it.
    Rect Frame()
    {
        var top = Style.CellOrigin(_lane.HeadPoint);
        var bottom = Style.CellOrigin(
          new GridPoint(_lane.TermX, _lane.Y + _depth - 1)) +
          new Vector2(Style.CellWidth, Style.CellHeight);

        return new Rect(top, bottom - top);
    }

    // Private members

    readonly JacquardApp _app;

    // The pictures, in the order they are shown: a list of steps, each a list of tiles.
    readonly List<List<List<Tile>>> _keyframes = new();

    // The sample each picture's last lap ends on.
    double[] _lines;

    CoreProject _project, _returning;
    Lane _lane;

    // The one the sequencer is playing and the one the screen is showing, which are the
    // same picture for all but the last fraction of a second of each example.
    int _stage, _shown;

    long _start;
    int _depth = 1;
    Vector2Int _plane;
    Rect _frame;
    Vector2 _asked = new Vector2(float.NaN, float.NaN);

    bool _animating;

    // The frame the reel was last asked to change state on. See Toggle.
    int _toggled = -1;

    // How big the plane is held while the reel runs, in cells.
    //
    // It is not a plane to write on: it is the ground the one picture is scrolled to the
    // middle of, and the picture sits exactly halfway across it so that the offset which
    // centres it is one the scroll area will take — it refuses a negative one, and half
    // a plane therefore has to lie above and to the left of the picture.
    //
    // One screenful and a margin, rather than a large round number. Every cell of it is
    // a dot in the lattice redrawn each time a picture settles, and a plane held at
    // several screenfuls is thousands of dots drawn where nobody can see them. The
    // margin is what the picture itself takes plus a few cells, since the plane has to
    // hold the picture as well as the screen.
    //
    // Read at the top of the reel and not followed afterwards. A window resized
    // mid-take is not a thing that happens while filming, and P twice picks up the new
    // size.
    Vector2Int PlaneFor(int steps)
    {
        var scale = Mathf.Max(1.0f, _app.PromoScale);

        var columns = Mathf.CeilToInt(Screen.width / scale / Style.StrideX) + steps + 6;
        var rows = Mathf.CeilToInt(Screen.height / scale / Style.StrideY) + _depth + 6;

        return new Vector2Int(Mathf.Max(48, columns), Mathf.Max(28, rows));
    }

    enum Motion { Moving, Arriving, Leaving }

    struct Item
    {
        public VisualElement Element;
        public Motion Kind;
        public Vector2 From, To;
        public float Delay;
    }

    readonly List<Item> _items = new();

    // A tile and the cell it stands on, which is the whole of what a picture is once it
    // has been taken out of its lists.
    struct Cellular
    {
        public Tile Tile;
        public int Step, Depth;
    }

    static List<Cellular> Placed(List<List<Tile>> frame)
    {
        var placed = new List<Cellular>();

        for (var i = 0; i < frame.Count; i++)
            for (var d = 0; d < frame[i].Count; d++)
                placed.Add(new Cellular { Tile = frame[i][d], Step = i, Depth = d });

        return placed;
    }

    GridPoint Cell(Cellular placed) => _lane.CellPoint(placed.Step, placed.Depth);

    Vector2 Origin(Cellular placed) => Style.CellOrigin(Cell(placed));
}

} // namespace Jacquard.App
