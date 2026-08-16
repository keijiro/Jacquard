Web
===

What the browser does differently, in one place. Three of the four things that
matter here are written up where they belong and only pointed at from this file:
the synth is pushed from `Update` rather than pulled by the pipeline
([impl-audio.md]), the chrome is sized in CSS pixels rather than by the inch
([impl-style.md]), and a save reaches IndexedDB only because of the flag the
page below sets ([impl-files.md]).

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
