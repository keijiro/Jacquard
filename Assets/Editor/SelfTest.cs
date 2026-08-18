using Jacquard.App;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Jacquard.Editor {

// A few checks that are quicker to run from a menu item than to reason about: the
// file format has to round-trip, the runners have to produce the notes the mockup
// score describes, and the oscillator and the two effect buses have to make the
// shapes their parameters promise.

static class SelfTest
{
    const float SampleRate = 48000.0f;

    [MenuItem("Jacquard/Run Self Test")]
    public static void Run()
    {
        var log = new System.Text.StringBuilder("Jacquard self test\n");

        RoundTrip(log);
        SampleScore(log);
        Plane(log);
        Playback(log);
        Holding(log);
        Lanes(log);
        Switching(log);
        Stack(log);
        CopyStack(log);
        Tuning(log);
        Locks(log);
        Channels(log);
        Mutes(log);
        Sends(log);
        Pan(log);
        Unison(log);
        Live(log);
        Synth(log);
        Delay(log);
        Reverb(log);
        Limiter(log);

        Debug.Log(log.ToString());
    }

    // Writing, reading and writing again has to give the same text, which is the
    // cheapest way to know that nothing about a tile is being dropped.
    static void RoundTrip(System.Text.StringBuilder log)
    {
        var original = Project.CreateSample();
        var first = ProjectFormat.Write(original);
        var second = ProjectFormat.Write(ProjectFormat.Read(first));

        log.Append(first == second ? "  round trip: identical\n"
                                   : "  ROUND TRIP MISMATCH\n");

        if (first != second)
        {
            log.Append("--- first ---\n").Append(first);
            log.Append("--- second ---\n").Append(second);
            return;
        }

        var reloaded = ProjectFormat.Read(first);

        log.Append("  lanes: ").Append(reloaded.Score.Lanes.Count).Append('\n');

        // The jump has to find its way back to the branch lane, since that pairing
        // is the one thing the file expresses as a coordinate.
        var branch = reloaded.Score.Lanes.Find(lane => lane.IsBranch);

        log.Append(branch?.JumpSource != null
          ? "  branch link: resolved\n" : "  BRANCH LINK LOST\n");
    }

    // The sample score is a file rather than code, so nothing about it is checked by
    // compiling. What can go stale is the version it was written at: the reader takes an
    // older file, but a sample left behind by a format bump loses whatever the bump
    // added, silently and in the one slot a fresh install is meant to be impressed by.
    // Reading it and writing it back at the current version says both things at once —
    // that it still parses, and whether it is already what this build would write.
    static void SampleScore(System.Text.StringBuilder log)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SceneBuilder.SampleScorePath);

        if (asset == null)
        {
            log.Append("  SAMPLE SCORE MISSING at ")
               .Append(SceneBuilder.SampleScorePath).Append('\n');
            return;
        }

        try
        {
            var project = ProjectFormat.Read(asset.text);
            var rewritten = ProjectFormat.Write(project);

            log.Append("  sample score: ").Append(project.Score.Lanes.Count)
               .Append(" lanes at ").Append(project.Tempo).Append("bpm\n");

            log.Append(rewritten == asset.text
              ? "  sample version: current\n"
              : "  sample version: readable but not what this build writes; "
                + "save it again from the app\n");
        }
        catch (System.Exception error)
        {
            log.Append("  SAMPLE SCORE UNREADABLE: ").Append(error.Message).Append('\n');
        }
    }

    // The score's extent on the plane, and its right to be moved about on one.
    //
    // A score is carried bodily so that the plane can keep free ground on its left and
    // above without a coordinate ever going negative — the view does the carrying, and
    // what is checked here is that the model comes through it unhurt. Everything a
    // translation must not disturb is something read relatively: the order the runners
    // are born in, the lane a piece's length is measured on, and the one pairing the
    // file states as a coordinate.
    static void Plane(System.Text.StringBuilder log)
    {
        // Where a score begins, against a lane put down on purpose: its head is one
        // column left of it, and a stack three deep on row 4 ends on row 6, so seven
        // rows are used. Height counting the row after that is what used to leave the
        // plane a row deeper below the score than above it.
        var measured = new Project().Score;
        var tall = measured.AddLane(1, 4, new ChannelTile { Channel = 1 }, 2);

        for (var i = 0; i < 3; i++) tall.Steps[1].Tiles.Add(new NoteTile { Note = 60 + i });

        Check(log, "the corner is the leftmost head and the topmost rail",
              measured.MinX == 0 && measured.MinY == 4,
              "corner " + measured.MinX + "," + measured.MinY);

        Check(log, "height reaches the deepest stack and no further",
              measured.Height == 7,
              "height " + measured.Height + " for a lane on row 4 three deep");

        // Now the sample score, which has everything a translation could disturb: three
        // channel lanes whose order is read off position, and a jump reaching a branch
        // lane, which is the one pairing the file states as a coordinate.
        var project = Project.CreateSample();
        var score = project.Score;

        var jump = (JumpTile)null;

        foreach (var lane in score.Lanes)
            foreach (var step in lane.Steps)
                jump ??= step.Find<JumpTile>();

        var master = score.MasterLane;
        var order = Rows(score);

        var lanes = score.Lanes.ToArray();
        var was = new GridPoint[lanes.Length];
        for (var i = 0; i < lanes.Length; i++) was[i] = new GridPoint(lanes[i].X, lanes[i].Y);

        var (width, height) = (score.Width, score.Height);

        score.Translate(9, 7);

        var together = true;
        for (var i = 0; i < lanes.Length; i++)
            together &= lanes[i].X == was[i].X + 9 && lanes[i].Y == was[i].Y + 7;

        Check(log, "every lane moved by the same amount", together,
              lanes.Length + " lanes now at " + Cells(score));

        Check(log, "the corner and the extent moved with them",
              score.MinX == 9 && score.MinY == 8 &&
              score.Width == width + 9 && score.Height == height + 7,
              "corner " + score.MinX + "," + score.MinY +
              ", extent " + score.Width + "x" + score.Height);

        // The order the runners are born in is read off position, so this is the check
        // that says moving a score does not change what it plays.
        Check(log, "the runners keep their order", Rows(score, 7) == order,
              "was " + order + ", now " + Rows(score));

        // And the lane a switch between two scores measures its lap line on, which is
        // that same order asked one more question.
        Check(log, "the master lane is still the same lane",
              master != null && score.MasterLane == master,
              master == null ? "no master lane" : "row " + score.MasterLane.Y);

        Check(log, "a jump still reaches its branch lane",
              jump != null && score.Locate(jump) != null &&
              score.DestinationOf(jump) != null,
              jump == null ? "no jump in the sample score"
                           : "jump at " + score.Locate(jump));

        // A moved score has to write a file that reads back as the same score, which for
        // the branch link means the coordinate written and the coordinate resolved being
        // worked out from the same positions.
        var reread = ProjectFormat.Read(ProjectFormat.Write(project));
        var branch = reread.Score.Lanes.Find(lane => lane.IsBranch);

        Check(log, "a moved score survives the file",
              branch?.JumpSource != null &&
              reread.Score.MinX == 9 && reread.Score.MinY == 8,
              branch?.JumpSource == null ? "branch link lost"
                : "corner " + reread.Score.MinX + "," + reread.Score.MinY);
    }

    // The rows the channel lanes come back in, which is the order their runners are
    // born in. Offset by what a translation moved them, so the two readings compare.
    static string Rows(Score score, int offset = 0)
    {
        var text = new System.Text.StringBuilder();
        foreach (var lane in score.ChannelLanes)
            text.Append(text.Length == 0 ? "" : ",").Append(lane.Y - offset);
        return text.ToString();
    }

    static string Cells(Score score)
    {
        var text = new System.Text.StringBuilder();
        foreach (var lane in score.Lanes)
            text.Append(text.Length == 0 ? "" : " ").Append(lane.X).Append(',').Append(lane.Y);
        return text.ToString();
    }

    // Runs the mockup score for four laps' worth of samples and counts what comes
    // out, which exercises gates, locks, the branch and the accent lane.
    static void Playback(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var project = Project.CreateSample();
        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);

        // Sixteen steps at 132bpm, four laps, plus a margin.
        var length = (long)(16 * 4 * 60.0 / project.Tempo / 4.0 * sampleRate);
        var window = sampleRate / 10;

        for (var position = 0L; position < length; position += window)
            sequencer.Schedule(position, window, sampleRate, notes);

        var jumped = false;
        foreach (var runner in sequencer.Runners)
            if (runner.Pass >= 4) jumped = true;

        log.Append("  notes over four laps: ").Append(notes.Count).Append('\n');
        log.Append("  runners: ").Append(sequencer.Runners.Count).Append('\n');
        log.Append(jumped ? "  laps counted\n" : "  LAPS NOT COUNTED\n");

        var plain = project.Patches[1].level;
        var loudest = 0.0f;
        var untouched = 0;

        foreach (var note in notes)
        {
            loudest = Mathf.Max(loudest, note.level);
            if (Mathf.Abs(note.level - plain) < 0.001f) untouched++;
        }

        // The accent lane's relative lock is the only thing that can push a note
        // past the patch level, and the accent lane sits above the main one, so
        // seeing it proves the pass runs down the plane.
        Check(log, "the accent reached the lane below it", loudest > plain + 0.01f,
              "loudest=" + loudest + " against a patch level of " + plain);

        // And the steps the accent lane does not land on have to come out at the
        // patch level, since a lock is over when the step it sits on is, and the accent
        // lane divides the bar the same way the main one does.
        Check(log, "a lock is gone by the next step", untouched > 0,
              untouched + " of " + notes.Count + " notes at the patch level");
    }

    // A lock lasts as long as the step it sits on, which says nothing at all while every
    // lane divides the bar the same way and says everything the moment one of them does
    // not: an eighth-note lock lane over a sixteenth-note lane of notes covers two of
    // them with one step, and both have to be lifted.
    //
    // Three things are asked of the one score. The lock reaches every note that falls
    // inside the step holding it; the empty cell after it lets the channel go, so the
    // notes under that one are plain again; and the same two lanes with the lock
    // underneath lift nothing at all, since a held lock is read at the place in the pass
    // its own lane occupies and a lock only ever colours what is processed after it.
    static void Holding(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var lifted = Emit(Holder(1, 3), sampleRate);

        Check(log, "a lock holds every note inside the step it sits on",
              Count(lifted, 72) == 2 && First(lifted, 72) == 0, Heard(lifted));

        Check(log, "the empty cell after a lock lets the channel go",
              Count(lifted, 60) == 2 && First(lifted, 60) > 0, Heard(lifted));

        var below = Emit(Holder(3, 1), sampleRate);

        Check(log, "a held lock reaches nothing above the lane holding it",
              Count(below, 72) == 0 && Count(below, 60) == 4, Heard(below));
    }

    // An eighth-note lane holding one lock over a sixteenth-note lane of four plain
    // notes, both on channel one and placed on the rows given: score5's shape, and the
    // same thing upside down.
    static Project Holder(int lockRow, int noteRow)
    {
        var project = new Project();
        var score = project.Score;

        var locks = score.AddLane(1, lockRow,
                                  new ChannelTile { Channel = 1, Division = 8 }, 2);

        var lift = new RelativeParamTile();
        lift.Engage(ParamTargets.Transpose, 12.0f);
        locks.Steps[0].Tiles.Add(lift);

        var main = score.AddLane(1, noteRow,
                                 new ChannelTile { Channel = 1, Division = 16 }, 4);

        for (var step = 0; step < 4; step++)
            main.Steps[step].Tiles.Add(new NoteTile { Note = 60 });

        return project;
    }

    // One lap of that score and not a sample more — a beat, which is four sixteenths —
    // so the lap that follows cannot add notes to what is being counted.
    static System.Collections.Generic.List<FmNoteEvent> Emit(Project project,
                                                             int sampleRate)
    {
        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();
        var lap = (long)(60.0 / project.Tempo * sampleRate);

        sequencer.Play(0, 0);
        sequencer.Schedule(0, lap, sampleRate, notes);

        return notes;
    }

    static string Heard(System.Collections.Generic.List<FmNoteEvent> notes)
      => Count(notes, 72) + " lifted and " + Count(notes, 60) + " plain, out of "
         + notes.Count;

    // A lane that is not running.
    //
    // Everything here is about the two moments the switch is read at rather than about
    // the switch itself: a lane stops at the end of the lane and never part way along
    // one, and it starts on the turn of the piece and at no other instant. Both are
    // measured in samples, since "it came back roughly in time" is exactly the failure
    // this feature would have.
    //
    // The scores are the switching checks' own — a four step master on channel one and a
    // two step lane on channel two — because a lane whose lap divides the master's is the
    // one that shares an instant with it, and sharing an instant is where a stop and a
    // start could tread on each other.
    static void Lanes(System.Text.StringBuilder log)
    {
        // Four steps at 120bpm: 24000 samples to the master's lap, 12000 to the other
        // lane's, so every figure below is exact.
        const long lap = SwitchStep * 4;

        // Switched off before there is anything to hear, which is the case a standing
        // start has to get right by itself: the lane is seated and never started.
        var quiet = SwitchScore(60, 4);
        Second(quiet, 2);
        Switch(quiet, 2, false);

        var quietNotes = SwitchRun(new Sequencer { Project = quiet }, lap * 2, at => {});

        Check(log, "a lane switched off before the start is never heard",
              Count(quietNotes, 84) == 0 && quietNotes.Count == 8,
              quietNotes.Count + " notes, " + Count(quietNotes, 84) + " from the lane");

        // The master is what hands out the moment a lane starts on, so it cannot be the
        // thing that stops. Its switch is taken and kept and ignored.
        var master = SwitchScore(60, 4);
        Switch(master, 1, false);

        var masterNotes = SwitchRun(new Sequencer { Project = master }, lap * 2,
                                    at => {});

        Check(log, "the master lane runs with its switch off",
              masterNotes.Count == 8,
              masterNotes.Count + " notes over two laps");

        // Switched off part way along its lap. It has to finish the lap — the note on its
        // second step is still due — and stop at the end of it rather than where the hand
        // landed, so the last thing heard is 85 and the lap that would start at 12000
        // never does.
        var cut = SwitchScore(60, 4);
        Second(cut, 2);

        var cutSeq = new Sequencer { Project = cut };
        var cutNotes = SwitchRun(cutSeq, lap * 2, at =>
        {
            if (at != SwitchStep) return;
            Switch(cut, 2, false);
            cutSeq.Resync();
        });

        Check(log, "a lane switched off plays out its lap and stops at the end of it",
              Count(cutNotes, 84) == 1 && Count(cutNotes, 85) == 1 &&
              Last(cutNotes, 85) == SwitchStep,
              Count(cutNotes, 84) + " of the first step, " + Count(cutNotes, 85) +
              " of the second, last at " + Last(cutNotes, 85));

        // A lane whose lap divides the master's stops on an instant they share, and the
        // master turning over on that same instant must not start it again. This is the
        // one ordering in the whole feature that could go wrong quietly: the stop and the
        // lap line are the same sample.
        var shared = SwitchScore(60, 4);
        Second(shared, 2);

        var sharedSeq = new Sequencer { Project = shared };
        var sharedNotes = SwitchRun(sharedSeq, lap * 3, at =>
        {
            if (at != SwitchStep * 3) return;
            Switch(shared, 2, false);
            sharedSeq.Resync();
        });

        Check(log, "a lane stopping on the master's own line is not started by it",
              Count(sharedNotes, 84) == 2 && Last(sharedNotes, 84) == SwitchStep * 2,
              Count(sharedNotes, 84) + " of the first step, last at " +
              Last(sharedNotes, 84));

        // Switched back on, which waits: nothing at the sample the hand moved, and the
        // first note exactly on the line the master draws. Half a lap of the other lane
        // goes by in between, and it is not heard.
        var back = SwitchScore(60, 4);
        Second(back, 2);
        Switch(back, 2, false);

        var backSeq = new Sequencer { Project = back };
        var backNotes = SwitchRun(backSeq, lap * 2, at =>
        {
            if (at != SwitchStep) return;
            Switch(back, 2, true);
            backSeq.Resync();
        });

        Check(log, "a lane switched on comes in on the turn of the piece",
              Count(backNotes, 84) == 2 && First(backNotes, 84) == lap,
              Count(backNotes, 84) + " of the first step, first at " +
              First(backNotes, 84));

        // A lane drawn while the sequence plays has nothing else to be in step with, so
        // it waits for the same line. This and the check above have to agree — a lane
        // coming back and a lane just written are the same arrival.
        var grown = SwitchScore(60, 4);
        var grownSeq = new Sequencer { Project = grown };

        var grownNotes = SwitchRun(grownSeq, lap * 2, at =>
        {
            if (at != SwitchStep) return;
            Second(grown, 2);
            grownSeq.Resync();
        });

        Check(log, "a lane drawn while playing waits for the turn of the piece",
              Count(grownNotes, 84) == 2 && First(grownNotes, 84) == lap,
              Count(grownNotes, 84) + " of the first step, first at " +
              First(grownNotes, 84));

        // The lap count starts again, which is what a cycle gate on the lane reads: a lane
        // that comes back has to fire on the lap it fires on from a standing start, rather
        // than resuming at whatever phase it was left at.
        //
        // Off at 18000 leaves it stopped at 24000 with a lap behind it; on at 30000 brings
        // it back on the master's line at 48000, and it runs two laps from there. So two
        // if the count was reset and three if it was carried, which is the only reason the
        // figures here are this far apart.
        var counted = SwitchScore(60, 4);
        Second(counted, 2);

        var countedSeq = new Sequencer { Project = counted };

        SwitchRun(countedSeq, lap * 3, at =>
        {
            if (at == SwitchStep * 3) { Switch(counted, 2, false); countedSeq.Resync(); }
            if (at == SwitchStep * 5) { Switch(counted, 2, true); countedSeq.Resync(); }
        });

        var laps = -1;
        foreach (var runner in countedSeq.Runners)
            if (runner.Channel == 2) laps = runner.Pass;

        Check(log, "a lane that comes back counts its laps from the start again",
              laps == 2, "channel 2 has run " + laps + " laps");

        // And the switch has to survive the file, which is what version 16 is for.
        var saved = SwitchScore(60, 4);
        Second(saved, 2);
        Switch(saved, 2, false);

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(saved)).Score;

        Check(log, "the switch round trips",
              reloaded.Lanes[0].Channel.Enabled && !reloaded.Lanes[1].Channel.Enabled,
              "ch1=" + reloaded.Lanes[0].Channel.Enabled +
              " ch2=" + reloaded.Lanes[1].Channel.Enabled);

        // A file from before the key existed reads as a score where every lane runs,
        // which is what every lane in such a file did.
        var legacy = ProjectFormat.Read(
          "jacquard 15\ntempo 120\nlane 1 1 CHAN:1 div=16\n  step C4\n").Score;

        Check(log, "a file without the key runs every lane",
              legacy.Lanes[0].Channel.Enabled,
              "ch1=" + legacy.Lanes[0].Channel.Enabled);
    }

    // A second lane on channel two, half the master's lap, with a note on every step so
    // that where it stopped can be read off the last one heard. Divide puts one on the
    // first step only, which cannot say whether the rest of a lap was played.
    static Lane Second(Project project, int steps)
    {
        var lane = project.Score.AddLane(1, 3, new ChannelTile { Channel = 2 }, steps);

        for (var i = 0; i < steps; i++)
            lane.Steps[i].Tiles.Add(new NoteTile { Note = 84 + i });

        return lane;
    }

    static void Switch(Project project, int channel, bool enabled)
    {
        foreach (var lane in project.Score.Lanes)
            if (lane.Channel?.Channel == channel) lane.Channel.Enabled = enabled;
    }

    static int Count(System.Collections.Generic.List<FmNoteEvent> notes, int pitch)
    {
        var count = 0;
        foreach (var note in notes) if (Sounds(note, pitch)) count++;
        return count;
    }

    static long First(System.Collections.Generic.List<FmNoteEvent> notes, int pitch)
    {
        foreach (var note in notes) if (Sounds(note, pitch)) return note.startSample;
        return -1;
    }

    static long Last(System.Collections.Generic.List<FmNoteEvent> notes, int pitch)
    {
        var last = -1L;
        foreach (var note in notes) if (Sounds(note, pitch)) last = note.startSample;
        return last;
    }

    // A score coming in at the turn of the piece.
    //
    // What has to hold is that the seam falls on the master lane's lap line, that the
    // two scores read as one across it — no gap, no overlap, no step missed and none
    // played twice — and that the line is the one the lap actually ended on rather than
    // the one the lane's step count would predict, since a jump can shorten a lap.
    static void Switching(System.Text.StringBuilder log)
    {
        // Four steps to the lap, which at 120bpm is 24000 samples exactly, so every
        // boundary below is a whole number and nothing here is read off a rounding.
        const long lap = SwitchStep * 4;

        // The seam itself, walked note by note. Asked for part way through the second
        // lap, so the score going out is played whole and the line at the end of that
        // lap is where the other one starts.
        var plain = new Sequencer { Project = SwitchScore(60, 4) };
        var second = SwitchScore(72, 4);

        var notes = SwitchRun(plain, lap * 4,
                              at => { if (at == lap + SwitchStep) plain.SwitchTo(second); });

        var seam = lap * 2;
        var wrong = -1;

        for (var i = 0; i < notes.Count; i++)
        {
            var due = SwitchStep * i;

            if (notes[i].startSample == due &&
                Sounds(notes[i], (due < seam ? 60 : 72) + i % 4)) continue;

            wrong = i;
            break;
        }

        Check(log, "two scores read as one across the seam",
              notes.Count == 16 && wrong < 0,
              notes.Count + " notes, " +
              (wrong < 0 ? "every one on its own step"
                         : "note " + wrong + " at " + notes[wrong].startSample));

        // A lane whose lap divides the master's lands on the line to the bit, and that
        // instant belongs to the score arriving. Letting it play there would be the
        // outgoing score sounding on top of the incoming one — and worse, would sweep
        // the master into the same slice and play the first step of the new lap twice.
        var pairing = SwitchScore(60, 4);
        var pairedWith = SwitchScore(72, 4);

        Divide(pairing, 84);
        Divide(pairedWith, 85);

        var paired = new Sequencer { Project = pairing };

        var pairs = SwitchRun(paired, lap * 3,
                              at => { if (at == SwitchStep) paired.SwitchTo(pairedWith); });

        var strays = 0;
        var arrivals = 0;

        foreach (var note in pairs)
        {
            if (Sounds(note, 84) && note.startSample >= lap) strays++;
            if (Sounds(note, 85) && note.startSample == lap) arrivals++;
        }

        Check(log, "a lane that divides the lap fires once on the line",
              strays == 0 && arrivals == 1,
              strays + " late from the score going out, " +
              arrivals + " on the line from the one coming in");

        // A lap is as long as it turns out to be. Asked for once the first lap has gone
        // by, so what it waits through is the lap that takes the jump: two steps and
        // then the branch's two, which is half of what the lane's own length predicts.
        var gated = new Sequencer { Project = GatedScore() };

        var jumped = SwitchRun(gated, SwitchStep * 20,
                               at => { if (at == SwitchStep * 8) gated.SwitchTo(SwitchScore(72, 4)); });

        var line = SwitchStep * 12;
        var landed = -1L;
        var overran = 0;

        foreach (var note in jumped)
        {
            if (FromTheScoreComingIn(note))
            {
                if (landed < 0) landed = note.startSample;
            }
            else if (note.startSample >= line)
            {
                overran++;
            }
        }

        Check(log, "the line is where the lap ended and not where it was due",
              landed == line && overran == 0,
              "in at " + landed + " against " + line + ", " +
              overran + " late from the score going out");

        // Nothing to wait through means nothing to wait for.
        var idle = new Sequencer { Project = SwitchScore(60, 4) };
        var waiting = SwitchScore(72, 4);
        var announced = 0;

        idle.Switched += _ => announced++;
        idle.SwitchTo(waiting);

        Check(log, "a score asked for while stopped comes in at once",
              idle.Project == waiting && !idle.IsSwitchPending && announced == 1,
              "pending=" + idle.IsSwitchPending + ", announced " + announced + " times");

        // And one asked for while playing and then stopped under arrives rather than
        // being lost: the file the hand asked for is the file that ends up open.
        var abandoned = SwitchScore(84, 4);

        idle.Play(0, 0);
        idle.SwitchTo(abandoned);

        var held = idle.IsSwitchPending;
        idle.Stop();

        Check(log, "stopping the transport lets a waiting score in",
              held && idle.Project == abandoned && !idle.IsSwitchPending,
              "waited=" + held + ", pending=" + idle.IsSwitchPending);

        // There is one next, so the last thing asked for is the thing that plays.
        var replaced = new Sequencer { Project = SwitchScore(60, 4) };
        var dropped = SwitchScore(72, 4);
        var kept = SwitchScore(84, 4);

        var swapped = SwitchRun(replaced, lap * 2, at =>
        {
            if (at == SwitchStep) replaced.SwitchTo(dropped);
            if (at == SwitchStep * 2) replaced.SwitchTo(kept);
        });

        var ghosts = 0;
        var right = 0;

        foreach (var note in swapped)
        {
            if (Sounds(note, 72)) ghosts++;
            if (Sounds(note, 84) && note.startSample == lap) right++;
        }

        Check(log, "the score asked for last is the one that comes in",
              ghosts == 0 && right == 1,
              ghosts + " notes from the score that was replaced");
    }

    // A sixteenth at 120bpm, which is what every score in the switching check is
    // written in and what its windows are a step of.
    const long SwitchStep = 6000;

    // One lane on channel one, a note a step, each score in its own register so that
    // where a note came from can be read off the note.
    static Project SwitchScore(int firstNote, int steps)
    {
        var project = new Project { Tempo = 120.0f };

        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, steps);

        for (var i = 0; i < steps; i++)
            lane.Steps[i].Tiles.Add(new NoteTile { Note = firstNote + i });

        return project;
    }

    // A second lane, half the master's lap long, sounding once a lap of its own — so it
    // lands on every line the master draws as well as halfway between them.
    static void Divide(Project project, int note)
    {
        var lane = project.Score.AddLane(1, 3, new ChannelTile { Channel = 2 }, 2);
        lane.Steps[0].Tiles.Add(new NoteTile { Note = note });
    }

    // Eight steps whose second lap is four: a cycle gate lets the jump through on the
    // lap after the first, and the branch behind it is shorter than what it skips.
    static Project GatedScore()
    {
        var project = new Project { Tempo = 120.0f };
        var score = project.Score;

        var lane = score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 8);

        for (var i = 0; i < 8; i++)
            lane.Steps[i].Tiles.Add(new NoteTile { Note = 60 + i });

        var jump = new JumpTile();

        lane.Steps[1].Tiles.Clear();
        lane.Steps[1].Tiles.Add(new CycleGateTile { Period = 2, Pattern = "01" });
        lane.Steps[1].Tiles.Add(jump);

        var branch = score.AddBranchLane(jump, new GridPoint(1, 3), 2);

        for (var i = 0; i < 2; i++)
            branch.Steps[i].Tiles.Add(new NoteTile { Note = 50 + i });

        return project;
    }

    // Runs a sequence in windows one step long, letting a hand reach it before each of
    // them. A window that carries exactly the step it opens on is what lets the checks
    // above say when a score was asked for in samples rather than in frames.
    static System.Collections.Generic.List<FmNoteEvent>
      SwitchRun(Sequencer sequencer, long span, System.Action<long> hand)
    {
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);

        for (var position = 0L; position < span; position += SwitchStep)
        {
            hand(position);
            sequencer.Schedule(position, SwitchStep, 48000, notes);
        }

        return notes;
    }

    static bool Sounds(in FmNoteEvent note, int pitch)
      => Mathf.Abs(note.frequency - Pitch.ToFrequency(pitch)) < 0.01f;

    // Every score written to come in above here, and every score written to go out
    // below it, so a note says which of the two it belongs to.
    static bool FromTheScoreComingIn(in FmNoteEvent note)
      => note.frequency > Pitch.ToFrequency(70);

    // Copying a stack is the one edit whose result is invisible until it is put
    // down again, so what it took is checked here rather than by eye: the tiles that
    // cannot travel have to be stepped over rather than to end the walk, and what
    // comes back has to be free of the tiles it was taken from. The paste is checked
    // for the mistake that costs a score — a run that will not fit has to be refused
    // whole rather than half written.
    static void CopyStack(System.Text.StringBuilder log)
    {
        var score = new Score();
        var lane = score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 4);

        var held = new AbsoluteParamTile();
        held.Engage(0, 0.25f);

        var tiles = lane.Steps[0].Tiles;
        tiles.Add(new NoteTile { Note = 64 });
        tiles.Add(held);
        tiles.Add(new JumpTile());
        tiles.Add(new NoteTile { Note = 60, Length = 2.0f });

        var cells = new System.Collections.Generic.List<GridPoint>();
        var copied = score.CopyStack(lane.CellPoint(0, 1), cells);

        Check(log, "a copy leaves behind what cannot travel",
              copied != null && copied.Count == 2 &&
              copied[0] is AbsoluteParamTile && copied[1] is NoteTile,
              copied == null ? "nothing was copied"
                             : Tokens(copied) + " from a stack of " + tiles.Count);

        // Everything below reads what came back, so a copy that came back wrong is
        // reported once rather than throwing over the checks that follow it.
        if (copied == null || copied.Count != 2) return;

        Check(log, "the lit cells are the cells that travelled",
              cells.Count == 2 && cells[0] == lane.CellPoint(0, 1) &&
              cells[1] == lane.CellPoint(0, 3),
              cells.Count + " cells for " + (copied?.Count ?? 0) + " tiles");

        Check(log, "what a lock holds comes with it",
              copied?[0] is AbsoluteParamTile p && p.IsEngaged(0) &&
              Mathf.Abs(p[0] - 0.25f) < 1e-4f,
              copied?[0].Token ?? "nothing");

        // The tiles that were copied from, edited afterwards. A copy that is really
        // the same object would follow them.
        held.Engage(0, 0.9f);
        ((NoteTile)tiles[3]).Note = 72;

        Check(log, "a copy is not the tile it came from",
              copied[0] is AbsoluteParamTile kept &&
              Mathf.Abs(kept[0] - 0.25f) < 1e-4f &&
              copied[1] is NoteTile note && note.Note == 60,
              Tokens(copied) + " after the originals moved");

        Check(log, "a tile that cannot travel copies nothing at all",
              score.CopyStack(lane.CellPoint(0, 2)) == null &&
              score.CopyStack(lane.HeadPoint) == null &&
              score.CopyStack(lane.CellPoint(1, 0)) == null,
              "a jump, a head and an empty step");

        // Onto the empty step next door.
        var pasted = score.PlaceStack(lane.CellPoint(1, 0), Copies(copied));

        Check(log, "a stack pastes onto an empty step in the order it was taken",
              pasted && Tokens(lane.Steps[1].Tiles) == Tokens(copied),
              Tokens(lane.Steps[1].Tiles));

        // Onto the terminator, which is a step the lane does not have yet.
        var grown = score.PlaceStack(lane.TermPoint, Copies(copied));

        Check(log, "a paste onto the terminator grows the lane",
              grown && lane.Steps.Count == 5 &&
              Tokens(lane.Steps[4].Tiles) == Tokens(copied),
              lane.Steps.Count + " steps, the last one " + Tokens(lane.Steps[4].Tiles));

        Check(log, "a paste onto an occupied cell is refused",
              !score.PlaceStack(lane.CellPoint(0, 0), Copies(copied)) &&
              lane.Steps[0].Tiles.Count == 4,
              lane.Steps[0].Tiles.Count + " tiles left in the stack it was aimed at");

        // A lane immediately below leaves the empty step room for one tile and no
        // more, which is what a two tile paste has to notice before it writes one.
        score.AddLane(1, 2, new ChannelTile { Channel = 2 }, 4);

        Check(log, "a paste with nowhere to go writes nothing",
              !score.PlaceStack(lane.CellPoint(2, 0), Copies(copied)) &&
              lane.Steps[2].IsEmpty,
              lane.Steps[2].Depth + " tiles under a lane one row down");
    }

    static System.Collections.Generic.List<Tile>
      Copies(System.Collections.Generic.List<Tile> tiles)
    {
        var copies = new System.Collections.Generic.List<Tile>();
        foreach (var tile in tiles) copies.Add(tile.Copy());
        return copies;
    }

    static string Tokens(System.Collections.Generic.IEnumerable<Tile> tiles)
      => string.Join(" ", System.Linq.Enumerable.Select(tiles, tile => tile.Token));

    // A stack is read downwards, so a gate reaches what is below it and nothing
    // above it. This is the one case where getting the direction wrong is
    // inaudible in the sample score but obvious in a score somebody writes: with a
    // note on the rail, a gate under it and a note under that, the first note has
    // to sound every lap and the second one lap in four.
    static void Stack(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;
        const int laps = 8;

        var project = new Project();
        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 4);

        lane.Steps[0].Tiles.Add(new NoteTile { Note = 64 });
        lane.Steps[0].Tiles.Add(new CycleGateTile { Period = 4, Pattern = "1000" });
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });

        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);

        // One extra step of margin would schedule the note that opens lap nine, so
        // the window stops just short of the last step of the last lap.
        var step = 60.0 / project.Tempo * 4.0 / 16.0 * sampleRate;
        var length = (long)(step * 4 * laps - step);
        var window = sampleRate / 20;

        for (var position = 0L; position < length; position += window)
            sequencer.Schedule(position, window, sampleRate, notes);

        var above = 0;
        var below = 0;

        foreach (var note in notes)
        {
            // Told apart by pitch, since the gate is all that separates them.
            if (Mathf.Abs(note.frequency - 329.628f) < 1.0f) above++;
            if (Mathf.Abs(note.frequency - 261.626f) < 1.0f) below++;
        }

        Check(log, "a gate leaves the note above it alone", above == laps,
              above + " of " + laps + " laps sounded the note on the rail");

        Check(log, "a gate governs the note below it", below == laps / 4,
              below + " notes under a four lap gate over " + laps + " laps");
    }

    // What a written note sounds as, which is two settings in one order: the channel's
    // transpose moves it and then the scale decides whether it will have it there. Both
    // ends of that are worth checking and so is the order — a scale applied first and
    // transposed afterwards would let every note straight back out of the key, and
    // nothing about a single note at a single setting would show it.
    //
    // The live effects are checked here too, from the other side: they have to reach
    // pitches the scale forbids, because they stand after the note was made and a
    // gesture is not a key signature.
    static void Tuning(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        // One degree allowed says the most about the walk: from a note in the middle of
        // the eleven that are not, below, above and the exact tie are all reachable.
        var single = new Scale();
        for (var degree = 1; degree < Scale.Degrees; degree++)
            single.SetAllowed(degree, false);

        Check(log, "a note outside the scale falls to the nearest one in it",
              single.Snap(61) == 60 && single.Snap(67) == 72 && single.Snap(72) == 72,
              single.Snap(61) + " " + single.Snap(67) + " " + single.Snap(72));

        Check(log, "a note the same distance from two of them falls to the lower",
              single.Snap(66) == 60, single.Snap(66).ToString());

        var none = new Scale();
        for (var degree = 0; degree < Scale.Degrees; degree++)
            none.SetAllowed(degree, false);

        Check(log, "a scale with nothing on lets every note through",
              none.Snap(61) == 61 && none.Snap(66) == 66,
              none.Snap(61) + " " + none.Snap(66));

        Check(log, "a fresh scale is no scale at all",
              new Scale().Snap(61) == 61, new Scale().Snap(61).ToString());

        // The order, through the sequencer. C major, and a C written on the plane: at
        // two semitones it comes out as the D that is in the key, and at one it is
        // moved onto a C# the key does not have and falls back to where it started.
        // Snapping first would have given 61 instead, since a C is already in the key
        // and nothing would have been there to catch what the transpose then did.
        Check(log, "the transpose moves the note and the scale then catches it",
              Written(60, 2.0f, sampleRate) == 62 && Written(60, 1.0f, sampleRate) == 60,
              Written(60, 2.0f, sampleRate) + " and " + Written(60, 1.0f, sampleRate));

        Check(log, "a channel with no transpose and no scale is where it was written",
              Written(60, 0.0f, sampleRate) == 60,
              Written(60, 0.0f, sampleRate).ToString());

        // A lock on the transpose, which is what the target is in the list for: the
        // sequencer reads the working patch, so it reaches the notes under it in that
        // step and no others.
        var project = new Project();
        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 2);

        var lift = new RelativeParamTile();
        lift.Engage(ParamTargets.Transpose, 12.0f);

        lane.Steps[0].Tiles.Add(lift);
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });
        lane.Steps[1].Tiles.Add(new NoteTile { Note = 60 });

        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 5, sampleRate, notes);

        Check(log, "a lock on the transpose lifts its own step and no other",
              notes.Count == 2 && Sounds(notes[0], 72) && Sounds(notes[1], 60),
              notes.Count + " notes");

        // The live effects, against a scale that allows one note in twelve. Every step
        // of the chromatic run is snapped onto a C, and the rise then walks straight
        // off it a semitone at a time.
        var chromatic = new Scale();
        for (var degree = 1; degree < Scale.Degrees; degree++)
            chromatic.SetAllowed(degree, false);

        var span = LiveSixteenth * 8;
        var plain = LiveRun(null, span, sampleRate, scale: chromatic);

        Check(log, "the scale reaches what the sequencer hands over",
              LiveNoteAt(plain, 0) == 60 && LiveNoteAt(plain, 1) == 60 &&
              LiveNoteAt(plain, 7) == 72,
              LiveNoteAt(plain, 0) + " " + LiveNoteAt(plain, 1) + " " +
              LiveNoteAt(plain, 7));

        var rise = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Rise, LiveLookahead); },
          span, sampleRate, scale: chromatic);

        Check(log, "a live effect reaches the notes the scale will not have",
              LiveNoteAt(rise, 0) == 60 && LiveNoteAt(rise, 1) == 61 &&
              LiveNoteAt(rise, 2) == 62,
              LiveNoteAt(rise, 0) + " " + LiveNoteAt(rise, 1) + " " +
              LiveNoteAt(rise, 2));

        // The file. Both are written in full, so what comes back is compared against
        // what went out rather than against a default that happens to match.
        var saved = new Project();
        saved.Scale.SetAllowed(1, false);
        saved.Scale.SetAllowed(6, false);
        saved.Patches[2].transpose = -5.0f;

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(saved));

        Check(log, "the scale and the transpose round trip",
              !reloaded.Scale.Allows(1) && !reloaded.Scale.Allows(6) &&
              reloaded.Scale.Allows(0) && reloaded.Scale.Allows(11) &&
              Mathf.Abs(reloaded.Patches[2].transpose + 5.0f) < 0.001f,
              "transpose " + reloaded.Patches[2].transpose);

        // An older file has neither, and has to come back as the piece it was: every
        // note allowed, and no channel moved.
        var older = ProjectFormat.Read("jacquard 13\ntempo 120\n");
        var chromaticBack = true;
        for (var degree = 0; degree < Scale.Degrees; degree++)
            chromaticBack &= older.Scale.Allows(degree);

        Check(log, "a file from before either one is unmoved",
              chromaticBack && Mathf.Abs(older.Patches[1].transpose) < 0.001f,
              "every degree on: " + chromaticBack);
    }

    // A note written on a one step lane, sounded through a C major scale at the given
    // transpose, and read back as the note number it came out as.
    static int Written(int note, float transpose, int sampleRate)
    {
        var project = new Project();

        foreach (var degree in new[] { 1, 3, 6, 8, 10 })
            project.Scale.SetAllowed(degree, false);

        project.Patches[1].transpose = transpose;

        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 1);
        lane.Steps[0].Tiles.Add(new NoteTile { Note = note });

        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 10, sampleRate, notes);

        return notes.Count > 0 ? Semitones(notes[0]) : -1;
    }

    // One lock holding two parameters has to move both of them and leave the rest of
    // the patch alone, and it has to survive a file. The easy mistakes here are
    // symmetrical: applying only the first parameter a lock names, and writing only
    // the first one out.
    static void Locks(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var project = new Project();
        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 1);

        var held = new AbsoluteParamTile();
        held.Engage(ParamTargets.Level, 0.5f);
        held.Engage(ParamTargets.ModIndex, 9.0f);

        // A lock holding nothing, which is what one looks like the moment it is
        // placed. It has to be inert rather than an error, and it has to still be
        // there after a round trip.
        lane.Steps[0].Tiles.Add(new RelativeParamTile());
        lane.Steps[0].Tiles.Add(held);
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });

        // Moved off its default so that a lock writing every target rather than the
        // ones it holds would show up as a zero here instead of matching by luck.
        project.Patches[1].feedback = 4.0f;

        var patch = project.Patches[1];
        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 10, sampleRate, notes);

        var sounded = notes.Count > 0;

        Check(log, "a lock holding two parameters moved both",
              sounded && Mathf.Abs(notes[0].level - 0.5f) < 0.001f &&
                         Mathf.Abs(notes[0].modulationIndex - 9.0f) < 0.001f,
              sounded ? "level=" + notes[0].level + " index=" + notes[0].modulationIndex
                      : "nothing sounded");

        // The one that proves the loop is not simply writing the whole patch.
        Check(log, "a lock left the parameters it does not hold alone",
              sounded && Mathf.Abs(notes[0].feedback - patch.feedback) < 0.001f,
              sounded ? "feedback=" + notes[0].feedback + " against a patch " +
                        patch.feedback : "nothing sounded");

        var text = ProjectFormat.Write(project);
        var reloaded = ProjectFormat.Read(text).Score.Lanes[0].Steps[0].Tiles;

        var back = reloaded.Find(tile => tile is AbsoluteParamTile) as ParamTile;
        var empty = reloaded.Find(tile => tile is RelativeParamTile) as ParamTile;

        // The step line alone, since that is where a lock is written and the eight
        // patch lines above it would bury the one thing being checked.
        var written = text.Substring(text.LastIndexOf("  step")).TrimEnd();

        Check(log, "both parameters came back from the file",
              back != null && back.IsEngaged(ParamTargets.Level) &&
              back.IsEngaged(ParamTargets.ModIndex) &&
              !back.IsEngaged(ParamTargets.Feedback) &&
              Mathf.Abs(back[ParamTargets.ModIndex] - 9.0f) < 0.001f,
              written);

        Check(log, "a lock holding nothing survived the file",
              empty != null && empty.IsEmpty, written);

        // A file naming a target the synth has since dropped has to open, losing the
        // lock rather than taking the score down with it. This is the shape of one of
        // the saved scores in Application.persistentDataPath: a version 1 step whose
        // second lock is on the carrier decay, which version 2 deleted.
        var legacy = "jacquard 1\ntempo 120\npatch level=0.5\n" +
                     "lane 1 1 CHAN:1 div=16\n" +
                     "  step PREL:feedback,6.2 PREL:cardecay,-1.989 C5/0.25\n";

        var opened = true;
        var tiles = (System.Collections.Generic.List<Tile>)null;

        try { tiles = ProjectFormat.Read(legacy).Score.Lanes[0].Steps[0].Tiles; }
        catch (System.Exception e) { (opened, written) = (false, e.Message); }

        // Two tiles, not three: the lock that named only a retired target had nothing
        // left to do and went, and the live one beside it came through untouched.
        var survivor = opened ? tiles.Find(tile => tile is ParamTile) as ParamTile : null;

        Check(log, "a lock on a retired target is dropped, not refused",
              opened && tiles.Count == 2 && survivor != null &&
              survivor.IsEngaged(ParamTargets.Feedback) &&
              Mathf.Abs(survivor[ParamTargets.Feedback] - 6.2f) < 0.001f,
              opened ? tiles.Count + " tiles, feedback=" +
                       (survivor == null ? "none"
                        : survivor[ParamTargets.Feedback].ToString())
                     : written);

        // A target that changed units rather than leaving, which is the other way a
        // file can hold a number the synth would now read as something else. Version
        // 10 turned the FM decay from a time into a slope, so a version 9 patch line
        // and the absolute lock over it both have to arrive converted — 120ms and
        // 300ms as the slopes that decay at those rates — while the relative lock
        // beside them keeps the shift it was written with.
        var older = ProjectFormat.Read(
          "jacquard 9\ntempo 120\npatch 1 md=0.12\n" +
          "lane 1 1 CHAN:1 div=16\n" +
          "  step PABS:moddecay,0.3 PREL:moddecay,0.25 C5\n");

        var steps = older.Score.Lanes[0].Steps[0].Tiles;
        var absolute = steps.Find(tile => tile is AbsoluteParamTile) as ParamTile;
        var relative = steps.Find(tile => tile is RelativeParamTile) as ParamTile;

        Check(log, "a version 9 FM decay comes back as the same slope",
              Mathf.Abs(older.Patches[1].modulatorDecay - 0.19355f) < 0.001f &&
              absolute != null &&
              Mathf.Abs(absolute[ParamTargets.ModDecay] - 0.375f) < 0.001f &&
              relative != null &&
              Mathf.Abs(relative[ParamTargets.ModDecay] - 0.25f) < 0.001f,
              "patch md=" + older.Patches[1].modulatorDecay +
              " absolute=" + (absolute == null ? "none"
                              : absolute[ParamTargets.ModDecay].ToString()) +
              " relative=" + (relative == null ? "none"
                              : relative[ParamTargets.ModDecay].ToString()));
    }

    // Two lanes on different channels, each with its own patch, playing the same
    // note: what comes out has to differ, and differ per channel rather than per
    // lane. A patch bank is easy to wire up so that every note takes channel one's
    // sound, and nothing else here would notice.
    static void Channels(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var project = new Project();
        var score = project.Score;

        score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 1);
        score.AddLane(1, 3, new ChannelTile { Channel = 2 }, 1);

        foreach (var lane in score.Lanes)
            lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });

        // One value per channel, far enough apart to tell which note came from
        // where. Level is enough: it reaches the event untouched.
        project.Patches[1].level = 0.25f;
        project.Patches[2].level = 0.75f;

        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 10, sampleRate, notes);

        var quiet = 0;
        var loud = 0;

        foreach (var note in notes)
        {
            if (Mathf.Abs(note.level - 0.25f) < 0.001f) quiet++;
            if (Mathf.Abs(note.level - 0.75f) < 0.001f) loud++;
        }

        Check(log, "each channel plays its own patch",
              notes.Count == 2 && quiet == 1 && loud == 1,
              notes.Count + " notes, " + quiet + " at ch1, " + loud + " at ch2");

        // And the bank has to survive the file, which is what the version 3 patch
        // line is for.
        var reloaded = ProjectFormat.Read(ProjectFormat.Write(project));

        Check(log, "the bank round trips",
              Mathf.Abs(reloaded.Patches[1].level - 0.25f) < 0.001f &&
              Mathf.Abs(reloaded.Patches[2].level - 0.75f) < 0.001f,
              "ch1=" + reloaded.Patches[1].level + " ch2=" + reloaded.Patches[2].level);

        // A version 2 file had one patch for everything, and that is what its single
        // line still means.
        var legacy = ProjectFormat.Read("jacquard 2\ntempo 120\npatch level=0.5\n");

        Check(log, "a version 2 patch line fills the bank",
              Mathf.Abs(legacy.Patches[1].level - 0.5f) < 0.001f &&
              Mathf.Abs(legacy.Patches[PatchBank.Channels].level - 0.5f) < 0.001f,
              "ch1=" + legacy.Patches[1].level +
              " ch" + PatchBank.Channels + "=" +
              legacy.Patches[PatchBank.Channels].level);
    }

    // A mute drops notes and nothing else. The promise is that the run is unchanged —
    // the laps go on counting, so a channel let back in is heard from where the
    // sequence has got to — and that a solo overrules a mute rather than clearing it.
    static void Mutes(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var project = new Project();
        var mutes = project.Mutes;
        var score = project.Score;

        // Two lanes of one step, told apart by their patch level the way the channel
        // check tells its two apart.
        score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 1);
        score.AddLane(1, 3, new ChannelTile { Channel = 2 }, 1);

        foreach (var lane in score.Lanes)
            lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });

        project.Patches[1].level = 0.25f;
        project.Patches[2].level = 0.75f;

        var sequencer = new Sequencer { Project = project };

        mutes.SetMuted(1, true);

        var muted = Play(sequencer, sampleRate);

        Check(log, "a muted channel is silent and the other one is not",
              muted.Count == 1 && Mathf.Abs(muted[0].level - 0.75f) < 0.001f,
              muted.Count + " notes");

        // The lap the muted lane was on has to have turned over anyway, which is what
        // says the runner was never stopped.
        var laps = 0;
        foreach (var runner in sequencer.Runners)
            if (runner.Channel == 1) laps = runner.Pass;

        Check(log, "a muted channel goes on running", laps > 0,
              "channel 1 is on lap " + (laps + 1));

        // Soloing the channel that is muted is the case that says which of the two
        // wins: it sounds, and the other one does not, without the mute being touched.
        mutes.SetSoloed(1, true);

        var soloed = Play(sequencer, sampleRate);

        Check(log, "a solo overrules a mute rather than clearing it",
              soloed.Count == 1 && Mathf.Abs(soloed[0].level - 0.25f) < 0.001f &&
              mutes.IsMuted(1),
              soloed.Count + " notes, and the mute is " +
              (mutes.IsMuted(1) ? "kept" : "GONE"));

        // And dropping the last solo gives back the mix that was underneath it.
        mutes.SetSoloed(1, false);

        var restored = Play(sequencer, sampleRate);

        Check(log, "dropping the last solo gives the mutes back",
              restored.Count == 1 && Mathf.Abs(restored[0].level - 0.75f) < 0.001f,
              restored.Count + " notes");

        // And both sets have to survive the file, which is what version 12 is for. The
        // interesting half is the mute: with channel 2 soloed it is not being consulted
        // at all, so a file that wrote only the audible answer would come back having
        // quietly cleared it.
        mutes.SetSoloed(2, true);

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(project)).Mutes;

        Check(log, "the mutes round trip",
              reloaded.IsMuted(1) && !reloaded.IsMuted(2) &&
              reloaded.IsSoloed(2) && !reloaded.IsSoloed(1),
              "muted 1=" + reloaded.IsMuted(1) + " 2=" + reloaded.IsMuted(2) +
              ", soloed 1=" + reloaded.IsSoloed(1) + " 2=" + reloaded.IsSoloed(2));

        // A file from before the line existed reads as nothing held back, which is what
        // a load used to leave behind whatever the file said.
        var legacy = ProjectFormat.Read("jacquard 11\ntempo 120\n").Mutes;

        Check(log, "a file without a mutes line holds nothing back",
              !legacy.AnySoloed && !legacy.IsMuted(1),
              "soloing=" + legacy.AnySoloed + " ch1 muted=" + legacy.IsMuted(1));
    }

    // One lap of a sequence, from a standing start.
    static System.Collections.Generic.List<FmNoteEvent> Play(Sequencer sequencer,
                                                             int sampleRate)
    {
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 10, sampleRate, notes);

        return notes;
    }

    // A send amount is a field of the patch, so a lock reaches it the way it reaches
    // a timbre and the descent decides which notes it colours. Which is the whole
    // argument for putting the sends there rather than beside the effects, and it is
    // worth a check of its own because it is the one thing that would silently still
    // work if a send were made global by mistake — every note would simply be wet.
    static void Sends(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        var project = new Project();
        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 1);

        var wet = new AbsoluteParamTile();
        wet.Engage(ParamTargets.ReverbSend, 0.8f);

        // One note above the lock and two below it, which is the split a chord with a
        // lock partway down it makes.
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 72 });
        lane.Steps[0].Tiles.Add(wet);
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 60 });
        lane.Steps[0].Tiles.Add(new NoteTile { Note = 64 });

        var sequencer = new Sequencer { Project = project };
        var notes = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, 0);
        sequencer.Schedule(0, sampleRate / 10, sampleRate, notes);

        var dry = 0;
        var sent = 0;

        foreach (var note in notes)
        {
            if (note.reverbSend < 0.001f) dry++;
            if (Mathf.Abs(note.reverbSend - 0.8f) < 0.001f) sent++;
        }

        Check(log, "a send lock reaches the notes below it",
              notes.Count == 3 && dry == 1 && sent == 2,
              notes.Count + " notes, " + dry + " dry and " + sent + " sent");

        // The effects themselves are the project's, so they travel with it rather than
        // with a patch. Two of the seven and one send are enough to know the line is
        // being written and read: what would break is the whole line, not one key.
        project.Fx.reverbSize = 0.9f;
        project.Fx.delayBeats = DelayTime.Beats[DelayTime.Beats.Length - 1];
        project.Fx.delaySpread = 0.6f;
        project.Patches[1].delaySend = 0.4f;

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(project));

        Check(log, "the effects round trip",
              Mathf.Abs(reloaded.Fx.reverbSize - 0.9f) < 0.001f &&
              Mathf.Abs(reloaded.Fx.delayBeats - project.Fx.delayBeats) < 0.001f &&
              Mathf.Abs(reloaded.Fx.delaySpread - 0.6f) < 0.001f &&
              Mathf.Abs(reloaded.Patches[1].delaySend - 0.4f) < 0.001f,
              "size=" + reloaded.Fx.reverbSize + " beats=" + reloaded.Fx.delayBeats +
              " spread=" + reloaded.Fx.delaySpread +
              " dsend=" + reloaded.Patches[1].delaySend);

        // A version 6 file has no fx line and no sends, and has to come back as a
        // project that simply never touched either.
        var legacy = ProjectFormat.Read("jacquard 6\ntempo 120\npatch 1 level=0.5\n");

        Check(log, "a version 6 file reads with the effects at their defaults",
              Mathf.Abs(legacy.Fx.delayBeats -
                        DelayTime.Beats[DelayTime.Default]) < 0.001f &&
              legacy.Patches[1].reverbSend == 0.0f &&
              Mathf.Abs(legacy.Patches[1].level - 0.5f) < 0.001f,
              "beats=" + legacy.Fx.delayBeats +
              " rsend=" + legacy.Patches[1].reverbSend +
              " level=" + legacy.Patches[1].level);
    }

    // The pan law, which is two claims and both of them are audible if they are wrong.
    // A centred note has to come out exactly as it did before there was a pan at all,
    // or every project written until now quietly changes level; and the pair of gains
    // has to hold its power as it crosses, or a note thrown to the side sags in the
    // middle of the journey. Neither shows up anywhere else — the voice pool applies
    // these gains inside a Burst job, where nothing can be measured.
    static void Pan(System.Text.StringBuilder log)
    {
        var note = new FmNoteEvent();

        note.pan = 0.0f;
        note.PanGains(out var centreL, out var centreR);

        Check(log, "a centred note renders as it did unpanned",
              Mathf.Abs(centreL - 1.0f) < 0.001f && Mathf.Abs(centreR - 1.0f) < 0.001f,
              "L=" + centreL + " R=" + centreR);

        note.pan = -1.0f;
        note.PanGains(out var leftL, out var leftR);

        note.pan = 1.0f;
        note.PanGains(out var rightL, out var rightR);

        Check(log, "hard over is on one side only",
              leftR < 0.001f && rightL < 0.001f &&
              Mathf.Abs(leftL - rightR) < 0.001f,
              "hard left=" + leftL + "/" + leftR +
              " hard right=" + rightL + "/" + rightR);

        // Equal power: the two gains are a point on a circle, so the sum of their
        // squares is the same wherever the note sits. A pair of straight fades passes
        // the two checks above and fails this one by 3dB in the middle, which is the
        // dip the law exists to avoid.
        var flattest = 0.0f;

        for (var i = 0; i <= 20; i++)
        {
            note.pan = i / 10.0f - 1.0f;
            note.PanGains(out var l, out var r);
            flattest = Mathf.Max(flattest, Mathf.Abs(l * l + r * r - 2.0f));
        }

        Check(log, "the power holds all the way across", flattest < 0.01f,
              "largest departure " + flattest + " from a constant 2");

        // And it has to travel with the patch, since it is a field of one like any
        // other. A lock on it is what the shared ParamTargets machinery covers.
        var project = new Project();
        project.Patches[1].pan = -0.75f;

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(project));

        // A version 7 file has no pan at all and has to come back centred, which is
        // where every note in one already was.
        var legacy = ProjectFormat.Read("jacquard 7\ntempo 120\npatch 1 level=0.5\n");

        Check(log, "a pan round trips and an older file comes back centred",
              Mathf.Abs(reloaded.Patches[1].pan + 0.75f) < 0.001f &&
              legacy.Patches[1].pan == 0.0f,
              "pan=" + reloaded.Patches[1].pan +
              " version 7 pan=" + legacy.Patches[1].pan);
    }

    // Unison, which is the pan law asked a second question: a note is a pair, tuned
    // apart and thrown apart. Four things can go wrong with that and every one of them
    // is silent in the code and obvious in the room. A pair that sat beside the note
    // instead of straddling it would take a chord flat as the setting was turned up. A
    // detune in hertz rather than in cents would be a different interval on every part
    // it was used on. A pair that did not hold its level would make this a volume
    // control with a chorus attached. And a note with no unison has to be the note
    // there was, or every project written until now quietly changes.
    static void Unison(System.Text.StringBuilder log)
    {
        // A plain sine — no modulation, no sweep — so that the zero crossing estimate
        // can read each half's pitch, held for longer than the measurement window.
        var note = new FmNoteEvent
          { frequency = 440.0f, level = 1.0f, duration = 2.0f,
            modulatorRatio = 2.0f, modulationIndex = 0.0f, modulatorDecay = 1.0f,
            carrierAttack = 0.005f, carrierRelease = 0.01f };

        // Nothing beside the note when it is off, which is the debt the pan paid too.
        // The ratio has to be exactly one and not nearly one, since it is what the
        // increment is divided by: anything else and every existing project is
        // retuned by a fraction of a cent.
        RenderPair(note, 0.2f, out var alone, out var silent);

        var leaked = 0.0f;
        foreach (var sample in silent) leaked = Mathf.Max(leaked, Mathf.Abs(sample));

        note.UnisonGains(out var singleL, out var singleR, out var offL, out var offR);

        Check(log, "a note without unison is one voice and nothing beside it",
              note.DetuneRatio == 1.0f && note.UnisonGain == 1.0f &&
              leaked == 0.0f && offL == 0.0f && offR == 0.0f &&
              Mathf.Abs(singleL - 1.0f) < 0.001f && Rms(alone, 0, alone.Length) > 0.1f,
              "ratio=" + note.DetuneRatio + " gain=" + note.UnisonGain +
              " second partial peaks at " + leaked);

        // Where the pair sits. Read either side rather than as a beat, because what
        // has to hold is that the written note is in the middle of the two.
        note.unison = 1.0f;
        RenderPair(note, 0.5f, out var low, out var high);

        var (from, to) = (Seconds(0.05f), Seconds(0.45f));

        var below = Frequency(low, from, to);
        var above = Frequency(high, from, to);

        var centre = Mathf.Sqrt(below * above);
        var cents = 1200.0f * Mathf.Log(above / below, 2.0f);

        Check(log, "the pair straddles the note that was written",
              Mathf.Abs(centre - 440.0f) < 1.0f,
              "centred on " + centre + "Hz, from " + below + " to " + above);

        Check(log, "the far end is the interval the synth promises",
              Mathf.Abs(cents - FmNoteEvent.MaxDetuneCents) < 1.0f,
              cents + " cents apart");

        // And an interval rather than a distance: two octaves down, the same setting
        // has to come out at the same number of cents, which is a quarter of the beat.
        var deep = note;
        deep.frequency = 110.0f;

        RenderPair(deep, 0.5f, out var deepLow, out var deepHigh);

        var deepCents = 1200.0f * Mathf.Log(Frequency(deepHigh, from, to) /
                                            Frequency(deepLow, from, to), 2.0f);

        Check(log, "the same setting is the same interval two octaves down",
              Mathf.Abs(deepCents - cents) < 1.0f,
              deepCents + " cents against " + cents);

        // The image opens over the first three tenths and no further, so that the rest
        // of the travel is the detune alone.
        var (quarter, third, all) = (note, note, note);
        (quarter.unison, third.unison, all.unison) = (0.075f, 0.3f, 1.0f);

        Check(log, "the image opens over three tenths and then stands still",
              Mathf.Abs(quarter.Spread - 0.25f) < 0.001f &&
              third.Spread == 1.0f && all.Spread == 1.0f,
              "0.075 spreads " + quarter.Spread + ", 0.3 spreads " + third.Spread);

        // And the pan reaches the end of its own travel whatever the unison is, which
        // is the whole point of the pair closing up rather than piling against a wall.
        // A hard panned note is on one side and silent on the other at every setting —
        // the thing a clamped pair could not do, since its inner half stayed behind.
        var thrown = note;
        (thrown.unison, thrown.pan) = (1.0f, 1.0f);

        thrown.UnisonGains(out var thrownLA, out var thrownRA,
                           out var thrownLB, out var thrownRB);

        var halfWay = note;
        (halfWay.unison, halfWay.pan) = (1.0f, 0.5f);

        Check(log, "the pan still reaches the end however wide the pair is",
              thrown.Reach == 0.0f &&
              thrownLA < 0.001f && thrownLB < 0.001f &&
              Mathf.Abs(thrownRA - thrownRB) < 0.001f &&
              Mathf.Abs(halfWay.Reach - 0.5f) < 0.001f,
              "hard over reaches " + thrown.Reach + " and leaks " +
              (thrownLA + thrownLB) + " to the far side; halfway reaches " +
              halfWay.Reach);

        // The level across the travel, which is what the gain law is for. Both ends
        // are exact and the two ends are exact for different reasons, so both are
        // measured: at the bottom the pair is on one spot and every channel hears the
        // two halves in step, and at the top each channel hears one half only.
        //
        // In between it is neither, because how much of the two a channel still hears
        // in step is falling as the pair opens — the one setting does both — so the
        // reading is handed over from one to the other across the same travel. That
        // crossing is a description of what the ear meets and not a law, which is why
        // what is asserted about the middle is loose where the ends are exact.
        //
        // Swept across the pan as well as across the unison, since the pan is now what
        // decides how far a pair actually opens: a law that held only down the middle
        // of the image would be a note that changed level as it was moved.
        var flattest = 0.0f;

        for (var i = 0; i <= 20; i++)
        for (var j = -4; j <= 4; j++)
        {
            var wide = note;
            (wide.unison, wide.pan) = (i / 20.0f, j / 4.0f);

            wide.UnisonGains(out var la, out var ra, out var lb, out var rb);

            var gain = wide.UnisonGain;

            // Two halves adding as one signal, against two adding as two — handed over
            // by the spread, since what decides which of the two a pair is doing is how
            // far apart it is tuned and not where it has been put. Reading the reach
            // here instead is the mistake this sweep exists to catch: it calls a hard
            // panned pair coherent because the two halves are on one spot, when sixty
            // cents of detune has long since stopped them agreeing, and it costs such a
            // note 3dB.
            var together = (la + lb) * (la + lb) + (ra + rb) * (ra + rb);
            var apart = la * la + lb * lb + ra * ra + rb * rb;

            var spread = wide.Spread;
            var power = gain * gain *
                        (together * (1.0f - spread) + apart * spread);

            // Against the 2 a single centred note carries, in decibels.
            flattest = Mathf.Max(flattest, Mathf.Abs(10.0f * Mathf.Log10(power / 2.0f)));
        }

        Check(log, "the level holds however wide the pair is opened and wherever it sits",
              flattest < 0.5f, "largest departure " + flattest + "dB");

        // And it travels with the patch, like every other field of one.
        var project = new Project();
        project.Patches[1].unison = 0.4f;

        var reloaded = ProjectFormat.Read(ProjectFormat.Write(project));
        var legacy = ProjectFormat.Read("jacquard 14\ntempo 120\npatch 1 level=0.5\n");

        Check(log, "a unison round trips and an older file comes back single",
              Mathf.Abs(reloaded.Patches[1].unison - 0.4f) < 0.001f &&
              legacy.Patches[1].unison == 0.0f,
              "unison=" + reloaded.Patches[1].unison +
              " version 14 unison=" + legacy.Patches[1].unison);
    }

    // The live effects, which are the one thing that colours a note without being
    // written anywhere on the plane.
    //
    // Driven exactly the way the app drives them, since that is most of what there is
    // to get wrong: the sequencer runs a window ahead and the handover follows at a
    // much shorter one, and what a live effect reaches is precisely what has not
    // been handed over yet. A run that only called the modifiers would prove nothing
    // about the thing this feature is actually made of.
    static void Live(System.Text.StringBuilder log)
    {
        const int sampleRate = 48000;

        // Forty sixteenths, which is long enough to see a ramp turn over.
        const long span = LiveSixteenth * 40;

        var origin = LiveLookahead;

        // With nothing held this layer is a length of pipe, and a feature that is up
        // on the screen more often than it is being used has to be exactly that.
        var plain = LiveRun(null, span, sampleRate);

        var inert = plain.Count == 40;
        for (var i = 0; i < 40 && inert; i++) inert = LiveNoteAt(plain, i) == 60 + i % 16;

        Check(log, "nothing held hands the sequence over untouched",
              inert, plain.Count + " notes");

        var up = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.OctaveUp, origin); }, span, sampleRate);

        Check(log, "an octave up is an octave up",
              LiveNoteAt(up, 0) == 72 && LiveNoteAt(up, 5) == 77,
              LiveNoteAt(up, 0) + " and " + LiveNoteAt(up, 5));

        // Both octaves at once is the case that says the semitones are summed and the
        // frequency multiplied once, rather than each effect multiplying in turn.
        var flat = LiveRun((live, now) =>
          { if (now > 0) return;
            live.Press(LiveEffect.OctaveUp, origin);
            live.Press(LiveEffect.OctaveDown, origin); }, span, sampleRate);

        Check(log, "an octave each way cancels",
              LiveNoteAt(flat, 3) == 63, LiveNoteAt(flat, 3).ToString());

        // Both ends of the gate on their own first, since each one reaches the release
        // as well and a stab whose tail is a quarter of a second is not a stab. The
        // patch's release is 120ms, so the one is cut to ten and the other doubled.
        var stab = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Stab, origin); }, span, sampleRate);

        var stabbed = LiveEventAt(stab, 3);

        Check(log, "a stab cuts the tail down with the gate",
              Mathf.Abs(stabbed.duration - 0.0125f) < 0.0001f &&
              Mathf.Abs(stabbed.carrierRelease - 0.01f) < 0.0001f,
              stabbed.duration + "s over " + stabbed.carrierRelease + "s");

        var sustain = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Sustain, origin); }, span, sampleRate);

        var sustained = LiveEventAt(sustain, 3);

        Check(log, "a sustain stretches the tail with the gate",
              Mathf.Abs(sustained.duration - 0.25f) < 0.0001f &&
              Mathf.Abs(sustained.carrierRelease - 0.24f) < 0.0001f,
              sustained.duration + "s over " + sustained.carrierRelease + "s");

        // Together: Stab sets both and Sustain doubles whatever it finds, which is the
        // whole of what "in one order" buys. A release already under ten milliseconds
        // is left alone, since a button that means shorter must not lengthen one.
        var gated = LiveRun((live, now) =>
          { if (now > 0) return;
            live.Press(LiveEffect.Stab, origin);
            live.Press(LiveEffect.Sustain, origin); }, span, sampleRate);

        var both = LiveEventAt(gated, 3);

        Check(log, "a stab held under a sustain is a fifth of a step",
              Mathf.Abs(both.duration - 0.025f) < 0.0001f &&
              Mathf.Abs(both.carrierRelease - 0.02f) < 0.0001f,
              both.duration + "s over " + both.carrierRelease + "s");

        var brief = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Stab, origin); },
          span, sampleRate, 0.004f);

        Check(log, "a stab leaves a tail already shorter than it would make one",
              Mathf.Abs(LiveEventAt(brief, 3).carrierRelease - 0.004f) < 0.0001f,
              LiveEventAt(brief, 3).carrierRelease + "s");

        // A semitone a step from the press, turning over after two bars of them.
        var rise = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Rise, origin); }, span, sampleRate);

        Check(log, "a rise climbs a semitone a step and resets after two bars",
              LiveNoteAt(rise, 0) == 60 && LiveNoteAt(rise, 1) == 62 &&
              LiveNoteAt(rise, 31) == 106 && LiveNoteAt(rise, 32) == 60,
              LiveNoteAt(rise, 0) + " " + LiveNoteAt(rise, 1) + " " +
              LiveNoteAt(rise, 31) + " " + LiveNoteAt(rise, 32));

        var wet = LiveRun((live, now) =>
          { if (now == 0) live.Press(LiveEffect.Reverb, origin); }, span, sampleRate);

        Check(log, "a throw puts every note in the reverb",
              wet.TrueForAll(note => Mathf.Abs(note.reverbSend - 1.0f) < 0.0001f),
              wet.Count + " notes");

        // Pressed a third of the way through the fifth step, which is the case the
        // record of what has sounded exists for: that step's note was handed over
        // before the press and is not in anything still to come.
        var roll = LiveRun((live, now) =>
          { if (now == origin + LiveSixteenth * 4 + 2000)
                live.Press(LiveEffect.Roll1, now); }, span, sampleRate);

        Check(log, "a sixteenth roll catches the step the hand was on and stands in for the rest",
              roll.Count == 40 && LiveNoteAt(roll, 4) == 64 &&
              LiveNoteAt(roll, 5) == 64 && LiveNoteAt(roll, 10) == 64,
              roll.Count + " notes, " + LiveNoteAt(roll, 5) + " where 65 was");

        // Letting go hands the sequence straight back, at the step it had reached
        // rather than the one it was standing on when the hand arrived.
        var released = LiveRun((live, now) =>
          { if (now == origin + LiveSixteenth * 4 + 2000)
                live.Press(LiveEffect.Roll1, now);
            if (now == origin + LiveSixteenth * 12 + 400)
                live.Release(LiveEffect.Roll1); }, span, sampleRate);

        Check(log, "letting a roll go gives the sequence back where it had got to",
              released.Count == 40 && LiveNoteAt(released, 12) == 64 &&
              LiveNoteAt(released, 13) == 73 && LiveNoteAt(released, 21) == 65,
              LiveNoteAt(released, 12) + " " + LiveNoteAt(released, 13) + " " +
              LiveNoteAt(released, 21));

        // The three longer ones, which are also the other half of the mechanism: a
        // window that reaches past what has already sounded lets the sequence play on
        // and writes it down before anything stands in for it. One check each, on the
        // step where the window closes and on the one after it, since that pair is
        // where a length that was counted wrong would show.
        LiveRoll(log, LiveEffect.Roll2, 2, span, sampleRate);
        LiveRoll(log, LiveEffect.Roll3, 3, span, sampleRate);
        LiveRoll(log, LiveEffect.Roll4, 4, span, sampleRate);

        // All four again at a tempo whose sixteenth is a fraction of a sample, which
        // is about half of them and is the one thing 120bpm cannot ask. Everything
        // above lands on a whole number and so never noticed that this counted its
        // grid one way and the sequencer counted it the other.
        LiveRollOffGrid(log, LiveEffect.Roll1, 1, sampleRate);
        LiveRollOffGrid(log, LiveEffect.Roll2, 2, sampleRate);
        LiveRollOffGrid(log, LiveEffect.Roll3, 3, sampleRate);
        LiveRollOffGrid(log, LiveEffect.Roll4, 4, sampleRate);

        // A roll pressed into a gap. The sixteenth on a lane playing every other step
        // is the case a hand meets, since a sixteenth is the one length that can miss
        // a note by landing a step out; the longer ones only meet it where a whole
        // window is silent, which is the sparse lane underneath.
        // lands is where the window that catches something opens, which is not the
        // step that broke the gap unless the roll is a sixteenth: a longer window
        // reaches the note from a rest or two before it, and opening there is what
        // keeps the roll on the grid it was pressed against.
        LiveRollRest(log, LiveEffect.Roll1, 1, 2, 7, 8, sampleRate);
        LiveRollRest(log, LiveEffect.Roll2, 2, 4, 5, 7, sampleRate);
        LiveRollRest(log, LiveEffect.Roll4, 4, 8, 3, 7, sampleRate);

        // And the same press where the step does carry a note, which is every case
        // that already worked and has to keep working: the window is taken where the
        // hand put it and nothing waits for anything.
        LiveRollRest(log, LiveEffect.Roll1, 1, 2, 6, 6, sampleRate);

        // Two at once is the one thing here that does not stack, since both answer
        // what plays instead of the score. The longer one is pressed second and takes
        // it; letting that go hands back to the sixteenth, which has been catching all
        // along and is ready.
        var over = LiveRun((live, now) =>
          { if (now == origin + LiveSixteenth * 4 + 2000)
                live.Press(LiveEffect.Roll1, now);
            if (now == origin + LiveSixteenth * 8 + 400)
                live.Press(LiveEffect.Roll4, now);
            if (now == origin + LiveSixteenth * 20 + 400)
                live.Release(LiveEffect.Roll4); }, span, sampleRate);

        Check(log, "the roll pressed last is the one that plays, and hands back on release",
              over.Count == 40 &&
              LiveNoteAt(over, 7) == 64 && LiveNoteAt(over, 9) == 69 &&
              LiveNoteAt(over, 13) == 69 && LiveNoteAt(over, 21) == 64,
              over.Count + " notes: " + LiveNoteAt(over, 7) + " " +
              LiveNoteAt(over, 9) + " " + LiveNoteAt(over, 13) + " " +
              LiveNoteAt(over, 21));
    }

    // One roll of a given length, pressed on the eighth step so that its window sits
    // clear of both ends of the run. What it stands in for from the far end of that
    // window is the window itself, step for step.
    static void LiveRoll(System.Text.StringBuilder log, LiveEffect fx, int steps,
                          long span, int sampleRate)
    {
        const int from = 7;

        var run = LiveRun((live, now) =>
          { if (now == LiveLookahead + LiveSixteenth * from + 800)
                live.Press(fx, now); }, span, sampleRate);

        // Through the window the sequence is still the sequence, and past it every
        // step is the one a whole number of windows back.
        var ok = run.Count == 40;

        for (var i = from; i < 40 && ok; i++)
        {
            var source = i < from + steps ? i : from + (i - from) % steps;
            ok = LiveNoteAt(run, i) == 60 + source % 16;
        }

        Check(log, "a roll of " + steps + " records that many steps and then plays them",
              ok, LiveNoteAt(run, from + steps) + " where " +
                  (60 + (from + steps) % 16) + " was");
    }

    // The same roll at 129bpm, where a sixteenth is 5581.4 samples and no boundary is
    // a whole number. Two things a whole number hides are asked for here and nowhere
    // else: that the step at the far end of the window is outside it — a grid rounded
    // up where the sequencer rounded down put it one sample inside, so it neither
    // stopped nor was left behind, and every pass afterwards carried it a sample ahead
    // of its own first note — and that the roll is still on the grid twenty passes
    // later, which laying each pass a rounded length past the last one did not manage.
    static void LiveRollOffGrid(System.Text.StringBuilder log, LiveEffect fx, int steps,
                                 int sampleRate)
    {
        const float tempo = 129.0f;
        const int from = 7;

        var sixteenth = 60.0 / tempo / 4.0 * sampleRate;
        var span = LiveLookahead + (long)(40 * sixteenth);

        // The sample the sequencer puts a step on, which is the truncation its own
        // position gets and is what everything below is read against.
        long At(int step) => LiveLookahead + (long)(step * sixteenth);

        // A third of the way into the step, clear of the handover at either end of it.
        var press = At(from) + (long)(sixteenth / 3.0);

        var run = LiveRun((live, now)
                            => { if (now >= press && now < press + LiveFrame)
                                     live.Press(fx, now); },
                          span, sampleRate, tempo: tempo);

        var ok = true;
        var why = "";

        for (var i = from; i < 34 && ok; i++)
        {
            var source = i < from + steps ? i : from + (i - from) % steps;
            var want = 60 + source % 16;

            // Within a sample or two of the grid, since a pass placed on it and a note
            // offset into it are two truncations against the sequencer's one.
            var found = 0;
            var last = -1;

            foreach (var note in run)
                if (System.Math.Abs(note.startSample - At(i)) <= 2)
                    { found++; last = Semitones(note); }

            if (found == 1 && last == want) continue;

            (ok, why) = (false, "step " + i + " is " +
                                (found == 0 ? "silent" :
                                 found == 1 ? last.ToString() :
                                 found + " notes at once") + " where " + want + " was");
        }

        if (ok) why = (34 - from) + " steps, each one note and on the grid";

        Check(log, "a roll of " + steps + " keeps the sequencer's grid at 129bpm",
              ok, why);
    }

    // A roll pressed into a gap, on a lane that carries a note every stride steps.
    //
    // A window over a rest has nothing to lay down, and a roll that stands in for the
    // score with nothing is silence held for as long as the button — which is the one
    // thing a hand reaching for a roll can never have wanted. So the empty window is
    // let go and the next one along is taken, until one of them catches something.
    // What is asked here is that the score is never stopped while that search runs,
    // and that the roll ends up on the step named by lands.
    static void LiveRollRest(System.Text.StringBuilder log, LiveEffect fx, int steps,
                              int stride, int press, int lands, int sampleRate)
    {
        var span = LiveLookahead + 40 * LiveSixteenth;
        var at = LiveLookahead + press * LiveSixteenth + LiveSixteenth / 2;

        var run = LiveRun((live, now)
                            => { if (now >= at && now < at + LiveFrame)
                                     live.Press(fx, now); },
                          span, sampleRate, stride: stride);

        var ok = true;
        var why = "";

        // Up to the window it lands on, the score is untouched — which is the whole of
        // what the search costs and the whole of what it must not cost more than.
        for (var i = press; i < lands + steps && ok; i++)
            if (LiveNoteAt(run, i) != (i % stride == 0 ? 60 + i % 16 : -1))
                (ok, why) = (false, "step " + i + " is " + LiveNoteAt(run, i) +
                                    " where the score had " +
                                    (i % stride == 0 ? 60 + i % 16 : -1));

        // Past it, the window it landed on, laid down again and again.
        for (var i = lands + steps; i < 34 && ok; i++)
        {
            var source = lands + (i - lands) % steps;
            var want = source % stride == 0 ? 60 + source % 16 : -1;

            if (LiveNoteAt(run, i) != want)
                (ok, why) = (false, "step " + i + " is " + LiveNoteAt(run, i) +
                                    " where step " + source + "'s " + want + " was");
        }

        if (ok) why = "the score through, then steps " + lands + " to " +
                      (lands + steps - 1) + " over";

        Check(log, "a roll of " + steps + " pressed on step " + press +
                   " opens its window on step " + lands, ok, why);
    }

    // 120bpm puts a sixteenth at exactly 6000 samples, so every boundary the checks
    // read off this is a whole number and nothing measured against it is measured
    // against a rounding. Which is also what those checks cannot ask, and why
    // LiveRollOffGrid runs the rolls again at a tempo that has no such luck.
    const long LiveSixteenth = 6000;

    // The app's own two windows and its frame, at the scale of that tempo: an eighth
    // of a second for the sequencer, a thirty-second for the handover, and sixty
    // frames a second between them.
    const long LiveLookahead = 6000;
    const long LiveLead = 1500;
    const long LiveFrame = 800;

    // A lane with a different pitch on every step, so that a roll can be told from the
    // sequence it stood in for simply by reading the notes back.
    // release is the one patch value any of these checks cares about; zero leaves the
    // channel at the default the rest of them are measured against.
    // stride is how often a step carries a note at all: one is the lane every check
    // but the rests one reads, and anything above it leaves gaps for a roll to be
    // pressed into. A step with no tile on it is a rest and the runner simply passes
    // over it, which is the whole of what makes one here.
    static Project LiveScore(float release, Scale scale, float tempo, int stride)
    {
        var project = new Project { Tempo = tempo };
        var lane = project.Score.AddLane(1, 1, new ChannelTile { Channel = 1 }, 16);

        for (var i = 0; i < 16; i++)
            if (i % stride == 0)
                lane.Steps[i].Tiles.Add(new NoteTile { Note = 60 + i });

        if (release > 0.0f) project.Patches[1].carrierRelease = release;

        // Chromatic unless a caller wants the notes caught on the way out, which is
        // what the live effects are asked about in Tuning.
        if (scale != null)
            for (var degree = 0; degree < Scale.Degrees; degree++)
                project.Scale.SetAllowed(degree, scale.Allows(degree));

        return project;
    }

    static System.Collections.Generic.List<FmNoteEvent> LiveRun(
      System.Action<LiveFx, long> hand, long span, int sampleRate,
      float release = 0.0f, Scale scale = null, float tempo = 120.0f, int stride = 1)
    {
        var project = LiveScore(release, scale, tempo, stride);
        var sequencer = new Sequencer { Project = project };
        var live = new LiveFx();

        var pending = new System.Collections.Generic.List<FmNoteEvent>();
        var output = new System.Collections.Generic.List<FmNoteEvent>();

        sequencer.Play(0, LiveLookahead);
        live.Start(LiveLookahead);

        for (var now = 0L; now < span; now += LiveFrame)
        {
            hand?.Invoke(live, now);

            pending.Clear();
            sequencer.Schedule(now, LiveLookahead, sampleRate, pending);
            live.Enqueue(pending);
            live.HandOver(now + LiveLead, project.Tempo, sampleRate, output);
        }

        return output;
    }

    // By the sample a note starts on rather than by where it sits in the list, since
    // what is being asked is what sounded at that moment and not what was handed over
    // in what order.
    static FmNoteEvent LiveEventAt(System.Collections.Generic.List<FmNoteEvent> notes,
                                    long step)
      => notes.Find(note => note.startSample == LiveLookahead + step * LiveSixteenth);

    static int LiveNoteAt(System.Collections.Generic.List<FmNoteEvent> notes, long step)
      => Semitones(LiveEventAt(notes, step));

    // The note number an event came out as, which is the only way to read a pitch once
    // it has been through the live effects: they colour a frequency, having no semitone
    // left to move by then.
    static int Semitones(in FmNoteEvent note)
      => note.frequency > 0.0f
         ? Mathf.RoundToInt(69.0f + 12.0f * Mathf.Log(note.frequency / 440.0f, 2.0f))
         : -1;

    // The oscillator, which neither the round trip nor the note counts say
    // anything about. Ported from unity-sap-test's offline checks, and worth
    // keeping here because Jacquard runs the same maths through FastMath's
    // approximations rather than Unity.Mathematics.
    static void Synth(System.Text.StringBuilder log)
    {
        // A note that holds a flat level, so every measurement is taken on a
        // steady signal. Modulation is off, leaving a pure sine whose pitch the
        // zero crossing estimate can read reliably, and its decay is at the top of
        // its travel where there is no decay at all, so what depth there is stays
        // constant.
        var plain = new FmNoteEvent
          { frequency = 440.0f, level = 1.0f, duration = 1.0f,
            modulatorRatio = 2.0f, modulationIndex = 0.0f, modulatorDecay = 1.0f,
            carrierAttack = 0.005f, carrierRelease = 0.01f };

        // The carrier holds full level for the whole gate, so a long note must not
        // sag between its attack and its release.
        var held = Render(plain, 1.0f);
        var early = Rms(held, Seconds(0.05f), Seconds(0.15f));
        var late = Rms(held, Seconds(0.8f), Seconds(0.9f));

        Check(log, "carrier holds level", Mathf.Abs(late / early - 1.0f) < 0.01f,
              "late/early=" + late / early);

        // The modulation decays to nothing on its own, so the tail of a long note
        // ends up a plain sine whatever the index was. The curve is exponential and
        // front-loaded, so the onset is read just after the attack and the tail
        // long after anything is left of the modulation.
        var voiced = plain;
        voiced.frequency = 220.0f;
        voiced.modulationIndex = 4.0f;
        voiced.modulatorDecay = 0.3f;  // A slope, not a time: 40ms of time constant

        var bite = Render(voiced, 1.0f);
        var onset = Brightness(bite, Seconds(0.008f), Seconds(0.04f));
        var tail = Brightness(bite, Seconds(0.6f), Seconds(0.9f));

        Check(log, "modulation decays away", onset > tail * 2.0f,
              "brightness " + onset + " -> " + tail);

        // The two ends of that slope are the two ways a patch can have no modulation
        // envelope at all, and they are the reason the parameter is a slope rather
        // than a time: neither of them is a number of milliseconds. Read over the same
        // two windows, so both are comparable with the pair above — and against each
        // other, since a note with the modulation switched off has to measure the same
        // as the tail of one that let it decay.
        var muted = voiced;
        muted.modulatorDecay = 0.0f;

        var sustained = voiced;
        sustained.modulatorDecay = 1.0f;

        var silent = Brightness(Render(muted, 1.0f), Seconds(0.008f), Seconds(0.04f));
        var flat = Render(sustained, 1.0f);
        var flatOnset = Brightness(flat, Seconds(0.008f), Seconds(0.04f));
        var flatTail = Brightness(flat, Seconds(0.6f), Seconds(0.9f));

        Check(log, "a decay of zero leaves no modulation at all",
              silent < tail * 1.1f,
              "brightness " + silent + " against a decayed tail of " + tail);

        Check(log, "a decay of one holds it for the whole note",
              Mathf.Abs(flatTail / flatOnset - 1.0f) < 0.05f,
              "brightness " + flatOnset + " -> " + flatTail);

        // The pitch envelope bends the onset and then arrives exactly on the note
        // frequency, in both directions. Measured with a sweep far slower than a
        // kick's, because a kick's own decay is over in fewer cycles than a zero
        // crossing estimate needs; the steepness is checked separately below.
        var falling = plain;
        falling.pitchSweep = 3.0f;  // Three octaves above the note at the onset
        falling.pitchDecay = 0.4f;

        var rising = falling;
        rising.pitchSweep = -3.0f;  // And the same distance below it

        var down = Render(falling, 0.5f);
        var up = Render(rising, 0.5f);

        var from = Seconds(0.005f);  // Past the attack, so there is signal to read
        var to = Seconds(0.045f);

        var high = Frequency(down, from, to);
        var low = Frequency(up, from, to);
        var settled = Frequency(down, Seconds(0.3f), Seconds(0.45f));

        Check(log, "pitch bends the onset up", high > 900.0f, high + "Hz");
        Check(log, "a negative sweep bends it down", low < 300.0f, low + "Hz");
        Check(log, "pitch arrives at the note", Mathf.Abs(settled - 440.0f) < 5.0f,
              settled + "Hz");

        // Steepness, which is what separates a kick's thump from an audible sweep:
        // with a kick's own setting the pitch has to be all but home a fifth of the
        // way into the decay, leaving the rest of it inaudible. A gentler curve
        // fails this by a wide margin, which is the point of measuring it: the
        // shape is the sound here, not just the two numbers.
        var kick = plain;
        kick.frequency = 880.0f;    // High enough for 40ms to be many cycles
        kick.pitchSweep = 2.0f;
        kick.pitchDecay = 0.05f;

        var rest = Frequency(Render(kick, 0.3f), Seconds(0.01f), Seconds(0.05f));

        Check(log, "the sweep is home early in the decay",
              Mathf.Abs(rest / 880.0f - 1.0f) < 0.05f,
              "over the last four fifths=" + rest + "Hz");

        // And a note ends in silence rather than being cut off, which is what the
        // normalized fade is for.
        var whole = Render(plain, plain.TotalDuration);

        Check(log, "the release ends in silence",
              Mathf.Abs(whole[whole.Length - 1]) < 0.001f,
              "last sample=" + whole[whole.Length - 1]);
    }

    // The delay, which has two properties worth measuring and no way to see either
    // without rendering it: that a repeat lands where the tempo says it should, and
    // that moving the time while it is running does not splice the signal.
    static void Delay(System.Text.StringBuilder log)
    {
        // A beat is exactly half a second at this tempo, so an eighth note is 12000
        // samples and the expected positions are whole numbers.
        const float tempo = 120.0f;
        const float feedback = 0.35f;

        var fx = SendFx.Default;
        var tap = fx.DelaySeconds(tempo) * SampleRate;

        // Tone off, so a repeat is the one before it times the feedback and nothing
        // else. The lowpass inside the loop is what makes the repeats darken, and it
        // would smear the impulse into a tail with no peak left to find.
        var trace = RenderDelay(Impulse((int)(tap * 3.5f)), tap, feedback, 0.0f, 0.0f);

        var first = Peak(trace, (int)(tap * 0.5f), (int)(tap * 1.5f));
        var second = Peak(trace, (int)(tap * 1.5f), (int)(tap * 2.5f));

        Check(log, "a repeat lands on the beat it was asked for",
              Mathf.Abs(first - tap) <= 2.0f && Mathf.Abs(second - tap * 2.0f) <= 3.0f,
              "peaks at " + first + " and " + second + " against a tap of " + tap);

        var fell = trace[second] / Mathf.Max(trace[first], 1e-9f);

        Check(log, "each repeat falls by the feedback",
              Mathf.Abs(fell - feedback) < 0.02f,
              "second/first=" + fell + " against a feedback of " + feedback);

        // The one the rate limit exists for. A tone held through a change of rung has
        // to stay a tone: a tap that jumped would read from somewhere unrelated to
        // where it was, and the seam would show up as one step far larger than any the
        // signal itself takes. Measured against the same signal at a steady tap, which
        // is the only honest yardstick for what "large" means here.
        var steady = Continuity(tap, tap);
        var moved = Continuity(tap, tap * 2.0f);

        Check(log, "changing the delay time does not splice the signal",
              moved < steady * 2.0f,
              "largest step " + moved + " while moving against " + steady + " steady");
    }

    // The reverb, which is a feedback network and so has exactly one way of being
    // wrong that matters: not settling. A tail that grows, or that reaches a NaN and
    // stays there, is silent in the editor and a dead output on a device.
    static void Reverb(System.Text.StringBuilder log)
    {
        var input = Burst(Seconds(1.5f), Seconds(0.01f));

        var small = RenderReverb(input, 0.2f, 0.5f, 1.0f);
        var middle = RenderReverb(input, SendFx.Default.reverbSize, 0.5f, 1.0f);
        var large = RenderReverb(input, 1.0f, 0.5f, 1.0f);

        var finite = true;
        foreach (var sample in large)
            if (float.IsNaN(sample) || float.IsInfinity(sample)) finite = false;

        var early = Rms(large, Seconds(0.02f), Seconds(0.1f));
        var late = Rms(large, Seconds(1.2f), Seconds(1.5f));

        Check(log, "the tail stays finite", finite, "over " + large.Length + " samples");

        // At the top of the size range the tail is meant to be long, so what is
        // checked there is only that it is going down: a network whose feedback had
        // been let past one would grow instead, and that is the failure that matters.
        Check(log, "the longest tail still settles", finite && late < early && early > 1e-4f,
              "early=" + early + " late=" + late);

        // Where the panel starts, a second and a half is long enough that the tail
        // should be all but gone.
        var settling = Rms(middle, Seconds(0.02f), Seconds(0.1f));
        var gone = Rms(middle, Seconds(1.2f), Seconds(1.5f));

        Check(log, "a default sized tail dies away", gone < settling * 0.05f,
              "early=" + settling + " late=" + gone);

        // And the control has to do what it is named: the same input into a larger
        // room has to still be sounding when a smaller one has finished.
        var tight = Rms(small, Seconds(1.2f), Seconds(1.5f));

        Check(log, "a larger size decays more slowly", late > tight * 2.0f,
              "late RMS " + late + " at size 1 against " + tight + " at size 0.2");
    }

    // The limiter, which has one promise per control: that the ceiling holds, that the
    // drive is what pushes the mix into it, that the attack is a hole in the limiting
    // for as long as it lasts, and that a project which never opens the panel is
    // untouched.
    static void Limiter(System.Text.StringBuilder log)
    {
        var settings = Jacquard.Limiter.Default;

        // What a project opens with, against a signal already at full scale: this has
        // to come back as it went in, since a default limiter is a ceiling at full scale
        // with nothing under it to hold down and a make-up of one.
        var open = RenderLimiter(Tone(Seconds(0.2f), 1.0f), settings);
        var passed = Rms(open, Seconds(0.1f), Seconds(0.2f));
        var plain = Rms(Tone(Seconds(0.2f), 1.0f), Seconds(0.1f), Seconds(0.2f));

        Check(log, "a default limiter leaves the mix alone",
              Mathf.Abs(passed / plain - 1.0f) < 0.01f,
              "out/in=" + passed / plain);

        // A full scale mix under a ceiling well below it. Once the gain has settled the
        // peaks have to come back at full scale rather than down at the ceiling, which is
        // the make-up giving back precisely what the ceiling took off: what the bar moves
        // is how much of the mix is held down and not how loud the result is.
        settings.ceiling = -12.0f;

        var squeezed = RenderLimiter(Tone(Seconds(0.5f), 1.0f), settings);
        var held = 0.0f;

        for (var i = Seconds(0.2f); i < Seconds(0.5f); i++)
            held = Mathf.Max(held, Mathf.Abs(squeezed[i]));

        Check(log, "the make-up puts the peaks back at full scale",
              held <= 1.02f && held > 0.9f,
              "peak " + held + " against a ceiling " + settings.ceiling + "dB down");

        // And a signal quiet enough never to reach the ceiling is lifted by the whole
        // make-up and nothing else: this is the half of the arrangement that makes a mix
        // louder rather than flatter, and it is what the drive used to do.
        var quiet = RenderLimiter(Tone(Seconds(0.2f), 0.02f), settings);
        var lifted = Rms(quiet, Seconds(0.1f), Seconds(0.2f)) /
                     Rms(Tone(Seconds(0.2f), 0.02f), Seconds(0.1f), Seconds(0.2f));
        var makeUp = Jacquard.Limiter.Gain(-settings.ceiling);

        Check(log, "a mix under the ceiling is lifted by the whole make-up",
              Mathf.Abs(lifted - makeUp) < 0.1f,
              "lifted by " + lifted + " against a make-up of " + makeUp);

        // The attack is a hole in the limiting for as long as it lasts, which is what
        // a kick is heard through. The same burst under the slowest attack the panel
        // offers has to arrive louder than under the fastest.
        settings.attack = Jacquard.Limiter.MinAttack;
        var clamped = RenderLimiter(Tone(Seconds(0.05f), 0.5f), settings);

        settings.attack = Jacquard.Limiter.MaxAttack;
        var punched = RenderLimiter(Tone(Seconds(0.05f), 0.5f), settings);

        var front = Seconds(0.002f);

        Check(log, "a slow attack lets the front of a transient through",
              Rms(punched, 0, front) > Rms(clamped, 0, front) * 1.5f,
              "first 2ms " + Rms(punched, 0, front) + " slow against " +
              Rms(clamped, 0, front) + " fast");

        // And the release has to give the gain back, or every loud note would leave
        // the mix quiet behind it for good.
        settings.attack = 0.005f;
        settings.release = 0.05f;

        var recovered = RenderLimiter(LoudThenQuiet(Seconds(0.5f), Seconds(0.1f),
                                                   0.5f, 0.02f), settings);
        var after = Rms(recovered, Seconds(0.3f), Seconds(0.5f));
        var target = 0.02f * makeUp / Mathf.Sqrt(2.0f);

        Check(log, "the gain comes back after a loud passage",
              after > target * 0.9f,
              "tail RMS " + after + " against " + target + " at full gain");

        // A version 16 threshold meets the staging shift on the way in. The mix in front
        // of the limiter is 10.1dB smaller than it was, and a threshold is a level, so
        // the same place in the mix is now a different number and the file has to be
        // given it. One already at the end of the bar has nowhere left to go.
        var shifted = ProjectFormat.Read("jacquard 16\nlimiter ceiling=-6\n").Limiter;
        var floored = ProjectFormat.Read("jacquard 16\nlimiter ceiling=-40\n").Limiter;

        Check(log, "a version 16 threshold comes down by the staged headroom",
              Mathf.Abs(shifted.ceiling + 16.103f) < 0.01f &&
              Mathf.Abs(floored.ceiling - Jacquard.Limiter.MinCeiling) < 0.001f,
              "ceiling=" + shifted.ceiling + ", and -40 lands on " + floored.ceiling);

        // And a file with no limiter line at all takes the same shift, which is the one
        // case where reading it as it stands would be audible: a threshold left at full
        // scale against a mix a quarter of the size is a limiter that has stopped
        // reaching the music it used to hold down.
        var silent = ProjectFormat.Read("jacquard 10\ntempo 120\n").Limiter;

        Check(log, "a file from before the limiter is shifted with the rest",
              Mathf.Abs(silent.ceiling - shifted.ceiling - 6.0f) < 0.01f,
              "ceiling=" + silent.ceiling + " from a default of " +
              Jacquard.Limiter.Default.ceiling);

        // A version 12 file said the same squeeze with two numbers, a drive pushing up
        // into a ceiling that held the output down, so it has to arrive as the one that
        // carries it now: 12dB of push into a ceiling 6dB down is 18dB of squeeze. Then
        // it takes the shift above like every other older threshold, which is what the
        // expectation is written against rather than a second copy of the number. The
        // last pair reaches past the bar before either conversion and stays at the end
        // of it.
        var folded = ProjectFormat.Read(
          "jacquard 12\nlimiter drive=12 ceiling=-6 attack=0.01 release=0.2\n").Limiter;

        var beyond = ProjectFormat.Read(
          "jacquard 12\nlimiter drive=48 ceiling=-6\n").Limiter;

        Check(log, "a version 12 drive folds into the ceiling ahead of it",
              Mathf.Abs(folded.ceiling - (shifted.ceiling - 12.0f)) < 0.01f &&
              Mathf.Abs(folded.attack - 0.01f) < 0.0001f &&
              Mathf.Abs(beyond.ceiling - Jacquard.Limiter.MinCeiling) < 0.001f,
              "ceiling=" + folded.ceiling + " attack=" + folded.attack +
              ", and 48dB over 6 clamps to " + beyond.ceiling);
    }

    // Rendering helpers
    //
    // Both buses hold their state in NativeArrays, so a check has to allocate one the
    // way the audio thread's Configure does and hand back a plain array to measure.

    static float[] Impulse(int frames)
    {
        var buffer = new float[frames];
        buffer[0] = 1.0f;
        return buffer;
    }

    // A short tone rather than an impulse, since a reverb's input is a note: one
    // sample carries too little energy for the late tail to rise out of nothing.
    static float[] Burst(int frames, int length)
    {
        var buffer = new float[frames];

        for (var i = 0; i < length; i++)
            buffer[i] = Mathf.Sin(2.0f * Mathf.PI * 440.0f * i / SampleRate) * 0.5f;

        return buffer;
    }

    // A steady tone at an amplitude, which is what the limiter is measured against:
    // its two times are in the shape of the signal rather than in its spectrum, so
    // there is nothing to be gained from anything more complicated.
    static float[] Tone(int frames, float amplitude)
    {
        var buffer = new float[frames];

        for (var i = 0; i < frames; i++)
            buffer[i] = Mathf.Sin(2.0f * Mathf.PI * 220.0f * i / SampleRate) * amplitude;

        return buffer;
    }

    // The same tone, loud for a while and then quiet, which is what a release is read
    // off: what the gain does after the loud part is over is the whole question.
    static float[] LoudThenQuiet(int frames, int loudFrames, float loud, float quiet)
    {
        var buffer = Tone(frames, 1.0f);

        for (var i = 0; i < frames; i++)
            buffer[i] *= i < loudFrames ? loud : quiet;

        return buffer;
    }

    // The limiter works in place on the two sides of a finished mix rather than adding
    // to them, so it renders through a pass of its own rather than through Render.
    static float[] RenderLimiter(float[] input, in Jacquard.Limiter settings)
    {
        const int block = 256;

        var bus = LimiterBus.Create();
        var runtime = LimiterRuntime.FromSettings(settings, SampleRate);

        var left = new NativeArray<float>(block, Allocator.Persistent);
        var right = new NativeArray<float>(block, Allocator.Persistent);
        var trace = new float[input.Length];

        for (var position = 0; position < input.Length; position += block)
        {
            var frames = Mathf.Min(block, input.Length - position);

            for (var i = 0; i < block; i++)
                left[i] = right[i] = i < frames ? input[position + i] : 0.0f;

            bus.Process(left, right, frames, runtime);

            for (var i = 0; i < frames; i++) trace[position + i] = left[i];
        }

        left.Dispose();
        right.Dispose();
        bus.Dispose();

        return trace;
    }

    static float[] RenderDelay(float[] input, float tap, float feedback, float tone,
                               float spread)
    {
        var bus = DelayBus.Create(SampleRate);
        var trace = Render(input, (i, l, r, n) =>
          bus.Process(i, l, r, n, tap, feedback, tone, spread));
        bus.Dispose();
        return trace;
    }

    static float[] RenderReverb(float[] input, float size, float damp, float width)
    {
        var bus = ReverbBus.Create(SampleRate);
        var trace = Render(input, (i, l, r, n) =>
          bus.Process(i, l, r, n, SampleRate, size, damp, width));
        bus.Dispose();
        return trace;
    }

    delegate void BusPass(NativeArray<float> input, NativeArray<float> wetL,
                          NativeArray<float> wetR, int frameCount);

    // One block at a time, like the render job, so that anything a bus carries across
    // a block boundary is exercised rather than skipped.
    static float[] Render(float[] input, BusPass pass)
    {
        const int block = 256;

        var buffers = new NativeArray<float>[3];
        for (var i = 0; i < 3; i++)
            buffers[i] = new NativeArray<float>(block, Allocator.Persistent);

        var (source, wetL, wetR) = (buffers[0], buffers[1], buffers[2]);
        var trace = new float[input.Length];

        for (var position = 0; position < input.Length; position += block)
        {
            var frames = Mathf.Min(block, input.Length - position);

            for (var i = 0; i < block; i++)
            {
                source[i] = i < frames ? input[position + i] : 0.0f;
                // The buses add to the wet buffers, since two of them share a pair.
                (wetL[i], wetR[i]) = (0.0f, 0.0f);
            }

            pass(source, wetL, wetR, frames);

            for (var i = 0; i < frames; i++) trace[position + i] = wetL[i];
        }

        foreach (var buffer in buffers) buffer.Dispose();

        return trace;
    }

    // The largest step between neighbouring samples once the line is full, with the
    // tap starting at one distance and being asked for another partway through.
    static float Continuity(float from, float to)
    {
        // Long enough that the second half is entirely past the longer of the two
        // taps, so what is measured is signal rather than the line filling up.
        var frames = (int)(from + to) * 3;
        var input = new float[frames];

        for (var i = 0; i < frames; i++)
            input[i] = Mathf.Sin(2.0f * Mathf.PI * 220.0f * i / SampleRate) * 0.5f;

        // Rendered by hand rather than through Render, because the tap has to change
        // partway along and the helper holds one setting for the whole pass.
        var bus = DelayBus.Create(SampleRate);
        var change = frames / 3;
        var trace = new float[frames];

        const int block = 256;

        var source = new NativeArray<float>(block, Allocator.Persistent);
        var wetL = new NativeArray<float>(block, Allocator.Persistent);
        var wetR = new NativeArray<float>(block, Allocator.Persistent);

        for (var position = 0; position < frames; position += block)
        {
            var count = Mathf.Min(block, frames - position);

            for (var i = 0; i < block; i++)
            {
                source[i] = i < count ? input[position + i] : 0.0f;
                (wetL[i], wetR[i]) = (0.0f, 0.0f);
            }

            bus.Process(source, wetL, wetR, count,
                        position < change ? from : to, 0.0f, 0.0f, 0.0f);

            for (var i = 0; i < count; i++) trace[position + i] = wetL[i];
        }

        source.Dispose();
        wetL.Dispose();
        wetR.Dispose();
        bus.Dispose();

        var largest = 0.0f;

        // From the change onwards, which is where a splice would land. The line has
        // been full since long before it, so everything measured here is signal.
        for (var i = change; i < frames; i++)
            largest = Mathf.Max(largest, Mathf.Abs(trace[i] - trace[i - 1]));

        return largest;
    }

    // Measurement helpers

    static int Peak(float[] buffer, int from, int to)
    {
        var (index, height) = (from, 0.0f);

        for (var i = Mathf.Max(from, 0); i < Mathf.Min(to, buffer.Length); i++)
        {
            if (Mathf.Abs(buffer[i]) <= height) continue;
            (index, height) = (i, Mathf.Abs(buffer[i]));
        }

        return index;
    }

    static int Seconds(float time) => (int)(time * SampleRate);

    static float[] Render(in FmNoteEvent note, float seconds)
    {
        var buffer = new float[Seconds(seconds)];
        var voice = new FmVoiceState();

        voice.Trigger(note, SampleRate);

        for (var i = 0; i < buffer.Length; i++)
        {
            voice.Next(i / SampleRate, out var lower, out var upper);
            buffer[i] = lower + upper;
        }

        return buffer;
    }

    // The two halves of a unison voice kept apart, which is the only way to measure
    // what each one is tuned to: summed, all a pair leaves behind is its beat.
    static void RenderPair(in FmNoteEvent note, float seconds,
                           out float[] lower, out float[] upper)
    {
        lower = new float[Seconds(seconds)];
        upper = new float[lower.Length];

        var voice = new FmVoiceState();

        voice.Trigger(note, SampleRate);

        for (var i = 0; i < lower.Length; i++)
            voice.Next(i / SampleRate, out lower[i], out upper[i]);
    }

    static float Rms(float[] buffer, int from, int to)
    {
        var sum = 0.0;
        for (var i = from; i < to; i++) sum += (double)buffer[i] * buffer[i];
        return (float)System.Math.Sqrt(sum / (to - from));
    }

    // Pitch from the spacing of the upward zero crossings, which is all a sine
    // needs and is what makes the pitch envelope measurable at all.
    static float Frequency(float[] buffer, int from, int to)
    {
        int first = -1, last = -1, count = 0;

        for (var i = from + 1; i < to; i++)
        {
            if (buffer[i - 1] > 0.0f || buffer[i] <= 0.0f) continue;
            if (first < 0) first = i;
            (last, count) = (i, count + 1);
        }

        return count < 2 ? 0.0f : (count - 1) * SampleRate / (last - first);
    }

    // Harmonic content, as the size of the sample to sample difference relative to
    // the signal itself: a plain sine scores low, a modulated one high.
    static float Brightness(float[] buffer, int from, int to)
    {
        var sum = 0.0;

        for (var i = from + 1; i < to; i++)
        {
            var d = (double)buffer[i] - buffer[i - 1];
            sum += d * d;
        }

        return (float)System.Math.Sqrt(sum / (to - from - 1)) /
               Mathf.Max(Rms(buffer, from, to), 1e-9f);
    }

    // Upper case on failure, matching the rest of this log: a scan down the left
    // edge is enough to see whether anything went wrong.
    static void Check(System.Text.StringBuilder log, string name, bool ok, string detail)
      => log.Append("  ").Append(ok ? name : name.ToUpperInvariant())
            .Append(ok ? ": " : " FAILED: ").Append(detail).Append('\n');
}

} // namespace Jacquard.Editor
