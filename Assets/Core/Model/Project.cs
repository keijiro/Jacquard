namespace Jacquard {

// The unit of file saving, sitting above the score.
//
// It holds what applies to everything: tempo and meter, per sequencer.md, plus
// the timbre — the synth has nowhere to keep a patch, so the project is where it
// belongs. There is one score for now; nothing has yet turned up that would make
// binding several of them worthwhile.

public sealed class Project
{
    public float Tempo { get; set; } = 132.0f;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;

    public Score Score { get; set; } = new Score();
    public FmPatch Patch = FmPatch.Default;

    // An empty project still needs one lane to type into.
    public static Project CreateEmpty()
    {
        var project = new Project();
        project.Score.AddLane(1, 1, new ChannelTile(), 16);
        return project;
    }

    // The mockup score from bp.html, which is also the demonstration case: three
    // lanes, a conditional jump into a variation, and an accent lane that has no
    // notes of its own.
    public static Project CreateSample()
    {
        var project = new Project();
        var score = project.Score;

        var main = score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 16);

        Fill(main, 0, new NoteTile { Note = N("C4"), Length = 4 },
                      new NoteTile { Note = N("E4") },
                      new NoteTile { Note = N("G4") });
        Fill(main, 2, new NoteTile { Note = N("F#4"), Length = 0.5f });
        Fill(main, 3, new NoteTile { Note = N("A4") },
                      new AbsoluteParamTile { Target = ParamTargets.ModIndex, Amount = 7.0f });
        Fill(main, 5, new NoteTile { Note = N("G4") },
                      new AccumParamTile { Target = ParamTargets.Detune, Amount = 0.5f });
        Fill(main, 8, new CycleGateTile { Period = 4, Index = 3 },
                      new NoteTile { Note = N("F4") },
                      new NoteTile { Note = N("G#4"), Length = 1.5f },
                      new NoteTile { Note = N("C5") });

        var jump = new JumpTile();
        Fill(main, 9, new CycleGateTile { Period = 4, Index = 4 }, jump);

        Fill(main, 10, new NoteTile { Note = N("A#4") });
        Fill(main, 11, new ProbGateTile { Percent = 35 },
                       new NoteTile { Note = N("B4") },
                       new NoteTile { Note = N("D5") });
        Fill(main, 13, new NoteTile { Note = N("E5"), Length = 2 },
                       new RelativeParamTile { Target = ParamTargets.CarDecay, Amount = 0.5f });

        // Four steps against the main lane's sixteen, with no notes of its own:
        // the locks sit on the rail, so they reach whatever the channel is
        // sounding at that moment. Its CHAN is below the main one, so its runner
        // goes second and gets to overwrite.
        var accent = score.AddLane(1, 6, new ChannelTile { Channel = 1 }, 4);
        Fill(accent, 0, new RelativeParamTile { Target = ParamTargets.Level, Amount = 0.2f });
        Fill(accent, 2, new RelativeParamTile { Target = ParamTargets.Level, Amount = -0.35f });

        // The branch lane, entered only through that one jump. Ten main steps plus
        // six here comes to sixteen either way, so a lap is the same length
        // whether it jumps or not.
        var variation = score.AddLane(6, 8, new JumpDestTile(), 6);
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

    static int N(string name)
    {
        Pitch.TryParse(name, out var note);
        return note;
    }
}

} // namespace Jacquard
