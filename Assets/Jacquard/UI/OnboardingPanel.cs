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

        // The header is the page's subject — Play, Scores, User guide — which is the
        // rule every panel here follows and is also what tells one page from the next.
        // A panel titled "Welcome" with the subject repeated inside it would spend the
        // one line the eye goes to on the word that changes least.
        Root = Controls.Panel(Pages[0].Header, out _header);

        // Half again a column panel, because what is on it is a picture of a row and a
        // row is wide. 288 units under a mouse and 372 on a touch screen, against the
        // 774 a phone held in landscape has to spare — so it fits the narrowest screen
        // this ships to with room either side. LivePanel is the other panel that sets
        // its own width, and for the same kind of reason.
        Root.style.width = Controls.PanelWidth * WidthOfPanel;

        _picture = new VisualElement();
        // Centred rather than stretched: the crops are not all one width, and a picture
        // of a row blown up to fill a panel is a picture of a row nobody can read.
        _picture.style.alignSelf = Align.Center;
        _picture.style.marginBottom = Controls.Gap;
        Root.Add(_picture);

        // Set the way SystemPanel's restart note is, and for the same reason: every
        // label this UI builds is one line in a fixed box, and this is a paragraph.
        _body = Controls.Text("", Controls.FontSize, Style.Label);
        _body.style.width = StyleKeyword.Auto;
        _body.style.height = StyleKeyword.Auto;
        _body.style.whiteSpace = WhiteSpace.Normal;
        _body.style.unityTextAlign = TextAnchor.UpperLeft;
        _body.style.marginBottom = Controls.Gap;
        Root.Add(_body);

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
        boxRow.Add(caption);
        Root.Add(boxRow);

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

        Root.Add(foot);

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

    // As wide as the file buttons on the transport row, which is what these two are:
    // one word each, and no reason for the pair at the foot to measure differently
    // from the pair the page before them is a picture of.
    const float ButtonWidth = 46.0f;

    // What the panel has to draw a page in, which is what a picture is fitted to.
    static float ContentWidth
      => Controls.PanelWidth * WidthOfPanel - Controls.Inset * 2;

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
        _body.text = page.Body;

        Picture(_page < _pages?.Length ? _pages[_page] : null);

        _count.text = $"{_page + 1} of {Pages.Length}";
        _next.text = _page + 1 < Pages.Length ? "Next" : "Done";

        Controls.SetActive(_box, _ticked);
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
