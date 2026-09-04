using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The detail window for whatever the cursor is on.
//
// sequencer-spec.md draws the line here: a cell carries the kind of tile and the figure
// you need in order to read the score, and everything else — which parameter a lock
// points at, how far it moves it, the exact percentage behind a pie chart — is set
// in a window of its own rather than crammed into thirty pixels.
//
// It is also where a tile is put down and taken away, since the cursor is already
// the answer to where: a cell that will take a tile offers the tiles instead of a
// description of nothing, ground with no lane on it offers a lane, and everything
// else offers what it is and a way to remove it. There is no palette elsewhere on
// the screen, so a button that is not on this panel cannot apply to this cell.
//
// Everything the cell decides is here, which is why the panel runs long: a channel
// start carries the lane it heads and the sound that channel is voiced in as well as
// its own three rows, and a lock carries a row for every parameter it could take hold
// of. Those were panels of their own stacked under this one, each up only while this
// one was showing a particular kind of tile — which is a group of this panel wearing a
// frame, and a second header saying what the cursor had already said. What pays for the
// length is that a column of panels scrolls.
//
// What it cost is the reason for the shape of Refresh. The panel was 152 elements and
// 122 of them were one of those two long groups — fifteen rows of eight for the sound,
// fifteen rows for a lock. Built afresh on every click, a channel start came to 1.12 ms
// and 451 KB of garbage, 357 KB of it the sound group alone. Which is a rebuild paying
// for a picture that is the same picture: the fifteen parameters are the same fifteen in
// the same order whichever tile the cursor is on, and all that differs is what they read
// and write.
//
// So the fields wait for their first edit and the two groups are kept, and between them a
// channel start click is 85 KB and 0.31 ms, from 451 KB and 1.12 ms — the fields a
// hundred and sixty-odd KB of it and the groups two hundred, and the forty-five elements
// they took off the panel are three apiece for fifteen fields that were never opened. A
// lock click is ten or twelve KB, from 212. What a click builds is thirty elements rather
// than a hundred and fifty. The two arms are one session and one score apart and nothing
// else: the reuse was defeated at runtime for the measurement, the three fields emptied
// before each showing, which is what every showing used to do.
//
// What the reuse does not buy is the style resolution behind it, and that was worth
// measuring rather than assuming. It is 30.4 KB either way, to the tenth of a KB, and the
// time barely moves: UI Toolkit resolves style over a subtree handed back to it much as
// over one built from nothing, and the text shaping goes the same way. Keeping the groups
// permanently parented and hiding them with display instead takes that 30.4 KB to
// nothing — measured, not guessed — and it is deliberately not done. What the Clear in
// Refresh buys is that everything under _body is on screen and bound to the tile being
// shown, which is the whole of why the sync path there can sweep the tree with no filter
// and why ValueBar.SyncAll can argue against keeping a list at all. Hidden groups would
// make both sweeps wrong by default; they would have the sync path — which a scrubbed bar
// runs on every pointer-move frame — reach fifteen bars and fifteen rows through
// forty-five and thirty; and they would leave a rule with no method to belong to, that a
// hidden row must stay safe on a tile since deleted. Thirty kilobytes a click is the
// cheaper thing to go on paying.

sealed class InspectorPanel
{
    public VisualElement Root { get; }

    public InspectorPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Tile", out _title);

        _body = new VisualElement();
        Root.Add(_body);

        Refresh(true);
    }

    // Rebuilt only when the cursor has moved onto something else: a control that edits
    // the tile in place would otherwise pull itself out from under the drag that drove
    // it.
    //
    // And rebuilt is less than it sounds even then. The two long groups — the sound
    // under a channel start, the fifteen rows under a lock — are made on the first tile
    // that calls for them and kept in fields afterwards; what a move onto another tile
    // of the same kind does is clear the body, point the standing group at the new tile
    // and add it back. So there are two paths through here and not one shape of work:
    // the early return below, for a tile that is still the same tile, and the build,
    // which for these two is itself only a re-binding. Everything else on the panel —
    // the palette, a note's bars, a channel's steppers, the delete button — is short
    // enough to be made again and is.
    public void Refresh(bool force = false)
    {
        var tile = _editor.Selected;
        var lane = _editor.SelectedLane;
        // Two empty cells are not the same cell: one may take a tile and the other
        // only a lane, and neither has a tile to tell them apart by.
        var place = _editor.CanPlace;
        // And a lock can change channels without moving: renumbering the CHAN above it
        // does that, and so does dragging the lane under a different jump. Asked only
        // for a lock, since nothing else here is built out of it.
        var channel = tile is ParamTile ? _editor.Score.ChannelOf(lane) : 0;

        if (!force && tile == _tile && lane == _lane && place == _place &&
            channel == _channel)
        {
            // The same tile, but its values may have changed from elsewhere — a
            // transpose from the keys, a load — so the bars still have to be pulled
            // back in line with it. The lap switches go the same way, and one of the
            // things that moves them is the Period bar standing over them.
            //
            // The Play switch above all, since the thing that moves it most is a double
            // click on the very cell this panel is showing: the tile is the same tile, so
            // nothing here is rebuilt, and a switch left as it was drawn would read On
            // over a cell that had just gone grey.
            ValueBar.SyncAll(_body);
            _laps?.Sync();
            SyncPlay();
            // And the channel number beside it, for the same reason and from the other
            // side of the screen: the Channels panel can exchange two channels, which
            // leaves this the same ChannelTile object with a different number on it.
            _syncChannel?.Invoke();
            // A lock row shows a bar and whether the lock is holding it, and the second
            // of those is not a bar. Everything it shows can change without the row
            // changing, which is why they are synced rather than built again.
            foreach (var row in _body.Query<LockRow>().Build()) row.Sync();
            return;
        }

        (_tile, _lane, _place, _channel) = (tile, lane, place, channel);

        _body.Clear();
        Build(tile, lane);
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly Label _title;
    readonly VisualElement _body;

    Tile _tile;
    Lane _lane;
    bool _place;
    int _channel;

    // The bars are found again by a query over the body; these two are held onto
    // instead, and let go of whenever the body they stood in is cleared — they belong to
    // one tile, and the next showing builds its own.
    LapSwitches _laps;
    Button _play;

    // A chooser paints once and when it is stepped, so the one on a CHAN tile shows a
    // number that can move without this panel being rebuilt — see BuildChannel.
    System.Action _syncChannel;

    // The three that do not. These outlive every clear of the body: made on the first
    // tile that asks for them, detached and re-added afterwards. See BuildSound and
    // BuildLock, and the paragraph on Refresh for what keeping them is worth.
    VisualElement _sound;
    LockGroup _lockAbsolute, _lockRelative;

    void Build(Tile tile, Lane lane)
    {
        _title.text = Title(tile);

        // The two that belong to one tile, and the readout that belongs to one of them.
        // The three kept groups are deliberately not here: they are handed the new tile
        // instead, which is the whole point of them.
        (_laps, _play, _syncChannel) = (null, null, null);

        // Free ground, whether that is a lane's own empty step or the terminator it
        // grows from. What such a cell is for is the tile that goes on it.
        if (_place) { Section(BuildPalette()); return; }

        // Off any lane there is nothing to put a tile on, so what is on offer is
        // somewhere to put one.
        if (tile == null) { Section(BuildNewLane()); return; }

        Section(BuildTile(tile));

        // The head is the one cell that is the lane rather than something standing
        // on it, so it is where the lane itself is worked on.
        if (_editor.Cell.Kind == CellKind.Head && lane != null) Section(BuildLane(lane));

        // And under the lane, for the one head that names a channel, the sound that
        // channel is voiced in.
        if (tile is ChannelTile) Section(BuildSound());

        Section(BuildDelete());
    }

    // Adds a section unless it turned out to hold nothing: a lock keeps everything it
    // owns on the Lock panel, so what would go here is an empty box, and a section that
    // carries air over it would leave that air behind.
    //
    // Which is now only about BuildTile, and there only about the flow tiles that have
    // nothing to set — a jump, a terminator, a jump target. The kept groups always have
    // their rows in them, so neither the sound nor a lock can ever arrive here empty.
    //
    // Nothing is drawn between two of them. What ends up on this panel is a list of
    // rows and a button under it, so the only break is the air the foot row carries;
    // a rule between the sections would be marking a seam the eye has no use for, now
    // that a rule belongs to a heading rather than to the seam between two groups.
    void Section(VisualElement content)
    {
        if (content.childCount == 0) return;
        _body.Add(content);
    }

    // The panel's own header, which says what is under the cursor rather than which
    // panel this is. A cell that holds nothing is the only thing here that is not a
    // tile, and the pitch is left out of a note's: the bar underneath spells it, and
    // a header that changed as a pitch was dragged would be a second readout.
    static string Title(Tile tile) => tile == null ? "Empty Cell" : Name(tile) + " Tile";

    // In words, not in tokens. The four character codes are how a tile is spelled in
    // a saved file and how this codebase talks about one, and neither is a reason to
    // make a user learn that PABS is the lock that sets a value: the cell already
    // carries the icon, and what this name owes it is what it does.
    static string Name(Tile tile) => tile switch
    {
        NoteTile => "Note",
        AbsoluteParamTile => "Absolute Lock",
        RelativeParamTile => "Relative Lock",
        CycleGateTile => "Cycle Gate",
        ProbGateTile => "Chance Gate",
        ChannelTile => "Channel Start",
        TerminatorTile => "Lane End",
        JumpTile => "Jump",
        JumpDestTile => "Jump Target",
        _ => "Unknown"
    };

    // The tiles a free cell will take, the note first because that is what most of
    // them get. Nothing here asks where: the cell asked for the list.
    VisualElement BuildPalette()
    {
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;

        foreach (var (label, kind) in Palette)
        {
            var pick = kind;
            var button = Controls.Push(label, () => Act(() => _editor.Put(pick)),
                                       PaletteButtonWidth);
            button.style.marginBottom = Controls.Gap;
            grid.Add(button);
        }

        return grid;
    }

    VisualElement BuildNewLane()
    {
        var row = Controls.Row();
        row.Add(Controls.Push("New lane", () => Act(_editor.NewChannelLane), 66));
        return row;
    }

    // The controls belonging to the tile itself. A jump has none: there is nothing to
    // set on one, since where it goes is drawn on the plane.
    VisualElement BuildTile(Tile tile)
    {
        // A lock's group is one of the two this panel keeps between showings, so it is
        // handed back whole rather than filled into a box made here. Taken before the
        // switch rather than as a case of it, since a case would have nothing to return.
        if (tile is ParamTile param) return BuildLock(param);

        var body = new VisualElement();

        switch (tile)
        {
            case NoteTile note: BuildNote(body, note); break;
            case CycleGateTile cycle: BuildCycle(body, cycle); break;
            case ProbGateTile prob: BuildProb(body, prob); break;
            case ChannelTile channel: BuildChannel(body, channel); break;
        }

        return body;
    }

    // What one parameter lock takes hold of.
    //
    // The same list the sound group shows, in the same order and over the same ranges,
    // because it is the same set: every field of the patch is a lock target. Reading a
    // lock against the timbre it colours only works if the two are laid out alike.
    //
    // A row starts released and greyed, and reads out what the channel does without it.
    // Moving its bar is what takes hold of that parameter — there is no separate step
    // for arming one, since a value nobody set is not a lock — and clicking its name
    // lets go again. Whatever is left grey is untouched by this tile, so a lock holding
    // nothing at all does nothing at all, which is what a freshly placed one is.
    //
    // The heading carries the channel, which is the one thing here a lock cannot say for
    // itself: the tile does not hold a number, and a branch lane borrows one from the
    // jump that reaches it. It was the header of a panel of its own, standing under this
    // one and up only while this one was showing a lock — a group of this panel wearing
    // a frame, the same as the sound was.
    //
    // Being the same set in the same order as the sound group is also what lets the two
    // be built the same way: fifteen rows that are the same fifteen rows whichever tile
    // the cursor is on, so what moving between two locks changes is what they read and
    // write and nothing about the run itself. Kept and pointed at the new tile, the way
    // the sound group is and for the same measured reason.
    VisualElement BuildLock(ParamTile tile)
    {
        var absolute = tile is AbsoluteParamTile;
        var group = absolute ? _lockAbsolute : _lockRelative;

        if (group == null)
        {
            group = new LockGroup(this, tile);
            if (absolute) _lockAbsolute = group; else _lockRelative = group;
        }

        group.Apply(tile, _channel);
        return group;
    }

    // What a lock row shows while nothing holds it: where the channel already stands for
    // an absolute lock, and no shift at all for a relative one. Either way it is what the
    // parameter does if this tile is left alone, which is also where a drag that takes
    // hold of it starts from.
    float Released(ParamTile tile, int target)
      => tile is AbsoluteParamTile
         ? ParamTargets.Get(_editor.Project.Patches[_channel], target) : 0.0f;

    // One channel's timbre, under the tile that names the channel.
    //
    // It was a panel of its own standing under this one, which is a panel that could
    // only ever be up while this one was showing a CHAN tile, headed with the number
    // the row above it already gives. What it is instead is the last group of the tile
    // it belongs to: the cursor is on the cell, and everything the cell decides is on
    // the one panel, in the order the decisions are made — which lane, how it runs,
    // how long it is, and then what it sounds like.
    //
    // What it lists is the parameter lock targets in their own order, and that is the
    // whole patch: seeing what a lock can reach, and where the channel currently sits
    // inside each range, is what makes a lock's amount mean something.
    //
    // The rows read the channel off the tile under the cursor rather than off a number
    // of their own, so renumbering the CHAN cell moves the whole group to the other
    // channel with nothing rebuilt. What widens that from a renumber to a different CHAN
    // cell is only where the tile is read from: a field the panel already keeps rather
    // than a value the rows closed over when they were made. So the group is made once
    // and handed on, and moving between two CHAN cells is fifteen readouts changing
    // rather than a hundred and twenty elements being built — see Refresh for what that
    // is worth. The field has to hold the tile and never its number, or the renumber
    // half of the promise above goes with it.
    //
    // A channel with no lane cannot be edited here, which costs nothing: it has no way
    // of sounding either, and its patch is still saved and loaded with the rest.
    VisualElement BuildSound()
    {
        if (_sound != null) { ValueBar.SyncAll(_sound); return _sound; }

        _sound = new VisualElement();

        _sound.Add(Controls.Heading("Sound", follows: true));

        // Every bar sounds a note on the channel once its value has settled, which is
        // the whole of the auditioning: a drag down a bar is one note rather than a
        // burst of them, and a parameter is heard where it was left. There is no button
        // beside it asking for the same note again — a bar that has just been moved has
        // already played it, and one that has not is one nothing was asked about.
        //
        // And every name is double clicked to put its parameter back where a fresh patch
        // holds it, which is the same gesture that lets a lock go of its target: a row
        // taken back to saying nothing of its own. A whole patch cannot be reset in one
        // press, and deliberately — a sound is arrived at one parameter at a time, and
        // the way back from a dead end is the parameter that was last touched rather
        // than everything that came before it.
        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var index = target;
            _sound.Add(Controls.Bar(ParamTargets.Name(index), ParamRanges.Of(index),
                                    () => ParamTargets.Get(Patch(), index),
                                    value => Set(index, value),
                                    Audition,
                                    () => ParamTargets.Get(FmPatch.Default, index)));
        }

        return _sound;
    }

    // The bank hands out a reference, which is what lets a field be written in place
    // and a lock target be pointed at.
    //
    // Which channel comes off the tile the panel is showing, which is what lets the
    // sound group be handed from one CHAN cell to the next without being made again.
    // The group is only ever on the panel while that tile is a CHAN tile, so the other
    // branch cannot be reached from the UI — but a ref return has to point somewhere
    // regardless, and the first channel is a place in the bank rather than a patch of
    // this panel's own to keep and explain.
    ref FmPatch Patch()
      => ref _editor.Project.Patches[_tile is ChannelTile channel ? channel.Channel : 1];

    // Nothing to tell the sequencer either way: it reads the bank afresh every instant,
    // since a lock never outlives one.
    void Set(int target, float value) => ParamTargets.Set(ref Patch(), target, value);

    // The note a new tile would arrive as rather than a middle C, so a patch is heard
    // where the piece is being written: see ScoreEditor.PreviewRemembered, which owns
    // the argument along with the note it reads.
    void Audition()
    {
        if (_tile is ChannelTile channel) _editor.PreviewRemembered(channel.Channel);
    }

    VisualElement BuildDelete()
    {
        // Deleting a lane's head is how the whole lane goes, which the button had
        // better say rather than leave to be found out.
        var head = _editor.Cell.Kind == CellKind.Head;

        var row = Controls.Foot();
        row.Add(Controls.Push(head ? "Delete lane" : "Delete",
                              () => Act(_editor.Delete), head ? 74 : 54));
        return row;
    }

    // A pitch is two bars and not one, because the two halves of it are set for
    // different reasons and a single bar serves neither. Eighty-four semitones over the
    // hundred and sixty pixels a drag covers is under two pixels a note, so landing on
    // the note meant was a matter of luck, and moving an octave meant carrying the bar
    // most of the way across the panel. Split, a semitone is thirteen pixels and an
    // octave is eighteen, and the letter can be changed without disturbing the register
    // or the register without disturbing the letter — which is how a pitch is thought
    // about anyway, and how the cell has always drawn one.
    //
    // Neither bar holds anything. They read the two halves off the one note number and
    // write it back together, so the tile is unchanged and so is the file: what is
    // stored is still one MIDI note. The bar being dragged pulls its partner along with
    // it through the Refresh that Touch runs, and a drag survives that because it is
    // measured from where the hand went down rather than from the value.
    //
    // The class bar stops at B rather than turning the octave over. A drag on a bar is
    // clamped to its own travel, so a carry could never come from one anyway, and the
    // octave is the next row down.
    //
    // The note is heard where a drag ends rather than at every semitone it crosses: a
    // scrub over an octave is twelve notes on top of each other and none of them the one
    // being chosen. A typed pitch sounds straight away, since it never passed through
    // the eleven others.
    void BuildNote(VisualElement body, NoteTile note)
    {
        body.Add(Controls.Bar("Note", NoteRange, () => Pitch.ToClass(note.Note),
                              value => SetPitch(note, Pitch.ToOctave(note.Note),
                                                Mathf.RoundToInt(value)),
                              () => _editor.Preview(note.Note)));

        body.Add(Controls.Bar("Octave", OctaveRange, () => Pitch.ToOctave(note.Note),
                              value => SetPitch(note, Mathf.RoundToInt(value),
                                                Pitch.ToClass(note.Note)),
                              () => _editor.Preview(note.Note)));

        // Length is in steps, so what it means in real time depends on the
        // channel's division, its gate ratio and the project tempo.
        body.Add(Controls.Bar("Length", LengthRange, () => note.Length,
                              value => { note.Length = Mathf.Clamp(value, 0.25f, 64.0f);
                                         _editor.RememberNote(note);
                                         Touch(); }));
    }

    // Where the two halves are put back together, so that the clamp and what follows it
    // are written once rather than once per bar.
    void SetPitch(NoteTile note, int octave, int pitchClass)
    {
        note.Note = Mathf.Clamp(Pitch.FromParts(octave, pitchClass),
                                Pitch.Lowest, Pitch.Highest);
        _editor.RememberNote(note);
        Touch();
    }

    // The period, and then a switch per lap of it.
    //
    // Which lap a gate fires on used to be a second bar, which could only ever name
    // one of them: a gate on the first and the third of four was two gates in two
    // cells, and nothing about the tile required that. A switch per lap says any of
    // the patterns the tile can hold, and it says it as the shape the cell then draws
    // — the run under the bar and the boxes on the plane are the same row of laps
    // read at two sizes.
    //
    // It stands under a heading rather than beside a caption because it is a block
    // and not a row: the caption column would leave a hundred pixels for thirty-two
    // switches, which is a target no fingertip could land on.
    void BuildCycle(VisualElement body, CycleGateTile cycle)
    {
        body.Add(Controls.Bar("Period", PeriodRange, () => cycle.Period,
                              value => { cycle.Period = Mathf.RoundToInt(value);
                                         Touch(); }));

        body.Add(Controls.Heading("Fires on", follows: true));

        _laps = new LapSwitches(cycle, lap => Act(() =>
                  { cycle.SetFires(lap, !cycle.Fires(lap)); Touch(); }));
        body.Add(_laps);
    }

    void BuildProb(VisualElement body, ProbGateTile prob)
      => body.Add(Controls.Bar("Chance", ChanceRange, () => prob.Percent,
                               value => { prob.Percent = value; Touch(); }));

    void BuildChannel(VisualElement body, ChannelTile channel)
    {
        // Whether the lane runs at all, which is also what a double click on the cell
        // toggles — this is the same switch written down where a tile's settings are named.
        //
        // On the master lane it can still be thrown and it still saves, and the lane goes
        // on running: which lane is the master is a position and not a property, so the
        // switch belongs to the lane for whenever it stops being the one. The cell is what
        // shows the difference, by staying solid.
        // The state is written on it as well as shown by the fill, the way the step
        // length row prints the division it is set to: one press instead of the chooser's
        // two arrows, and the same reading either way.
        //
        // Pressed, it writes the switch and lets Touch bring the panel back into line, the
        // same road a double click on the cell takes. Two roads into one control is exactly
        // where the two would drift apart if each drew itself.
        _play = Controls.Push("", () => { channel.Enabled = !channel.Enabled; Touch(); }, 44);
        SyncPlay();

        var row = Controls.Row();
        row.Add(Controls.Caption("Play"));
        row.Add(_play);
        body.Add(row);

        // Stepped through with arrows rather than scrubbed on a bar. A channel is not a
        // quantity: eight of them is a list of eight things to sound on, and the
        // distance between the third and the seventh is not four of anything. A bar
        // said otherwise twice over — it drew a fill that grew as if a higher channel
        // were more of something, and it asked a hand to hit one eighth of its length
        // to pick one. The arrows are what this panel already gives a choice from a
        // written-down list, which is what the row under this one is.
        var channels = new List<string>();
        for (var i = 1; i <= PatchBank.Channels; i++) channels.Add(i.ToString());

        // The readout is held onto rather than left to paint itself, which is the Play
        // switch's problem again from the other side of the screen: the Channels panel
        // can exchange two channels, and what that leaves under the cursor is the same
        // ChannelTile object carrying a different number — so Refresh takes its early
        // return and this row would go on showing the number that was there.
        //
        // Widening what Refresh calls the same tile to include the number is not an
        // option, and not merely because it would rebuild more than it has to: stepping
        // this chooser calls set and then paints, set runs Touch and so Refresh, and a
        // rebuild would clear the body out from under the arrow that is mid-press and
        // then write text onto a label that is no longer in the tree.
        body.Add(Controls.Chooser("Channel", channels,
                                  () => channel.Channel - 1,
                                  index => { channel.Channel = index + 1; Touch(); },
                                  out _syncChannel));

        var divisions = new List<string>();
        foreach (var d in ChannelTile.Divisions) divisions.Add("1/" + d);

        // "Step length" and not "Step", which named the thing rather than what is being
        // set about it: what the row holds is how long one step of this lane lasts, and
        // it stands two rows above a count of those steps. One of them says how long and
        // the other says how many, and neither can be read as the other now.
        body.Add(Controls.Chooser("Step length", divisions,
                                  () => System.Array.IndexOf(ChannelTile.Divisions,
                                                             channel.Division),
                                  index => { channel.Division = ChannelTile.Divisions[index];
                                             Touch(); }));
    }

    // Pulls the Play switch back onto whatever the tile now says, for the times the panel
    // is not rebuilt: the cell it is showing is the cell a double click toggles.
    void SyncPlay()
    {
        if (_play == null || _tile is not ChannelTile channel) return;

        _play.text = channel.Enabled ? "On" : "Off";
        Controls.SetActive(_play, channel.Enabled);
    }

    // What the head cell sets about the lane hanging off it, which is one row.
    //
    // No heading over it. A heading is for a group, and a group of one is a line of
    // chrome saying what the row under it could say itself: named "Lane steps" the row
    // is already the lane's, and on a jump target — where there is nothing else on the
    // panel at all — the heading was a title over a single stepper.
    //
    // So it joins the rows above it rather than standing apart from them, which is also
    // the truth of the thing: how long a step lasts is on the head cell and how many of
    // them there are is the lane, and a hand setting one is usually setting the other.
    VisualElement BuildLane(Lane lane)
    {
        var body = new VisualElement();

        // The one number here that is still stepped rather than scrubbed: a step is a
        // cell, growing only happens where there is free ground for one, and a refused
        // step is something to see one at a time rather than to drag through.
        body.Add(Controls.Stepper("Lane steps", () => lane.Steps.Count,
                                  value => _editor.ResizeLane(
                                    value > lane.Steps.Count ? 1 : -1), 1, "0"));

        // Where the lane sits is not set here. A lane further down runs later, so
        // moving one is also how an accent lane gets to overwrite what the lanes
        // above it did — and that is a thing to see happen on the plane, which is
        // what dragging the head cell does.

        return body;
    }

    // Runs an edit and hands the keys back to the grid: a click leaves the focus on
    // the button, and the arrows are supposed to land on the plane.
    void Act(System.Action action)
    {
        action();
        _editor.View?.Focus();
    }

    // Redraws the score for a change made here, without rebuilding this panel.
    void Touch()
    {
        _editor.Commit();
        Refresh();
    }

    // Parameter ranges
    //
    // What a synth parameter's bar covers comes from ParamRanges, which the Lock and
    // Sound panels use. These are the ranges of the sequencer's own
    // numbers, which nothing outside this panel has to know about.

    // The letter half of a pitch, read out as the letter and nothing else: the number
    // behind it is an index into the twelve and says nothing a name does not, which is
    // the one case Range.Display exists for. Typing still goes through that number, the
    // same as the pitch bar these two replaced.
    static readonly ValueBar.Range NoteRange =
      ValueBar.Integer(0.0f, 11.0f,
                       value => Pitch.ToClassName(Mathf.RoundToInt(value)));

    // The register half, which stops one short of the plane's own top. C9 is the highest
    // note there is and it is the only one in its octave, so a bar reaching it would
    // spend a twelfth of its travel on a stop where eleven of the twelve letters are
    // refused and the bar above snaps back to C. Every octave this one covers takes all
    // twelve; the last note is still typed.
    static readonly ValueBar.Range OctaveRange = ValueBar.Integer(0.0f, 8.0f);

    // A length in steps. Dragging lands on quarters of one, since that is where a note
    // either fits the grid or deliberately overlaps the step after it, and it reaches
    // eight where the tile allows sixty-four: a note that long is typed, not scrubbed.
    //
    // The unit is printed because this and the channel's gate ratio are the same
    // multiplication on the step, and the step is what tells them apart: this one
    // counts them, that one takes a percentage of what this one counted.
    static readonly ValueBar.Range LengthRange =
      new ValueBar.Range(0.25f, 8.0f, snap: 0.25f, unit: "steps");

    // Whole percents. The wedge on the cell cannot show a tenth of one anyway, and any
    // percentage at all is still allowed by typing it.
    static readonly ValueBar.Range ChanceRange =
      new ValueBar.Range(0.0f, 100.0f, snap: 1.0f, digits: 0, unit: "%");

    // How many laps the cycle is long, which is also how many switches stand under
    // it. Dragging it covers the whole range, since every value on the way is a
    // pattern the run below can be read at.
    static readonly ValueBar.Range PeriodRange =
      ValueBar.Integer(CycleGateTile.MinPeriod, CycleGateTile.MaxPeriod);

    // What can be put on a free cell, in the order the buttons read: the note first
    // because it is the tile a cell usually wants, then the pairs, then the jump.
    //
    // Each button says what the tile does rather than what it is called in a file. A
    // row of four letter tokens fitted three to a line and told a newcomer nothing,
    // and this panel is the only place a tile is ever chosen, so the words have to
    // carry it. They are the same words the header then shows over the placed tile, so
    // a button and what it made read as the same thing.
    static readonly (string Label, TileKind Kind)[] Palette =
      { ("Note", TileKind.Note),
        ("Jump", TileKind.Jump),
        ("Absolute Lock", TileKind.AbsoluteLock),
        ("Relative Lock", TileKind.RelativeLock),
        ("Cycle Gate", TileKind.CycleGate),
        ("Chance Gate", TileKind.ChanceGate) };

    // Two to a line, which is what the words need and what the panel has room for
    // once the margin between a pair is counted. A wider button would fall to one a
    // line and make a six tile palette six rows tall.
    const float PaletteButtonWidth = 82.0f;

    // Eight laps to a line, which is a bar of sixteenths and puts the longest cycle
    // there can be in four lines. Four to a line would read against the cell, whose
    // boxes go four across, but it would also stand thirty-two switches eight lines
    // deep in a column that has to reach the bottom of the shortest screen this runs
    // on; eight is where a line is still a phrase.
    //
    // It is also where the switch stops being square under a mouse, and this is the
    // one run in the interface that goes past that. Measured at a panel scale of 2:
    // eight to a line comes out 25 by 25 on a touch screen, which is the screen the
    // number was chosen for, and 18 by 22 under a mouse, where the box asks for less
    // height than a button's own padding and border will give it — see Controls.Switch
    // for the floor. A run of slightly tall boxes is what that costs, and it is cheaper
    // than the eight lines the alternative stands in a column that has nowhere to put
    // them.
    const int LapsPerRow = 8;

    // The laps of a cycle gate, one switch each.
    //
    // Every lap the tile could have gets a switch, and the ones past the current
    // period are hidden rather than built and torn down: the period is set on a bar
    // standing directly over them, and a run that rebuilt itself as that bar moved
    // would pull the bar out from under the drag that was moving it. Hiding is also
    // what keeps a switch that goes out of reach and comes back — the tile keeps the
    // bit, so the run shows it again exactly as it was left.
    //
    // This is the older half of the argument the sound group and LockGroup now make at
    // the length of a whole group: a run of elements that is the same run whatever the
    // number behind it, pulled back into line by a Sync rather than made again.
    sealed class LapSwitches : VisualElement
    {
        public LapSwitches(CycleGateTile cycle, System.Action<int> toggle)
        {
            _cycle = cycle;

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;

            for (var lap = 1; lap <= CycleGateTile.MaxPeriod; lap++)
            {
                var which = lap;
                _switches[lap - 1] = Controls.Switch(LapsPerRow, () => toggle(which));
                Add(_switches[lap - 1]);
            }

            Sync();
        }

        // Pulls the run back in line with the tile, for a switch that was just
        // clicked and for a period that has just moved under it.
        public void Sync()
        {
            for (var lap = 1; lap <= CycleGateTile.MaxPeriod; lap++)
            {
                _switches[lap - 1].style.display =
                  lap <= _cycle.Period ? DisplayStyle.Flex : DisplayStyle.None;
                Controls.SetActive(_switches[lap - 1], _cycle.Fires(lap));
            }
        }

        readonly CycleGateTile _cycle;
        readonly Button[] _switches = new Button[CycleGateTile.MaxPeriod];
    }

    // The fifteen parameters one lock can take hold of, under the heading that says
    // which channel they belong to.
    //
    // Kept between showings, and kept twice over. A bar's range is fixed when it is
    // made, and the two kinds of lock do not share one: an absolute lock's bar runs over
    // the target's own range and a relative one's over a shift either side of nothing,
    // which is a different Low, High, Bipolar and Display. So one run of rows cannot
    // serve both kinds — but two runs serve every lock in the score, since nothing else
    // about a row depends on which tile it is showing.
    sealed class LockGroup : VisualElement
    {
        public LockGroup(InspectorPanel panel, ParamTile tile)
        {
            _heading = Controls.Heading("");
            Add(_heading);

            for (var target = 0; target < ParamTargets.Count; target++)
            {
                _rows[target] = new LockRow(panel, tile, target);
                Add(_rows[target]);
            }
        }

        // Points the group at another lock of its own kind. The channel comes with it
        // rather than off the tile, which does not hold one — see BuildLock.
        public void Apply(ParamTile tile, int channel)
        {
            _heading.text = "Channel " + channel;
            foreach (var row in _rows) row.Apply(tile);
        }

        readonly Label _heading;
        readonly LockRow[] _rows = new LockRow[ParamTargets.Count];
    }

    // A parameter of a lock, held or not.
    //
    // An element of its own rather than a row plus a list of closures, for the reason
    // ValueBar.SyncAll gives: the tree already knows what is on screen. Which is worth
    // more now than it was when it only saved a list — being an element is what gives
    // the row a Sync of its own and a tile it can be pointed at, and so what lets the
    // group above it outlive the showing it was made in.
    sealed class LockRow : VisualElement
    {
        public LockRow(InspectorPanel panel, ParamTile tile, int target)
        {
            (_panel, _tile, _target) = (panel, tile, target);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.marginBottom = Controls.Gap;

            // The name is the control that lets go of the parameter, double clicked,
            // and the hover is this row's rather than the caption's own because a held
            // row already lights its name. See Controls.ActionCaption.
            _caption = Controls.ActionCaption(ParamTargets.Name(target), Toggle, SetHover);
            Add(_caption);

            // An absolute lock holds a value the target could hold itself, so its bar
            // is the target's own; a relative one holds a shift, and reads from the
            // middle. Neither depends on whether the row is held, so taking hold of a
            // parameter never rebuilds anything — and neither depends on which tile is
            // being shown, only on its kind, which is the whole of why LockGroup is two
            // groups and not one.
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
            // lit: the group is detached rather than thrown away, so no PointerLeaveEvent
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

        readonly InspectorPanel _panel;
        readonly int _target;
        readonly Label _caption;
        readonly ValueBar _bar;

        // Whichever lock the group is standing for now. See Apply.
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
        // from it as well is worth having anyway: a parameter is sometimes wanted
        // exactly where it already is, and there is no drag that says so.
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
