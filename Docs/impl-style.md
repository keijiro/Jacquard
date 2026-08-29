Style and metrics
=================

What the interface is made of below the level of any one panel: how a control answers a
hand, how big everything is on which pointer, the face it is set in, and the marks. The
code is `Style` and `Controls` in `Assets/Jacquard/UI`, the panel settings at
`Assets/UI/DefaultSettings.asset`, and the scripts in [Branding], each of which argues
for its own numbers. What is here is the three rules that reach past any of them.

[Branding]: ../Branding/README.md

The palette is one ramp
-----------------------

One ramp of greys, no gradients, no shadows, nothing coloured to carry meaning — the
standing decision in [overview.md]. `Style.Grey` takes a single value, so there is no
longer anywhere to put a tint back; it used to carry a hint of blue at the dark end and
warmth at the light one, which left the mid greys sitting between two hues rather than on
one scale.

There is no shadow at all, which took a removal rather than a rule: the onboarding panel
carried one, being the one element with another panel behind it rather than the plane, and
it was drawn out of a stack of rings because USS here has text-shadow and no other shadow
to ask for. What retired it is the fog below — the panel is not raised until the screen
behind it is under the grey, so the sheet in front is parted from what is behind it before
it arrives. `OnboardingPanel` argues that, and keeps the line around its edge.

The one thing laid over the whole screen is the fog behind those same three pages, and it
is neither a shadow nor a gradient: it is one flat grey at one alpha, with a hole in it
around the control the page names. Grey and not black, because black heavy enough to be
noticed takes the plane, the row and the panels to the same near-nothing and leaves a
dialog on an empty ground; a grey takes them to the same grey, so the screen is flattened
rather than put out. Which grey is the palette's business and not taste's: it sits over
`Style.Background` and under `Style.ControlBackground`, so the control in the hole is
still the lightest ground and the brightest ink on the row, and light still means
*engaged*. It has to be a cover and cannot be an opacity written on what is under it,
because the visualizer is drawn by the camera behind the interface and is not an element
anything here can dim. `Style.DimmedOpacity` is the rule for a control that is out of
reach; this is the same idea aimed the other way — nothing under it is out of reach, and
what is left alone is what is being pointed at. See `OnboardingShade`, which keeps its
two numbers to itself rather than putting them in `Style`: a sheet laid over the ramp is
not an entry in the ramp.

**That fog is the one thing in this interface that moves.** It comes down a moment after a
launch rather than being there when the screen arrives, it lifts when the pages go, and
the hole in it holds a white that swells and falls while a page is being read. Nothing
else here animates — not a panel, not a switch, not a bar, not a cell — and the exception
is kept inside one file on purpose: the control a page is naming is the one thing on
screen worth spending motion on, and everywhere else position on the ramp and the air
around it are still the whole of what carries meaning. The numbers and the arguments for
them are `OnboardingShade`'s.

What a thing is saying is said by **where it sits on the ramp** and by **how much air is
around it**. Two consequences that reach every control:

- **Light is what *engaged* means.** A lit switch, a solid cell, a bar opened to be typed
  into are all the pale end. So a ground under a hand moves *up* the ramp — except where
  it is already pale and has nowhere up to go, which is the case `Style.Step` exists for.
- **The two polarities do not take the same weight of type.** `Style.SetInk` sets the
  colour and the cut together because they are one decision. Do not set one without the
  other.

[overview.md]: overview.md

The pointer decides the metrics, not the screen
-----------------------------------------------

**Two metric profiles rather than a UI scale.** A mouse lands on whatever it is over; a
fingertip covers about nine millimetres of glass whatever is under it. `Controls.Touch`
is settled once by `LayOutFor` before the first element is built, and every metric is
read at construction — so **no call site ever passes a profile-aware number**, and
nothing goes back to correct an element afterwards.

Two things deliberately do not move: `Style`'s cell pitch, because the score already read
right on the iPad and only the chrome did not; and the paddings, margins and dividers,
because the growth is spent on the targets rather than on the air between them.

**A scale on the panels is ruled out by what is coming.** Pinch zoom will put a
continuous fractional scale on the plane's content, which makes the score's on-screen
size the hand's decision — so the chrome has to stay the one place where layout values
are the real sizes and no transform is applied. Otherwise 1px borders and corner radii
sit permanently off the pixel grid beside a plane that is legitimately smeared only while
it is pinched.

Everything is sized by the inch, except where it cannot be
----------------------------------------------------------

`Assets/UI/DefaultSettings.asset` is a constant *physical* size at a reference DPI of
132. A unit is therefore a hundred-and-thirty-secondth of an inch, which on any @2x iPad
resolves to exactly two pixels — so **one UI pixel is one iOS point there**, and a 30pt
control row can be read against Apple's 44pt guideline rather than guessed at. That
arithmetic is what the touch metrics rest on, and it is worth not re-deriving: 132 keeps
the iPad exact and grows the Mac, because the two platforms could not agree on a physical
size.

Three platforms cannot be sized this way and each is corrected in its own place. None of
the three writes to the asset — the asset stays the only thing a player reads.

| | |
| --- | --- |
| The browser has no DPI to give | `JacquardApp.FollowTheBrowser`, `FollowTheZoom` — one unit, one CSS pixel |
| The editor resolves against the wrong monitor (UUM-136603) | `JacquardApp.StandInForTheDevice` |
| A non-retina 96 dpi screen resolves to 0.727 and is illegible | Nothing guards against it. Known, unfixed |

Where the rest is written
-------------------------

| | |
| --- | --- |
| The ramp, the cell pitch and what is derived from it, the rail and jump-link geometry | `Style` |
| The hover and press steps, and which way a ground moves | `Style.Step` |
| Ink: the two colours, the synthesised bold, and why dropping it was reverted | `Style.SetInk` |
| The five pointer events a button answers, and why the press is taken on the way down | `Controls.React` |
| Both profiles' numbers, and why `Auto` follows a simulated device and not the build target | `Controls` |
| The type size as a property of the face, and the ratio to hold rather than the numbers | `Controls.FontSize` |
| The face, its licence, and why the font asset is built at startup rather than checked in | `Style`, `Assets/UI/Fonts/` |
| The marks, the pixel grid they land on, and the master's cells-per-pixel | [Branding], `JacquardUI.MarkOfBox` |

The wordmark is the one thing on screen held off the edge by the display's corner rather
than by the safe area — see [impl-panels.md].

[impl-panels.md]: impl-panels.md
