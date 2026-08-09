using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jacquard {

// The file format: a line of text per lane element, tokens separated by spaces.
//
// A token is not the data — sequencer.md is explicit about that — so the file has
// its own spellings, chosen to be unambiguous rather than to look like a cell. A
// jump is recorded as the coordinate of the JUMP cell on the branch lane that
// answers to it, which is the same trick the mockup uses: there is nowhere to
// write a second jump, so one to one holds by construction.
//
//   jacquard 3
//   tempo 132
//   meter 4 4
//   fx rsize=0.5 rdamp=0.5 ...
//   patch 1 level=0.8 index=3 ...
//   lane 1 1 CHAN:1 div=16
//     step C4/4 E4 G4
//     step
//     step GCYC:4,4 JUMP
//   lane 6 8 JDST from=10,2
//     step D#5 C5 G#4

public static class ProjectFormat
{
    // Version 8 adds a pan to every patch: a pan= on the patch line, and a thirteenth
    // target a lock can name. An older file has neither and reads as a project whose
    // notes all sit in the centre, which is exactly where they were. The bump is for
    // the other direction, as it was last time: an older build would skip the key on a
    // patch line without a word but refuse a lock naming pan, and this is what turns
    // that into the message about a file from a newer version.
    //
    // Version 7 adds the two send effects: an fx line for what the reverb and the
    // delay are set to, and rsend= / dsend= on every patch for how much of a channel
    // reaches each. An older file needs no conversion — it has neither, so the sends
    // come back silent and the effects at their defaults, which is a file that sounds
    // exactly as it did. The bump is for the other direction: an older build would
    // refuse the fx line as an unknown keyword, and this is what turns that into the
    // message about a file from a newer version.
    //
    // Version 6 lets one lock take hold of any number of parameters, so its token
    // carries a run of key,value pairs rather than a single one. An older file needs
    // no conversion at all: one pair is one parameter engaged, which is what a lock
    // used to be.
    //
    // Version 5 drops the detune target and makes the carrier release one, so every
    // field of the patch is now something a lock can reach. An older file still
    // reads: its detune= is skipped the way any key nothing answers to is, and a
    // lock on detune is dropped rather than refused, since there is no longer a
    // parameter for it to move.
    //
    // Version 4 drops PACC, since a lock now reaches no further than the step it
    // sits in and there is nothing for one to accumulate into. An older file still
    // reads and its PACC tiles come back as PREL, which is the nearest surviving
    // meaning; what it cannot do is move a lock to where the new rules want it,
    // because a lock now colours the notes below it rather than the one above.
    //
    // Version 3 gives every channel its own timbre: the patch line takes a channel
    // number ahead of the parameters, and there is one line per channel.
    //
    // Version 2 was the two operator patch: the carrier ratio and the two full
    // ADSRs are gone, and a pitch envelope has arrived. A version 1 file still
    // reads, since a token nothing answers to is skipped, but the parameters that
    // no longer exist fall back to the default patch rather than being converted.
    public const int Version = 8;
    public const string Extension = ".jacquard";

    // Writing

    public static string Write(Project project)
    {
        var text = new StringBuilder();

        text.Append("jacquard ").Append(Version).Append('\n');
        text.Append("tempo ").Append(F(project.Tempo)).Append('\n');
        text.Append("meter ").Append(project.BeatsPerBar).Append(' ')
            .Append(project.BeatUnit).Append('\n');
        text.Append("fx ").Append(WriteFx(project.Fx)).Append('\n');
        // Every channel gets a line, whether anything plays on it or not: a regular
        // file is worth more here than a short one, and a bank of eight is small.
        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            text.Append("patch ").Append(channel).Append(' ')
                .Append(WritePatch(project.Patches[channel])).Append('\n');

        foreach (var lane in project.Score.Lanes) WriteLane(text, project.Score, lane);

        return text.ToString();
    }

    static void WriteLane(StringBuilder text, Score score, Lane lane)
    {
        text.Append("lane ").Append(lane.X).Append(' ').Append(lane.Y).Append(' ');

        if (lane.Channel is ChannelTile channel)
            text.Append("CHAN:").Append(channel.Channel)
                .Append(" div=").Append(channel.Division);
        else
        {
            text.Append("JDST");

            // Where the jump that reaches this lane currently sits. A branch lane
            // whose jump has gone missing is written without one and is read back
            // as unreachable rather than dropped.
            var source = lane.JumpSource == null ? null : score.Locate(lane.JumpSource);
            if (source.HasValue)
                text.Append(" from=").Append(source.Value.X).Append(',')
                    .Append(source.Value.Y);
        }

        text.Append('\n');

        foreach (var step in lane.Steps)
        {
            text.Append("  step");
            foreach (var tile in step.Tiles) text.Append(' ').Append(WriteTile(tile));
            text.Append('\n');
        }
    }

    static string WriteTile(Tile tile) => tile switch
    {
        NoteTile note => note.HasDefaultLength
          ? Pitch.ToName(note.Note)
          : Pitch.ToName(note.Note) + "/" + F(note.Length),
        AbsoluteParamTile p => "PABS" + WriteLock(p),
        RelativeParamTile p => "PREL" + WriteLock(p),
        CycleGateTile g => "GCYC:" + g.Period + "," + g.Index,
        ProbGateTile g => "GPRB:" + F(g.Percent),
        JumpTile => "JUMP",
        _ => tile.Token
    };

    // The parameters a lock has taken hold of, as key,value pairs. A lock holding
    // none of them writes as the bare token: there is nothing to say about it, and
    // a trailing colon would only look like something went missing.
    static string WriteLock(ParamTile tile)
    {
        var text = new StringBuilder();

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            if (!tile.IsEngaged(target)) continue;
            text.Append(text.Length == 0 ? ':' : ',')
                .Append(ParamTargets.Key(target)).Append(',').Append(F(tile[target]));
        }

        return text.ToString();
    }

    static string WritePatch(in FmPatch patch)
      => "level=" + F(patch.level) +
         " pan=" + F(patch.pan) +
         " gate=" + F(patch.gateScale) +
         " mratio=" + F(patch.modulatorRatio) +
         " index=" + F(patch.modulationIndex) +
         " fb=" + F(patch.feedback) +
         " md=" + F(patch.modulatorDecay) +
         " ca=" + F(patch.carrierAttack) +
         " cr=" + F(patch.carrierRelease) +
         " ps=" + F(patch.pitchSweep) +
         " pd=" + F(patch.pitchDecay) +
         " rsend=" + F(patch.reverbSend) +
         " dsend=" + F(patch.delaySend);

    static string WriteFx(in SendFx fx)
      => "rsize=" + F(fx.reverbSize) +
         " rdamp=" + F(fx.reverbDamp) +
         " rwidth=" + F(fx.reverbWidth) +
         " dbeats=" + F(fx.delayBeats) +
         " dfb=" + F(fx.delayFeedback) +
         " dtone=" + F(fx.delayTone) +
         " dspread=" + F(fx.delaySpread);

    static string F(float value)
      => value.ToString("0.#####", CultureInfo.InvariantCulture);

    // Reading

    public static Project Read(string text)
    {
        var project = new Project();
        var score = project.Score;

        Lane lane = null;

        // Resolved once every lane is in place, since a jump may well sit on a
        // lane that appears later in the file.
        var links = new List<(Lane lane, GridPoint point)>();

        var lines = text.Split('\n');

        for (var number = 0; number < lines.Length; number++)
        {
            var tokens = lines[number].Split(new[] { ' ', '\t', '\r' },
                                             StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens[0].StartsWith("#")) continue;

            switch (tokens[0])
            {
                case "jacquard":
                    if (tokens.Length > 1 && ReadInt(tokens[1]) > Version)
                        throw Fail(number, "file is from a newer version");
                    break;

                case "tempo":
                    project.Tempo = ReadFloat(Arg(tokens, 1, number));
                    break;

                case "meter":
                    project.BeatsPerBar = ReadInt(Arg(tokens, 1, number));
                    project.BeatUnit = ReadInt(Arg(tokens, 2, number));
                    break;

                case "fx":
                    ReadFx(ref project.Fx, tokens);
                    break;

                case "patch":
                    ReadPatchLine(project, tokens);
                    break;

                case "lane":
                    lane = ReadLane(score, tokens, number, links);
                    break;

                case "step":
                    if (lane == null) throw Fail(number, "step outside a lane");
                    ReadStep(lane.AddStep(), tokens, number);
                    break;

                default:
                    throw Fail(number, "unknown keyword " + tokens[0]);
            }
        }

        foreach (var (branch, point) in links)
            if (score.At(point).Tile is JumpTile jump) branch.JumpSource = jump;

        return project;
    }

    static Lane ReadLane(Score score, string[] tokens, int number,
                         List<(Lane, GridPoint)> links)
    {
        var x = ReadInt(Arg(tokens, 1, number));
        var y = ReadInt(Arg(tokens, 2, number));
        var head = Arg(tokens, 3, number);

        FlowTile tile;

        if (head.StartsWith("CHAN"))
        {
            var channel = new ChannelTile();
            var colon = head.IndexOf(':');
            if (colon >= 0) channel.Channel = ReadInt(head.Substring(colon + 1));
            tile = channel;
        }
        else if (head == "JDST")
            tile = new JumpDestTile();
        else
            throw Fail(number, "a lane head must be CHAN or JDST");

        var lane = score.AddLane(x, y, tile, 0);

        for (var i = 4; i < tokens.Length; i++)
        {
            var (key, value) = Split(tokens[i]);

            if (key == "div" && tile is ChannelTile ch)
                ch.Division = ReadInt(value);
            else if (key == "from")
                links.Add((lane, ReadPoint(value, number)));
        }

        return lane;
    }

    static void ReadStep(Step step, string[] tokens, int number)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            // A tile the synth has no answer for any more comes back as nothing, and
            // the step is simply one tile shorter than it was written with.
            var tile = ReadTile(tokens[i], number);
            if (tile != null) step.Tiles.Add(tile);
        }
    }

    static Tile ReadTile(string token, int number)
    {
        var colon = token.IndexOf(':');
        var head = colon < 0 ? token : token.Substring(0, colon);
        var args = colon < 0 ? "" : token.Substring(colon + 1);

        switch (head)
        {
            case "PABS": return ReadLock(new AbsoluteParamTile(), args, number);

            // A version 3 PACC becomes the relative lock it was a running total
            // of, which is as close as a file from before the change can get.
            case "PREL":
            case "PACC": return ReadLock(new RelativeParamTile(), args, number);

            case "GCYC":
            {
                var parts = args.Split(',');
                var gate = new CycleGateTile();
                if (parts.Length > 0) gate.Period = ReadInt(parts[0]);
                if (parts.Length > 1) gate.Index = ReadInt(parts[1]);
                return gate;
            }

            case "GPRB":
                return new ProbGateTile { Percent = ReadFloat(args) };

            case "JUMP": return new JumpTile();
        }

        // Anything else has to be a note, which is the one tile whose token is
        // its own value.
        var slash = token.IndexOf('/');
        var name = slash < 0 ? token : token.Substring(0, slash);

        if (!Pitch.TryParse(name, out var note))
            throw Fail(number, "cannot read the tile " + token);

        return new NoteTile
          { Note = note,
            Length = slash < 0 ? 1.0f : ReadFloat(token.Substring(slash + 1)) };
    }

    // Targets a file may still name that the synth no longer has. The pair naming
    // one is skipped, since there is nothing left for it to move; any other spelling
    // is one the format never had and stays an error.
    //
    // This is every key ParamTargets has ever dropped, which is what it has to be:
    // version 2 took the carrier's decay and sustain out and nothing was written here,
    // so a version 1 file with a lock on either was refused outright for four versions
    // instead of losing the lock. Only detune, dropped by version 5, was recorded at
    // the time. So a target leaving ParamTargets belongs in this list in the same
    // change — a file that cannot be opened at all is a worse answer than one that
    // opens with a lock nobody can move any more.
    static readonly string[] Retired = { "detune", "cardecay", "carsustain" };

    // A run of key,value pairs, or nothing at all for a lock that holds no
    // parameter. A version 5 token has exactly one pair, which reads here as the one
    // parameter it engaged.
    static ParamTile ReadLock(ParamTile tile, string args, int number)
    {
        if (args.Length == 0) return tile;

        var parts = args.Split(',');

        for (var i = 0; i < parts.Length; i += 2)
        {
            var target = ParamTargets.Parse(parts[i]);

            if (target < 0)
            {
                if (Array.IndexOf(Retired, parts[i]) < 0)
                    throw Fail(number, "unknown lock target " + parts[i]);

                continue;
            }

            tile.Engage(target, i + 1 < parts.Length ? ReadFloat(parts[i + 1]) : 0.0f);
        }

        // A lock that named only retired parameters has nothing left to do, so it
        // goes rather than staying on the plane as an empty one. A lock written
        // empty is a different thing and was returned above.
        return tile.IsEmpty ? null : tile;
    }

    // Which channel the line is for. A version 2 file has one patch line for the
    // whole project, so its first token is already a key=value pair; that line goes
    // into every channel, which is exactly what it used to mean.
    static void ReadPatchLine(Project project, string[] tokens)
    {
        if (tokens.Length > 1 && tokens[1].IndexOf('=') < 0)
        {
            var channel = PatchBank.Clamp(ReadInt(tokens[1]));
            ReadPatch(ref project.Patches[channel], tokens, 2);
            return;
        }

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            ReadPatch(ref project.Patches[channel], tokens, 1);
    }

    static void ReadPatch(ref FmPatch patch, string[] tokens, int from)
    {
        for (var i = from; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            var value = ReadFloat(text);

            switch (key)
            {
                case "level": patch.level = value; break;
                case "pan": patch.pan = value; break;
                case "gate": patch.gateScale = value; break;
                case "mratio": patch.modulatorRatio = value; break;
                case "index": patch.modulationIndex = value; break;
                case "fb": patch.feedback = value; break;
                case "md": patch.modulatorDecay = value; break;
                case "ca": patch.carrierAttack = value; break;
                case "cr": patch.carrierRelease = value; break;
                case "ps": patch.pitchSweep = value; break;
                case "pd": patch.pitchDecay = value; break;
                case "rsend": patch.reverbSend = value; break;
                case "dsend": patch.delaySend = value; break;
            }
        }
    }

    // The one fx line. Unknown keys are skipped and missing ones keep the default
    // the project was created with, which is the same tolerance a patch line gets:
    // a version 6 file has no line here at all and reads as a project whose effects
    // have never been touched.
    static void ReadFx(ref SendFx fx, string[] tokens)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            var value = ReadFloat(text);

            switch (key)
            {
                case "rsize": fx.reverbSize = value; break;
                case "rdamp": fx.reverbDamp = value; break;
                case "rwidth": fx.reverbWidth = value; break;
                case "dbeats": fx.delayBeats = value; break;
                case "dfb": fx.delayFeedback = value; break;
                case "dtone": fx.delayTone = value; break;
                case "dspread": fx.delaySpread = value; break;
            }
        }
    }

    // Token helpers

    static (string key, string value) Split(string token)
    {
        var equals = token.IndexOf('=');
        return equals < 0 ? (token, "")
               : (token.Substring(0, equals), token.Substring(equals + 1));
    }

    static string Arg(string[] tokens, int index, int number)
      => index < tokens.Length ? tokens[index]
         : throw Fail(number, "missing argument");

    static GridPoint ReadPoint(string text, int number)
    {
        var parts = text.Split(',');
        if (parts.Length != 2) throw Fail(number, "expected x,y");
        return new GridPoint(ReadInt(parts[0]), ReadInt(parts[1]));
    }

    static int ReadInt(string text)
      => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                      out var value) ? value : 0;

    static float ReadFloat(string text)
      => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var value) ? value : 0.0f;

    static FormatException Fail(int line, string message)
      => new FormatException("line " + (line + 1) + ": " + message);
}

} // namespace Jacquard
