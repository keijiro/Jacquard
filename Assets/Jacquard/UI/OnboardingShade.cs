using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The grey the three onboarding pages leave one hole in.
//
// The pages point at controls — Play, the score chooser, the "?" at the end of the row —
// and a paragraph pointing at a screen is only as good as the reader's search of it. So
// while the panel is up, everything that is not the control the page is about and not the
// panel itself goes under one flat grey, and the one thing left at its own brightness is
// the thing the words are naming.
//
// It is a sheet laid over, not an opacity taken off what is under it. Half of what has to
// go under is not an element at all: the camera clears to Style.Background and draws the
// visualizer behind the whole interface, so an opacity written on the plane would dim the
// lattice and leave the waveform under it exactly as bright as it was. A cover works on
// whatever is behind it and does not care whose it is.
//
// Three elements in two parents, because the row is a sibling of the body rather than a
// thing inside it — the same fact that keeps the onboarding panel from covering the very
// controls it points at, argued at the panel's construction in JacquardUI and in
// Docs/impl-panels.md. One sheet over the body covers the plane, both edges of panels and
// the dock; two bands inside the row cover the row either side of the subject.
//
// The hole is a vertical slot and not a frame of four sides. Every subject the three pages
// name stands on the transport row and fills its height, so a band to the left of the
// subject and a band to its right is the whole of the cut-out — which is the same shape
// the pictures on the panel are cropped to, and for the same reason.
//
// The bands go *inside* the strip's content container, which is the point of the whole
// arrangement. The row's content is moved by a transform when the strip is dragged, so a
// band that travels in that same subtree keeps its place against the control it is beside
// with nothing following anything: the hole moves with the row because it is part of the
// row. Hung outside the strip instead it would have to be written every frame from the
// subject's worldBound, and MonoBehaviour.Update runs before the layout it would be
// reading — so the hole would sit one frame behind the hand for the length of every drag.
//
// The bands are let run far outside the row on three sides and the strip's own
// overflow: Hidden cuts them (see ScrollStrip). That way nothing here has to know where
// the safe area put the content: the strip carries the notch as padding, the band starts
// above it and the strip clips it, so the grey reaches the top of the screen on a phone
// without a second number to keep in step with FollowTheSafeArea.
//
// Nothing here is picked, and that is deliberate rather than incidental. It is the exact
// opposite of the shield Controls.SetLocked lays over a panel: that one is picked so
// nothing under it can be pressed, and this one is transparent to the pointer so
// everything under it still can. The onboarding panel shields nothing and locks nothing,
// and darkening the screen must not quietly turn it into a panel that does — Play is
// still pressed through the grey, the plane still takes the cursor, and the row is still
// dragged.

sealed class OnboardingShade
{
    // body is the box the plane and the panels stand in, and row is the transport row
    // above it. Both are taken rather than found, since the one thing this has to get
    // right is the order it is added in — see where JacquardUI builds it, which is after
    // the dock and before the layer the onboarding panel is in.
    public OnboardingShade(VisualElement body, ScrollStrip row)
    {
        _sheet = Cover();
        _sheet.StretchToParentSize();
        body.Add(_sheet);

        // Both bands overhang the row above and below, and each overhangs the end of the
        // row it is nearest: the left one starts off the left end and is given its width,
        // the right one starts at the subject and runs off the right end.
        _before = Cover();
        _before.style.left = -Overhang;
        _before.style.top = -Overhang;
        _before.style.bottom = -Overhang;

        _after = Cover();
        _after.style.width = Overhang;
        _after.style.top = -Overhang;
        _after.style.bottom = -Overhang;

        row.Add(_before);
        row.Add(_after);

        Show(false);
    }

    // Up and down with the panel it belongs to, and by the same means: display, which
    // takes the sheet out of the picture entirely rather than leaving a transparent
    // element in front of the screen.
    public void Show(bool shown)
    {
        var display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        _sheet.style.display = display;
        _before.style.display = display;
        _after.style.display = display;
    }

    // Where the hole is, given the run of controls the page is about — one control, or
    // the first and last of a row of them standing together.
    //
    // The coordinates are read straight off the layout, with no WorldToLocal anywhere,
    // because the subject and the bands are children of the one box: both are laid out in
    // the content container's own space, and the transform that pans the row is applied to
    // that box rather than inside it.
    //
    // Written only when it moves, the way the lock and the safe area are. The row's layout
    // does not change while a hand is nowhere near it, so in practice this writes on the
    // frame a page turns and on a rotation, and on no other.
    public void Follow(VisualElement first, VisualElement last)
    {
        // The gap the row already leaves between two controls, added on either side, so
        // the hole is cut where the row's own air is rather than flush against the ink.
        var left = first.layout.xMin - Controls.Gap;
        var right = last.layout.xMax + Controls.Gap;

        // Nothing to cut around until the row has been through a layout, which it has not
        // on the frame the panel first goes up.
        if (float.IsNaN(left) || float.IsNaN(right)) return;

        if (left == _left && right == _right) return;

        (_left, _right) = (left, right);

        _before.style.width = left + Overhang;
        _after.style.left = right;
    }

    // Private members

    readonly VisualElement _sheet;
    readonly VisualElement _before;
    readonly VisualElement _after;

    // What the hole was last cut to. NaN so that the first real layout is a move.
    float _left = float.NaN;
    float _right = float.NaN;

    // What the sheet is and how much of it there is.
    //
    // Grey rather than black, and that is the difference between covering the screen and
    // erasing it. Black at an alpha heavy enough to be noticed takes everything under it
    // to the same near-nothing: the plane, the row and the panels all arrive at black
    // together, and what is left is a dialog on an empty ground rather than a screen with
    // one thing picked out of it. A grey takes them to the *same grey* instead, so what is
    // under the sheet keeps being a screen — it is flattened rather than put out — and the
    // one control left uncovered is read against a field that still has a texture to it.
    //
    // Which grey is decided by the palette's own rule rather than by taste. It sits a step
    // over Style.Background and well under Style.ControlBackground, so a lit control in the
    // hole is still the lightest ground anywhere on the row and its type is still the
    // brightest ink: light is what *engaged* means here, and a fog that came out lighter
    // than the thing it is pointing at would say the opposite. See Style.SetInk.
    //
    // Heavy, because a wash that has to be looked for is not doing the job. At the 0.55
    // it started at the plane still read as a score and the covered switches still read as
    // switches, which is a screen with a slightly dimmer half rather than a screen with one
    // thing on it.
    //
    // Neither number is in Style, for the reason OnboardingPanel.ShadowAlpha is not either:
    // the palette is a ramp of greys that things are *set in*, and this is a sheet laid over
    // the lot of them. There is no entry either of them could be, and putting them there
    // would invite a second element to be drawn in them.
    static readonly Color ShadeGrey = Style.Grey(0x24);
    const float ShadeAlpha = 0.78f;

    // How far the bands run past the row. One number for all three directions, and larger
    // than any screen this is drawn on rather than measured against one — it is cut by the
    // strip's overflow whatever it is, so the only thing it has to be is too big.
    const float Overhang = 2000.0f;

    static VisualElement Cover()
    {
        var cover = new VisualElement();
        cover.style.position = Position.Absolute;
        cover.style.backgroundColor = Style.Fade(ShadeGrey, ShadeAlpha);
        cover.pickingMode = PickingMode.Ignore;
        return cover;
    }
}

} // namespace Jacquard.App
