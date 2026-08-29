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
// One panel here is not on that row at all and comes up by itself: the three pages a
// first launch opens on, which are up because nobody has read them rather than because
// anything was pressed. Nothing switches it, so it is in a layer of its own over the
// lot and carries the one button on any panel here that puts its own panel away. See
// OnboardingPanel.
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
        _root = root;

        root.style.flexGrow = 1;
        // Not painted here any more. The camera clears to this exact colour and draws
        // the visualizer over it, so the panel is the layer above that and leaves the
        // ground to whoever is under it — which used to be nothing at all.
        root.style.backgroundColor = Color.clear;

        SetFace(root, app.Font);

        _row = BuildTransportRow();
        root.Add(_row);

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

        _rightEdge = PanelEdge(false, PanelColumn(_send.Root),
                                      PanelColumn(_inspector.Root));
        body.Add(_rightEdge);
        ShowSend(false);

        // The other edge, which is the one place a column is never covered by the
        // cursor's.
        _channels = new ChannelsPanel(_editor);
        _leftEdge = PanelEdge(true, PanelColumn(_channels.Root));
        body.Add(_leftEdge);
        ShowChannels(false);

        // In neither edge and not on the dock: what is set for the whole thing is read
        // against nothing on screen, so it comes up in the middle. So does what is not
        // set about the thing at all, which is the panel beside it here — the two are
        // the same kind of thing to look at, and being in the same place says so.
        _global = new GlobalPanel(_editor);
        _system = new SystemPanel(_app.Store, SetVisualizer, Refocus);
        _centre = PanelCentre(_global.Root, _system.Root);
        body.Add(_centre);
        ShowGlobal(false);
        ShowSystem(false);

        // Neither column, because this one is not read: it is played. The columns are
        // where the eye goes and the bottom edge is where the hands already are.
        _live = new LivePanel(app.Live, () => _app.Synth.CurrentSample, Refocus);
        _dock = PanelDock(_live.Root);
        body.Add(_dock);
        ShowLive(false);

        // The grey the three pages leave one hole in, built here because the order it is
        // added in is the whole of what it has to get right: after every panel in this
        // body, so it covers all of them, and before the layer the panel below stands in,
        // so the panel it belongs to is the one thing it does not cover. Its two bands go
        // on the transport row rather than in here, which is the same split the paragraph
        // under this one is about. See OnboardingShade.
        _shade = new OnboardingShade(body, _row);

        // In the middle as well, and in a layer of its own added after everything else
        // so it draws over both edges and the dock. It shares nothing with the two
        // above: those take turns being raised by a switch and are centred as a pair
        // when both are up, and this one answers to no switch at all — a pair it was
        // stacked with would be a panel shifted off centre by whatever a hand happened
        // to have raised behind it on the launch it came up on.
        //
        // It is deliberately not a shield over the screen. The transport row is a
        // sibling of this body rather than a thing inside it, so nothing here can cover
        // the controls the three pages point at, and a hand that would rather press
        // Play than read can.
        //
        // That is also why the screen around it goes under a grey rather than out of
        // reach. The three pages name controls, and a paragraph naming a control is worth
        // whatever the reader's search of the row is worth; the shade above answers that
        // by leaving the named control at its own brightness and putting the rest under
        // one flat grey. It picks nothing at all, so nothing in the sentence before this
        // one stops being true — see OnboardingShade, which argues the difference from a
        // shield.
        _onboarding = new OnboardingPanel(_app.OnboardingPages,
                                          () => ShowOnboarding(false), Refocus);
        _front = PanelCentre(_onboarding.Root);
        body.Add(_front);
        ShowOnboarding(false);

        _editor.Changed += OnChanged;

        _view.Rebuild();

        // Before the score is aimed at, since where the plane can be looked at is part
        // of that aim. Every frame after this one it is Update's, and it writes nothing
        // on the frames the screen has not turned over.
        FollowTheSafeArea();

        // After the rebuild, since where the score has come to rest on the plane is
        // what this is aiming at.
        ShowScore();

        _view.Focus();

        // Last of all, and after the keyboard has been given to the plane rather than
        // before: the panel is not modal, so what has focus while it is up is what has
        // focus without it, and a player who reads the first page and then presses
        // Space should hear the thing start.
        if (!Onboarding.Dismissed) ShowOnboarding(true);
    }

    // Called every frame from the app.
    public void Update()
    {
        _view.RefreshPlayheads();

        Controls.SetActive(_play, _app.Sequencer.IsPlaying);
        _play.text = _app.Sequencer.IsPlaying ? "Stop" : "Play";
        // A loaded project brings a tempo of its own, which the bar has to follow.
        _tempo.Sync();

        FollowTheSafeArea();
        FollowTheLock();
        FollowTheSubject();

        // After the hole has been cut, so a page that turned this frame is painted at its
        // new place on it, and outside the test that cuts it: the fog is still on screen
        // for the best part of a second after the pages have gone, and FollowTheSubject
        // has returned at its first line for every frame of that.
        _shade.Tick();

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

    // Holds everything that is read or pressed inside the part of the screen the system
    // is not standing on. See SafeArea for what that part is and why it is not a viewport.
    //
    // Four places answer it, and each one answers only the edge it is pinned to:
    //
    // - The transport row keeps its ground and moves what travels on it, by adding the
    //   inset to the padding at either end of its content. That also lengthens the strip,
    //   which is the whole of the fix rather than half of it: a row that is longer than
    //   its screen can be dragged, and `ScrollStrip.Travel` is measured against the
    //   content box — so without this the last switch on the row could be dragged as far
    //   as the screen's edge and no further, which on a phone left it under the camera
    //   housing with no way to bring it out. Nothing that is not pressed pays for it: the
    //   row's own bar of grey still runs the full width.
    //
    // - The columns of panels take it on the side they stand on and along the bottom.
    //   Their top is the row's business, since the row is above them and has already
    //   moved down by whatever the top edge keeps.
    //
    // - The dock takes it along the bottom, which is the one place a panel had a hand on
    //   it in the system's own strip: the Live FX buttons are held rather than pressed,
    //   and a finger holding one a hair above the home indicator is a finger one slip
    //   away from putting the app away mid-phrase.
    //
    // - The centred panels take three sides, so that a panel too tall for the screen
    //   comes to rest inside it rather than under the indicator.
    //
    // The wordmark is the exception, and it is the only thing on the screen that can be
    // one: it is looked at and never pressed, so what it owes the edge is not what a
    // switch owes it. See MarkAir.
    //
    // Read every frame and written only when it moves, the way the lock is. A rotation
    // is the one thing that changes it in practice, and there is no event to hang this on
    // that is cheaper to trust than the number itself — the screen has turned over by the
    // time either edge has anything different to say.
    void FollowTheSafeArea()
    {
        var safe = SafeArea.Read(_root.panel);
        if (safe.Equals(_safe)) return;

        _safe = safe;

        _row.style.height = Controls.TransportRowHeight + safe.Top;
        _row.style.paddingTop = safe.Top;

        // The mark's air is its own and owes this nothing — see MarkAir — so a row with a
        // mark on it is left where the row built it, and only a row without one takes the
        // inset at its left.
        if (_mark == null)
            _row.contentContainer.style.paddingLeft = Controls.Inset + safe.Left;

        _row.contentContainer.style.paddingRight = Controls.Inset + safe.Right;

        _leftEdge.style.left = Controls.PanelGap + safe.Left;
        _leftEdge.style.bottom = Controls.PanelGap + safe.Bottom;

        _rightEdge.style.right = Controls.PanelGap + safe.Right;
        _rightEdge.style.bottom = Controls.PanelGap + safe.Bottom;

        _centre.style.left = safe.Left;
        _centre.style.right = safe.Right;
        _centre.style.bottom = safe.Bottom;

        _front.style.left = safe.Left;
        _front.style.right = safe.Right;
        _front.style.bottom = safe.Bottom;

        // The gap under the dock is the panel's own margin, so this is the inset and
        // nothing more.
        _dock.style.bottom = safe.Bottom;

        _scroll.DeadBottom = safe.Bottom;
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

    // Cuts the shade's hole around whatever the page on screen is about, and brings that
    // control onto the row when it is off the end of it.
    //
    // Read every frame the panel is up and written when it moves, the way the two above
    // are. What moves it is a page turning, and past that a rotation or a change of
    // profile: nothing else touches the row's layout while three pages are being read.
    void FollowTheSubject()
    {
        if (!_onboardingShown) return;

        var (first, last) = Subject(_onboarding.Page);

        // Not before the row has been laid out and has a width to be measured against,
        // and it has neither on the frame the panel goes up — the panel is raised from
        // the constructor and the layout that answers it has not run yet.
        if (float.IsNaN(first.layout.xMin) || _row.contentRect.width <= 0.0f) return;

        // Once for each page rather than every frame, so a row dragged by hand after the
        // page arrived stays where the hand left it.
        if (_pointedAt != _onboarding.Page)
        {
            _pointedAt = _onboarding.Page;
            RevealOnTheRow(first, last);
        }

        _shade.Follow(first, last);
    }

    // What each page is about, as the run of controls the shade leaves lit.
    //
    // A run and not a list, because the row is what it is: the things one page names
    // stand next to each other on it, and a hole cut in two pieces would read as two
    // holes. Held as its two ends so that giving a page another control to point at is a
    // matter of moving one of them — the tempo bar joining Play on the first page is one
    // word here and nothing anywhere else.
    (VisualElement First, VisualElement Last) Subject(int page)
      => page switch
      {
          0 => (_play, (VisualElement)_play),
          1 => (_chooser, (VisualElement)_load),
          _ => (_guide, (VisualElement)_guide)
      };

    // Brings a run of controls onto the part of the row that is on screen, which is what
    // Reveal does for a cell on the plane.
    //
    // A rule for every page rather than the third page's own fix. That page is what asks
    // for it — the guide button stands last on a row longer than any screen this ships to,
    // so it is past the edge on all of them — but the second page's chooser is past the
    // edge of a phone held in landscape as well, and a page pointing at something that is
    // not on the screen is the same failure whichever page it is.
    //
    // The far end first and the near end second, so that a run wider than the screen comes
    // to rest on the control it starts at: what the words name first is where they name it.
    void RevealOnTheRow(VisualElement first, VisualElement last)
    {
        var view = _row.contentRect.width;
        var offset = _row.Offset;

        // The same air either side of the run that the shade cuts its hole at, so a
        // control brought to the edge arrives with the gap the hole gives it rather than
        // flush against the grey.
        var right = last.layout.xMax + Controls.Gap;
        if (right > offset + view) offset = right - view;

        var left = first.layout.xMin - Controls.Gap;
        if (left < offset) offset = left;

        _row.Offset = offset;
    }

    // Construction

    ScrollStrip BuildTransportRow()
    {
        var row = Bar();

        // The name of the thing, where an app's name goes. It is the one mark on the
        // row that does nothing when it is pressed, so it stands before the rule that
        // the transport starts at rather than among the switches.
        // Kept, because it is the one thing on this row the screen's own edges are allowed
        // to treat differently from the switches. See MarkAir.
        if (_app.Logo != null)
        {
            _mark = Wordmark(_app.Logo);
            row.Add(_mark);

            // The mark stands *in* the row's left air rather than behind it, so that air
            // is the mark's own and reads the same on both sides of it. Written here and
            // again in FollowTheSafeArea, since the row that has no mark on it takes the
            // safe inset instead and this is the only place that knows which row this is.
            row.contentContainer.style.paddingLeft = MarkAir;
        }

        _play = Controls.Push("Play", _app.TogglePlay, 54);
        row.Add(_play);

        // The tempo, on a bar rather than between a pair of nudges: a project is set
        // to a tempo once, and what is wanted then is to type the number, not to walk
        // to it a beat at a time.
        _tempo = Controls.Bar(TempoRange, () => _editor.Project.Tempo,
                              value => _editor.Project.Tempo = value);
        // As wide as a bar on a panel, since that is what it is. It was 62 — three
        // digits and not much else, itself cut from 78 to pay for the wordmark — which
        // read as a slot to type in rather than as a range to reach into, and the whole
        // point of putting the tempo on a bar is the second thing.
        _tempo.style.width = Controls.BarWidth;
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

        _chooser = Controls.Chooser(_slots,
                                    () => Mathf.Max(0, _slots.IndexOf(_app.Store.Name)),
                                    index => _app.Store.Name = _slots[index],
                                    out _syncSlots);
        // The widest thing on the row, and the first place to look when the row runs
        // out of screen. What it has to hold is a slot name between two arrows, and a
        // name longer than the box draws past it rather than being clipped — where a
        // switch that does not fit is a switch that cannot be pressed. Cut from 190 when
        // the row grew its fourth and fifth switch, which is what put the touch profile
        // back inside an iPad mini's 917 units at the time, and from 170 when the
        // wordmark arrived at the left of the row and had to be paid for from somewhere.
        //
        // It is not inside 917 any more, and has not been since the widening below.
        // Measured in the editor at the touch profile, the row's content comes to 1114
        // units, so the last of it is reached by dragging the strip on every screen this
        // ships to rather than on a phone alone — see the note beside the guide button
        // in this method, which is what stands last on it now. That is a row that has to
        // be dragged and not a control that cannot be pressed, which is the line this
        // paragraph is about: a name too long for the box still draws past it, and this
        // number is still what decides where that line falls.
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
        _chooser.style.width = Controls.Width(126);
        _chooser.style.marginBottom = 0;
        row.Add(_chooser);

        row.Add(Controls.Push("Save", () => { _app.Save(); Refocus(); }, 46));

        _load = Controls.Push("Load", () => { _app.Load(); Refocus(); }, 46);
        row.Add(_load);

        // And past the last rule, the one control up here that is not about the piece
        // at all: what everything else on this row does is play it, set it or save it,
        // and this leaves the app. That is the argument that put System last among the
        // switches, taken one step further — the guide is not even about the app, it is
        // about how to use it — so it stands after the score controls rather than among
        // them, with a rule of its own in front of it.
        //
        // It is here rather than at the foot of the System panel, which is where it
        // was. A player who has never opened this app has no reason to press *System*:
        // that panel is where a setting about the machine goes, and the guide is the
        // one thing that was on it that somebody wants before they have found any panel
        // at all. Nothing about it was a setting, so nothing about it was lost by
        // leaving.
        //
        // What it costs is that the row is already longer than any touch screen it is
        // drawn on, and being last makes this the first thing past the right edge on
        // every one of them. Measured in the editor at the touch profile: the row's
        // content comes to 1114 units with this on the end of it and 1068 without,
        // against an iPad mini's 917 and the 774 a phone has in landscape — and a
        // device's own right inset is added to the first number and not to the second.
        // The row is a ScrollStrip and is dragged, which is what the strip is for and
        // what the inset on its content is for — see FollowTheSafeArea; dragged to the
        // end it puts this button fully on screen with the content's trailing gap to
        // spare, which is what the strip owes whatever stands last on it. It is also why
        // the third onboarding page, which is about this button, sends the row to its end
        // as it comes up rather than asking anybody to find it — see RevealOnTheRow.
        row.Add(Separator());
        _guide = GuideButton();
        row.Add(_guide);

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
    static ScrollStrip Bar()
    {
        var row = new ScrollStrip(vertical: false);
        row.style.flexShrink = 0;
        row.style.height = Controls.TransportRowHeight;
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

    // The wordmark on the row, as tall as MarkOfBox makes it and as wide as that leaves it.
    //
    // The height follows the row it stands on and the width follows the texture, so a cell
    // of the type the mark is set in stays square whatever profile is in force. It used to
    // be the texture's own size at two pixels to a cell, which held it at 15 units in both
    // profiles: on the row the layout was judged on that is three quarters of a control's
    // box, and on the touch row — half again as tall — it was a third of the bar, the only
    // thing on it that had not grown.
    //
    // Nothing here reads the texture's resolution any more, only its shape, so a master
    // drawn at more pixels to a cell — `PPC` in Branding/make_logo_png.py — is a sharper
    // mark at the same size and needs no change here. It took one: the mark lands pixel for
    // pixel only where a cell comes to a whole number of device pixels, and at 22.5 units a
    // cell is 3 of them on a 2x screen, which is what the master is now cut at. Measured on
    // a simulated iPad, every cell of the mark on screen is one flat 3x3 block and not one
    // pixel of it is an interpolated value; a 458 ppi phone asks for 5.2 and cannot be met
    // by any whole number, so there the mark is resampled and always was.
    static VisualElement Wordmark(Texture2D texture)
    {
        var height = Controls.RowHeight * MarkOfBox;

        var mark = new VisualElement();
        mark.style.height = height;
        mark.style.width = height * texture.width / texture.height;
        mark.style.flexShrink = 0;
        // The mark's own air and not a gap: the name stands off the transport the way it
        // stands off the edge of the screen, so the air reads the same on both sides of it
        // rather than sitting it among the switches. See MarkAir.
        mark.style.marginRight = MarkAir;
        mark.style.backgroundImage = Background.FromTexture2D(texture);
        // Held to the colour the row's own type is, rather than the white it is drawn
        // in, so the name does not sit brighter than everything it names.
        mark.style.unityBackgroundImageTintColor = Style.NoteText;
        return mark;
    }

    // How tall the mark stands against a control's box on the same row.
    //
    // Three quarters, which is a measurement rather than a proportion anybody chose: the
    // texture is 15 cells tall and a control's box is 20 units in the profile the layout was
    // judged in, so three quarters is what the mark already came to there. Taking it as a
    // ratio is what carries that judgement across — 22.5 units against a 30 unit box — where
    // before it stayed at 15 while the box, the type and the bar all grew around it.
    //
    // A control's box and not the bar's height, because what the mark is read against is the
    // switches beside it rather than the air over them. The air is the other thing that
    // grew, and it grew for a reason of its own that has nothing to do with how big a name
    // should be — see MarkAir.
    const float MarkOfBox = 0.75f;

    // The air on either side of the wordmark, which is the one thing on this screen held
    // off the edge by something other than the safe area.
    //
    // What a switch is held off that edge by is the safe area, and a switch is right to
    // take it: it is pressed. The mark is not, so what it actually has to clear is the one
    // thing that would *cut* it — the corner the display is rounded to. That is a much
    // smaller distance than the inset, and at the top of the screen it is the only thing in
    // the way at all: a phone's camera housing sits in the middle of the edge it is on, so
    // in landscape it is nowhere near a row along the top.
    //
    // Neither of the two numbers the platform hands over says how big that corner is. The
    // radius is not reported at all, and the safe inset is no guide to it — an iPad is cut
    // to the same kind of corner and reports no inset whatever, so a mark placed by a share
    // of the inset is a mark placed at nothing on the one family of devices this is mostly
    // used on.
    //
    // So it is measured against the corner instead and then written down as a share of the
    // row's own height — MarkAirOfRow, which comes to 34.6 units in the touch profile. What
    // that has to beat is how far the curve has come in by the height the mark's top edge
    // sits at, 11 units down: **20.6 units** on a simulated iPhone 13 Pro Max, whose corner
    // is 53.3 units of radius, and **1.2** on an iPad Pro 11 at 18.
    //
    // The row's height rather than a number of its own because the mark is centred in the
    // row: how far down its top edge sits, which is what decides the answer, is set by that
    // height and by MarkOfBox — so the two have to be read together. It began at half the
    // row, which cleared that phone's corner by 6.8 units while the mark stood at 15 units
    // tall; the mark growing to 22.5 lifted its top edge into a deeper part of the curve and
    // left 2.5, which is inside the curve's own antialiasing on a real screen. The quarter
    // that makes up three quarters was added by eye against that, and it is the honest
    // description of this number: a distance judged on a device, held to the one metric on
    // the row that moves with what decides it.
    //
    // What it costs is row. The mark and its air come to 177 units in the touch profile
    // against 154 at half the row, and the row is the tightest thing in this interface on a
    // phone — see the note on the chooser's width in BuildTransportRow. It is a compromise
    // throughout, and the thing that would settle it properly is a radius nobody publishes.
    //
    // A touch screen only. A desktop window has no corner cutting into its own content, so
    // the mark keeps the row's inset there and the row looks as it always did — and forcing
    // the touch profile on a Mac shows the phone's spacing, which is what that override is
    // for.
    //
    // The switches lose nothing to this and need no inset of their own behind it: the mark
    // and its air come to 177 units on that phone against the 41 the safe area asked for,
    // so the first switch is well inside it either way.
    static float MarkAir
      => Controls.Touch ? Controls.TransportRowHeight * MarkAirOfRow : Controls.Inset;

    // Three quarters of the row, and a different three quarters from MarkOfBox above: that
    // one is a height against a control's box, this one is air against the bar.
    const float MarkAirOfRow = 0.75f;

    // The guide, on the one control up here with a mark of punctuation on it.
    //
    // "?" because there is no word for this that fits: "Guide" and "Help" are both a
    // house style away from what the page is called, and the row has no room for "User
    // guide" — which is what it said on the panel it came from, where a foot had the
    // width to spare. A question mark is the one character that can stand on this
    // button and be read as what it is, which is what lets a single glyph do the work
    // a caption has no room to.
    //
    // It was a drawn book before this, on the argument that a mark says what no word
    // here can. What that cost was a picture — cut on a cell grid, kept in Branding
    // with a script of its own, tinted and sized against the box by hand — for the one
    // control in the app carrying one, and it never sat in the row as well as the words
    // either side of it do. A character the face already has is set by the rules every
    // other button on this row is set by, and asks nothing of anybody.
    //
    // Told what the other one-glyph buttons are told — the arrows either side of a
    // score's name, the stepper's minus and plus — since a button carrying a single
    // character has no reason to measure differently for standing at the end of the
    // row. That is Controls.ArrowWidth said again here because it is private to the
    // file that draws them, and it is the shape those come out at: 22 by 22 under a
    // mouse and 30 by 30 on a touch screen at a panel scale of 2, square in both, since
    // every button on this row is 22 tall whatever height it is handed — a button
    // carries ten units of padding over and under its word and a unit of border on each
    // edge, which Push leaves alone and which floor the box at 22, over the 20 a row is
    // tall under a mouse, and Controls.Width floors the width at a row's height on a
    // touch screen for exactly this case.
    static Button GuideButton()
      => Controls.Push("?", () => Application.OpenURL(GuideUrl), 22);

    // No Refocus after it. Every other button on this row hands the keyboard back
    // because the press was the whole of what it did; this one has just sent the app to
    // the background, and on iOS it is a sheet over the top of it. Taking the focus back
    // under a sheet is a write to a screen nobody is looking at, and the plane still has
    // it when the sheet goes away — the press moved it to the button and the button is
    // gone from under the hand by then.
    //
    // The guide is a page on the web and not a copy of one inside the build. A manual
    // that shipped with the app would be the manual as it stood on the day that build
    // left, and this one is rewritten whenever what it describes moves.
    //
    // Handed straight to Application.OpenURL, which is a browser on the desktop, the
    // sheet over the app on iOS and a new tab on the Web — and on the Web it is the
    // press that makes that allowed, since a tab opened outside a hand's own gesture is
    // a tab the browser blocks.
    const string GuideUrl = "https://www.keijiro.tokyo/jacquard-doc/";

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

    // And for the one panel up here that no switch raised, which is why this is the one
    // of these with nothing to keep in step: there is no button whose look has to agree
    // with what is on screen. What put it up was a setting read once at startup, and
    // what puts it down is a button on the panel itself — the only panel here that has
    // one, because it is the only one with nothing else that could.
    void ShowOnboarding(bool shown)
    {
        _onboardingShown = shown;

        _onboarding.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        // And the grey around it, which is the one panel here that brings the rest of the
        // screen with it when it goes up and down — and the one thing here that does not
        // do it on this frame. The panel itself arrives and leaves at once, which is what
        // a panel answering a press owes; the fog takes the best part of a second either
        // way, and on a launch waits a moment before it starts down. See OnboardingShade.
        _shade.Show(shown);

        // Nothing has been pointed at yet, so the page that is up is read again from
        // scratch on the next frame — which is what brings its subject onto the row.
        _pointedAt = -1;
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

        // Against the part of the viewport that can be looked at rather than the whole
        // of it: a cursor brought to the left edge of a phone held in landscape is a
        // cursor under the camera housing, which is a cell that was revealed to nobody.
        // Three sides and not four — the plane begins under the transport row, so its
        // top is inside the safe area already.
        if (rect.xMin < offset.x + _safe.Left) offset.x = rect.xMin - _safe.Left;
        if (rect.xMax > offset.x + size.x - _safe.Right)
            offset.x = rect.xMax - size.x + _safe.Right;
        if (rect.yMin < offset.y) offset.y = rect.yMin;
        if (rect.yMax > offset.y + size.y - _safe.Bottom)
            offset.y = rect.yMax - size.y + _safe.Bottom;

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
        // the corner of the plane would — and clear of whatever the screen keeps on its
        // left, so that the corner is where the plane can be looked at rather than where
        // it happens to begin.
        var corner = new GridPoint(score.MinX - margin, score.MinY - margin);
        _scroll.Offset = Style.CellOrigin(corner) -
                         new Vector2(Style.Padding + _safe.Left, Style.Padding);
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

    // The boxes that answer to the screen's own edges, kept because what they are told
    // changes with the way the thing is held. See FollowTheSafeArea.
    readonly VisualElement _root;
    // The transport row, held as what it is rather than as a box: the onboarding shade
    // asks it where its content has travelled to and moves it when a page names a control
    // that is off the end of it. See RevealOnTheRow.
    readonly ScrollStrip _row;
    readonly VisualElement _leftEdge;
    readonly VisualElement _rightEdge;
    readonly VisualElement _centre;
    readonly VisualElement _dock;
    readonly VisualElement _front;

    readonly InspectorPanel _inspector;
    readonly SendPanel _send;
    readonly ChannelsPanel _channels;
    readonly GlobalPanel _global;
    readonly SystemPanel _system;
    readonly LivePanel _live;
    readonly OnboardingPanel _onboarding;
    readonly OnboardingShade _shade;

    Button _play;
    // The name of the thing, at the left of the row, when there is one to draw.
    VisualElement _mark;
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
    // The two the onboarding pages point at that nothing else here needed a name for:
    // the score chooser, which the second page's run starts at, and the guide button,
    // which is the whole of the third page's. See Subject.
    VisualElement _chooser;
    Button _guide;

    // Whether the three pages are up, since the shade is only followed while they are.
    bool _onboardingShown;

    // Which page the row was last aimed at, so that a page is revealed once and a row
    // dragged afterwards is left alone. Before any page, which is what a panel going up
    // is put back to.
    int _pointedAt = -1;

    // The score folder as the chooser has it, and the way to make the chooser say what
    // is in it again.
    List<string> _slots;
    System.Action _syncSlots;

    // What the score's controls were last put into, so that they are written to when
    // that changes and not every frame it has not.
    bool _locked;

    // And the same for what the screen keeps: what the chrome was last laid out to.
    SafeArea _safe;

    const float SeparatorAir = 8.0f;

    // A tempo below a walking pace or above a drum machine's top speed is of no
    // interest, so the bar covers the useful span and typing covers the rest.
    static readonly ValueBar.Range TempoRange =
      new ValueBar.Range(20.0f, 300.0f, snap: 1.0f, digits: 0, unit: "bpm");
}

} // namespace Jacquard.App
