Style and metrics
=================

What the interface is made of below the level of any one panel: how a control
answers a hand, how big everything is on which pointer, the face it is set in,
and the marks. The code is `Style` and `Controls` in `Assets/Jacquard/UI`, the
panel settings at `Assets/UI/DefaultSettings.asset`, and the scripts in
[Branding].

[Branding]: ../Branding/README.md

How a control answers a hand
----------------------------

**A button moves under the hand rather than staying where it is.** The ground goes one
step along the ramp for a pointer over it and four steps for a pointer pressing it —
`Style.HoverStep` and `Style.PressStep`, applied to whatever ground the control is
already on. Four times and not two and a half, which is where this started and which
could not be felt: a hover is a control saying it is under the pointer, which is a
whisper, and a press is the control answering a hand, which has to be felt without
being looked for — so the gap between them has to be bigger than the gap between a
hover and nothing.
**Which way it moves is decided by where the ground already is.** A dark ground goes
lighter, because light is what *engaged* means everywhere else here: a lit switch, a
solid cell and a bar opened to be typed into are all the pale end of the ramp. A pale
ground has nowhere to go that way — a lit switch is a hair under white, so a step
lands seven values from where it started and a switch that was on had no answer to a
press at all — so it goes down into the ramp instead. What the two cases share is the
part that carries the meaning: the ground moves, and it moves further under a press.
A bright border under the press was tried first as the way out of the pale case and
removed; it drew a second, louder thing on the screen to say what the ground now says
for itself. Written as an amount rather than as a second palette entry per control
because a switch has two grounds, dark when it is off and pale when it is lit, and a
hand on it means the same thing in either — the first step from the ordinary dark
ground lands exactly on `ControlHover`, which is what the bars were already lifting to
before the buttons did anything at all.
It is five events and not two, because a hand does not only arrive and leave: it
presses and releases in place, it slides off while still holding — where the press is
over as far as the button is concerned, since the stock `Clickable` will not fire — and
it slides back on again, which the pointer capture is what knows, and it is the same
capture the click itself is decided by. `PointerCaptureOutEvent` covers the ending that
reaches no release at all, a window deactivated or a touch cancelled, which is the one
that would otherwise leave a button lit with nothing on it.
The three things the colour is a function of — the ground, the hover, the press — are
set from different places, so they are hung on the button in a `Reaction` rather than
closed over: `SetActive` is handed a button by callers that have never heard of any of
this, and a switch is toggled by the very press that is lifting it, so the ground has
to be remembered rather than written straight to the style.

**The press has to be taken on the way down, and finding that out cost a wrong
answer.** `Clickable` answers a mouse through the compatibility `MouseDownEvent` rather
than through the pointer event, and it clears the way for that by calling
`StopImmediatePropagation` the moment it sees the mouse's own pointer id — so a
`PointerDownEvent` callback registered the ordinary way is *never called by a mouse*.
It is called by a finger, so the first cut of this worked under every touch profile
and did nothing at all under a mouse. `TrickleDown.TrickleDown` puts the handler ahead
of the manipulator instead of behind it, which is where something that only paints
belongs anyway: nothing here consumes the event or decides anything about the click.
`PointerUpEvent` is registered the same way for symmetry, and `PointerCaptureOutEvent`
is left on the ordinary phase because a capture event is delivered to one element and
never travels, so there is nothing ahead of it to stop it.

Two things about checking it, both learned the hard way. **Sending the event to the
button proves nothing about this**: `SendEvent` with the target set runs the same
propagation, but the wrong-phase handler still fired there because the events arrived
in an order a real device never produces — the whole bug lives in what a real mouse
does. And **the editor Game view is not the place to drive a real cursor**: it stops
processing input the moment `Application.isFocused` goes false, which it does whenever
the focused editor window is anything else, and it went false repeatedly between one
step of a test and the next. A `BuildOptions.Development` standalone player, activated
and driven with `CGEvent` from a ten-line Swift script, has none of that: hover, press
and release were read straight off `screencapture` at `#444444`, `#757575` with a
`#F2F2F2` border, and the lit ground after the click.

Two metric profiles
-------------------

**The chrome has two metric profiles rather than a UI scale**, and what separates
them is not the screen but the pointer: a mouse lands on whatever it is over, and a
fingertip covers about nine millimetres of glass whatever is under it. `Controls`
holds a `Touch` flag settled once by `LayOutFor` before the first element is built —
every metric is read at construction — and `JacquardApp.Pointer` is `Auto`, which
asks `UnityEngine.Device.Application` so a simulated device is believed, with
`Mouse` and `Touch` overrides because the layout cannot be judged on the Mac it is
written on without forcing it. Row height goes 20 to 30, type 11 to 13, the caption
column 74 to 88 and a panel 192 to 248; `Controls.Width` stretches any other width
by the type ratio with a floor of the row height, so **no call site ever passes a
profile-aware number**.

Two things deliberately do not move. `Style`'s cell pitch is untouched, because the
score already read right on the iPad and only the chrome did not. And paddings,
margins and dividers stay at their mouse values: the growth is spent on the targets
and not on the air between them, which fifteen rows of sound cannot afford.

That row count used to be the number to watch. In the touch profile a row costs 33pt,
and the column — transport, Tile panel over a `CHAN` head, Sound panel — stood at
roughly 919pt against 834 on an iPad Pro 11", 820 on an Air and 744 on a mini. The
column did not scroll, so the shortest screens genuinely lost their bottom rows, and
every further lock target cost another 33pt off the same budget. The transpose is the
row that took it from 853 to 886, and it was spent knowingly: what it buys is a lock
that moves a note rather than shapes one, which nothing else in the list can do. The
unison took it from 886 to 919 for the same kind of reason, and 886 was already over
every one of those screens — a second row in a row bought on credit, against a
statement here that the column needed somewhere to put a row before it could honestly
afford another.

**That is the debt the scrolling column pays off**, and it was paid in both of the
ways this section left open rather than in one. The cursor's column is a
`ScrollStrip`, so a row past the bottom of the screen is a row that has to be dragged
to rather than one that is gone. And the panels that used to stack in it were merged
into the one panel the cursor answers to, which gives back a header, two insets and a
panel gap per group — the sound and the lock each cost a frame to say what a heading
now says. A row still costs 33pt of travel, but it costs nobody a control they cannot
reach, which is what the budget was really counting.

The mouse profile measured 630pt of column at the same cursor, so this was always a
tablet's problem rather than a shape that is wrong everywhere. What is left of it is a
question of how far a hand has to drag before it sees the row it wants, which is a
thing to feel on glass and not a number to defend here.

A scale on the panels was the alternative and it is ruled out by what is coming.
Pinch zoom will put a continuous fractional scale on the plane's content, which
makes the score's on-screen size something the hand holding it decides — so the
chrome has to stay the one place where **layout values are the real sizes and no
transform is applied**, or 1px borders and corner radii sit permanently off the
pixel grid beside a plane that is legitimately smeared only while it is pinched.

Type
----

**The face is Jura, and both its weight and its size are decisions rather than
details.** It is put on the root element and inherited from there, so a control that
chose a font of its own would be the only way this could go wrong; the font asset is
built from the `Font` at startup rather than checked in beside it, since what such an
asset holds is a glyph atlas and a material made from the one thing this project
actually chose, and saving them is committing a cache. What *is* checked in is a static
Regular instance cut from the variable file Google ships, whose weight axis starts at
Light: `TrueTypeFontImporter` has no way to ask for a position on an axis, so leaving
the variable file in the project would have set the whole interface in Light.

**The type size is a property of the face, not only of the eye.** Every caption column
and button width in `Controls` was cut to a word set at eleven and thirteen pixels, so
a face that sets the same word wider is a face those numbers have to come down for.
The interim swap through Michroma made that concrete: half again as wide, it pushed
"Reverb send" out of its column and half the transport row's switches out of their
boxes, and since the boxes are the layout and the layout did not move, the pair went
to eight and nine and a half — the largest at which the longest word in this UI still
stood inside the narrowest box it is given. Jura is narrow enough to give the original
numbers back, and `Style.ControlSize` with them. What survives either way is the rule:
**hold the ratio between the two profiles rather than the numbers**, because
`Controls.Width` *is* that ratio, and type that fits one profile then fits the other.

Jura is monoline and light, so the weight rule it inherited is differently shaped but
no weaker. A Didone ran every stroke from a stem to a hairline, and set dark on light
at this size the counters filled in and the thin strokes went to grey; a monoline face
has no hairline to lose and only thins, because a dark mark on a bright ground is
eaten at its edges by the ground while a bright one on a dark ground spreads into it —
and on a stroke as thin as this one that is most of the stroke. Measured on the lit
Play switch in the original face, plain type carried a little over half the ink bold
does — 1.8 times, over the same box. So `Style.SetInk` still sets the colour and the
weight together: there is no light ground in this UI that takes plain type and no dark
one that takes bold, and the three places that ask are a lit switch, a bar opened to
be typed into and the solid `CHAN` cell, which is the only word the score itself sets
on light. Bold is the one cut dilated rather than a second one — Jura has real weights
up to 700, but a second file to carry them is a second thing to keep in step with the
first, for a difference this synthesises well enough at these sizes.

**Dropping the weight was tried and reverted by eye** (2026-08-15). The argument for
dropping it is a good one — a synthesised bold is a face that does not exist, with its
own spacing and thickened joins, standing in a run of switches next to the same word in
the real one — and it is beside the point: plain type on the pale ground is markedly
harder to read at this size, which is the thing the rule was written for. It is a
question about a screen and it was answered by looking at one; anything that revisits
it should be settled the same way.

The licence travels with the face: Jura is under the SIL Open Font License, and
what it asks for is kept beside the font at `Assets/UI/Fonts/`.

Sizing by the inch
------------------

**The interface is sized by the inch, and the asset is the only thing that says
so.** `Assets/UI/DefaultSettings.asset` is a constant *physical* size at a
reference DPI of 132, a fallback of 264 and a scale of one; there is no pixel
scale in code and nothing writes to the asset at startup. A unit is therefore a
hundred-and-thirty-secondth of an inch, which on any @2x iPad — every model but
the mini is 264 ppi — resolves to exactly two pixels. That is the arithmetic the
touch metrics rest on: **one UI pixel is one iOS point there**, so a 30pt control
row can be read against Apple's 44pt guideline rather than guessed at.

It replaced a whole number on `JacquardApp`, and the reasoning behind that number
is still true: the grid is drawn in whole pixels with hairlines on half-pixel
centres, and a fractional scale smears all of it. What it had nothing to say about
was a screen it had not met, and a touch target is a measurement of a fingertip,
which does not shrink on a denser display. So the smearing is now accepted where
it happens. The known weak spot is the other end: a 96 dpi non-retina screen
resolves to 0.727 and is illegible, and there is nothing here that guards against
it.

The two platforms could not agree on a physical size. A unit was 0.168mm on a Mac
reading 303 dpi against 0.192mm on a 264 ppi iPad, 14.8% apart, so any single
reference DPI had to move one of them: 132 keeps the iPad exact and grows the Mac.
Worth not re-deriving. And `Screen.dpi` on macOS is a property of the display
*mode* rather than of the panel, so picking More Space used to shrink the interface
and now does not — that is the mode doing what it says.

**The browser is the one platform that cannot be sized by the inch at all**, and
`JacquardApp.FollowTheBrowser` is where it stops being asked to. Web has no DPI to
give: nothing in the platform's JavaScript reports one, and `Screen.dpi` answers 96 —
the density a CSS pixel is nominally defined against — times the device pixel ratio
the runtime applied to the canvas, measured in Chrome as 96, 192 and 288 at ratios of
one, two and three. Against a reference of 132 that is 0.727, 1.455 and 2.182 pixels
to the unit, and since the drawing buffer is larger than the page by the same ratio,
the ratio cancels: **a unit came out 0.727 CSS pixels on every display there is** —
the figure the weak spot above names, except that on the Web it was not a weak spot
but the only outcome. On this Mac in More Space that is 0.122mm a unit against the
iPad's 0.192mm, and in Safari on an iPad 0.140mm, which puts a 30pt touch row at
21.8pt: under the 20pt row the desktop profile would have given it.

So the physical size is given up there and the panel is handed the ratio as a
constant pixel size instead — **one unit, one CSS pixel.** That is the browser's own
device-independent unit rather than a fudge factor, and on iOS it is exactly one iOS
point: an iPad's CSS pixel is a hundred-and-thirty-secondth of an inch, which is this
project's reference DPI, so the chrome comes out the size the native build gives it on
the one platform that has both. Browser zoom then works on the interface as well as
on the score, since zooming is a change to the ratio — and because a page's ratio
moves under a running app, unlike a device's density, `FollowTheZoom` reads it every
frame and writes only when it has moved. The ratio is read as `Screen.dpi / 96`, which
is the one the runtime actually applied rather than `window.devicePixelRatio`, so a
page that turns off canvas matching or pins the ratio is followed rather than argued
with. Measured in Chrome after the change: a 192 unit panel is 192, 384 and 576 device
pixels at ratios of one, two and three, an emulated iPad gets the touch profile at 248,
and changing the ratio under the running page rescales without a reload.

**The editor does not preview any of this by itself** either, which is what
`JacquardApp.StandInForTheDevice` is for: UUM-136603 has the panel resolve its
density against whichever monitor the view is on rather than against the simulated
device, so an editor-only copy of the settings is switched to a constant pixel size
and given `Screen.dpi / referenceDpi` worked out from the DPI the Device Simulator
does shim. The bug report records it as not reproducible under a constant pixel
size, so that is stepping off the broken path rather than correcting a value it
produced — which is why it needs no timer, unlike the workaround that stays in
physical size and folds a ratio into the scale.

The marks
---------

**The marks are generated, not drawn.** The logo, the wordmark on the transport
row, the app icon and the favicon are all cut from the same pixel font by the
scripts in [Branding], which reduce the type to its own sixty unit grid and
apply the glitch in whole cells, so nothing anywhere is off that grid. Only the
wordmark on the row is an asset the app loads — `Assets/Branding/Logo.png`,
wired to `JacquardApp.Logo` by the scene builder, and a bitmap rather than a
Painter2D drawing like every other mark in the interface, since what would be
drawn is the same grid of squares the texture already holds. Paying for its
width is what first cut the file chooser's caption down to the width of the
word, and then took it off altogether: a caption is as wide as the longest name
on a panel so that a column of rows lines up, and a chooser on that row has
nothing above or below it to line up with. The box gave back exactly what the
word took, so the slot name between the arrows is as long as it ever was — and
what the caption said is said by where the box stands, after the rule and
between Save and Load, and by the file name written in it.
