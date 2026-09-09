Web
===

What the browser does differently, in one place. Four of the five things that
matter here are written up where they belong and only pointed at from this file:
the synth is pushed from `Update` rather than pulled by the pipeline
([impl-audio.md]), the chrome is sized in CSS pixels rather than by the inch
([impl-style.md]), a save reaches IndexedDB only because of the flag the
page below sets ([impl-files.md]), and Export and Import stand in for a score
folder the browser has nothing to open ([impl-files.md], and the section at the
foot of this one for the browser's own half of it).

[impl-audio.md]: impl-audio.md
[impl-style.md]: impl-style.md
[impl-files.md]: impl-files.md

The Web page template
---------------------

**The Web page is the project's own, because the canvas has to be the window.** Both
built-in templates ship the canvas at the size Player Settings names and leave it
there, which for a plane that is panned around means the work area is whatever was
guessed at build time. `Assets/WebGLTemplates/Jacquard` fixes the canvas to all four
edges instead — Unity matches the drawing buffer to it every frame, device pixel ratio
included, so a resized window is simply more score. It also hands the canvas
`touch-action: none`, without which the browser keeps the drag, the pinch and the
double tap that the chrome and the plane are built around. It is also where saving
works at all: the runtime mounts `persistentDataPath` on IndexedDB, but persists it
automatically only when the page passes `autoSyncPersistentDataPath`, and nothing here
calls the sync by hand — the app writes plain files and never touches `PlayerPrefs`,
which is the one thing the engine syncs for itself. Without the flag a save reported
success and was gone on the next reload, in the in-memory filesystem the whole time.
It is still the browser's storage, so a cleared site is a cleared score; what the flag
fixes is losing one without leaving the page. Post-processing is off on
the renderer for the same platform: URP loads the FSR upscaling material whether or
not the upscaler is selected, and that shader does not exist on GLES3, so the only
way to stop the warning is to not have a post-process stack — and nothing here has
ever used one.

The size of the download
------------------------

`il2cppCodeGeneration` is `OptimizeSize` here as well as on iOS, so the wasm carries one
shared implementation of a generic where it would otherwise carry each instantiation.
What the setting is is argued once, for iOS, in [releasing.md]. What it is worth is not
the same on both, so it is measured here rather than carried across: a browser makes the
reader wait for the bytes before anything happens at all rather than once at install,
and it also has to compile what it was sent.

**Both halves of it are a reading, off two players built A/B and driven in Chrome.** The
wasm is 5.14 MB against 6.09 MB as served under Brotli — 0.94 MB of waiting — and 18.9 MB
against 26.3 MB before it, which is what buys the second half: navigation to a running
instance takes 635ms against 745ms over a fetch of 30ms, so the smaller module is 110ms
quicker to start once it has arrived. That 110ms is the browser's own price for the code
it was handed, and it is a price iOS does not pay at all.

What it costs is managed speed, and the measurement says which managed speed. A shared
implementation resolves its types at run time, so the cost lands on generic code and
nowhere else: four laps of the sample score scheduled cost 2.4 times what they cost
under `OptimizeSpeed`, and a synthetic run of generic containers 5.4 times, while the
format's round trip does not move out of the noise. Neither control moved at all — a
managed float loop through `FmVoiceState`, and 256 blocks of the real mix through its
Burst job, measure the same under both. **Burst is not reached by this setting**, and on
this platform that is worth saying as a reading rather than as an argument, since the
mix is rendered from `Update` here and shares its frame with everything else.

In the app it is **3 to 4 per cent of the main thread**: over twenty seconds of the
sample score playing, 4598ms of main-thread time against 4443ms, and the same again
while the plane is panned under it, with the frame interval, the worst frame and the
count of dropped frames identical either way. So the 2.4 times is 2.4 times of 1.2
microseconds, `Schedule` being called once an Update — this app's own managed code is not
what pays, and what does is the mass of engine managed code that runs every frame, UI
Toolkit above all. Two things would change the answer: a score large enough for
scheduling to cost milliseconds, and a panel rebuilt every frame.

Export and Import
-----------------

**The browser is the second platform with no score folder to hand over, and it gets the
same pair of buttons Android has.** Why there is nothing to open here is
[impl-files.md]'s to say, since it is the same argument on both platforms; what is the
browser's own is in `Assets/Plugins/WebGL/JacquardScoreTransfer.jslib`, whose header
carries the two readings the design rests on — that an import has to be opened with
`showPicker()` rather than `click()`, because Unity runs a button's C# handler in a
`requestAnimationFrame` task and WebKit's user gesture does not survive the hop, and that
an export needs no gesture at all. `ScoreTransfer` is the seam and holds both platform
arms.

**It asks nothing of where the build is hosted.** A Blob, an object URL and a file input
are all it uses, so this works on a page served from anywhere — which is the same thing
the audio path goes out of its way to keep true, and `JacquardAudioOut.jslib` argues what
it gave up for it.

**Safari asks for permission before a site's first download, and the page cannot see it.**
The click returns normally while the download waits behind a modal, so the app reports
that the file was handed over and nothing more. [manual.md] carries it, because the reader
is the only one who can see the prompt.

**iOS Safari is untested.** There is no device here, and a phone's download prompt is
likely to differ from the desktop's.

[manual.md]: manual.md
[releasing.md]: releasing.md
