namespace Jacquard {

// The unit of file saving, sitting above the score.
//
// It holds what applies to everything: tempo and meter, per sequencer-spec.md, plus the
// patch bank — the synth has nowhere to keep a timbre, so the project is where the
// bank belongs even though each patch in it answers to a channel rather than to
// the project. There is one score for now; nothing has yet turned up that would
// make binding several of them worthwhile.
//
// The send effects are here on the plainest reading of the same rule: there is one
// reverb and one delay for the whole thing, so what they are set to applies to
// everything. How much of a note reaches them does not, and lives in the patch.
//
// The limiter is here on a stronger reading of it still. A send is at least a thing a
// note can be given more or less of; the limiter is across the sum of everything, so
// there is no per note share of it to put anywhere else.
//
// The scale is here on the plainest reading of all: a piece is in a key. What it does
// happens a note at a time, which is what the channel's transpose does too, but the
// two are held apart for exactly the reason the reverb and its sends are — how far a
// channel is moved is a property of that channel, and what the whole thing is allowed
// to land on is not.

public sealed class Project
{
    // The tempo a score opens at, chosen for being unremarkable rather than for being
    // good: 120 is what every figure in Docs is written at and the rate a hand is
    // likeliest to already be counting in, so a fresh score and a read example agree.
    // A tempo is set once and typed over early, so what is wanted of the number it
    // starts at is that it says nothing about the piece.
    public float Tempo { get; set; } = 120.0f;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;

    // By reference, for the same reason PatchBank hands out one: the panel sets a
    // single field of a mutable struct, and a property would only ever hand it a
    // copy to write into.
    public ref SendFx Fx => ref _fx;

    // The one thing that is across everything rather than sent to from anywhere, which
    // is why it sits here beside the tempo and not in a patch: there is no amount of a
    // channel that reaches the limiter, since what reaches it is the finished mix.
    public ref Limiter Limiter => ref _limiter;

    public Score Score { get; set; } = new Score();
    public PatchBank Patches { get; } = new PatchBank();

    // Which channels are heard, here on the same reading that puts the bank here: a
    // mute answers to a channel rather than to the project, and the project is still
    // the only thing above it that a file is made of. It is the one part of this that
    // is played rather than set, and it is saved all the same — see ChannelMutes.
    //
    // Everything that reads the mutes reads them through the project it is already
    // holding, rather than being handed a set of its own to keep: a load replaces the
    // project, so anything with its own reference would go on pressing switches on the
    // file that was closed.
    public ChannelMutes Mutes { get; } = new ChannelMutes();

    // Which semitones are allowed to sound, here on the plainest reading of the rule
    // that puts the reverb here: there is one of it for the whole thing. A piece is in
    // a key, not a channel — two channels in two keys is two pieces — so this is the
    // one place it can be set from and the one place a load has to put it back.
    public Scale Scale { get; } = new Scale();

    // What a written note actually sounds as: the channel moves it, and then the scale
    // decides whether it will have it there.
    //
    // The order is the whole of what this says, and it is here rather than at the two
    // places that ask so that there is one of it. A transpose that ran after the snap
    // would carry every note straight back out of the scale, which is the one order
    // that makes both settings useless at once.
    //
    // The patch is passed in rather than looked up because the sequencer holds a
    // working copy of it — the locks of this instant have already coloured it — and
    // that is what makes a lock on the transpose reach one step.
    //
    // What the live effects do is deliberately not here: they colour a note that has
    // already been made, in hertz, and so they land outside the scale. See LiveFx.
    public int SoundingPitch(in FmPatch patch, int note)
      => Scale.Snap(note + (int)System.MathF.Round(patch.transpose));

    // Exchanges everything a channel number keys, so that two channels trade places and
    // the piece goes on sounding as it did.
    //
    // Here for the reason SoundingPitch is: this is the only object that can see all
    // three of the keyed things at once. The bank and the mutes are its own, the tiles
    // hang off its score, and neither Score nor PatchBank can reach the other. Three
    // things and no more — the timbre, the hand held over the channel, and which lanes
    // name it — because every other reader asks for its answer at the moment it needs
    // it: the sequencer copies the bank at the top of every instant and reads the mutes
    // at the note's exit, a runner reads its lane's CHAN live and so is renumbered
    // mid-step along with everything else, and a branch lane has no number of its own.
    //
    // Lane positions are deliberately not touched. Runner order is a vertical position,
    // so leaving the lanes where they are is what keeps the mix the same — and it is
    // what makes this its own inverse, which is this app's answer to having no undo: a
    // mistaken press is undone by pressing again, so nothing here needs confirming.
    //
    // Both numbers are folded in once, at the top, rather than at each access. Every
    // reader here clamps for itself — the bank's indexer, the mutes' index, the tile's
    // own setter — but the scan below compares against what it was handed, so a raw
    // argument would exchange the patches and mutes of 1 and 8 for SwapChannels(0, 9)
    // and renumber not one tile, which is the single state this operation promises
    // cannot happen. Folding rather than rejecting is PatchBank.Clamp's own law.
    //
    // Nothing about a swap can be refused, so there is nothing to return. A bool that
    // was false only for a == b would read at the call site as one that can fail.
    //
    // The one place it is not inaudible is the turn of the piece. The master lane is
    // the first channel one lane in runner order, so a swap involving channel 1 moves
    // the title — which is the spec's own rule, the master being a position rather than
    // a flag, and is the thing that was asked for when a hand renumbers. What the title
    // carries with it is a privilege: the master lane runs whatever its own Play switch
    // says. So the lane that gains it while switched off starts sounding, one lookahead
    // ahead of the audio position, and the lane that loses it while switched off drops
    // out at the end of its lap. Both stand, because the plane already tells the story —
    // a head cell is drawn from what will happen rather than from what is written on
    // it, so the rebuild turns the new master's cell solid and greys the one that gave
    // the title up. The period moves too when the new master is a different length.
    public void SwapChannels(int a, int b)
    {
        (a, b) = (PatchBank.Clamp(a), PatchBank.Clamp(b));
        if (a == b) return;

        // By value and never by ref: the indexer hands back a writable ref, and an
        // aliased read would make the exchange a no-op.
        var patch = Patches[a];
        Patches[a] = Patches[b];
        Patches[b] = patch;

        Mutes.Swap(a, b);

        // Lanes rather than ChannelLanes, which allocates and sorts for an ordering
        // this does not need. The else is what keeps it a swap: without it the second
        // test catches what the first has just written and folds both numbers onto one.
        foreach (var lane in Score.Lanes)
        {
            var channel = lane.Channel;
            if (channel == null) continue;

            if (channel.Channel == a) channel.Channel = b;
            else if (channel.Channel == b) channel.Channel = a;
        }
    }

    // What a score starts as: one lane of sixteen steps with a C4 on every fourth and a
    // tone to hear them in, which is what every empty slot on a fresh install holds and
    // what a score is initialized to anywhere else.
    //
    // Not an empty lane, which is what this used to be. A lane with nothing in it is
    // silent, so the first press of Play on a new score answered with nothing at all —
    // and a hand that has just met the app cannot tell that from an app that is not
    // working. Four notes on the one pitch play a bar as soon as it is asked for, and
    // they are the least a score can hold and still say what a step, a lane and a lap
    // are: the plane shows the shape, the playhead walks it, and every cell of it is
    // something to type over.
    //
    // A bar of sixteenths rather than four steps end to end, because the lap this hands
    // over is the one everything else here is written in: the steps are sixteenths, so a
    // note every fourth is a beat, the lane is a bar of four, and the twelve empty cells
    // between them are where the rest of the bar is typed. Four steps would have played
    // the same pulse four times as fast and left nowhere to put anything between two
    // notes without lengthening the lane first.
    public static Project CreateInitial()
    {
        var project = new Project();
        var lane = project.Score.AddLane(1, 1, new ChannelTile(), 16);

        // Spelled out rather than left to the tile's own default, which is this note:
        // what the score holds is written here, not somewhere a default can move.
        for (var step = 0; step < 16; step += 4)
            Fill(lane, step, new NoteTile { Note = N("C4") });

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            DialTheOpeningVoice(ref project.Patches[channel]);

        return project;
    }

    // The sound the four notes are heard in, which is a different question from what a
    // parameter falls back to. FmPatch.Default is the nothing end of every bar — a
    // plain sine, no FM at all — and it is deliberately not a sound to start a piece
    // in: the first press of Play would answer with a test tone. So the tone is dialled
    // here, on top of the bank the project was built with, and the difference between
    // the two is exactly the four numbers below.
    //
    // A modulator three times the carrier and a radian deep is an odd harmonic and a
    // thin one: a hollow tone a line can be written with, and one that goes somewhere
    // in both directions when a hand starts moving the bars. Its decay halfway along
    // its travel is a time constant of about a tenth of a second, so the colour is
    // heard through the front of the note and the tail settles into the sine underneath
    // it. A hundred milliseconds of release is a tail rather than a gate. All four were
    // settled by ear against a written part.
    //
    // Every channel and not only the one with a lane on it, since the second lane a
    // hand adds is on the second channel and has no more reason to be a test tone than
    // the first. The whole bank is written to a file either way.
    static void DialTheOpeningVoice(ref FmPatch patch)
    {
        patch.modulatorRatio = 3.0f;
        patch.modulationIndex = 1.0f;
        patch.modulatorDecay = 0.5f;
        patch.carrierRelease = 0.1f;
    }

    // The worked example the specification was written against, which is also the
    // demonstration case: three lanes, a conditional jump into a variation, and an
    // accent lane that has no notes of its own.
    public static Project CreateSample()
    {
        var project = new Project();
        var score = project.Score;

        // Four steps against the main lane's sixteen, with no notes of its own: the
        // locks reach whatever this channel sounds later in the same instant, and
        // later means further down the plane, so this lane sits at the top.
        var accent = score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 4);
        Fill(accent, 0, Lock(new RelativeParamTile(), ParamTargets.Level, 2.0f));
        Fill(accent, 2, Lock(new RelativeParamTile(), ParamTargets.Level, -5.0f));

        var main = score.AddLane(1, 3, new ChannelTile { Channel = 1 }, 16);

        Fill(main, 0, new NoteTile { Note = N("C4"), Length = 4 },
                      new NoteTile { Note = N("E4") },
                      new NoteTile { Note = N("G4") });
        Fill(main, 2, new NoteTile { Note = N("F#4"), Length = 0.5f });
        // Above the note it colours, which is the only place it can be.
        Fill(main, 3, Lock(new AbsoluteParamTile(), ParamTargets.ModIndex, 7.0f),
                      new NoteTile { Note = N("A4") });
        Fill(main, 5, new NoteTile { Note = N("G4") });
        // A lock partway down a chord, so the two notes under it are brighter than
        // the one above it: the stack is read downwards, so the split is legible.
        Fill(main, 8, new CycleGateTile { Period = 4, Pattern = "0010" },
                      new NoteTile { Note = N("F4") },
                      Lock(new RelativeParamTile(), ParamTargets.ModIndex, 3.0f),
                      new NoteTile { Note = N("G#4"), Length = 1.5f },
                      new NoteTile { Note = N("C5") });

        var jump = new JumpTile();
        Fill(main, 9, new CycleGateTile { Period = 4, Pattern = "0001" }, jump);

        Fill(main, 10, new NoteTile { Note = N("A#4") });
        Fill(main, 11, new ProbGateTile { Percent = 35 },
                       new NoteTile { Note = N("B4") },
                       new NoteTile { Note = N("D5") });
        Fill(main, 13, Lock(new RelativeParamTile(), ParamTargets.ModDecay, 0.5f),
                       new NoteTile { Note = N("E5"), Length = 2 });

        // The branch lane, entered only through that one jump. Ten main steps plus
        // six here comes to sixteen either way, so a lap is the same length
        // whether it jumps or not.
        var variation = score.AddLane(6, 9, new JumpDestTile(), 6);
        variation.JumpSource = jump;

        Fill(variation, 0, new NoteTile { Note = N("D#5") },
                           new NoteTile { Note = N("C5") },
                           new NoteTile { Note = N("G#4") });
        Fill(variation, 2, new NoteTile { Note = N("A#4"), Length = 0.5f });
        Fill(variation, 3, new ProbGateTile { Percent = 70 },
                           new NoteTile { Note = N("G4") });
        Fill(variation, 4, new NoteTile { Note = N("F4") });

        return project;
    }

    static void Fill(Lane lane, int step, params Tile[] tiles)
      => lane.Steps[step].Tiles.AddRange(tiles);

    // A lock holding one parameter, which is all this score needs. Written as a
    // helper because engaging one is a call rather than a field, and a call does
    // not fit inside the list of tiles a step is filled with.
    static ParamTile Lock(ParamTile tile, int target, float amount)
    {
        tile.Engage(target, amount);
        return tile;
    }

    SendFx _fx = SendFx.Default;
    Limiter _limiter = Limiter.Default;

    static int N(string name)
    {
        Pitch.TryParse(name, out var note);
        return note;
    }
}

} // namespace Jacquard
