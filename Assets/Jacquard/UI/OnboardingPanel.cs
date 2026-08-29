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
// player who would rather press Play than read can do it with the panel still up. That
// is also why Skip is a way out and not the way out.
//
// It is also the only panel here with an edge and a shadow. Every other one is read
// against the plane, where a lighter ground with air around it is the whole of what
// says it is a panel — Controls.Panel argues that case and refuses a border on it. This
// one comes up in front of whatever a launch happens to have left open, and two sheets
// of the same grey overlapping is one shape with a fold in it. A line around it and a
// shadow under it are the least that says which of the two is in front; see the
// constructor for the line and Shadow for what a shadow costs where there is no
// box-shadow to ask for.
//
// Only the box reaches disk. Skip closes it for this launch, Done on the last page
// does the same, and neither writes anything — see Onboarding, which also says why
// there is no way back once the box is ticked.
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
// - In the editor, in play mode, with JacquardApp.Pointer set to Touch before Start so
//   the row is built at the tablet's metrics, and the panel's own scale set to 2 by
//   hand — left alone the editor resolves the Mac's dpi and the crop comes out at some
//   fraction of a device pixel to the unit.
// - ScreenCapture.CaptureScreenshot, and wait for the file: it is written at the end
//   of the frame after the call and is not there when the call returns.
// - Cut out the controls that page is about, taking the whole height of the row so the
//   band reads as a piece of the interface rather than as a button on a grey field.
//
// That leaves each master at exactly two device pixels to the unit, which is what
// Picture below then relies on: it draws a page at half its pixel size, so a capture
// taken this way lands pixel for pixel on the 2x screens this app is used on and is
// resampled nowhere else. A crop wider than the panel's content is shrunk to fit
// instead, which is a picture that has been cut too wide rather than a layout that has
// gone wrong.

sealed class OnboardingPanel
{
    public VisualElement Root { get; }

    // pages is JacquardApp.OnboardingPages, which may be short or absent — a page whose
    // picture is missing shows its words and nothing else. close lowers the panel, and
    // is the caller's because a panel does not decide whether it is on screen; refocus
    // is the keyboard going back to the plane, which every panel with a button owes.
    public OnboardingPanel(Texture2D[] pages, Action close, Action refocus)
    {
        (_pages, _close, _refocus) = (pages, close, refocus);

        // The root is a wrapper and the panel stands inside it, which no other panel
        // here needs. A shadow has to be measured from the panel's edge, and a child of
        // the panel is laid out from inside the panel's padding instead — so the rings
        // hang off a box that has neither padding nor border of its own, and that box is
        // exactly the one they are a shadow of. Nothing else about the wrapper is load
        // bearing: it is one column with one thing in it.
        Root = new VisualElement();
        Root.style.width = PanelWidth;
        Root.style.flexShrink = 0;

        Shadow();

        // The header is the page's subject — Play, Scores, User guide — which is the
        // rule every panel here follows and is also what tells one page from the next.
        // A panel titled "Welcome" with the subject repeated inside it would spend the
        // one line the eye goes to on the word that changes least.
        var panel = Controls.Panel(Pages[0].Header, out _header);

        // Half again a column panel, because what is on it is a picture of a row and a
        // row is wide. 288 units under a mouse and 372 on a touch screen, against the
        // 774 a phone held in landscape has to spare — so it fits the narrowest screen
        // this ships to with room either side. LivePanel is the other panel that sets
        // its own width, and for the same kind of reason.
        panel.style.width = PanelWidth;

        // The gap a panel carries under it belongs to the column of panels it stands in,
        // and this one stands in a layer by itself. Left on, it would be twelve units of
        // wrapper below the panel that the shadow would then be measured from.
        panel.style.marginBottom = 0;

        // The edge. In its own shade rather than the grey every control is outlined in,
        // which is the difference between a sheet that is in front and one more button
        // — Style.FrontLine carries that argument and the one it answers.
        TileElement.SetBorderWidth(panel, BorderWidth);
        TileElement.SetBorderColor(panel, Style.FrontLine);

        Root.Add(panel);

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

        // A box that lights when it is on, which is what SetActive already draws: in
        // this palette a lit box *is* a ticked one, and a check mark would be the one
        // glyph on the screen that is a picture of a decision rather than the decision
        // itself. Told a row's height for both of its sides, which is what a control
        // with nothing written in it is measured by here — 30 by 30 on a touch screen
        // and 20 by 22 under a mouse, where a button is never shorter than its own
        // padding and border, so a box told 20 for both of its sides comes out two units
        // taller than it is wide. Controls.Switch is floored by the same thing from the
        // other side and carries the argument.
        _box = Controls.Push("", ToggleBox, 0);
        _box.style.width = Controls.RowHeight;
        _box.style.height = Controls.RowHeight;
        _box.style.flexShrink = 0;

        var boxRow = Controls.Row();
        boxRow.Add(_box);
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
        boxRow.Add(caption);
        panel.Add(boxRow);

        // Skip on the left, where a way out goes; the reading in the middle, so it is
        // read as a position rather than as a control; and the one button that moves
        // the panel on at the right, under the thumb that will press it three times.
        var foot = Controls.Foot();

        foot.Add(Controls.Push("Skip", Close, ButtonWidth));

        _count = Controls.Value("");
        _count.style.unityTextAlign = TextAnchor.MiddleCenter;
        foot.Add(_count);

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

    // Which the wrapper is told as well as the panel, since the rings hang off the
    // wrapper and a shadow the wrong width is a shadow of something else.
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

    // How far the shadow reaches and how dark it starts, which are the two numbers a
    // blur would have been given. Eight rings on a panel this size is a shadow that is
    // seen rather than looked at, and a third of black at the near end is as much as
    // the grounds it falls on will take: the plane is Style.Background and another panel
    // is Style.Panel, both of them close enough to black already that most of what this
    // does is done in the first two or three rings.
    const int ShadowRings = 8;
    const float ShadowAlpha = 0.30f;

    // How much further down the shadow goes than out to the sides, and so also how wide
    // each ring's bottom band is. See Shadow for why the drop is a shape rather than an
    // offset.
    const float ShadowDrop = 1.75f;

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

    // As wide as the file buttons on the transport row, which is what these two are:
    // one word each, and no reason for the pair at the foot to measure differently
    // from the pair the page before them is a picture of.
    const float ButtonWidth = 46.0f;

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

    // Skip and Done are the same act and are one method: the panel goes down and
    // nothing is written. What was going to be remembered was written when the box was
    // pressed — see Onboarding.
    void Close()
    {
        _close();
        _refocus();
    }

    void ToggleBox()
    {
        _ticked = !_ticked;
        Onboarding.Dismissed = _ticked;

        Controls.SetActive(_box, _ticked);
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

        Controls.SetActive(_box, _ticked);
    }

    // The shadow under the panel, which is drawn because there is nothing to ask for.
    //
    // USS here carries text-shadow and no other shadow at all: there is no box-shadow to
    // put on an element and no blur to reach for, so a soft edge has to be built out of
    // the one thing the layout engine will draw, which is a rectangle. These are square
    // rings around the panel, each one further out and fainter than the last, and what
    // the stack of them sums to is a falloff.
    //
    // Rings and not filled rectangles, and that is the whole of the trick. A child is
    // drawn over its parent's ground, so a filled shadow would be a black sheet across
    // the panel it is under; a ring at n units out is a line that never touches the
    // panel at all. Each ring's border is exactly as wide as the step to the next one,
    // so the bands meet rather than stripe.
    //
    // The drop is in the geometry rather than in an offset. A shadow moved down the
    // screen would put its top rings inside the panel — a ring is only ever outside the
    // box it hangs off — and starting them clear of that leaves a bare gap under the
    // panel where the shadow should be densest. So every ring is symmetrical about the
    // panel and reaches further below it than above, which is a shadow with a light
    // above it and no seam anywhere.
    //
    // Ignored by the pointer, all of them: a press that lands in the air around the
    // panel is meant for whatever is behind the panel.
    void Shadow()
    {
        for (var ring = 1; ring <= ShadowRings; ring++)
        {
            // Squared, so the first two or three rings carry the shadow and the rest of
            // it is the edge of a stain rather than a border a few values lighter.
            var fade = 1.0f - (ring - 1) / (float)ShadowRings;

            var mark = new VisualElement();

            mark.style.position = Position.Absolute;
            mark.style.left = -ring;
            mark.style.right = -ring;
            mark.style.top = -ring;
            mark.style.bottom = -ring * ShadowDrop;

            mark.style.borderLeftWidth = 1.0f;
            mark.style.borderRightWidth = 1.0f;
            mark.style.borderTopWidth = 1.0f;
            mark.style.borderBottomWidth = ShadowDrop;

            TileElement.SetBorderColor(mark, Style.Fade(Color.black,
                                                        ShadowAlpha * fade * fade));

            mark.pickingMode = PickingMode.Ignore;
            Root.Add(mark);
        }
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
