using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Spelled out, because UI Toolkit has a TextElement of its own and importing the
// namespace this comes from would put two of them in scope.
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Jacquard.App {

// Assembles the screen: one row of chrome above a scrolling score plane, with columns
// of panels floating over its top corners and one panel along the bottom edge.
//
// The row carries what belongs to the project as a whole and nothing else. Anything
// that applies to a cell is on the panel that follows the cursor, which is where the
// cell already is, and the plane keeps the screen that a palette and a paragraph of
// keys used to take. What is left on the row is a switch for each thing a cell cannot
// ask for, and they stand in the order of how much each one reaches: the channels,
// which are the mix rather than the score; the send effects, which are what a channel's
// amounts feed and belong to the project rather than to anything written on the plane;
// the live effects, which belong to nothing at all — they are held rather than set, and
// what they colour is gone as soon as the hand is off; what is set for the whole mix,
// which is across everything and so under nothing; and the configuration, which is not
// about the piece at all but about the app it is being made in.
//
// Every one of them is down until it is asked for. The plane is what the screen is for,
// and a switch that starts on is a decision nobody made.
//
// The specification leaves the application level UI to be designed here, so it is
// kept to what a prototype has to prove: that every kind of tile can be put down, tuned
// and heard, that a score survives a save and a load, and that the plane can be
// navigated when it grows past the screen.

sealed class JacquardUI
{
    public JacquardUI(VisualElement root, JacquardApp app)
    {
        _app = app;
        _editor = app.Editor;

        root.style.flexGrow = 1;
        // Not painted here any more. The camera clears to this exact colour and draws
        // the visualizer over it, so the panel is the layer above that and leaves the
        // ground to whoever is under it — which used to be nothing at all.
        root.style.backgroundColor = Color.clear;

        SetFace(root, app.Font);

        root.Add(BuildTransportRow());

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.position = Position.Relative;
        body.style.overflow = Overflow.Hidden;
        root.Add(body);

        _scroll = new ScrollArea { WheelSpeed = 2.0f };
        _scroll.style.position = Position.Absolute;
        _scroll.style.left = 0;
        _scroll.style.top = 0;
        _scroll.style.right = 0;
        _scroll.style.bottom = 0;
        body.Add(_scroll);

        _view = app.View;
        _scroll.Add(_view);

        _view.KeyPressed += OnKey;
        _view.CursorMoved += OnCursorMoved;
        _view.RevealRequested += Reveal;
        _view.Reframed += _scroll.Shift;
        _view.DoubleClicked += _editor.DoubleClick;
        _view.TilesDropped += _editor.DropTiles;
        _view.LaneDropped += _editor.DropLane;

        // The one panel the cursor answers to. What used to stand under it — the sound
        // of the channel a CHAN tile names, the hold a lock has on one — are groups of
        // it now: a panel that can only be up while this one is showing a particular
        // kind of tile was a group of this one wearing a frame.
        _inspector = new InspectorPanel(_editor);

        // The effects are the project's, not a cell's, so they get a column of their
        // own rather than a slot in the cursor's. One panel with a heading over each
        // effect: what the switch raises is one thing, so it arrives as one thing.
        _send = new SendPanel(_editor);

        body.Add(PanelEdge(false, PanelColumn(_send.Root),
                                  PanelColumn(_inspector.Root)));
        ShowSend(false);

        // The other edge, which is the one place a column is never covered by the
        // cursor's.
        _channels = new ChannelsPanel(_editor);
        body.Add(PanelEdge(true, PanelColumn(_channels.Root)));
        ShowChannels(false);

        // In neither edge and not on the dock: what is set for the whole thing is read
        // against nothing on screen, so it comes up in the middle. So does what is not
        // set about the thing at all, which is the panel beside it here — the two are
        // the same kind of thing to look at, and being in the same place says so.
        _global = new GlobalPanel(_editor);
        _system = new SystemPanel(_app.Store, SetVisualizer, Refocus);
        body.Add(PanelCentre(_global.Root, _system.Root));
        ShowGlobal(false);
        ShowSystem(false);

        // Neither column, because this one is not read: it is played. The columns are
        // where the eye goes and the bottom edge is where the hands already are.
        _live = new LivePanel(app.Live, () => _app.Synth.CurrentSample, Refocus);
        body.Add(PanelDock(_live.Root));
        ShowLive(false);

        _editor.Changed += OnChanged;

        _view.Rebuild();

        // After the rebuild, since where the score has come to rest on the plane is
        // what this is aiming at.
        ShowScore();

        _view.Focus();
    }

    // Called every frame from the app.
    public void Update()
    {
        _view.RefreshPlayheads();

        Controls.SetActive(_play, _app.Sequencer.IsPlaying);
        _play.text = _app.Sequencer.IsPlaying ? "Stop" : "Play";
        // A loaded project brings a tempo of its own, which the bar has to follow.
        _tempo.Sync();

        FollowTheLock();
        Report();
    }

    // Builds the file chooser again from what is in the score folder now. Called when
    // the app comes back to the front, which is the one moment it can be sure the folder
    // has been out of its hands — see JacquardApp.ReadTheFolderAgain.
    //
    // The list is refilled rather than replaced, since the chooser was handed this list
    // and holds it: a new one would be a chooser reading the folder as it stood when the
    // row was built, for as long as the app runs.
    //
    // A name that is no longer there is given up. The chooser would show the first slot
    // anyway — an index it cannot find reads as none — and leaving the store pointed at
    // the missing file would mean Save and Load working on a name nothing on screen says.
    public void RefreshSlots()
    {
        _slots.Clear();
        _slots.AddRange(_app.Store.Slots());

        if (_slots.Count > 0 && !_slots.Contains(_app.Store.Name))
            _app.Store.Name = _slots[0];

        _syncSlots();
    }

    // Puts the score's own controls out of reach while another score waits to come in,
    // and gives them back at the seam.
    //
    // Three things and no more: the plane, the panel a tile is edited on and the panel
    // a lock is edited on. What is left alone is the mix — the sound, the sends, the
    // channels, the tempo — and the live effects, since none of those writes the score
    // and the point of holding on until the turn of the piece is to play across it.
    //
    // The Load button goes with them: a request cannot be taken back, so the switch that
    // made it says so rather than looking ready to make another.
    //
    // Written only when it moves, since all of this is a style write and the frame it
    // does not move is every frame.
    void FollowTheLock()
    {
        if (_locked == _editor.Locked) return;

        _locked = _editor.Locked;

        _view.Locked = _locked;
        Controls.SetLocked(_inspector.Root, _locked);
        _load.style.opacity = _locked ? Style.DimmedOpacity : 1.0f;
    }

    // Construction

    VisualElement BuildTransportRow()
    {
        var row = Bar();

        // The name of the thing, where an app's name goes. It is the one mark on the
        // row that does nothing when it is pressed, so it stands before the rule that
        // the transport starts at rather than among the switches.
        if (_app.Logo != null) row.Add(Wordmark(_app.Logo));

        _play = Controls.Push("Play", _app.TogglePlay, 54);
        row.Add(_play);

        // The tempo, on a bar rather than between a pair of nudges: a project is set
        // to a tempo once, and what is wanted then is to type the number, not to walk
        // to it a beat at a time.
        _tempo = Controls.Bar(TempoRange, () => _editor.Project.Tempo,
                              value => _editor.Project.Tempo = value);
        // Cut from 78 to pay for the wordmark. What it has to hold is three digits,
        // which is a good deal less than it had.
        _tempo.style.width = Controls.Width(62);
        // The gap to whatever stands to its right, which every button on this row
        // carries and a bar does not: a ValueBar is built for a panel, where it is the
        // last thing on its row and the row carries the gap under it. Standing in a run
        // of controls it has to carry its own, or the rule that follows it — which is
        // short on the left by exactly this, because the thing before it has already
        // laid one down — comes out closer to the tempo than to the switch after it.
        _tempo.style.marginRight = Controls.Gap;
        row.Add(_tempo);

        row.Add(Separator());

        // The first of the five, because it is the narrowest thing any of them reaches:
        // one channel of the mix. A switch, because no cell can ask for what it raises —
        // nothing on the plane names a channel's mute.
        _channelsButton =
          Controls.Push("Channels",
                        () => { ShowChannels(!_channelsShown); Refocus(); }, 62);
        row.Add(_channelsButton);

        // Next, since it is what those channels feed. What a channel sends is a row of
        // its Sound panel and what it is sent to is here, so the two switches stand in
        // the order the signal takes.
        //
        // "Send FX" and not "Send", which would name the half of the arrangement that
        // is not here: what a channel sends is set on the Sound panel, and this is
        // what it is sent to.
        _sendButton = Controls.Push("Send FX",
                                    () => { ShowSend(!_sendShown); Refocus(); }, 62);
        row.Add(_sendButton);

        // Beside it, since the two are the same kind of thing: a panel no cell can ask
        // for, raised by the only switch it has. What separates them is that the send
        // effects are a setting of the project and these are not a setting at all, so
        // this one raises the buttons and the buttons are the whole of the effect.
        //
        // The same width as Send FX, which is the same word long. Two buttons standing
        // next to each other saying as much as each other should measure the same.
        _liveButton = Controls.Push("Live FX",
                                    () => { ShowLive(!_liveShown); Refocus(); }, 62);
        row.Add(_liveButton);

        // Wider again: what it raises is the panel for everything that is set for the
        // whole project and answers to no cell, so anything else of that sort arrives as
        // a group of rows on a panel that is already here rather than as a switch of its
        // own.
        _globalButton = Controls.Push("Global",
                                      () => { ShowGlobal(!_globalShown); Refocus(); },
                                      62);
        row.Add(_globalButton);

        // Last, since it is the one switch here that reaches past the piece: what is on
        // it is about the app rather than about anything that is saved. It is also what
        // the row grows through from now on — a setting that answers to no cell used to
        // mean another switch up here, and the visualizer's was the proof, since it was
        // the only one that raised nothing at all.
        _systemButton = Controls.Push("System",
                                      () => { ShowSystem(!_systemShown); Refocus(); },
                                      62);
        row.Add(_systemButton);

        row.Add(Separator());

        // The one list on this screen that is not written down anywhere: it is the score
        // folder, read out. The list object outlives every reading of it, since what the
        // chooser holds is this list rather than a copy of what was in it — see
        // RefreshSlots.
        _slots = _app.Store.Slots();

        var chooser = Controls.Chooser(_slots,
                                       () => Mathf.Max(0, _slots.IndexOf(_app.Store.Name)),
                                       index => _app.Store.Name = _slots[index],
                                       out _syncSlots);
        // The widest thing on the row, and the first place to look when the row runs
        // out of screen. What it has to hold is a slot name between two arrows, and a
        // name longer than the box draws past it rather than being clipped — where a
        // switch that does not fit is a switch that cannot be pressed. Cut from 190 when
        // the row grew its fourth and fifth switch, which is what put the touch profile
        // back inside an iPad mini's 917 units, and from 170 when the wordmark arrived
        // at the left of the row and had to be paid for from somewhere.
        //
        // The caption is gone rather than narrowed, and the box gives back exactly what
        // it took: the name between the arrows is as long as it ever was. What the word
        // said is said by where the box is standing — after the rule, between Save and
        // Load — and by what is written in it, which is the name of a file.
        //
        // Widened again to a name of eight wide characters — "MMMMMMMM", which is the
        // longest eight letters can measure in this face and so what eight of anything
        // fits inside. Every other number on this row is cut to a word that is written
        // here and cannot change; this one is cut to a word somebody else will type, so
        // it is the one box on the row that has to be sized to a length rather than to
        // a string.
        //
        // Which is why it is the touch profile that sets the number, and by more than
        // the ratio between the two. What is left for the name is the box less the two
        // arrows and their gaps, and an arrow is a one-glyph button — the one shape
        // Width floors at a row's height rather than scaling. So the arrows grow from 22
        // to 30 while the ratio would only have taken them to 26, and the eight
        // characters they are standing either side of grow by the ratio: the name is
        // squeezed from both directions at once. 126 is what leaves eight of them room
        // there, which leaves seven pixels going spare under a mouse.
        chooser.style.width = Controls.Width(126);
        chooser.style.marginBottom = 0;
        row.Add(chooser);

        row.Add(Controls.Push("Save", () => { _app.Save(); Refocus(); }, 46));

        _load = Controls.Push("Load", () => { _app.Load(); Refocus(); }, 46);
        row.Add(_load);

        return row;
    }

    // A column of panels. They stack in the order they are given rather than each
    // holding a corner of its own: in the cursor's column the Tile panel is always up
    // so it keeps the top, and whichever of Sound and Lock the cursor calls for falls
    // in under it. A panel that is down is display: none, which takes it out of the
    // column rather than leaving its gap behind.
    //
    // Under each other, not beside, because what is beside them is the score. A second
    // column of the cursor's panels would cost a panel's width of plane down the whole
    // height of the screen for the sake of one that is up only some of the time and is
    // never as tall as the screen; a column that grows downwards costs only what it is
    // using.
    //
    // A column is transparent to the pointer, so the plane stays reachable everywhere a
    // panel is not actually drawn: it reaches the bottom of the screen whether or not
    // anything is standing that far down it.
    //
    // It is a strip and not a plain box because what it holds can be taller than the
    // screen — a channel start puts a lane and a whole patch on the Tile panel — and a
    // parameter under the bottom edge is a parameter that cannot be set. Dragged, the
    // column travels; on a screen it fits, nothing about it moves and a press on a
    // panel is a press on a panel. See ScrollStrip.
    //
    // The width has to be said here, since the strip's content is positioned rather
    // than laid out and a positioned box has no width of its own to give back. Every
    // panel that stands in a column is this wide.
    static VisualElement PanelColumn(params VisualElement[] panels)
    {
        var column = new ScrollStrip(vertical: true);
        column.style.width = Controls.PanelWidth;
        column.style.flexShrink = 0;
        // The gap to whatever stands to the right of it, by the same rule the rest of
        // this file follows. The last column on an edge gives its own back below.
        column.style.marginRight = Controls.PanelGap;
        column.pickingMode = PickingMode.Ignore;
        column.contentContainer.pickingMode = PickingMode.Ignore;

        foreach (var panel in panels) column.Add(panel);

        return column;
    }

    // The columns down one edge of the screen, in the order they stand across it,
    // pinned to that top corner and no wider than what is on them.
    //
    // Two of them on the right, because a panel that answers to the cursor and one that
    // does not cannot share a column: standing the send effects under the Tile panel
    // would put a project setting in the queue behind whatever cell is selected, and
    // move it down the screen every time the Tile panel grew a line. So they get a
    // column of their own, and it goes beside the cursor's rather than in the far
    // corner.
    //
    // Beside, because the two are read together. What a channel sends is a row of its
    // Sound panel and what it is sent to is here, so the amount and the effect it feeds
    // are a glance apart instead of a screen apart — and the left edge, which the send
    // effects used to hold, is the one place a column can stand without ever being
    // covered by the cursor's, which is what the channels want.
    //
    // What it costs is the plane under it, and only while it is up: a column of panels
    // that is down is a column of nothing, and this one is down until the Send FX
    // button raises it.
    static VisualElement PanelEdge(bool onLeft, params VisualElement[] columns)
    {
        var edge = new VisualElement();
        edge.style.position = Position.Absolute;
        edge.style.top = Controls.PanelGap;
        // Down to the far corner, and stretched, which is what gives a column a height
        // to be longer than. Held off the bottom by the gap it is held off the top and
        // the side by, so a column that runs the whole way reads as one that carries on
        // rather than as one that has hit the edge of the screen.
        edge.style.bottom = Controls.PanelGap;
        edge.style.flexDirection = FlexDirection.Row;
        edge.style.alignItems = Align.Stretch;
        edge.pickingMode = PickingMode.Ignore;

        if (onLeft)
            edge.style.left = Controls.PanelGap;
        else
            edge.style.right = Controls.PanelGap;

        foreach (var column in columns) edge.Add(column);

        // The gap the last column carries has nothing but the screen's edge or the
        // plane on the other side of it, and the inset above has already paid for one.
        columns[columns.Length - 1].style.marginRight = 0;

        return edge;
    }

    // The panels in the middle of the screen, for the ones that are read against nothing
    // around them.
    //
    // The columns are all read against the plane — what a cell holds, what a channel's
    // sends feed, which channel is silent — and the dock is played over it. A limiter is
    // set while listening to the whole mix, with the eye nowhere in particular, so there
    // is no edge it wants to be near; the middle is also the one position on this screen
    // that says a panel is not part of the arrangement around the plane, which is what a
    // setting nobody visits twice a session should say.
    //
    // They stack the way a column does rather than take turns, and for the same reason:
    // a panel that is down is display: none, which takes it out of the stack rather than
    // leaving its gap behind. With one up the middle is where it always was; with both
    // up they are centred as a pair, which is the whole of the rule — no switch here has
    // to know what the other one raised.
    //
    // A panel covers the score while it is up, which is the price of the middle and is
    // paid by the same switch that raised it.
    static VisualElement PanelCentre(params VisualElement[] panels)
    {
        var centre = new VisualElement();
        centre.style.position = Position.Absolute;
        centre.style.left = 0;
        centre.style.right = 0;
        centre.style.top = 0;
        centre.style.bottom = 0;
        centre.style.alignItems = Align.Center;
        centre.style.justifyContent = Justify.Center;
        centre.pickingMode = PickingMode.Ignore;
        foreach (var panel in panels) centre.Add(panel);
        return centre;
    }

    // One panel along the bottom edge, centred, for the one panel that is played
    // rather than read.
    //
    // A column would put it under one hand and out of reach of the other, and the
    // corner it took would be a corner of the plane covered by something up only while
    // it is being used. Across the bottom it is the width of its own contents and no
    // more, it sits where both thumbs already are on a tablet held in two hands, and
    // the score it is played over stays where it was.
    //
    // The panel's own bottom margin is what holds it off the edge, which is the same
    // gap the columns are inset by and the same rule everything else here follows: a
    // gap belongs to the thing above and to the left of it.
    static VisualElement PanelDock(VisualElement panel)
    {
        var dock = new VisualElement();
        dock.style.position = Position.Absolute;
        dock.style.left = 0;
        dock.style.right = 0;
        dock.style.bottom = 0;
        dock.style.alignItems = Align.Center;
        dock.pickingMode = PickingMode.Ignore;
        dock.Add(panel);
        return dock;
    }

    // A row that can be dragged sideways, since what is on this one is not a list that
    // can be shortened: every switch on it raises something no cell can ask for, so a
    // narrow screen has to be able to reach all of them rather than the first few. See
    // ScrollStrip — on a screen the row fits, it is a row.
    static VisualElement Bar()
    {
        var row = new ScrollStrip(vertical: false);
        row.style.flexShrink = 0;
        row.style.height = Controls.ToolbarHeight;
        // The one part of the chrome that paints its own ground. It took it from the
        // root until the root gave up painting, and a row of controls with a waveform
        // running behind them is a row that has to be read through something.
        row.style.backgroundColor = Style.Background;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = Style.PanelLine;

        // The row's own air goes on what travels rather than on the frame it travels
        // in: it is the space before the first control and after the last one, so it
        // belongs to them and leaves with them. Left on the frame it would be a margin
        // the controls slide underneath, which is a row that looks like it has been cut
        // off rather than one that has been pushed along.
        row.contentContainer.style.paddingLeft = Controls.Inset;
        row.contentContainer.style.paddingRight = Controls.Inset;

        return row;
    }

    // The face every word on screen is set in, put on the root and inherited from there.
    //
    // The atlas is built here rather than kept as an asset beside the file it is cut
    // from. A font asset is a texture of glyphs and a material to draw it with, and
    // both are made from the one thing this project actually chose — the face — so
    // saving them would be checking in a cache and then having to remember it is one.
    // Dynamic is what such an asset would be set to anyway: the atlas fills with the
    // glyphs that are asked for, and what this interface asks for is a few dozen of
    // them.
    static void SetFace(VisualElement root, Font font)
    {
        if (font == null) return;

        var asset = FontAsset.CreateFontAsset(font);
        if (asset == null) return;

        root.style.unityFontDefinition = FontDefinition.FromSDFFont(asset);
    }

    // The wordmark on the row, one unit to a cell of the type it is set in.
    //
    // The texture holds two pixels per cell, so on a screen the panel doubles it lands
    // pixel for pixel and on one it does not it reduces by exactly two — either way a
    // cell stays a square. It is the same size in both profiles: nothing here is
    // pressed, so it has no target to grow, and the touch row has no width to give.
    static VisualElement Wordmark(Texture2D texture)
    {
        var mark = new VisualElement();
        mark.style.width = texture.width / LogoPixelsPerCell;
        mark.style.height = texture.height / LogoPixelsPerCell;
        mark.style.flexShrink = 0;
        // The row's own inset and not a gap: the name stands off the transport the
        // way it stands off the edge of the screen, so the air reads the same on both
        // sides of it rather than sitting it among the switches.
        mark.style.marginRight = Controls.Inset;
        mark.style.backgroundImage = Background.FromTexture2D(texture);
        // Held to the colour the row's own type is, rather than the white it is drawn
        // in, so the name does not sit brighter than everything it names.
        mark.style.unityBackgroundImageTintColor = Style.NoteText;
        return mark;
    }

    const float LogoPixelsPerCell = 2.0f;

    static VisualElement Separator()
    {
        var line = new VisualElement();
        line.style.width = 1;
        // Short of the controls either side of it, so it reads as a break in the row
        // rather than as an edge of one.
        line.style.height = Controls.RowHeight - 2;
        line.style.flexShrink = 0;
        line.style.backgroundColor = Style.PanelLine;
        // Wider air than the gap between two buttons, since this is what tells one
        // group of them from the next — and short on the left by the gap the control
        // before it has already left, the way a rule down a panel is short on top.
        line.style.marginLeft = SeparatorAir - Controls.Gap;
        line.style.marginRight = SeparatorAir;
        return line;
    }

    // Behaviour

    void OnChanged()
    {
        _view.Rebuild();
        _inspector.Refresh();
        // Not in OnCursorMoved, since nothing on the send panel answers to a cell. This
        // is for the one change that does reach it: a load, which arrives with effect
        // settings of its own.
        _send.Refresh();
        // And here for the one that reaches the channels: a lane arriving or leaving
        // is a channel becoming reachable or not.
        _channels.Refresh();
        // The Global panel takes a load the same way the send panel does, and for the
        // same reason: what is on it belongs to the project that was just replaced.
        _global.Refresh();
    }

    // The one panel that shows whatever the cursor is on: the tile, the lane it heads,
    // the sound it names and the hold it has on one, whichever of those the cell has.
    void OnCursorMoved() => _inspector.Refresh();

    // The one thing that raises and lowers the send effects. It is the transport button
    // and nothing else, so the button's own look and what it shows are set in the same
    // place and cannot come apart.
    void ShowSend(bool shown)
    {
        _sendShown = shown;

        _send.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_sendButton, shown);
    }

    // And the same again for the panel in the middle. A panel that covers the score is
    // one there has to be an obvious way out of, and pressing the button that raised it
    // is that way — the same one every other panel with a switch offers.
    void ShowGlobal(bool shown)
    {
        _globalShown = shown;

        _global.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_globalButton, shown);
    }

    // And for the channels, which is the same switch again on the other edge.
    void ShowChannels(bool shown)
    {
        _channelsShown = shown;

        _channels.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_channelsButton, shown);
    }

    // And for the panel that stands with it in the middle. What is on that one is not
    // the project's, so nothing here refreshes it and a load goes past it.
    void ShowSystem(bool shown)
    {
        _systemShown = shown;

        _system.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_systemButton, shown);
    }

    // The one setting on that panel, which is the one thing switched on this screen that
    // puts nothing on it: what moves is the component's own enabled flag, which is where
    // a MonoBehaviour's on and off already live. Disabled, its LateUpdate does not run
    // and nothing is handed to the renderer, so a visualizer nobody asked for costs a
    // frame nothing at all.
    //
    // It used to be a switch on the transport row, beside the ones that raise panels,
    // and it was the only one up there that raised nothing. The System panel is where a
    // question about the app rather than about the piece belongs, and this is the first
    // of them.
    void SetVisualizer(bool shown)
    {
        if (_app.Visualizer != null) _app.Visualizer.enabled = shown;
    }

    // The same switch for the one panel that holds nothing. Lowering it does not lift
    // whatever is held on it, because a button cannot be held once it is not on screen:
    // losing the panel loses the pointer capture, and losing the capture is already
    // what ends an effect.
    void ShowLive(bool shown)
    {
        _liveShown = shown;

        _live.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        Controls.SetActive(_liveButton, shown);
    }

    void OnKey(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Space || evt.character == ' ')
        {
            if (evt.keyCode == KeyCode.Space) _app.TogglePlay();
            evt.StopPropagation();
            return;
        }

        if (_editor.HandleKey(evt)) evt.StopPropagation();
    }

    // Keeps typing working after a button has been pressed: a click moves the focus
    // to the button, and the grid is where the keys are supposed to land.
    void Refocus() => _view.Focus();

    // Brings the cursor into view when it walks off the edge.
    //
    // Measured against what the plane was asked for rather than what it is showing. A
    // cursor moved by an edit is a cursor moved in the same breath as the plane growing
    // to the left, and the offset taking that up is a frame ahead of the layout that
    // allows it; reading the clamped value here would overwrite that and let the score
    // jump by the width of the whole margin.
    void Reveal(Rect rect)
    {
        var size = _scroll.contentRect.size;
        if (size.x <= 0.0f || size.y <= 0.0f) return;

        var offset = _scroll.Requested;

        if (rect.xMin < offset.x) offset.x = rect.xMin;
        if (rect.xMax > offset.x + size.x) offset.x = rect.xMax - size.x;
        if (rect.yMin < offset.y) offset.y = rect.yMin;
        if (rect.yMax > offset.y + size.y) offset.y = rect.yMax - size.y;

        _scroll.Offset = offset;
    }

    // Opens on the score rather than on the corner of the plane, with the cursor on it.
    //
    // The plane keeps ten columns and eight rows of empty ground above and to the left
    // of the score, so the corner is bare lattice and the score is somewhere off to the
    // right of it. Two cells of that margin are left showing: enough to say that the
    // plane goes on in that direction — which is the only way of saying so, since
    // nothing draws an edge — and not so much that the score is not the first thing
    // read. Scrolled flush to the score instead, the margin would be off screen
    // entirely, and a lane cannot be carried into ground that is not on the screen.
    //
    // The cursor goes to the score's own corner, which for a score written the usual way
    // is the head of its first lane. It used to arrive there by standing still — the
    // cursor starts at cell (1,1) and that is where a score used to begin — and now that
    // a score begins further in, where it begins has to be asked for.
    //
    // Only at startup. A score that comes in at the turn of the piece must move neither:
    // the seam is there so that two scores read as one, and the reframe has already put
    // the incoming one at the same corner the outgoing one was at.
    void ShowScore()
    {
        const int margin = 2;

        var score = _editor.Score;

        // Before the offset, since SetCursor asks to be brought into view and this is a
        // stronger statement about where to look than that one.
        _view.SetCursor(new GridPoint(score.MinX, score.MinY));

        // The same inset the plane gives its own edge, so the cell sits where a cell at
        // the corner of the plane would.
        var corner = new GridPoint(score.MinX - margin, score.MinY - margin);
        _scroll.Offset = Style.CellOrigin(corner) -
                         new Vector2(Style.Padding, Style.Padding);
    }

    // What the file controls have to say, which is the one thing the status line
    // carried that was not a running count.
    //
    // The line is gone: it was a paragraph of diagnostics — the cursor, the voice count,
    // a runner's step and lap — written across the widest part of the transport row and
    // read by nobody, and the row has since grown five switches that do have to be
    // reachable. What is left of it goes to the console, once each time it changes,
    // because a save that failed has to say so somewhere.
    void Report()
    {
        if (_app.Message == null || _app.Message == _reported) return;

        _reported = _app.Message;
        Debug.Log(_reported);
    }

    // Private members

    readonly JacquardApp _app;
    readonly ScoreEditor _editor;
    readonly ScoreView _view;
    readonly ScrollArea _scroll;
    readonly InspectorPanel _inspector;
    readonly SendPanel _send;
    readonly ChannelsPanel _channels;
    readonly GlobalPanel _global;
    readonly SystemPanel _system;
    readonly LivePanel _live;

    Button _play;
    // The last thing the file controls said, so that a message is logged when it
    // arrives rather than on every frame it is still true.
    string _reported;
    Button _sendButton;
    bool _sendShown;
    Button _liveButton;
    bool _liveShown;
    Button _globalButton;
    bool _globalShown;
    Button _channelsButton;
    bool _channelsShown;
    Button _systemButton;
    bool _systemShown;
    ValueBar _tempo;
    Button _load;

    // The score folder as the chooser has it, and the way to make the chooser say what
    // is in it again.
    List<string> _slots;
    System.Action _syncSlots;

    // What the score's controls were last put into, so that they are written to when
    // that changes and not every frame it has not.
    bool _locked;

    const float SeparatorAir = 8.0f;

    // A tempo below a walking pace or above a drum machine's top speed is of no
    // interest, so the bar covers the useful span and typing covers the rest.
    static readonly ValueBar.Range TempoRange =
      new ValueBar.Range(20.0f, 300.0f, snap: 1.0f, digits: 0, unit: "bpm");
}

} // namespace Jacquard.App
