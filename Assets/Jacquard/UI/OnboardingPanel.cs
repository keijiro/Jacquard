using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The three pages a first launch opens on.
//
// The app comes up on a score, with the plane, the transport row and nothing saying
// what to do with either. Every other panel here answers a question somebody already
// has — what is in this cell, how loud is it, what is the limiter doing — and this one
// is the only one that has to be asked first. So it is the one panel on this screen
// raised by neither the cursor nor a switch: it is up because nobody has read it yet.
//
// Three pages and not a tour. What has to be known before anything else works is that
// Play starts it, that another score is loaded from the right of the row, and that the
// rest is written in the guide; everything past that is the guide's job, and a fourth
// page would be the manual arriving in a window nobody asked for.
//
// It is not modal and takes no SetLocked shield. Nothing on this screen is modal, and
// the transport row is a sibling of the body this stands in rather than a thing under
// it — so the panel cannot cover the very controls the three pages point at, and a
// player who would rather press Play than read can do it with the panel still up.
//
// Which is also why there is no Skip on it. A way out is what a panel in the way owes
// whoever it is in the way of, and nothing here is being kept from anybody; three pages
// of Next is short enough to sit through, and a button offering to leave the first thing
// a first launch shows is a button arguing against the panel it is on. See the foot in
// the constructor, which is where that argument is written down.
//
// What it does have behind it is one flat grey, with a hole in it around the control the
// page on screen names. A page saying where a control is is only worth the search that
// follows it, and the row is a dozen switches wide; the grey answers that by taking the
// search away rather than by describing the place better. It changes nothing about the
// paragraph above — the shade picks nothing at all, so the row, the plane and Play are
// as reachable through it as they were without it. It is not this panel's: JacquardUI
// owns it, because the hole is cut on the transport row and the row is not down here.
// See OnboardingShade, and Page below for the one thing it asks this panel.
//
// It is also the only panel here with an edge. Every other one is read against the
// plane, where a lighter ground with air around it is the whole of what says it is a
// panel — Controls.Panel argues that case and refuses a border on it. This one comes up
// in front of whatever a launch happens to have left open, and two sheets of the same
// grey overlapping is one shape with a fold in it. A line around it is what says which
// of the two is in front; see the constructor for it.
//
// There was a shadow under it as well, and the fog is what took it away. The shadow was
// drawn before the shade existed: the panel then stood on a live screen, and a stack of
// black rings was what parted it from whatever panel it happened to have come up over.
// Now nothing behind it is at its own brightness by the time it arrives — the grey is all
// the way down before the panel is raised at all, which is what OnboardingShade.Covered
// is for — so the one sheet in front of the fog is being lifted off a screen that has
// already been put behind it. What was left was the only soft edge in an interface of
// flat rectangles, drawn out of eight elements, saying a second time what the line around
// the panel and the grey behind it both already say.
//
// Only the box reaches disk. Done on the last page closes it for this launch and writes
// nothing — see Onboarding, which also says why there is no way back once the box is
// ticked.
//
// How the pictures are made
// -------------------------
//
// They are crops of real captures of this app's own transport row rather than drawings
// of it, and the procedure is written down rather than automated — the same bargain
// Docs/Figures/README.md makes for the manual's figures, and for the same reason: they
// are retaken when the row changes, which is rarely, and a generator for three
// screenshots is more to keep right than the three screenshots are.
//
// - In the editor, in play mode, with the panel's own scale set to 2 by hand — left
//   alone the editor resolves the Mac's dpi and the crop comes out at some fraction of
//   a device pixel to the unit.
// - With JacquardApp.Pointer set to Touch before Start for the second and third pages,
//   so the row is built at the tablet's metrics — and left at Auto for the first, which
//   in the editor is the mouse's. That is the one thing that differs between the three
//   and it is the wordmark's doing. The first page's band starts at the screen's own
//   edge, and at the tablet's metrics most of what it then holds is air: the mark held
//   off the corner and held off Play by margins written for a fingertip, which lands on
//   neither. Cut at the mouse's, the same four things — the corner, the mark, Play, the
//   tempo bar — arrive at the spacing the eye reads them at, and the whole of the bar
//   fits with the rule after it to spare. What it costs is that this one page's row is
//   drawn at the desktop's height rather than the tablet's; what the page has to say is
//   which corner Play is in, and it still says it.
// - With a game view the row does not fit on — 960 units against the row's own 1115 —
//   and, for the two pages about the score controls and the guide, with the row dragged
//   to its end, which is where those controls actually stand on any screen this ships
//   to. Dragged there, the last of the row is the screen's own right edge, which is
//   what the third page's band is cut against. Neither applies to the first page: its
//   band is the head of the row, so the strip is where it starts, and how much of the
//   row fits past the tempo bar decides nothing.
// - With the Tile panel put down. It is never down in the app — it is the panel the
//   cursor answers to and something is always selected — but it stands twelve units
//   under the row at the right of the screen, so on two of the three bands it, and not
//   the plane, would be what the strip below the row is a strip of.
// - With the plane scrolled so that a row of the lattice falls inside that strip. Where
//   the dots land is wherever the plane happens to be scrolled to, and they are 36 units
//   apart down the screen against a strip of 24 — so left alone it is even money whether
//   the plane in the picture has anything in it at all.
// - ScreenCapture.CaptureScreenshot, and wait for the file: it is written at the end
//   of the frame after the call and is not there when the call returns.
// - Cut the band described below.
//
// What the band is
// ----------------
//
// The subject, the rules either side of it, and the neighbouring control where one fits
// — plus the whole height of the row and 24 units of the plane under it, which is the
// row's own bottom rule and one row of the lattice.
//
// With one thing taken back off: the second page stops at the rule after Load rather
// than running on to the "?" past it, because that button is the third page's whole
// subject. Cut to the rule the two bands came out all but identical, which is three
// pages the eye cannot tell apart on the one thing that changes between them.
//
// And one thing put on, on the first page only: it begins at the screen's own left edge
// and takes the wordmark with it. That page is about the first control on the row, and
// the mark standing in the corner is the one landmark in this interface that says which
// corner without anything else in the shot having to. At the tablet's metrics that cost
// the right hand end of the band — the mark, its air, Play and the tempo bar came to 384
// units against the 350 the panel can draw, so the band was cut at the cap and the tempo
// bar ran off the edge of it with its reading still whole, which is a control cut by the
// edge of a crop and reads as the row carrying on. Cut at the mouse's metrics the four
// of them come to 262: the bar is whole, the rule past it stands in the shot the way the
// other two pages' rules do, and nothing is up against the cap at all.
//
// It began as the subject alone at the row's full height, which is a picture of a
// control on a grey field: it says what the control looks like and nothing about where
// on the screen to find it. The rule either side of it is the nearest thing the row has
// to a landmark, and the strip of plane under it is what says this band is the top of
// the screen rather than a piece cut out of the middle of it — an edge of the picture
// that is also an edge of the interface, without having to put the screen's own corner
// in the shot. It is the same argument the shade behind the panel makes, done in the
// picture rather than on the screen, and the two are meant to be read together: the
// picture says what it looks like and the grey says which one it is.
//
// A cap of CaptureScale * 350 pixels on the width, which is what the touch profile
// leaves for a picture — see ContentWidth. Past that a crop is shrunk rather than cut,
// and a picture of a row that has been resampled is a picture of a row with soft type
// in it. There is no such cap under a mouse and there cannot be: that profile leaves
// 266 units, which the first page now comes in under and the other two do not — so
// those two are drawn small on a desktop and always were. Picture is built for that;
// 1:1 is what the touch screens get.
//
// That leaves each master at exactly two device pixels to the unit, which is what
// Picture below then relies on: it draws a page at half its pixel size, so a capture
// taken this way lands pixel for pixel on the 2x screens this app is used on and is
// resampled nowhere else. A crop wider than the panel's content is shrunk to fit
// instead, which is a picture that has been cut too wide rather than a layout that has
// gone wrong.
//
// The shade is not in the shot. The picture says what the control looks like and the
// shade says which one on the screen it is; a picture of the screen already under the grey
// would be answering the second question twice and the first one not at all.

sealed class OnboardingPanel
{
    public VisualElement Root { get; }

    // Which page is up, for the one thing outside this panel that has to know: the shade
    // behind it cuts its hole around the control the page on screen is about. The panel
    // does not tell anybody it turned — nothing here has an event and the page turns
    // three times in the life of the app — so it is read rather than announced, on the
    // frames the caller is already spending on the safe area and the lock.
    public int Page => _page;

    // pages is JacquardApp.OnboardingPages, which may be short or absent — a page whose
    // picture is missing shows its words and nothing else. close lowers the panel, and
    // is the caller's because a panel does not decide whether it is on screen; refocus
    // is the keyboard going back to the plane, which every panel with a button owes.
    public OnboardingPanel(Texture2D[] pages, Action close, Action refocus)
    {
        (_pages, _close, _refocus) = (pages, close, refocus);

        // The header is the page's subject — Play, Scores, User guide — which is the
        // rule every panel here follows and is also what tells one page from the next.
        // A panel titled "Welcome" with the subject repeated inside it would spend the
        // one line the eye goes to on the word that changes least.
        var panel = Controls.Panel(Pages[0].Header, out _header);

        // And the panel is the root, with nothing wrapped around it — which it is worth
        // saying is the ordinary arrangement and was not always this one. The root was a
        // wrapper while there was a shadow to hang: a ring is only ever outside the box
        // it is a ring of, and a child of the panel is laid out inside the panel's
        // padding instead, so the rings needed a box with neither padding nor border to
        // hang off. With the shadow gone that box was one column with one thing in it.
        Root = panel;

        // Half again a column panel, because what is on it is a picture of a row and a
        // row is wide. 288 units under a mouse and 372 on a touch screen, against the
        // 774 a phone held in landscape has to spare — so it fits the narrowest screen
        // this ships to with room either side. LivePanel is the other panel that sets
        // its own width, and for the same kind of reason.
        panel.style.width = PanelWidth;

        // The gap a panel carries under it belongs to the column of panels it stands in,
        // and this one stands in a layer by itself. Left on, it would be twelve units
        // under the panel that the centring is measured through, which is a panel
        // standing half a gap high on the screen.
        panel.style.marginBottom = 0;

        // The edge. In its own shade rather than the grey every control is outlined in,
        // which is the difference between a sheet that is in front and one more button
        // — Style.FrontLine carries that argument and the one it answers.
        TileElement.SetBorderWidth(panel, BorderWidth);
        TileElement.SetBorderColor(panel, Style.FrontLine);

        // Which page this is, at the end of the header's own row rather than at the
        // foot. It is a reading and not a control: it says where in the three the
        // reader is, which is a thing about the whole panel and not about any row of
        // it, and the top right of a panel is where a reading of that kind goes. At
        // the foot it stood at the left of the row the button is on, which is where a
        // hand is already looking for something to press.
        //
        // Set the way every other word on this screen that is not a header is set —
        // Controls.FontSize in the caption grey. It was in Controls.Value's bright ink,
        // which is the ink the header beside it is in and the ink the paragraph below
        // is in, and a figure that changes three times in the life of the app has no
        // claim on the shade this panel keeps for the words that are actually read.
        _count = Controls.Text("", Controls.FontSize, Style.Label);
        _count.style.unityTextAlign = TextAnchor.MiddleRight;
        _header.parent.Add(_count);

        _picture = new VisualElement();
        // Centred rather than stretched: the crops are not all one width, and a picture
        // of a row blown up to fill a panel is a picture of a row nobody can read.
        _picture.style.alignSelf = Align.Center;
        _picture.style.marginBottom = Controls.Gap;
        panel.Add(_picture);

        // Set the way SystemPanel's restart note is, and for the same reason: every
        // label this UI builds is one line in a fixed box, and this is a paragraph.
        //
        // And set unlike any of them past that, because it is the only prose in the app.
        // The chrome's type is scanned for a word already known to be there — a caption
        // beside the bar it names, a number in a box — and it is set to be scanned: one
        // size, the caption grey, lines that never meet each other. This is read from
        // the beginning by somebody who has not seen the thing it describes, so it takes
        // a step of size, the bright ink the header is in rather than the grey a caption
        // is, leading between the lines and air around the block. What that costs is a
        // taller panel, on the one panel with nothing under it to push off the screen.
        _body = Controls.Text("", BodySize, Style.NoteText);
        _body.style.width = StyleKeyword.Auto;
        _body.style.height = StyleKeyword.Auto;
        _body.style.whiteSpace = WhiteSpace.Normal;
        _body.style.unityTextAlign = TextAnchor.UpperLeft;
        // Held off the panel's inset on both sides and given more than a gap under it,
        // so the words read as a block laid on the panel rather than as another row of
        // it. The picture above has already put a gap under itself, which is why the top
        // adds only what is missing — the rule Controls.Gap states.
        _body.style.marginLeft = TextMarginX;
        _body.style.marginRight = TextMarginX;
        _body.style.marginTop = TextMarginY - Controls.Gap;
        _body.style.marginBottom = TextMarginY;
        panel.Add(_body);

        // A grey box with a mark inside it when it is ticked, rather than a box that
        // lights whole. The mark is drawn in the shade every switch in this app lights
        // in — this is a lit state narrowed and not a new idea — over the ground the box
        // keeps in both states. It is still not a check mark: a tick would be the one
        // glyph on the screen that is a picture of a decision rather than the decision
        // itself.
        //
        // The whole box lit is what Controls.SetActive draws, and it is right where it is
        // used. A switch in a run of switches is read against the ones beside it that are
        // dark, so a lit ground there has something to be lit against. This box stands on
        // its own at the foot of a panel with a sentence beside it, and a small square
        // filled edge to edge has nothing to be brighter than: it stopped reading as a
        // box that had been ticked and started reading as a box that had been coloured
        // in. Keeping the grey and putting the light inside it says the same thing about
        // the state and says *box* while it says it.
        //
        // Told a row's height for both of its sides, which is what a control with nothing
        // written in it is measured by here — 30 on a touch screen and 20 under a mouse —
        // and stripped of the air a button carries inside it for a word, on all four
        // sides. This one has no word, and with a mark standing in it that air is no
        // longer only spacing: it is what the mark has to fit inside. At the sides it
        // leaves six units of a twenty unit box for a mark ten wide, and over and under
        // it leaves nothing at all — measured under the mouse profile, a box with its own
        // padding left on comes out 20 by 22 with the mark ten wide and zero high, which
        // is a ticked box that draws no tick.
        //
        // Controls.Switch takes that padding off the sides of a blank box for the spacing
        // half of the reason, and says what is over and under cannot be taken off — which
        // is what floors its own runs at 22 units. Setting it to zero here does take it
        // off, and the box comes out square. Nothing is changed there on the strength of
        // that: what the floor costs a run of slots is a shape rather than a mark.
        _box = Controls.Push("", ToggleBox, 0);
        _box.style.width = Controls.RowHeight;
        _box.style.height = Controls.RowHeight;
        _box.style.flexShrink = 0;
        _box.style.paddingLeft = 0;
        _box.style.paddingRight = 0;
        _box.style.paddingTop = 0;
        _box.style.paddingBottom = 0;
        _box.style.alignItems = Align.Center;
        _box.style.justifyContent = Justify.Center;

        // The mark itself, centred by the two lines above and shown only while the box is
        // ticked. A child of the box rather than a second ground on it, because what is
        // wanted is two areas at once — the grey the pointer plays on, which still moves
        // under a hand in both states, and the light that says ticked.
        _mark = new VisualElement();
        _mark.style.width = MarkSize;
        _mark.style.height = MarkSize;
        _mark.style.backgroundColor = Style.NoteLine;
        TileElement.SetBorderRadius(_mark, MarkRadius);
        _box.Add(_mark);

        // Not a Caption, which is pinned to the caption column so that a panel's rows
        // line up; there is no column here and the words are longer than one.
        var caption = Controls.Text("Don't show this again", Controls.FontSize,
                                    Style.Label);
        caption.style.width = StyleKeyword.Auto;
        // Further off the box than the gap the box already carries to its right. That
        // gap is what parts two buttons, which are two things of the same kind standing
        // in a row; this is a word naming the square beside it, and at one gap the word
        // and the square read as a single mark.
        caption.style.marginLeft = Controls.GroupGap;
        // What holds the button out to the far end of the row, now that there is no
        // reading between the two to do it. Which means the word is the thing being
        // stretched, so it has to be told where to stand inside what it is given: every
        // label this UI builds is centred in its own box, which is right for a reading
        // in a slot and wrong for a word naming the square to its left.
        caption.style.flexGrow = 1;
        caption.style.unityTextAlign = TextAnchor.MiddleLeft;

        // The box, the word that names it, and the one button that moves the panel on,
        // on the one row at the foot.
        //
        // They were two rows, the box and its word above and the button below. Neither
        // is a row of the panel the way the picture and the paragraph are — between
        // them they are the whole of what a hand can do here, and stacked they spent a
        // band of the panel's height saying that a box to tick and a button to press
        // are different kinds of thing, which is a distinction the two shapes already
        // make. On one row the panel ends where its words end, and what stands under
        // the thumb is a line rather than a column.
        //
        // They keep their two ends of it and are not a pair. The box is the thing this
        // panel is not for, which is why it sits where the reading did and is read on
        // the way past; the button is what the panel is asking for, at the right, under
        // the thumb that will press it three times.
        //
        // There was a Skip beside the reading, and taking it out is what left the row
        // with room for the box. A way out is what a panel in the way owes whoever it is
        // in the way of, and this one is in nobody's way: it covers no control the three
        // pages point at, and the row and the plane behind it work with it still up. So
        // the button was an exit from something nothing was being kept from — offered at
        // the foot of the panel, under the thumb, on the one screen a first launch opens
        // on, which is close to the last place a player should be invited to leave. And
        // there was nowhere better to put it: every other position on this panel says
        // less about what it does than that one did. Three pages is short enough to sit
        // through, so Next three times is the whole of the way out now.
        var foot = Controls.Foot();

        foot.Add(_box);
        foot.Add(caption);

        _next = Controls.Push("", Next, ButtonWidth);
        // The last thing on the row, with the panel's own inset on the other side of
        // it: the gap every button carries would read as the panel being wider on the
        // right than on the left.
        _next.style.marginRight = 0;
        foot.Add(_next);

        panel.Add(foot);

        // Read once, here, the way SystemPanel reads the visualizer's: the panel is
        // where the box is drawn, so it is also what puts the box into the state the
        // setting was left in. It is false every time this panel is actually seen —
        // the caller only raises it while the setting says no — and reading it anyway
        // is what keeps the box and the setting from being two answers to one question.
        _ticked = Onboarding.Dismissed;

        Sync();
    }

    // Private members

    readonly Texture2D[] _pages;
    readonly Action _close;
    readonly Action _refocus;
    readonly Label _header;
    readonly VisualElement _picture;
    readonly Label _body;
    readonly Button _box;
    readonly VisualElement _mark;
    readonly Label _count;
    readonly Button _next;

    int _page;
    bool _ticked;

    // What the three pages say. The words are here rather than in a file for the reason
    // every other string on this screen is: there is one build of this app and one
    // language in it, and a table of strings nothing else reads is a second place for
    // the interface to be described.
    //
    // Each is what a player has to know before the thing works at all, in the order
    // they need it: start it, put something else in it, find out about the rest.
    static readonly (string Header, string Body)[] Pages =
    {
        ("Play",
         "Play starts the sequence and stops it again. The bar beside it is the " +
         "tempo — drag it, or double click to type a number."),
        ("Scores",
         "The arrows at the right of the row pick one of the saved scores, Load " +
         "opens it and Save writes the piece back to it. Drag the row sideways if it " +
         "runs off the screen."),
        ("User guide",
         "Everything else — the tiles, the lanes, the sounds a channel is given — is " +
         "written in the guide, and the \"?\" at the end of the row opens it."),
    };

    // Half again a column panel. See the constructor for what that is measured against.
    const float WidthOfPanel = 1.5f;

    // Named rather than written on the line that sets it, since what a page is drawn in
    // is measured from it as well — see ContentWidth.
    static float PanelWidth => Controls.PanelWidth * WidthOfPanel;

    // The edge is a hairline — the width every control here is outlined at, since what
    // has to be different about this one is the shade and not the weight. A width on a
    // panel is the outside of its box, so the border comes out of what the panel has to
    // draw in and ContentWidth allows for it.
    const float BorderWidth = 1.0f;

    // The paragraph's type. One step over the chrome and no more: enough to carry a
    // block of words, and not enough to make this panel look like a different piece of
    // software from the one behind it.
    static float BodySize => Controls.FontSize + 1.0f;

    // How far apart the paragraph's lines are set, as a percentage of what the face
    // asks for. Said as a markup tag on the text rather than as a style, because there
    // is no line height in USS at all — it carries text-shadow and nothing else of that
    // family, and the tag is what the text engine under the label does understand. See
    // Sync, which is where it is put on.
    //
    // A third again. The chrome's single lines have no use for leading and this is the
    // one place two lines of the same sentence stand under each other; at the face's own
    // spacing, which is tight, three lines of a paragraph this wide read as a block.
    const int LineHeight = 130;

    // The air let into the panel around the paragraph, over and above the panel's own
    // inset. Twice a gap at the sides, which is what parts one group of rows from the
    // next: the words are not a row, and this is what says so.
    //
    // Half again that over and under, and not for symmetry's sake. What the block is
    // held off at the sides is the panel's edge, which is already an edge; what it is
    // held off above and below is a picture of the interface and a row of controls, and
    // a paragraph tight to either of those reads as a caption on it rather than as the
    // thing the page is.
    const float TextMarginX = Controls.Gap * 2;
    const float TextMarginY = Controls.Gap * 3;

    // As wide as the file buttons on the transport row, which is what this one is: one
    // word on it, and no reason for the button at the foot to measure differently from
    // the pair the page before it is a picture of.
    const float ButtonWidth = 46.0f;

    // The mark inside the box, and the corner it is cut at. Half the box's own side, so
    // that a quarter of the box is left as ground on either hand of the mark: as much
    // grey as light, near enough, which is what makes the light read as something
    // standing in the box rather than as the box filled in with a border left over. Two
    // profiles, because the box is a row high and a row is not the same height under a
    // finger as under a mouse.
    static float MarkSize => Controls.Touch ? 14.0f : 10.0f;

    // One value for both profiles, unlike the box it stands in. The two boxes are cut at
    // radii a unit apart, which is a difference a corner this small cannot show — what a
    // radius has to do here is say the mark is of the same family as the box around it,
    // and two units says that at either size.
    const float MarkRadius = 2.0f;

    // What the panel has to draw a page in, which is what a picture is fitted to. The
    // border comes out of it as well as the two insets, since a panel's width here is
    // the outside of the box and not what is inside it.
    static float ContentWidth
      => PanelWidth - Controls.Inset * 2 - BorderWidth * 2;

    // Two device pixels to the unit, which is what the captures are cut at — see the
    // procedure at the top of this file. A page is drawn at half its pixel size, so a
    // master taken that way lands 1:1 wherever the panel resolves two device pixels to
    // a unit and is resampled where it does not, which is the wordmark's story too.
    const float CaptureScale = 2.0f;

    void Next()
    {
        if (_page + 1 < Pages.Length)
        {
            _page++;
            Sync();
            _refocus();
            return;
        }

        Close();
    }

    // Done on the last page: the panel goes down and nothing is written. What was going
    // to be remembered was written when the box was pressed — see Onboarding. It is a
    // method of its own rather than a line inside Next because what it means is its own
    // thing, and because it is what the caller's close callback is named after.
    void Close()
    {
        _close();
        _refocus();
    }

    void ToggleBox()
    {
        _ticked = !_ticked;
        Onboarding.Dismissed = _ticked;

        _mark.style.display = _ticked ? DisplayStyle.Flex : DisplayStyle.None;
        _refocus();
    }

    void Sync()
    {
        var page = Pages[_page];

        _header.text = page.Header;
        // With the leading in front of it. See LineHeight for why it is said here and
        // not in the style, and Pages for why the words themselves carry no markup.
        _body.text = $"<line-height={LineHeight}%>{page.Body}";

        Picture(_page < _pages?.Length ? _pages[_page] : null);

        _count.text = $"{_page + 1} of {Pages.Length}";
        _next.text = _page + 1 < Pages.Length ? "Next" : "Done";

        _mark.style.display = _ticked ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // The page's picture at half its pixel size, or nothing at all where there is no
    // picture to draw — which is a panel of three pages that read as three pages, and
    // is what the first launch looked like before any of them were taken.
    //
    // Shrunk rather than clipped if the crop is wider than the panel: a master cut too
    // wide is a mistake in the picture, and a picture running off the edge of the panel
    // would be a mistake in the interface.
    void Picture(Texture2D texture)
    {
        if (texture == null)
        {
            _picture.style.display = DisplayStyle.None;
            return;
        }

        var width = Mathf.Min(ContentWidth, texture.width / CaptureScale);

        _picture.style.display = DisplayStyle.Flex;
        _picture.style.width = width;
        _picture.style.height = Mathf.Round(width * texture.height / texture.width);
        _picture.style.backgroundImage = Background.FromTexture2D(texture);
    }
}

} // namespace Jacquard.App
