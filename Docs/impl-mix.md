Mix
===

What happens to the sum of the voices: the two send effects, the staging that
decides how loud a note is, and the limiter the mix is played through. The code
is the buses in `Assets/Jacquard/Audio` and the settings on `Project` in
`Assets/Core/Model`. The voice that feeds it is [impl-synth.md].

[impl-synth.md]: impl-synth.md

Smoothing the delay time
------------------------

**The delay time is the one number in the project that is smoothed**, and the reason
is what kind of quantity it is. The reverb's size and damping are coefficients, so
moving one changes how what is already in the lines decays and there is no seam. A
delay tap is a *position*: moved outright, the read pointer lands somewhere
unrelated to where it was and the join is a click. So it is rate limited rather than
set — a constant speed, which is a constant interval of pitch while it catches up
and nothing once it arrives, the sound a tape delay makes when its head is moved. An
exponential approach was rejected for starting the glide at whatever speed the jump
happened to be wide. A pair of taps and a crossfade is the alternative if the glide
is ever unwanted; it costs a second read per sample and cannot be played.

Effect settings and the audio thread
------------------------------------

**The effect settings are the only mutable state the audio thread reads.** Everything
else reaches it stamped into a note, which is what `SendFxRuntime` and the
`FmSynth.SetFx` message exist to work around — one reverb serving eight channels
cannot ride on a note. `JacquardApp.Update` sends it whenever it differs from the
last one sent, and since the delay time is converted to samples on the way, that one
comparison covers a bar being dragged, the tempo changing and a file being loaded
without any of them knowing that anything downstream cares.

Mix staging and the note budget
-------------------------------

**The mix is staged so that full scale is four notes**, which is one number —
`FmSynth.MasterGain`, a quarter — and it is what the threshold below rests on. It was
four fifths, which given a pan law at unity in the centre is a budget of one note: two
measured +4.1dBFS and a triad +7.6, and everything over the top was rounded off by the
soft clip. So a plain fifth at level 1 sounded dirty, and it was not the chord that was
wrong.

What the quarter costs is 10.1dB, and **the threshold is where it comes back** — the
make-up is the inverse of it, so pulling the bar down hands the level back and hardens
the mix on the way. That bar could not do this before: a mix arriving already at full
scale left it nothing to sit under, since a threshold below the mix squeezed everything
at once and one at the mix caught nothing. So what the gain really buys is a range of
levels for the one control here to mean something in.

It is a **format version**, and a conversion rather than a re-reading. A threshold is a
level, so a version 16 number met by a mix 10.1dB smaller would be a different setting
wearing the old one's spelling; shifted by exactly that, an older project comes back at
the level it had, with limiting beginning on the same note of the same bar. An older
piece is reproduced rather than approximated — the headroom is for what is written
next, and nothing is owed by what was written already. The shift is applied to the
project rather than to the limiter line, so that a file predating the limiter entirely
is carried with the rest instead of keeping a threshold at full scale over a mix a
quarter of the size.

The limiter, and the Global panel
---------------------------------

**The limiter is not there to stop the mix clipping**; a soft clip was already doing
that, and doing it without a control. It is there for the thing a limiter is actually
reached for on a drum machine — squeezing the mix hard enough that the loud parts hold
still and the quiet ones come up behind them. So there is one control, the
**threshold**, and it says how far the mix is squeezed rather than where the output
lands: **the make-up gain is derived from it and gives back exactly what it took off**,
so pulling the bar down makes the thing louder and harder together. There is no ratio
either — it is infinite, which is what makes this a limiter rather than something to be
dialled in.

The label is Threshold and the field is `ceiling`, which is the one deliberate
disagreement of that kind here: with the make-up automatic the output always lands at
full scale, so what the hand is choosing is where limiting begins, while down in the
bus it is still the level the gain holds the mix under. Renaming the field would be a
format bump for a word.

**The bar reaches 48dB down**, which is a make-up of 251 and most of its travel spent
somewhere no limiter is meant to be taken. That is the point: past a certain depth
everything in the mix is above the threshold, the gain stops articulating anything and
what is heard is the soft clip on the whole mix. It is an instrument, so the far end of
a bar is a sound rather than a warning.

**It used to be a pair, and the make-up is what collapsed them.** A drive pushed the
mix up into a ceiling that held the output down where it was put, which is the same
knob read from opposite ends: every setting worth having had one of them parked while
the other did the work, a ceiling below the drive was the two of them fighting with the
output quieter for it, and the only thing the drive was really for was getting the level
back. Deriving that instead of offering it removes a bar, removes the way of setting the
two against each other, and leaves the one number a hand reaches for. What it costs is
that the output is no longer somewhere a project can put it — full scale is where every
mix now lands, and the soft clip is what stands behind that.

The make-up is applied **after** the moving gain rather than before it, which is not a
detail: the detector has to go on reading the mix as it arrives, or the ceiling would be
measured against a signal that has already been given back what the ceiling took off and
nothing would ever settle. In that order the two multiply out to something simple —
under the ceiling the mix is lifted by the make-up and nothing else, at the peaks the
output lands on full scale however far down the ceiling is.

**The attack is a hole in the limiting for as long as it lasts, and that hole is the
punch.** The gain carries the attack and the release rather than a follower ahead of
it: a detector smoothed on the way in reaches the ceiling late and then holds the whole
note down, where a gain smoothed on the way out is wide open when the transient arrives
and takes exactly as long as the attack says to arrive at where it should have been. A
slow attack is therefore a kick with its front intact and everything under it ducking;
a release short enough to recover inside a step is a tail that swells.

The peak feeding it is **held rather than followed**, which is the one thing here that
had to be found by measuring. Read sample by sample the loudness of a tone goes to
nothing twice a cycle, so the gain climbed back between the peaks and met each one too
high — at 220Hz a cycle is 4.5ms against an attack of 5, and the output sat a fifth
over the ceiling however long it was given to settle. Holding the peak and letting it
go at the release leaves the gain a constant to converge on, so the only thing over the
ceiling is what the attack deliberately let past.

What is over the ceiling is what the **soft clip is now for**. It used to be the whole
of the output stage; it is now the limiter's backstop, rounding off the few samples a
slow attack lets through — which is what makes a lookahead unnecessary, and a lookahead
is the one thing here that would have cost latency.

One limiter, across the sum of everything. Per channel limiting and a side chain are
both a working answer to a real problem and both are more machinery than this asks for:
what is wanted is a switch that makes the thing louder and harder, not a mixing desk.
It sits on `Project` beside the tempo on a stronger reading of the rule that put the
send effects there — a send is at least a thing a note can be given more or less of,
and there is no per note share of a limiter to put in a patch.

One of its three numbers is in **decibels, which nothing else in this project is.** A
ceiling is a ratio of amplitude and the useful span of one is a few doublings, so a
linear bar spends most of its travel on the first of them and every number on it reads
as a multiplier nobody thinks in. A dB is already a logarithm, so the bar over one is
straight and a pixel is worth the same amount wherever it is taken. The conversion to a
gain, to the make-up that answers it, and of the two times to one pole coefficients all
happen once on the way to the audio thread.

An older file is **converted rather than having its drive skipped**, which is what every
other retired key here gets: the two numbers together said what one of them says now, so
`ProjectFormat` folds a drive of d dB into a ceiling c dB down as a ceiling of c − d and
version 13 is what makes that possible. The shape survives exactly; the level does not,
and cannot — the old pair left the output down at the ceiling and the make-up is
precisely the decision to stop doing that, so a converted project comes back |c| dB
louder.

**The panel is Global rather than Limiter**, which was a name for what would be on it
rather than for what was on it — and the scale is the first thing to arrive and prove
it. A Limiter panel would have been the right name for exactly as long as the limiter
was the only setting of its kind, and a panel per setting is a row of switches on the
transport for what is really one question — *what is set for the whole thing?* So the
panel answers that and each group inside it is headed. It was the one panel built
that way, against an argument that a panel already says what a heading would; the send
effects and the cursor's panel are grouped the same way now, and what is left of the
distinction is that the groups here have nothing in common but being global. The
scale
stands above the limiter in the order a note meets them: what it is allowed to be,
and then what the sum of everything is held under.

**The scale is a keyboard because a run of twelve boxes is not one.** Seven switches
across the bottom and five in the gaps above them, with the two gaps a keyboard does
not have — E to F and B to C — left empty. That is the whole of the shape and
deliberately so: what it has to do is let a hand find a semitone without counting, and
the two missing blacks do all of it. Narrower blacks, an overlap, a drawn key would be
a picture of a keyboard, and these are switches — a press allows a note rather than
playing one. They carry no captions for the reason a lap switch carries none: position
is what a switch in a run means, and here the position is a pitch. The size comes from
`Controls.SwitchSize`, since it is a metric of the profile in force and the blacks have
to be placed against it rather than laid out by a row.

It comes up **in the middle of the screen**. The columns are all read against the plane
and the dock is played over it, but a limiter is set while listening to the whole mix
with the eye nowhere in particular — and the middle is the one position on this screen
that says a panel is not part of the arrangement around the plane, which is what a
setting nobody visits twice a session should say. It covers the score while it is up,
and the switch that raised it is the way back.
