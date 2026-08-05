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
//   jacquard 2
//   tempo 132
//   meter 4 4
//   patch level=0.8 index=3 ...
//   lane 1 1 CHAN:1 div=16
//     step C4/4 E4 G4
//     step
//     step GCYC:4,4 JUMP
//   lane 6 8 JDST from=10,2
//     step D#5 C5 G#4

public static class ProjectFormat
{
    // Version 2 is the two operator patch: the carrier ratio and the two full
    // ADSRs are gone, and a pitch envelope has arrived. A version 1 file still
    // reads, since a token nothing answers to is skipped, but the parameters that
    // no longer exist fall back to the default patch rather than being converted.
    public const int Version = 2;
    public const string Extension = ".jacquard";

    // Writing

    public static string Write(Project project)
    {
        var text = new StringBuilder();

        text.Append("jacquard ").Append(Version).Append('\n');
        text.Append("tempo ").Append(F(project.Tempo)).Append('\n');
        text.Append("meter ").Append(project.BeatsPerBar).Append(' ')
            .Append(project.BeatUnit).Append('\n');
        text.Append("patch ").Append(WritePatch(project.Patch)).Append('\n');

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
        AbsoluteParamTile p => "PABS:" + WriteLock(p),
        RelativeParamTile p => "PREL:" + WriteLock(p),
        AccumParamTile p => "PACC:" + WriteLock(p),
        CycleGateTile g => "GCYC:" + g.Period + "," + g.Index,
        ProbGateTile g => "GPRB:" + F(g.Percent),
        JumpTile => "JUMP",
        _ => tile.Token
    };

    static string WriteLock(ParamTile tile)
      => ParamTargets.Key(tile.Target) + "," + F(tile.Amount);

    static string WritePatch(in FmPatch patch)
      => "level=" + F(patch.level) +
         " detune=" + F(patch.detune) +
         " gate=" + F(patch.gateScale) +
         " mratio=" + F(patch.modulatorRatio) +
         " index=" + F(patch.modulationIndex) +
         " fb=" + F(patch.feedback) +
         " md=" + F(patch.modulatorDecay) +
         " ca=" + F(patch.carrierAttack) +
         " cr=" + F(patch.carrierRelease) +
         " ps=" + F(patch.pitchSweep) +
         " pd=" + F(patch.pitchDecay);

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

                case "patch":
                    ReadPatch(ref project.Patch, tokens);
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
            step.Tiles.Add(ReadTile(tokens[i], number));
    }

    static Tile ReadTile(string token, int number)
    {
        var colon = token.IndexOf(':');
        var head = colon < 0 ? token : token.Substring(0, colon);
        var args = colon < 0 ? "" : token.Substring(colon + 1);

        switch (head)
        {
            case "PABS": return ReadLock(new AbsoluteParamTile(), args, number);
            case "PREL": return ReadLock(new RelativeParamTile(), args, number);
            case "PACC": return ReadLock(new AccumParamTile(), args, number);

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

    static ParamTile ReadLock(ParamTile tile, string args, int number)
    {
        var parts = args.Split(',');
        var target = ParamTargets.Parse(parts[0]);

        if (target < 0) throw Fail(number, "unknown lock target " + parts[0]);

        tile.Target = target;
        tile.Amount = parts.Length > 1 ? ReadFloat(parts[1]) : 0.0f;

        return tile;
    }

    static void ReadPatch(ref FmPatch patch, string[] tokens)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            var value = ReadFloat(text);

            switch (key)
            {
                case "level": patch.level = value; break;
                case "detune": patch.detune = value; break;
                case "gate": patch.gateScale = value; break;
                case "mratio": patch.modulatorRatio = value; break;
                case "index": patch.modulationIndex = value; break;
                case "fb": patch.feedback = value; break;
                case "md": patch.modulatorDecay = value; break;
                case "ca": patch.carrierAttack = value; break;
                case "cr": patch.carrierRelease = value; break;
                case "ps": patch.pitchSweep = value; break;
                case "pd": patch.pitchDecay = value; break;
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
