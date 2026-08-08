using UnityEditor;
using UnityEngine;

namespace Jacquard.Editor {

// A few checks that are quicker to run from a menu item than to reason about: the
// file format has to round-trip, the runners have to produce the notes the mockup
// score describes, and the oscillator has to make the shapes the patch promises.

static class SelfTest
{
    const float SampleRate = 48000.0f;

    [MenuItem("Jacquard/Run Self Test")]
    public static void Run()
    {
        var log = new System.Text.StringBuilder("Jacquard self test\n");

        RoundTrip(log);
        Playback(log);
        Stack(log);
        Locks(log);
        Channels(log);
        Synth(log);

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
        // patch level, since a lock is over when its instant is.
        Check(log, "a lock is gone by the next step", untouched > 0,
              untouched + " of " + notes.Count + " notes at the patch level");
    }

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
        lane.Steps[0].Tiles.Add(new CycleGateTile { Period = 4, Index = 1 });
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

    // The oscillator, which neither the round trip nor the note counts say
    // anything about. Ported from unity-sap-test's offline checks, and worth
    // keeping here because Jacquard runs the same maths through FastMath's
    // approximations rather than Unity.Mathematics.
    static void Synth(System.Text.StringBuilder log)
    {
        // A note that holds a flat level, so every measurement is taken on a
        // steady signal. Modulation is off, leaving a pure sine whose pitch the
        // zero crossing estimate can read reliably, and the modulator's decay is
        // far longer than any window so what depth there is stays constant.
        var plain = new FmNoteEvent
          { frequency = 440.0f, level = 1.0f, duration = 1.0f,
            modulatorRatio = 2.0f, modulationIndex = 0.0f, modulatorDecay = 20.0f,
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
        // long after the decay is over.
        var voiced = plain;
        voiced.frequency = 220.0f;
        voiced.modulationIndex = 4.0f;
        voiced.modulatorDecay = 0.3f;

        var bite = Render(voiced, 1.0f);
        var onset = Brightness(bite, Seconds(0.008f), Seconds(0.04f));
        var tail = Brightness(bite, Seconds(0.6f), Seconds(0.9f));

        Check(log, "modulation decays away", onset > tail * 2.0f,
              "brightness " + onset + " -> " + tail);

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

    // Measurement helpers

    static int Seconds(float time) => (int)(time * SampleRate);

    static float[] Render(in FmNoteEvent note, float seconds)
    {
        var buffer = new float[Seconds(seconds)];
        var voice = new FmVoiceState();

        voice.Trigger(note, SampleRate);

        for (var i = 0; i < buffer.Length; i++) buffer[i] = voice.Next(i / SampleRate);

        return buffer;
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
