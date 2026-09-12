using UnityEngine.UIElements;

namespace Jacquard.App {

// The whole of a channel's timbre, at once, with a picture of it in the middle.
//
// These fifteen were two long groups on the Tile panel — the sound under a CHAN tile,
// the fifteen rows a lock can take hold of under a PABS or a PREL — and before that two
// panels of their own. The argument that folded them in was that the two standalone
// panels were the same column-width list wearing a frame: a header, two insets and a
// panel gap spent repeating what the cursor had already said.
//
// That argument is answered rather than reversed. What comes back out is not the list
// that went in. It is four columns of grouped rows with a waveform of the voice standing
// among them, three times the width of a PanelColumn, and a thing that wide cannot be a
// group of anything. What is being made is a picture of a patch, and a patch has a
// shape.
//
// The headings carry the prefixes the row names used to
// ----------------------------------------------------
//
// Every one of the fifteen captions got shorter for it, and two of them stop being
// ambiguous only because of the heading over them: *Decay* under *Pitch sweep* and
// *Decay* under *Frequency modulation* are a semitone apart in meaning, and the heading
// is the whole of what tells them apart. Which is why the table below owns the naming
// as well as the placing — a caption and the heading over it are one decision, and
// splitting them across two files is the drift CLAUDE.md warns about. ParamTargets no
// longer names anything.
//
// The table is also the one thing this design can get wrong that the
// `for (target = 0; target < Count; target++)` it replaces could not: a target added to
// ParamTargets and forgotten here. Which is what the table is public for, and the panel
// with it: the self test reads it and checks that every target is placed exactly once
// and carries a caption, since that is the loop's one guarantee this replaced.
//
// The middle group is two rows of two fields and not two columns of two — across then
// down, which is also ParamTargets' own order read in reading order. So the run on
// screen is still the run in the list, which is the thing a lock is read against.
//
// The swap unit is the column
// ---------------------------
//
// The body is a left slot, a middle column holding the plot above a middle slot, and a
// right slot; a run of rows is three elements, and a cursor move onto another kind of
// tile is three detaches and three adds. Three whole skeletons cannot be the unit,
// because there is one plot and three skeletons, and a plot in each is three renders to
// keep in step.
//
// Keeping the runs at all is InspectorPanel's measured argument, carried over with the
// rows it was about. The panel there was 152 elements and 122 of them were these two
// groups; built afresh on every click a channel start came to 1.12 ms and 451 KB of
// garbage, 357 KB of it the sound group alone, against 85 KB and 0.31 ms once the groups
// were kept and the bars' text fields made to wait for their first edit. The fifteen are
// the same fifteen in the same order whichever tile the cursor is on, and all that
// differs is what they read and write — so one run per binding is kept in a field and
// pointed at the new subject.
//
// Measured again on this shape, since a showing is two panels now where those figures
// are one. A channel start costs 0.13 ms and about 1.5 KB here — the run is detached and
// added back and not one element is made — against 0.27 ms and about 75 KB on the Tile
// panel beside it, which builds its thirty afresh. So the pair is 0.4 ms and 77 KB where
// the single panel was 0.31 ms and 85 KB: the garbage is down because these fifteen are
// no longer built for anybody, and the time is up because two panels now answer a cursor
// move where one did. Editor figures under Mono, as the ones above are.
//
// Three runs and not one, because a bar's range is fixed when it is made:
//
// - the patch's, for a CHAN tile: ParamRanges.Of, reading and writing Patches[n];
// - the absolute lock's: ParamRanges.Of as well, but it dims and it engages;
// - the relative lock's: ParamRanges.Relative, which is a different Low, High and
//   Bipolar.
//
// Three differences have to survive the merge, since one panel now hides what two panels
// made obvious. **Only the patch's run auditions** — it hands Audition to Controls.Bar
// as its settled callback, where a lock's bar deliberately passes none. **Only a lock's
// run dims and engages**, which is LockRow.UpdateAppearance. And **only a lock's run
// writes the score**, which is what decides SetLocked below.
//
// Parented permanently and hidden with display: none is refused, and the argument gets
// stronger here rather than weaker: ValueBar.SyncAll sweeps with no filter, so hidden
// runs would have a scrub's per-frame sync reach forty-five bars instead of fifteen, and
// a hidden LockRow would hold a tile that may since have been deleted with nothing
// calling Apply to correct it — a rule with no method to belong to. Detached, neither
// can arise.
//
// Where it stands, and what that costs
// ------------------------------------
//
// In the middle, in a PanelCentre layer of its own under Global and System. A timbre is
// read against nothing on screen — it is dialled with the ears, not against a cell — and
// the middle is the only place on this screen with room for something this wide.
//
// Not joined to _centre. That stack is panels that take turns being raised by a switch
// and are centred as a pair when both are up; this one is up whenever the cursor is on a
// CHAN or a lock, which is most of a session, so in that stack pressing Global would
// shove it up by half the Global panel's height and pressing System would shove it
// again. A panel raised by the cursor cannot queue with panels raised by switches, which
// is PanelEdge's own argument one layer in.
//
// Three consequences, named rather than fixed:
//
// - **A panel in the middle covers the score.** It goes the moment the cursor moves off,
//   which is the whole of the way out — there is no switch to press, because there was
//   no switch to raise it.
// - **It covers part of the Tile panel on every device.** The cursor's column takes 272
//   units off the right edge; a 760 wide panel centred on an 11-inch iPad leaves 217 a
//   side, on a mini about 187, and on a landscape phone nothing at all, since it has
//   shrunk to the safe area by then. No width fixes this: clearing 272 a side needs 1304
//   units of screen and nothing this ships to has it, and the dock is centred
//   horizontally too. It is the price of a panel this wide, not of this position — and
//   if it turns out to be fatal the fallback is PanelDock, which at least leaves the top
//   two thirds of the plane.
// - **JacquardUI.Reveal knows nothing about it**, so a cursor arrowed onto a CHAN cell
//   near the middle of the screen can end up behind the panel it raised.

public sealed class SoundPanel
{
    public VisualElement Root { get; }

    public SoundPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("", out _title);

        // Wider than a column panel, and a ceiling rather than a size.
        //
        // Controls.Panel writes PanelWidth, which is right for a panel standing in a
        // column exactly that wide and wrong here: PanelCentre is inset by the safe
        // area, so on a notched phone in landscape the container is 774 less two 44-unit
        // insets — 686, where a rigid 760 would hang 37 units off each side under the
        // camera housing with nothing to clip it. Against the ceiling the four field
        // columns give up about 18 units apiece and everything stays on the glass. The captions
        // keep flexShrink = 0, so no name ever clips; what gets shorter is the bars,
        // which is the part of a row that can afford it.
        //
        // The whole of what makes the ceiling a ceiling is the percentage under it, and
        // it was a width of Auto first, which does not work and was measured not
        // working: the panel came out 760 wide inside a 686 container and hung over both
        // edges. A flexShrink cannot save that — PanelCentre lays its children out down
        // the screen, so a panel's width is the cross axis there, and shrink factors are
        // spent along the main one. What a cross-axis item takes is its content, and it
        // is allowed to overflow. A percentage is measured against the container instead,
        // so the used width is the smaller of the two numbers with no shrinking involved
        // at all: 760 on anything wide enough for it, and the safe area on anything that
        // is not.
        //
        // Widened the way LivePanel widens itself, for the same reason: every other
        // panel here is one column of rows, so PanelWidth is the answer for all of them,
        // and the two that are not measure themselves instead.
        Root.style.width = Length.Percent(100);
        Root.style.maxWidth = Controls.WidePanelWidth;

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.alignItems = Align.Stretch;
        Root.Add(body);

        _left = Slot(Controls.FieldWidth, gap: true);
        body.Add(_left);

        var centre = Slot(Controls.FieldWidth * 2 + Controls.PanelGap, gap: true);
        body.Add(centre);

        // The plot takes what the middle column has left over rather than a height of
        // its own, so it is the tallest column — Mix over Sends, at five rows and two
        // headings — that decides how tall the panel is, and the picture fills whatever
        // that leaves. Which lands at two rows' worth either way, and that is also its
        // floor: below two rows a waveform is a texture rather than a shape.
        _plot = new SoundPlot();
        _plot.style.flexGrow = 1;
        _plot.style.minHeight = Controls.RowHeight * 2;
        _plot.style.marginBottom = Controls.Gap;
        centre.Add(_plot);

        _centre = new VisualElement();
        centre.Add(_centre);

        _right = Slot(Controls.FieldWidth, gap: false);
        body.Add(_right);

        Refresh(true);
    }

    // Raised and lowered by the cursor and by nothing else, so this writes its own
    // display where the five panels with a switch have a Show* method in JacquardUI.
    // Every one of those exists to keep a button's SetActive in step with what is on
    // screen; with no button there is nothing to keep in step, and putting the test up
    // there would be a second copy of *is this a CHAN or a lock* to keep in step with
    // the choice of run down here.
    //
    // It takes the same early return InspectorPanel.Refresh does, and for the same
    // hazard: without it a LockRow.Set would run Commit, and Commit runs Changed, and
    // Changed arrives back here — pulling a bar out from under the drag that is moving
    // it. The test is the tile and the channel, and nothing else; the lane and whether
    // the cell would take a tile are the Tile panel's business.
    //
    // The channel is in the test for the reason it used to be in InspectorPanel's: a
    // lock can change channels without moving, since renumbering the CHAN above it does
    // that and so does dragging its lane under a different jump. Which is also why the
    // header is rewritten on the early-return path — a renumber and a Swap both leave
    // the same tile carrying a different number.
    public void Refresh(bool force = false)
    {
        var tile = _editor.Selected;
        var channel = ChannelOf(tile);

        if (!force && tile == _tile && channel == _channel)
        {
            _title.text = Title(tile, channel);
            _run?.Sync();
            ShowPlot();
            return;
        }

        (_tile, _channel) = (tile, channel);

        Build();
    }

    // Shields the rows while a load waits for the lap line, and only while it is showing
    // a lock.
    //
    // This is the first panel to stand on both sides of the line FollowTheLock draws.
    // What is held is what writes the score, and a lock does; the rest of the mix — the
    // sound, the sends — is left alone, since none of it writes the score and the point
    // of holding on until the turn of the piece is to play across it. So the panel
    // answers for itself, the way ChannelsPanel does.
    //
    // Written from two places: here when the load starts waiting, and from Build when
    // the cursor moves between a lock and a CHAN while it is still waiting.
    // Controls.SetLocked is idempotent, and the shield goes on Root rather than on the
    // body so that a run swapped in underneath arrives exactly as far out of reach as
    // the one it replaced.
    public void SetLocked(bool locked)
    {
        _locked = locked;
        ApplyLock();
    }
    // Where the fifteen stand, and what each one is called once the heading over it has
    // taken the first half of the name. This is the reading order now. A group name
    // opens a group and a null joins the row to the one above it.
    //
    // Sentence case on the headings, since every heading in this interface is one —
    // *Fires on*, *Send FX*, *Swap*. *Depth* rather than *Amount* for the pitch sweep,
    // so that the panel does not carry two rows called Amount in two different columns;
    // the field is documented as the depth of the pitch envelope in octaves, so the word
    // is already the one used for it. *Mix* rather than *Mixing*, which is the word the
    // rest of this codebase and impl-mix.md use for the thing these three rows place a
    // note in. *Sends* rather than *Send*, since there are two of them and the panel
    // they feed is already called *Send FX* — the plural is what separates a channel's
    // amount from the effect it reaches.
    public enum Column { Left, Middle, Right }

    public static readonly
      (Column Where, string Group, string Caption, int Target)[] Fields =
      { (Column.Left,   "Note",                 "Transpose", ParamTargets.Transpose),
        (Column.Left,   null,                   "Gate",      ParamTargets.Gate),
        (Column.Left,   "Pitch sweep",          "Depth",     ParamTargets.PitchSweep),
        (Column.Left,   null,                   "Decay",     ParamTargets.PitchDecay),

        (Column.Middle, "Frequency modulation", "Ratio",     ParamTargets.ModRatio),
        (Column.Middle, null,                   "Amount",    ParamTargets.ModIndex),
        (Column.Middle, null,                   "Feedback",  ParamTargets.Feedback),
        (Column.Middle, null,                   "Decay",     ParamTargets.ModDecay),
        (Column.Middle, "Amp envelope",         "Attack",    ParamTargets.CarAttack),
        (Column.Middle, null,                   "Release",   ParamTargets.CarRelease),

        (Column.Right,  "Mix",                  "Level",     ParamTargets.Level),
        (Column.Right,  null,                   "Pan",       ParamTargets.Pan),
        (Column.Right,  null,                   "Unison",    ParamTargets.Unison),
        (Column.Right,  "Sends",                "Reverb",    ParamTargets.ReverbSend),
        (Column.Right,  null,                   "Delay",     ParamTargets.DelaySend) };


    // Private members

    readonly ScoreEditor _editor;
    readonly Label _title;
    readonly VisualElement _left, _centre, _right;
    readonly SoundPlot _plot;

    // Made on the first tile that calls for each, and pointed at the new subject
    // afterwards. See the header for what keeping them is worth and why there are three.
    Run _patch, _absolute, _relative;

    // Whichever of the three is on the panel now, or null while the panel is down.
    Run _run;

    Tile _tile;
    int _channel;
    bool _locked;

    // Which channel the fifteen belong to. For a CHAN tile the number comes off the
    // tile, because the promise the run makes is that it holds the tile and never its
    // number — that is what makes a renumber a re-bind rather than a rebuild. A lock
    // holds no number at all and a branch lane borrows one from the jump that reaches
    // it, so for a lock it is the editor's answer, which is the channel of the lane the
    // cursor is on.
    int ChannelOf(Tile tile) => tile switch
    {
        ChannelTile channel => channel.Channel,
        ParamTile => _editor.Channel,
        _ => 0
    };

    // The panel's subject, which is the one rule every panel obeys. A lock is the one
    // thing here that cannot name the channel it colours, so the header does it.
    static string Title(Tile tile, int channel) => tile switch
    {
        ChannelTile => "Channel " + channel,
        AbsoluteParamTile => "Absolute Lock on Channel " + channel,
        RelativeParamTile => "Relative Lock on Channel " + channel,
        _ => ""
    };

    // Points the panel at whatever the cursor has landed on, which is three detaches and
    // three adds and no elements built after the first tile of each kind.
    void Build()
    {
        _run = _tile switch
        {
            ChannelTile => _patch ??= new Run(this, null),
            AbsoluteParamTile absolute => Bind(ref _absolute, absolute),
            RelativeParamTile relative => Bind(ref _relative, relative),
            _ => null
        };

        // Cleared whether or not there is a run to put back, so that a panel which is
        // down is holding nothing: a detached LockRow cannot be left pointing at a lock
        // that has since been deleted, and a detached bar cannot be reached by a sweep.
        _left.Clear();
        _centre.Clear();
        _right.Clear();

        Root.style.display = _run == null ? DisplayStyle.None : DisplayStyle.Flex;

        if (_run == null) return;

        _title.text = Title(_tile, _channel);

        _left.Add(_run.Left);
        _centre.Add(_run.Middle);
        _right.Add(_run.Right);

        _run.Sync();
        ApplyLock();
        ShowPlot();
    }

    Run Bind(ref Run run, ParamTile tile)
    {
        run ??= new Run(this, tile);
        run.Apply(tile);
        return run;
    }

    void ApplyLock()
      => Controls.SetLocked(Root, _locked && _tile is ParamTile);

    // What the plot is handed, which for a lock is the sound the lock makes rather than
    // the channel's own: a row that moves FM amount and leaves the picture still would
    // read as broken. The patch is resolved the way the sequencer resolves it, which is
    // the whole of why ParamTile.ApplyTo exists.
    void ShowPlot()
    {
        if (_run == null) return;

        var patch = _editor.Project.Patches[_channel];
        if (_tile is ParamTile param) param.ApplyTo(ref patch);

        _plot.Show(patch);
    }

    // A column of the body. The two outer ones are a field wide and the middle is two
    // fields and the gap between them, which is what puts four field columns and three
    // gaps inside the panel's own two insets — see Controls.WidePanelWidth, where that
    // arithmetic is written once.
    static VisualElement Slot(float width, bool gap)
    {
        var slot = new VisualElement();
        slot.style.width = width;
        slot.style.flexShrink = 1;
        if (gap) slot.style.marginRight = Controls.PanelGap;
        return slot;
    }

    // Two fields across, for the middle column. A plain element rather than a
    // Controls.Row, because the two rows inside it carry their own bottom gap and a Row
    // would lay a second one under them; BuildPalette does the same thing for the same
    // reason.
    //
    // Neither field is given a width. They split the row evenly from a basis of nothing,
    // which comes out at a field apiece and also shrinks evenly when the panel is
    // narrowed into a safe area — where two fixed widths would have had to be told how
    // to give way.
    static VisualElement Pair(VisualElement first, VisualElement second)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        foreach (var field in new[] { first, second })
        {
            field.style.flexGrow = 1;
            field.style.flexBasis = 0;
            field.style.flexShrink = 1;
            row.Add(field);
        }

        first.style.marginRight = Controls.PanelGap;

        return row;
    }

    // The bank hands out a reference, which is what lets a field be written in place.
    //
    // Which channel comes off the tile the panel is showing, which is what lets the
    // patch's run be handed from one CHAN cell to the next without being made again. The
    // run is only ever on the panel while that tile is a CHAN tile, so the other branch
    // cannot be reached from the UI — but a ref return has to point somewhere regardless,
    // and the first channel is a place in the bank rather than a patch of this panel's
    // own to keep and explain.
    ref FmPatch Patch()
      => ref _editor.Project.Patches[_tile is ChannelTile channel ? channel.Channel : 1];

    // Nothing to tell the sequencer either way: it reads the bank afresh every instant,
    // since a lock never outlives one.
    //
    // The plot is repainted from here because this is the one path to the fifteen that
    // does not pass through Commit. A lock row writes its tile and commits, and the
    // score change comes back around through JacquardUI.OnChanged to Refresh, which
    // shows the plot on its way out; the patch's own run writes the bank, which is not
    // the score and has nothing to commit. So the six that reach the picture would move
    // under a drag with the picture standing still — which is exactly what a waveform
    // display must not do. Show is a struct compare on the other nine, so calling it on
    // every step of every drag is what the guard in SoundPlot is for rather than a cost
    // to be avoided here.
    void Set(int target, float value)
    {
        ParamTargets.Set(ref Patch(), target, value);
        ShowPlot();
    }

    // The note a new tile would arrive as rather than a middle C, so a patch is heard
    // where the piece is being written: see ScoreEditor.PreviewRemembered, which owns
    // the argument along with the note it reads.
    void Audition()
    {
        if (_tile is ChannelTile channel) _editor.PreviewRemembered(channel.Channel);
    }

    // What a lock row shows while nothing holds it: where the channel already stands for
    // an absolute lock, and no shift at all for a relative one. Either way it is what the
    // parameter does if this tile is left alone, which is also where a drag that takes
    // hold of it starts from.
    float Released(ParamTile tile, int target)
      => tile is AbsoluteParamTile
         ? ParamTargets.Get(_editor.Project.Patches[_channel], target) : 0.0f;

    // One binding's worth of the fifteen: the three columns they stand in, and — for a
    // lock — the rows that have to be pointed at whichever tile is being shown.
    //
    // tile is null for the patch's own run, which is the one that reads the bank rather
    // than a tile and so has nothing to be re-pointed at: what moving between two CHAN
    // cells changes is the field the getters already read through.
    sealed class Run
    {
        public readonly VisualElement Left, Middle, Right;

        public Run(SoundPanel panel, ParamTile tile)
        {
            var columns = new[] { new VisualElement(), new VisualElement(),
                                  new VisualElement() };
            var follows = new bool[columns.Length];

            if (tile != null) _rows = new LockRow[ParamTargets.Count];

            VisualElement pending = null;

            foreach (var (where, group, caption, target) in Fields)
            {
                var column = columns[(int)where];

                if (group != null)
                {
                    // A group heading always opens a row, so a pair never straddles one.
                    // Which holds because every group in the middle column has an even
                    // number of fields in it.
                    pending = null;
                    column.Add(Controls.Heading(group, follows[(int)where]));
                    follows[(int)where] = true;
                }

                VisualElement field;

                if (tile == null)
                {
                    // Every bar sounds a note on the channel once its value has settled,
                    // which is the whole of the auditioning: a drag down a bar is one
                    // note rather than a burst of them, and a parameter is heard where it
                    // was left. And every name is double clicked to put its parameter
                    // back where a fresh patch holds it, which is the same gesture that
                    // lets a lock go of its target: a row taken back to saying nothing of
                    // its own. A whole patch cannot be reset in one press, and
                    // deliberately — a sound is arrived at one parameter at a time, and
                    // the way back from a dead end is the parameter that was last touched
                    // rather than everything that came before it.
                    field = Controls.Bar(caption, ParamRanges.Of(target),
                                         () => ParamTargets.Get(panel.Patch(), target),
                                         value => panel.Set(target, value),
                                         panel.Audition,
                                         () => ParamTargets.Get(FmPatch.Default, target),
                                         Controls.FieldLabelWidth);
                }
                else
                {
                    field = _rows[target] = new LockRow(panel, tile, target, caption);
                }

                if (where != Column.Middle) { column.Add(field); continue; }

                if (pending == null) { pending = field; continue; }

                column.Add(Pair(pending, field));
                pending = null;
            }

            (Left, Middle, Right) = (columns[0], columns[1], columns[2]);
        }

        // Points a lock run at another lock of its own kind. The patch's run has nothing
        // to point: it reads whichever CHAN tile the panel is holding.
        public void Apply(ParamTile tile)
        {
            if (_rows == null) return;
            foreach (var row in _rows) row.Apply(tile);
        }

        // Pulls the run back in line with what it is showing, for the times the panel is
        // not rebuilt: a value changed from the keys, a load, a lock's own Commit.
        public void Sync()
        {
            if (_rows == null)
            {
                ValueBar.SyncAll(Left);
                ValueBar.SyncAll(Middle);
                ValueBar.SyncAll(Right);
                return;
            }

            foreach (var row in _rows) row.Sync();
        }

        // Null on the patch's own run, which is the whole of what tells the two apart.
        readonly LockRow[] _rows;
    }

    // A parameter of a lock, held or not.
    //
    // A row starts released and greyed, and reads out what the channel does without it.
    // Moving its bar is what takes hold of that parameter — there is no separate step for
    // arming one, since a value nobody set is not a lock — and clicking its name lets go
    // again. Whatever is left grey is untouched by this tile, so a lock holding nothing
    // at all does nothing at all, which is what a freshly placed one is.
    //
    // An element of its own rather than a row plus a list of closures, for the reason
    // ValueBar.SyncAll gives: the tree already knows what is on screen. Which is worth
    // more than it was when it only saved a list — being an element is what gives the row
    // a Sync of its own and a tile it can be pointed at, and so what lets the run it
    // stands in outlive the showing it was made in.
    sealed class LockRow : VisualElement
    {
        public LockRow(SoundPanel panel, ParamTile tile, int target, string caption)
        {
            (_panel, _tile, _target) = (panel, tile, target);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.marginBottom = Controls.Gap;

            // The name is the control that lets go of the parameter, double clicked, and
            // the hover is this row's rather than the caption's own because a held row
            // already lights its name. See Controls.ActionCaption.
            _caption = Controls.ActionCaption(caption, Toggle, SetHover,
                                              Controls.FieldLabelWidth);
            Add(_caption);

            // An absolute lock holds a value the target could hold itself, so its bar is
            // the target's own; a relative one holds a shift, and reads from the middle.
            // Neither depends on whether the row is held, so taking hold of a parameter
            // never rebuilds anything — and neither depends on which tile is being shown,
            // only on its kind, which is the whole of why there are two lock runs and not
            // one.
            //
            // No settled callback. A lock is not auditioned: what it would have to sound
            // is a note of the channel it colours, and the step it colours is somewhere
            // else on the plane.
            var range = tile is AbsoluteParamTile
              ? ParamRanges.Of(target) : ParamRanges.Relative(target);

            _bar = Controls.Bar(range, Get, Set);
            _bar.style.flexGrow = 1;
            Add(_bar);

            Sync();
        }

        // Points the row at another lock. Only ever another of the same kind, since the
        // bar's range was decided above and the two kinds do not share one.
        //
        // The naming is TileElement.Apply's, and so is the obligation behind it:
        // anything this row shows that comes off the tile has to be written here, in the
        // same edit that adds it. Today that is the two things Sync covers. A third,
        // added later and left out, is the one failure this design can have and the one
        // the compiler cannot see.
        public void Apply(ParamTile tile)
        {
            _tile = tile;

            // Or a row the pointer was over when the cursor moved elsewhere comes back
            // lit: the run is detached rather than thrown away, so no PointerLeaveEvent
            // is ever sent to it. The bar inside guards its own lift the same way, on
            // DetachFromPanelEvent.
            _hover = false;

            Sync();
        }

        // Pulls the row back in line with the tile, in both of the things it shows: the
        // number, and whether the lock is holding it.
        public void Sync()
        {
            _bar.Sync();
            UpdateAppearance();
        }

        // Private members

        readonly SoundPanel _panel;
        readonly int _target;
        readonly Label _caption;
        readonly ValueBar _bar;

        // Whichever lock the run is standing for now. See Apply.
        ParamTile _tile;

        bool _hover;

        bool Engaged => _tile.IsEngaged(_target);

        float Get() => Engaged ? _tile[_target] : _panel.Released(_tile, _target);

        // Setting a value is what takes hold of the parameter. Nothing else does, which
        // is what makes an untouched row mean untouched.
        void Set(float value)
        {
            _tile.Engage(_target, value);
            UpdateAppearance();
            _panel._editor.Commit();
        }

        // Letting go is the only thing the name does that the bar cannot. Taking hold
        // from it as well is worth having anyway: a parameter is sometimes wanted exactly
        // where it already is, and there is no drag that says so.
        void Toggle()
        {
            if (Engaged) _tile.Release(_target); else _tile.Engage(_target, Get());

            Sync();
            _panel._editor.Commit();
        }

        void SetHover(bool on)
        {
            _hover = on;
            UpdateAppearance();
        }

        // A released row is dimmed whole, bar and all, the way the rails and a note's
        // length label are dimmed: it is the same content, further back. The name lights
        // under the pointer, since clicking it is the one thing here that a greyed
        // control would otherwise say is not available.
        void UpdateAppearance()
        {
            var engaged = Engaged;

            style.opacity = engaged ? 1.0f : Style.DimmedOpacity;
            _caption.style.color = engaged || _hover ? Style.NoteText : Style.Label;
        }
    }
}

} // namespace Jacquard.App
