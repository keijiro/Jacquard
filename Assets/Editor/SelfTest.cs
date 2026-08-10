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
        StartupScore(log);
        Playback(log);
        Stack(log);
        Locks(log);
        Channels(log);
        Sends(log);
        Pan(log);
        Synth(log);
        Delay(log);
        Reverb(log);

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

    // The score the app opens on is a file rather than code, so nothing about it is
    // checked by compiling. What can go stale is the version it was written at: the
    // reader takes an older file, but a startup score left behind by a format bump
    // loses whatever the bump added, silently and on every launch. Reading it and
    // writing it back at the current version says both things at once — that it still
    // parses, and whether it is already what this build would write.
    static void StartupScore(System.Text.StringBuilder log)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SceneBuilder.StartupScorePath);

        if (asset == null)
        {
            log.Append("  STARTUP SCORE MISSING at ")
               .Append(SceneBuilder.StartupScorePath).Append('\n');
            return;
        }

        try
        {
            var project = ProjectFormat.Read(asset.text);
            var rewritten = ProjectFormat.Write(project);

            log.Append("  startup score: ").Append(project.Score.Lanes.Count)
               .Append(" lanes at ").Append(project.Tempo).Append("bpm\n");

            log.Append(rewritten == asset.text
              ? "  startup version: current\n"
              : "  startup version: readable but not what this build writes; "
                + "save it again from the app\n");
        }
        catch (System.Exception error)
        {
            log.Append("  STARTUP SCORE UNREADABLE: ").Append(error.Message).Append('\n');
        }
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
