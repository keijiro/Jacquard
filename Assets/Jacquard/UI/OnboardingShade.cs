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
// It comes down rather than simply being there. The one thing a fog over a screen has to
// say is that it *arrived*: a reader who was not watching it happen is looking at a dark
// screen with a panel on it, and a screen that has always been this way has nothing to do
// with the sentence on the panel. So the app comes up at its own brightness,
// waits half a second while a hand settles, and then takes four tenths of a second to go
// under — long enough to be watched, and short enough that nobody waits for it. That last
// is a stricter thing to ask than it was, and it is what halved the fade from the eight
// tenths it started at: the panel does not arrive until the fog is down (see Covered), so
// this is time spent in front of the first sentence rather than under it, and a fall that
// was worth watching on its own became a fall somebody is waiting out. The wait
// pays for something else on the way past. The bands cannot be cut to the row until the
// row has been laid out, and on the frame the panel goes up Follow has NaN to work with
// and returns, which leaves _after standing at left 0 with its whole overhang and the
// head of the row, Play included, grey for a frame. Half a second puts that frame
// somewhere nobody is looking.
//
// And the hole has a light in it. A hole on its own is a negative mark: it says "not
// here" about everything else and leaves the reader to find the one place that was not
// covered, which on a row of switches is a search rather than an answer. So the shape the
// fog is cut out of is painted white again, swelling from nothing to half and back while
// the page is read. It is the only thing in this interface that moves, and that exception
// is kept to this one file on purpose — the control the words are naming is the one thing
// on screen worth spending motion on. Docs/impl-style.md says as much where it says the
// palette carries meaning by position and air alone.
//
// Four elements in two parents, because the row is a sibling of the body rather than a
// thing inside it — the same fact that keeps the onboarding panel from covering the very
// controls it points at, argued at the panel's construction in JacquardUI and in
// Docs/impl-panels.md. One sheet over the body covers the plane, both edges of panels and
// the dock; two bands inside the row cover the row either side of the subject, and the
// light stands in the row between them.
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

        // The light goes in last of the three and is therefore over everything the row
        // holds, the bands included: nothing here asks for an order it does not already
        // have from being added last. Its top and bottom are the bands' own, so the
        // strip's overflow cuts all three at one edge and the white comes out exactly the
        // shape the grey is not; its left and width are Follow's, written on the line the
        // hole is written on.
        //
        // Cover leaves it transparent to the pointer, as it does the other three, and
        // that matters more here than anywhere else in this file: a white rectangle laid
        // over Play that took the pointer would be the one way this could stop Play being
        // pressed, which is the whole of what the paragraph at the top of the file says
        // must not happen.
        _light = Cover();
        _light.style.backgroundColor = HighlightWhite;
        _light.style.top = -Overhang;
        _light.style.bottom = -Overhang;

        row.Add(_before);
        row.Add(_after);
        row.Add(_light);

        // Down, and out of the picture rather than merely clear. The four fields all
        // start at nothing, so this only has to write what they already mean.
        Show(false);
        Paint();
    }

    // Up and down with the panel it belongs to, but not on the panel's own frame: this
    // says which way it is going and Tick spends the time getting there.
    //
    // Display is still what takes it out of the picture — a fog at nothing is still an
    // element standing in front of the screen, and Paint drops it the moment it has
    // nothing left to draw — but what decides display is now the level rather than the
    // caller.
    public void Show(bool shown)
    {
        _wanted = shown;

        // The wait belongs to the launch and not to the panel. Going up, the app has just
        // arrived and no hand is near anything; coming down, a button has been pressed and
        // the answer to a press is owed at once.
        _delay = shown ? RaiseDelay : 0.0f;
    }

    // Whether the fog is all the way down, which is what the panel waits for — see
    // JacquardUI.FollowTheFog, which is the only reader of this.
    //
    // A page arriving over a screen that is still on its way under would be read against a
    // ground that is still moving, and the reader would have both to follow at once; worse,
    // the hole would be least of all a hole at the moment the words first point into it. So
    // the fall is watched with nothing on it, and the words arrive on the frame it lands.
    //
    // Nothing here answers the way down: what puts the panel away is the press, on the
    // press's own frame, and the fog lifting behind it is what is left over.
    public bool Covered => _level >= 1.0f;

    // Where the fog has got to, every frame, from JacquardUI.Update.
    //
    // Time.deltaTime is the clock the one other moving thing in the app is written
    // against — see the visualizer's SlotFall — and there is no schedule here to keep
    // beyond it: this is a level moved towards a target and a phase turned over, both of
    // which survive a frame of any length.
    public void Tick()
    {
        var dt = Time.deltaTime;

        // The wait is spent before anything at all is written, so the screen the app
        // launches on is the screen it would have had with none of this in it.
        if (_delay > 0.0f)
        {
            _delay = Mathf.Max(_delay - dt, 0.0f);
            if (_delay > 0.0f) return;
        }

        var target = _wanted ? 1.0f : 0.0f;

        // Down and staying down, which is every frame of the app's life bar the ones a
        // first launch spends on these three pages. Written only when it moves, the way
        // Follow and FollowTheLock are.
        if (_level == 0.0f && target == 0.0f) return;

        _level = Mathf.MoveTowards(_level, target, dt / FadeSeconds);

        // The light breathes only while there is a fog for it to be a hole in. Past that
        // the phase is left where it stopped, and Follow sets it back to dark whenever the
        // hole moves.
        if (_level > 0.0f)
        {
            _phase += dt;
            if (_phase >= PulseSeconds) _phase -= PulseSeconds;
        }

        Paint();
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

        // The light is the same cut seen from the other side: it fills exactly what the
        // two bands leave, so one pair of numbers cuts the hole and paints what is in it.
        _light.style.left = left;
        _light.style.width = right - left;

        // From dark, every time the hole moves. A page that turns should be seen lighting
        // its new subject up rather than found with it already lit, since the rise is what
        // the reader's eye is being asked to follow.
        _phase = 0.0f;
    }

    // Private members

    readonly VisualElement _sheet;
    readonly VisualElement _before;
    readonly VisualElement _after;
    readonly VisualElement _light;

    // Which way it is going, what is left of the wait before it starts, how far down it
    // has come, and where the light is in its cycle. Show writes the first two and Tick
    // spends them; the last two are what Paint is written from.
    bool _wanted;
    float _delay;
    float _level;
    float _phase;

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
    // Neither number is in Style, and the palette's own shape is why: it is a ramp of greys
    // that things are *set in*, and this is a sheet laid over the lot of them. There is no
    // entry either of them could be, and putting them there would invite a second element
    // to be drawn in them.
    static readonly Color ShadeGrey = Style.Grey(0x24);
    const float ShadeAlpha = 0.78f;

    // How far the bands run past the row. One number for all three directions, and larger
    // than any screen this is drawn on rather than measured against one — it is cut by the
    // strip's overflow whatever it is, so the only thing it has to be is too big.
    const float Overhang = 2000.0f;

    // The fog's own clock: half a second before it starts down, and four tenths of a
    // second from nothing to everything or back. The wait is argued at the top of this
    // file and is the launch's alone — Show gives it to the way up and not to the way
    // down. One number for both directions, since a fog that took longer to lift than it
    // took to fall would be answering a press more slowly than it answered a launch.
    const float RaiseDelay = 0.5f;
    const float FadeSeconds = 0.4f;

    // And the light's. One turn, dark to half-lit and back, in a second and a fifth: the
    // rate of a slow breath rather than of a blink, which holds a place at the edge of the
    // eye for as long as three paragraphs take to read instead of pulling at it. Half and
    // not full, because what is under it is a lit control on a dark row and white laid over
    // that at full strength is a white rectangle rather than a control pointed at.
    //
    // It swells on a cosine rather than on a triangle. A triangle turns at a corner at
    // each end of its travel, and a corner in a brightness is seen as a tick — the eye
    // finds a break in a rate more readily than it reads the rate itself. A cosine comes
    // to rest at both ends, which is what "smoothly" is worth writing down as.
    const float PulseSeconds = 1.2f;
    const float HighlightAlpha = 0.5f;

    // The only full white in the interface: the ramp's own ink stops at Style.NoteText and
    // nothing is set in this. It is neither ink nor a ground — it is light let into a hole,
    // over a control that is already the lightest ground on the row — which is also why it
    // is not in Style, for the reason given above ShadeGrey.
    static readonly Color HighlightWhite = Style.Grey(0xff);

    // What the four elements are drawn at, given the level and the phase.
    void Paint()
    {
        // Nothing transparent is left standing in front of the screen: at nothing the fog
        // is gone rather than clear, which is what the display in Show used to say on the
        // caller's frame and now says on the level's.
        var display = _level > 0.0f ? DisplayStyle.Flex : DisplayStyle.None;

        _sheet.style.display = display;
        _before.style.display = display;
        _after.style.display = display;
        _light.style.display = display;

        _sheet.style.opacity = _level;
        _before.style.opacity = _level;
        _after.style.opacity = _level;

        // The level is a factor here rather than a gate, so the light arrives with the fog
        // and leaves with it: half a fog is half a light, and the hole never holds a mark
        // brighter than the grey that makes it a hole.
        _light.style.opacity = _level * HighlightAlpha * 0.5f *
                               (1.0f - Mathf.Cos(2.0f * Mathf.PI * _phase / PulseSeconds));
    }

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
