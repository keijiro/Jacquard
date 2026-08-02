using UnityEditor;
using UnityEngine;

namespace Jacquard.Editor {

// A few checks that are quicker to run from a menu item than to reason about: the
// file format has to round-trip, and the runners have to produce the notes the
// mockup score describes.

static class SelfTest
{
    [MenuItem("Jacquard/Run Self Test")]
    public static void Run()
    {
        var log = new System.Text.StringBuilder("Jacquard self test\n");

        RoundTrip(log);
        Playback(log);

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

        var level = 0.0f;
        foreach (var note in notes) level = Mathf.Max(level, note.velocity);

        // The accent lane's relative lock is the only thing that can push a note
        // past the patch level, so seeing it proves the slice ordering works.
        log.Append(level > project.Patch.level + 0.01f
          ? "  accent reached notes: yes\n" : "  ACCENT DID NOT REACH NOTES\n");
    }
}

} // namespace Jacquard.Editor
