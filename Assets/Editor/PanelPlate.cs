using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.Editor {

// Reading the interface back as pixels, at a size no window here has.
//
// Two tools want this and want it for the same reason. PlaneCapture needs a plate of
// the whole plane at twice the interface's own scale; ShotCapture needs a frame at the
// size a store asks its screenshots in. Neither size is a size the Game View is, and a
// screenshot of a window is only ever the size of the window — so the panel is pointed
// at a render texture and the picture is read off that instead.
//
// The two differ in what is behind the interface, which is the whole of the fork this
// file holds. A plate of the plane has nothing behind it and the panel paints its own
// ground; a store screenshot has the visualizer behind it, and the visualizer is a mesh
// on a camera rather than anything on the panel, so the only way it reaches a texture
// is for the camera to be pointed at the same one. Both are the same shutter with the
// clear moved.
//
// What each tool does to the interface before the shutter is its own business, and
// stays in its own file. This is only the shutter.
static class PanelPlate
{
    // Nothing behind the interface: the panel clears the texture to `clear` and draws
    // on top of it, so the ground is a flat colour and the caller chooses which.
    public static IEnumerator Take(PanelSettings settings, int width, int height,
                                   int scale, Color clear, Action<Texture2D> hand)
      => Take(settings, width, height, scale, clear, null, hand);

    // A camera behind the interface, pointed at the same texture, so the ground is
    // whatever it draws — which on this app's one camera is the visualizer over the
    // colour the camera clears to. Nothing is handed in for that colour: the camera
    // already holds the one the scene gave it, and a clear the render pipeline performs
    // takes the colour conversion that UI Toolkit's does not, so the value that is
    // right here is the wrong value there.
    public static IEnumerator Take(PanelSettings settings, int width, int height,
                                   int scale, Camera ground, Action<Texture2D> hand)
      => Take(settings, width, height, scale, null, ground, hand);

    // Points `settings` at a texture of this size, waits for the panel to draw into it,
    // and hands the result to `hand`. The caller owns what it is handed and destroys
    // it.
    //
    // Constant pixel size for the length of the capture, whatever the panel was on. The
    // asset ships at constant physical size, which is the right answer for a control
    // under a fingertip and no answer at all for a texture nobody is holding: it scales
    // by Screen.dpi, and a render texture has no inches for that to be a ratio of. So
    // the scale is handed in, and each caller argues for the number it hands.
    //
    // Written over the asset and put back, rather than applied to a copy of it. Handing
    // the document a different PanelSettings takes its tree off one panel and puts it
    // on another, and what is being captured is a tree the app built by hand and holds
    // references into; the asset is restored to the values it came with, so what is
    // left behind is a file marked dirty and identical.
    static IEnumerator Take(PanelSettings settings, int width, int height, int scale,
                            Color? clear, Camera ground, Action<Texture2D> hand)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB);

        var scaleMode = settings.scaleMode;
        var scaleWas = settings.scale;
        var texture = settings.targetTexture;
        var clearing = settings.clearColor;
        var clearValue = settings.colorClearValue;
        var clearingDepth = settings.clearDepthStencil;
        var cameraTexture = ground != null ? ground.targetTexture : null;

        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.scale = scale;
        settings.targetTexture = target;

        // A render texture starts out holding whatever the driver left in it, and a
        // picture with one uncovered pixel of that is a picture with one pixel of
        // somebody else's frame in it. So it is cleared, and cleared to what the caller
        // says is behind the interface rather than to nothing.
        //
        // Handed the linear value rather than the colour: a clear is written straight
        // to an sRGB target and takes none of the conversion the interface's own
        // colours take, so the colour itself comes out three times too light.
        //
        // With a camera on the same texture there is nothing for this to insure against
        // — the camera's own clear covers every pixel before the panel draws — and it
        // has to be off rather than merely harmless, since a panel that clears the
        // colour clears the frame it was meant to stand on.
        settings.clearColor = clear.HasValue;
        if (clear.HasValue) settings.colorClearValue = clear.Value.linear;

        // The depth and stencil clear, on the other hand, is required in both cases and
        // was the one thing here that had to be found rather than reasoned out. UI
        // Toolkit clips with the stencil buffer, so a panel drawing into a buffer it
        // did not clear clips against whatever was in it: the transport row's tempo bar
        // came back as a solid white box with its label gone, which is that bar's fill
        // and text drawn with their clip rectangles thrown away. It is the value the
        // asset ships, and it is forced anyway, because what the asset holds is not
        // this file's to assume.
        settings.clearDepthStencil = true;

        // Both draw into the same texture and the frame's own order is what stacks
        // them: cameras render, and a runtime panel repaints after everything a
        // coroutine is resumed by, which is the same reason the wait below ends where
        // it does.
        if (ground != null) ground.targetTexture = target;

        // Two frames: one for the panel to take the texture and lay itself out against
        // it, and one to draw. Then the end of that frame, since a runtime panel is
        // repainted after everything a coroutine is resumed by.
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        var plate = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var active = RenderTexture.active;

        RenderTexture.active = target;
        plate.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        plate.Apply();
        RenderTexture.active = active;

        settings.scaleMode = scaleMode;
        settings.scale = scaleWas;
        settings.targetTexture = texture;
        settings.clearColor = clearing;
        settings.colorClearValue = clearValue;
        settings.clearDepthStencil = clearingDepth;
        if (ground != null) ground.targetTexture = cameraTexture;

        target.Release();
        UnityEngine.Object.Destroy(target);

        hand(plate);
    }
}

} // namespace Jacquard.Editor
