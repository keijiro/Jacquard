using UnityEngine;
using UnityEngine.UIElements;

// The one the Device Simulator stands in for, spelled the way Controls spells its
// Application for the same reason: the simulated Screen lives in this namespace, and a
// preview asked the plain one answers with the Mac the editor is running on.
using DeviceScreen = UnityEngine.Device.Screen;

namespace Jacquard.App {

// What the screen keeps for itself, in the units the interface is laid out in.
//
// A phone hands an app the whole display and then covers parts of it. In landscape,
// which is the only way this app is held, that is a camera housing cut out of one short
// edge and a home indicator along the bottom — and behind the indicator, the swipe it
// stands for, which the system claims before the app is told a finger landed. None of it
// makes the screen smaller: the ground under it is still drawn, and the app's own edges
// are still where they were. What it is not is anywhere to put something that has to be
// read or pressed.
//
// So this is not a viewport. Nothing here shrinks the plane or the row's own ground —
// the score carries on under the housing the way it carries on off the screen, and a
// row of chrome that stopped short of the edge would read as a row that had been cut
// off. What moves is only what is inside them.
//
// Four numbers rather than the rectangle they come from, because every place that reads
// one of them is pinned to a single edge and wants that edge's inset. It is also the
// arithmetic that is easy to get wrong twice: the platform's rectangle is in device
// pixels and counts its y from the bottom of the screen, while everything in this
// interface is in layout units and counts down from the top.
readonly struct SafeArea : System.IEquatable<SafeArea>
{
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }

    public SafeArea(float left, float right, float top, float bottom)
      => (Left, Right, Top, Bottom) = (left, right, top, bottom);

    public bool Equals(SafeArea other)
      => Left == other.Left && Right == other.Right &&
         Top == other.Top && Bottom == other.Bottom;

    // Read against the panel that is going to be laid out to it, since the conversion
    // from pixels to units is that panel's own scale and no one else's. It is not the
    // device's pixel ratio: this panel is sized by density, so on an iPad a unit lands
    // on an iOS point exactly and on a 458 ppi phone it is a sixth larger than one.
    //
    // Nothing to say on a screen that keeps nothing, which is every desktop and every
    // browser — the rectangle is the whole display there and all four come out zero.
    // Empty is treated as the same answer rather than as a screen that is entirely
    // unsafe, since a platform with nothing to report is likelier than a display with
    // no usable part.
    public static SafeArea Read(IPanel panel)
    {
        if (panel == null || panel.scaledPixelsPerPoint <= 0.0f) return default;

        var area = DeviceScreen.safeArea;
        if (area.width <= 0.0f || area.height <= 0.0f) return default;

        var scale = panel.scaledPixelsPerPoint;
        var width = DeviceScreen.width;
        var height = DeviceScreen.height;

        return new SafeArea(Inset(area.xMin, scale),
                            Inset(width - area.xMax, scale),
                            Inset(height - area.yMax, scale),
                            Inset(area.yMin, scale));
    }

    // Clamped, because a rectangle reported larger than the screen it is on would
    // otherwise pull the chrome outwards rather than leaving it where it stands.
    static float Inset(float pixels, float scale) => Mathf.Max(pixels, 0.0f) / scale;
}

} // namespace Jacquard.App
