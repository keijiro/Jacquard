using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jacquard {

// The file format: a line of text per lane element, tokens separated by spaces.
//
// A token is not the data — sequencer-spec.md is explicit about that — so the file has
// its own spellings, chosen to be unambiguous rather than to look like a cell. A
// jump is recorded as the coordinate of the JUMP cell on the branch lane that
// answers to it, which is the same trick the mockup uses: there is nowhere to
// write a second jump, so one to one holds by construction.
//
//   jacquard 18
//   tempo 132
//   meter 4 4
//   fx rsize=0.5 rdamp=0.5 ...
//   limiter ceiling=0 attack=0.005 release=0.15
//   mutes muted=01000000 soloed=00000000
//   scale notes=101011010101
//   patch 1 transpose=0 level=-2 index=3 ...
//   lane 1 1 CHAN:1 div=16 on=1
//     step C4/4 E4 G4
//     step
//     step GCYC:4,0001 JUMP
//   lane 6 8 JDST from=10,2
//     step D#5 C5 G#4

public static class ProjectFormat
{
    // Version 18 makes a level a number of decibels rather than an amplitude: level= on
    // a patch line, and the level a lock names, now run from silence at -60 up to six
    // over full scale. What forced it is the relative lock. A shift had to be *added* to
    // an amplitude, so one and the same lock was five decibels off a loud channel and
    // silence off a quiet one, and a channel mixed low could not be accented downwards at
    // all — it was already against the floor. In decibels a shift is the ratio it sounds
    // like wherever it lands, which is the whole of the change: the rule that a relative
    // lock adds is untouched and only the space it adds in has moved.
    //
    // Three things in a file hold one, and all three are converted exactly. A patch
    // line's amplitude and an absolute lock's are the same logarithm, taken where they
    // are read. A relative lock holds a shift, which has no image on its own — but it has
    // one against the channel it sits on, and the file states that: 20 log10((base + x) /
    // base) is precisely the level the old lock arrived at. That one is LevelShifts,
    // after the whole file rather than at the token, because a branch lane's channel is
    // whichever lane jumps into it.
    //
    // The one thing deliberately not carried across is a shift that used to be clipped. A
    // lock asking for more than an amplitude of one got one, so what it *sounded* was the
    // clamp and not the number it was written with, and the conversion takes the sound —
    // the shift is measured against the level the note actually came out at. An older
    // piece therefore does not move at all, which is worth more than a number that never
    // sounded; the room over full scale is for what is written next.
    //
    // Version 17 stages the mix: the sum of the voices is scaled by a quarter on the way
    // out where it used to be scaled by four fifths, so that full scale is four notes
    // rather than one and there is room under it for a threshold to mean something. An
    // older file is converted by StagedThreshold rather than being read as it stands,
    // because a threshold is a level and everything reaching the limiter is now 10.1dB
    // lower than it was — the same number read against a mix that is no longer the same
    // size would be a different setting wearing the old one's spelling.
    //
    // Shifted by exactly that, the conversion is exact in both halves and an older piece
    // does not move at all. The make-up is the inverse of the threshold, so the quarter
    // is given straight back and the file returns at the level it had; and limiting
    // begins where the mix used to cross the old threshold, so it begins on the same note
    // of the same bar. That is the whole intent — the headroom is for what is written
    // next, and nothing is owed by what was written already.
    //
    // The shift is applied once, to the project rather than to the limiter line, which is
    // the one thing here worth being careful about: a file from before version 11 has no
    // such line and would otherwise keep a threshold at full scale against a mix a
    // quarter of the size, which is the one case where doing nothing is audible.
    //
    // Version 16 gives a channel start a switch: an on= on the lane line, saying whether
    // that lane runs at all. A file without one reads as a score where every lane runs,
    // which is what every lane in such a file did, so nothing about an older piece plays
    // differently for being read here.
    //
    // This bump matters more than most in the other direction. An older build skips an
    // unknown key on a lane line without a word, so it would read on=0 and play the lane
    // anyway — a file saying a part is silent, played with the part in it. Refusing the
    // file as being from a newer version is the only honest answer to that.
    //
    // Version 15 adds a unison to every patch: a uni= on the patch line, and a fourth
    // target a lock can name before the ones that shape the tone. An older file has
    // none and reads as a project where every note is a single voice, which is what
    // every note in one was, so nothing about it sounds different for being read here.
    // There is no conversion to make, because the setting that does nothing is the one
    // a missing key already falls back to. The bump is for the other direction, as it
    // was for the pan: an older build skips an unknown key on a patch line without a
    // word but refuses a lock naming unison, and this is what turns that into the
    // message about a file from a newer version.
    //
    // Version 14 adds the two things that decide which note a written note sounds as: a
    // scale line for the whole piece, and a transpose on each patch. A file without
    // either reads exactly as it did — a project starts chromatic, which is the scale
    // that does nothing, and a patch with no transpose= keeps the zero it is built with,
    // so nothing about an older piece moves. The bump is for the other direction, since
    // an older build meets an unknown keyword on the scale line and refuses the file.
    //
    // Both are one change because they are one feature: a transpose without a scale
    // walks a part out of its key, and a scale without a transpose has nothing to catch.
    //
    // Version 13 drops the limiter's drive and makes its ceiling automatic: the make-up
    // gain is now whatever the ceiling took off, so there is no drive= on the limiter
    // line any more. An older file is converted by LimiterSqueeze rather than having its
    // drive skipped, because the two numbers together said what one of them now says: a
    // drive of d into a ceiling c dB down squeezed the peaks by d - c, which is the
    // ceiling this build would write. What that conversion cannot preserve is the level,
    // and deliberately — the old pair left the output down at the ceiling and the make-up
    // is exactly the decision to stop doing that, so a converted project comes back the
    // same shape and |c| dB louder.
    //
    // Version 12 saves the mutes: a mutes line carrying a digit per channel for what is
    // silenced and another for what is soloed. An older file has none and reads as a
    // project with nothing held back, which is where a load used to leave the switches
    // anyway, so nothing about such a file sounds different for being read here. Both
    // sets are written even though only one of them is ever consulted, for the reason
    // ChannelMutes gives: the mutes under a solo are what dropping it gives back. The
    // bump is for the other direction, as it has been for every line added here.
    //
    // Version 11 adds the limiter across the finished mix: a limiter line for the four
    // numbers it holds. An older file has none, and reads as a project whose limiter has
    // never been touched — which is a limiter with no drive under a ceiling at full
    // scale, so the file sounds exactly as it did. That promise now runs through version
    // 17's shift rather than through doing nothing, since the mix under the ceiling
    // changed size; what it means has not. The bump is for the other direction, as it
    // has been for every line added here: an older build would refuse the keyword
    // outright, and this is what turns that into the message about a file from a newer
    // version.
    //
    // Version 10 makes the FM decay a slope rather than a length of time, so md= on a
    // patch line and an absolute lock naming moddecay are converted on the way in by
    // DecaySlope, and an older file keeps the modulation it had. A relative lock is
    // left alone: it holds a shift rather than a value, there is no image of a shift
    // under a curve, and it needs none — the old parameter ran over the same span of
    // numbers as the new one, so a shift reaches exactly as far across its bar as it
    // did. The bump is what makes any of this possible: nothing about the number
    // itself says which of the two things it is.
    //
    // Version 9 gives a cycle gate a switch per lap rather than one lap picked by
    // number, so its second argument is a run of digits as long as the period. An
    // older file needs no conversion and is not even a special case at the reading
    // end: a lap number is one digit and a pattern is at least two, so the two
    // spellings tell themselves apart and a version 8 GCYC:4,3 reads as the 0010 it
    // always meant. The bump is for the other direction, and for the period, which
    // now reaches 32 where an older build clamps it to 8.
    //
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
    public const int Version = 18;
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
        text.Append("limiter ").Append(WriteLimiter(project.Limiter)).Append('\n');
        text.Append("mutes ").Append(WriteMutes(project.Mutes)).Append('\n');
        text.Append("scale ").Append(WriteScale(project.Scale)).Append('\n');
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
                .Append(" div=").Append(channel.Division)
                .Append(" on=").Append(channel.Enabled ? 1 : 0);
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
        CycleGateTile g => "GCYC:" + g.Period + "," + g.Pattern,
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
      => "transpose=" + F(patch.transpose) +
         " level=" + F(patch.level) +
         " pan=" + F(patch.pan) +
         " uni=" + F(patch.unison) +
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

    static string WriteLimiter(in Limiter limiter)
      => "ceiling=" + F(limiter.ceiling) +
         " attack=" + F(limiter.attack) +
         " release=" + F(limiter.release);

    // A digit per channel for each of the two sets, which is the spelling a cycle gate's
    // laps already use: a run as long as the thing it is a switch per, so which channel
    // a digit is for is its position and nothing has to be numbered. Written in full
    // whether anything is held back or not, for the reason every channel gets a patch
    // line.
    static string WriteMutes(ChannelMutes mutes)
    {
        var text = new StringBuilder("muted=");

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            text.Append(mutes.IsMuted(channel) ? '1' : '0');

        text.Append(" soloed=");

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            text.Append(mutes.IsSoloed(channel) ? '1' : '0');

        return text.ToString();
    }

    // A digit per semitone, C first, in the same spelling for the same reason: which
    // note a digit is for is where it stands. Written in full even when every one of
    // them is on, which is a scale that does nothing — a file says what it is set to
    // rather than only what is unusual about it.
    static string WriteScale(Scale scale)
    {
        var text = new StringBuilder("notes=");

        for (var degree = 0; degree < Scale.Degrees; degree++)
            text.Append(scale.Allows(degree) ? '1' : '0');

        return text.ToString();
    }

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

        // What the file was written at, which decides whether a value needs converting
        // on the way in. The version line is the first one a file has, so it is settled
        // before anything that reads it; a fragment without one is taken as current,
        // since a file that old would have said so.
        var version = Version;

        for (var number = 0; number < lines.Length; number++)
        {
            var tokens = lines[number].Split(new[] { ' ', '\t', '\r' },
                                             StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens[0].StartsWith("#")) continue;

            switch (tokens[0])
            {
                case "jacquard":
                    if (tokens.Length > 1) version = ReadInt(tokens[1]);
                    if (version > Version)
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

                case "limiter":
                    ReadLimiter(ref project.Limiter, tokens, version);
                    break;

                case "mutes":
                    ReadMutes(project.Mutes, tokens);
                    break;

                case "scale":
                    ReadScale(project.Scale, tokens);
                    break;

                case "patch":
                    ReadPatchLine(project, tokens, version);
                    break;

                case "lane":
                    lane = ReadLane(score, tokens, number, links);
                    break;

                case "step":
                    if (lane == null) throw Fail(number, "step outside a lane");
                    ReadStep(lane.AddStep(), tokens, number, version);
                    break;

                default:
                    throw Fail(number, "unknown keyword " + tokens[0]);
            }
        }

        foreach (var (branch, point) in links)
            if (score.At(point).Tile is JumpTile jump) branch.JumpSource = jump;

        // Last, and to the project rather than to the line it usually came in on, so
        // that a file with no limiter line is shifted along with one that has it. See
        // version 17 above.
        if (version < 17)
            project.Limiter.ceiling = StagedThreshold(project.Limiter.ceiling);

        // After the links above, and it has to be: a relative shift on the level is
        // converted against the channel the lane sounds on, and a branch lane borrows
        // that from whichever jump reaches it.
        if (version < 18) LevelShifts(project);

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
            // A file from before this key says nothing, and a lane that says nothing
            // runs — which is what every lane in such a file did.
            else if (key == "on" && tile is ChannelTile on)
                on.Enabled = ReadInt(value) != 0;
            else if (key == "from")
                links.Add((lane, ReadPoint(value, number)));
        }

        return lane;
    }

    static void ReadStep(Step step, string[] tokens, int number, int version)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            // A tile the synth has no answer for any more comes back as nothing, and
            // the step is simply one tile shorter than it was written with.
            var tile = ReadTile(tokens[i], number, version);
            if (tile != null) step.Tiles.Add(tile);
        }
    }

    // version reaches only as far as the locks: they are the one kind of tile that
    // carries a synth parameter's own value, and so the one kind a change of units
    // under the synth can leave holding the wrong number.
    static Tile ReadTile(string token, int number, int version)
    {
        var colon = token.IndexOf(':');
        var head = colon < 0 ? token : token.Substring(0, colon);
        var args = colon < 0 ? "" : token.Substring(colon + 1);

        switch (head)
        {
            case "PABS": return ReadLock(new AbsoluteParamTile(), args, number, version);

            // A version 3 PACC becomes the relative lock it was a running total
            // of, which is as close as a file from before the change can get.
            case "PREL":
            case "PACC":
                return ReadLock(new RelativeParamTile(), args, number, version);

            case "GCYC":
            {
                var parts = args.Split(',');
                var gate = new CycleGateTile();
                if (parts.Length > 0) gate.Period = ReadInt(parts[0]);
                if (parts.Length > 1) ReadLaps(gate, parts[1]);
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

    // The laps a cycle gate fires on, in either of the two spellings the format has
    // had. A run of digits as long as the period is the pattern version 9 writes;
    // anything else is the single lap a version 8 file names by number, which is the
    // same gate with one switch on. Nothing has to know which version it came from,
    // since the shortest period is two and the longest lap number is one digit.
    static void ReadLaps(CycleGateTile gate, string text)
    {
        if (text.Length == gate.Period && IsPattern(text))
        {
            gate.Pattern = text;
            return;
        }

        gate.Pattern = "";
        gate.SetFires(ReadInt(text), true);
    }

    static bool IsPattern(string text)
    {
        foreach (var c in text) if (c != '0' && c != '1') return false;
        return true;
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
    static ParamTile ReadLock(ParamTile tile, string args, int number, int version)
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

            var value = i + 1 < parts.Length ? ReadFloat(parts[i + 1]) : 0.0f;

            // An absolute lock holds the parameter itself, so version 10's change of
            // units reaches it exactly as it reaches the patch line. A relative one
            // holds a shift and is left as written, for the reason given up at the
            // version note.
            if (version < 10 && target == ParamTargets.ModDecay &&
                tile is AbsoluteParamTile) value = DecaySlope(value);

            // And version 18's, which reaches an absolute lock the same way and for the
            // same reason. A relative one is left as written here and converted once the
            // file is read, since what it is worth in decibels depends on the level of
            // the channel it stands on. See LevelShifts.
            if (version < 18 && target == ParamTargets.Level &&
                tile is AbsoluteParamTile) value = Decibels(value);

            tile.Engage(target, value);
        }

        // A lock that named only retired parameters has nothing left to do, so it
        // goes rather than staying on the plane as an empty one. A lock written
        // empty is a different thing and was returned above.
        return tile.IsEmpty ? null : tile;
    }

    // Which channel the line is for. A version 2 file has one patch line for the
    // whole project, so its first token is already a key=value pair; that line goes
    // into every channel, which is exactly what it used to mean.
    static void ReadPatchLine(Project project, string[] tokens, int version)
    {
        if (tokens.Length > 1 && tokens[1].IndexOf('=') < 0)
        {
            var channel = PatchBank.Clamp(ReadInt(tokens[1]));
            ReadPatch(ref project.Patches[channel], tokens, 2, version);
            return;
        }

        for (var channel = 1; channel <= PatchBank.Channels; channel++)
            ReadPatch(ref project.Patches[channel], tokens, 1, version);
    }

    // A version 9 FM decay in seconds as the slope version 10 holds in its place.
    //
    // The old parameter was the time the modulation took to reach zero, along a curve
    // that spent five e-foldings getting there, so its time constant was a fifth of it.
    // The new one is a plain exponential whose time constant is a tenth of a second at
    // the middle of its travel and v / (1 - v) tenths elsewhere. Equating the two gives
    // this, so a converted patch decays at the rate it always did rather than near it —
    // to within the hundredth the old curve's normalization moved it by.
    //
    // The two numbers are written out rather than read off the synth on purpose. This
    // says what version 9 meant, and has to keep saying it however the slope is tuned
    // afterwards.
    static float DecaySlope(float seconds) => seconds / (5.0f * 0.1f + seconds);

    static void ReadPatch(ref FmPatch patch, string[] tokens, int from, int version)
    {
        for (var i = from; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            var value = ReadFloat(text);

            switch (key)
            {
                case "transpose": patch.transpose = value; break;
                // A version 17 amplitude as the decibels version 18 holds in its
                // place. Zero is a channel that was silent and stays silent, which is
                // the bottom of the new range rather than the bottom of a logarithm.
                case "level":
                    patch.level = version < 18 ? Decibels(value) : value;
                    break;
                case "pan": patch.pan = value; break;
                case "uni": patch.unison = value; break;
                case "gate": patch.gateScale = value; break;
                case "mratio": patch.modulatorRatio = value; break;
                case "index": patch.modulationIndex = value; break;
                case "fb": patch.feedback = value; break;
                case "md":
                    patch.modulatorDecay = version < 10 ? DecaySlope(value) : value;
                    break;
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

    // The one limiter line, read with the same tolerance the fx line gets: a missing
    // key keeps the default, which for a version 10 file is every one of them.
    //
    // The drive is the one retired key in this project that is read rather than skipped,
    // because it is not a parameter that went away — it is half of a number that is still
    // here. Held until the line is finished, since the keys arrive in whatever order they
    // were written in and a conversion cannot be done on the first of the two to turn up.
    static void ReadLimiter(ref Limiter limiter, string[] tokens, int version)
    {
        var drive = 0.0f;

        for (var i = 1; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            var value = ReadFloat(text);

            switch (key)
            {
                case "drive": drive = value; break;
                case "ceiling": limiter.ceiling = value; break;
                case "attack": limiter.attack = value; break;
                case "release": limiter.release = value; break;
            }
        }

        if (version < 13) limiter.ceiling = LimiterSqueeze(limiter.ceiling, drive);
    }

    // A version 12 drive and ceiling as the one ceiling that now stands for both.
    //
    // The old pair pushed the mix d dB up into a ceiling c dB down and left the output
    // sitting there, so what the peaks were squeezed by was d - c: everything the drive
    // added, plus everything the ceiling was already below full scale by. That figure is
    // what the ceiling holds on its own now, with the make-up gain putting the output
    // back at full scale — so a converted file keeps its shape exactly and comes back
    // |c| dB louder, which is the change rather than a side effect of it.
    //
    // A pair that reaches further than the bar now does comes back at the end of it. That
    // is a real loss of squeeze and it takes a drive of more than 48dB over the ceiling
    // to reach: past there the old limiter was flattening everything to a constant, and
    // there is no number here that says so.
    static float LimiterSqueeze(float ceiling, float drive)
      => Math.Clamp(ceiling - drive, Limiter.MinCeiling, 0.0f);

    // A version 16 threshold as the one this build would write.
    //
    // The whole of the conversion is that the mix in front of it shrank. Four fifths to
    // a quarter is 10.1dB, and a threshold says where in a mix limiting begins, so it has
    // to come down by precisely the same amount to go on meaning the same place. Two
    // things follow and neither is a compromise: the make-up is derived from the
    // threshold rather than set beside it, so the 10.1dB comes back and the piece is as
    // loud as it was; and the gain reduction at every instant is what it was, so the
    // shape is untouched as well. An older file is not approximated here — it is
    // reproduced.
    //
    // A file that reached the bottom of the bar cannot come down any further and stays
    // there. That is a real loss of squeeze and it takes a project already at 38dB of it,
    // which is deep into the end of the travel where the gain has stopped articulating
    // anything at all.
    //
    // The number is written down rather than derived from FmSynth.MasterGain, and has to
    // be: it is the ratio between what that constant was at version 16 and what it became
    // at 17, which is a fact about two builds and not about the current one. A later
    // change to the staging is a later version with a shift of its own.
    const float StagedHeadroom = 10.103f; // 20 log10(0.8 / 0.25)

    // A version 17 amplitude as the level version 18 spells it, with the silent end
    // landing on the bottom of the range rather than running off the logarithm.
    static float Decibels(float amplitude)
      => amplitude <= 0.0f ? FmPatch.MinLevel : 20.0f * MathF.Log10(amplitude);

    // Every version 17 relative lock on the level, as the shift in decibels that does
    // what it did.
    //
    // The shift is read against the channel's own level, which is the only thing it can
    // be read against: an amplitude added to a level is a ratio that depends on the level
    // it was added to. Where the sum ran past the ends the old synth clamped it, so the
    // clamp is applied here too and what is converted is what was heard — an amplitude of
    // one where more was asked for, and silence where the shift took the level under
    // zero, which the bottom of the range holds exactly.
    //
    // The channel's level has already been converted by the time this runs, so it is
    // turned back into the amplitude the shift was written against rather than being
    // remembered from the line.
    static void LevelShifts(Project project)
    {
        var score = project.Score;

        foreach (var lane in score.Lanes)
        {
            var basis = FmPatch.Amplitude(project.Patches[score.ChannelOf(lane)].level);

            foreach (var step in lane.Steps)
                foreach (var tile in step.Tiles)
                    if (tile is RelativeParamTile shift &&
                        shift.IsEngaged(ParamTargets.Level))
                        shift.Engage(ParamTargets.Level,
                                     LevelShift(basis, shift[ParamTargets.Level]));
        }
    }

    // What one of those shifts is worth: the ratio between the level the channel came out
    // at with it and the level it stands at without it.
    static float LevelShift(float basis, float shift)
      => Decibels(Math.Clamp(basis + shift, 0.0f, 1.0f)) - Decibels(basis);

    static float StagedThreshold(float ceiling)
      => Math.Clamp(ceiling - StagedHeadroom, Limiter.MinCeiling, 0.0f);

    // The one mutes line, read with the tolerance the two lines above it get. A key
    // nothing answers to is skipped, a missing one leaves its whole set where the
    // project was created with it, and a run of digits shorter than the bank leaves the
    // channels past its end alone — so a hand written `mutes muted=1` silences the first
    // channel and says nothing about the other seven. Anything but a 1 is off, which is
    // what makes a run of the wrong length safe rather than an error worth refusing a
    // file over.
    static void ReadMutes(ChannelMutes mutes, string[] tokens)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);

            var muted = key == "muted";
            if (!muted && key != "soloed") continue;

            for (var channel = 1;
                 channel <= PatchBank.Channels && channel <= text.Length; channel++)
            {
                var on = text[channel - 1] == '1';
                if (muted) mutes.SetMuted(channel, on); else mutes.SetSoloed(channel, on);
            }
        }
    }

    // The scale line, read with the same tolerance the mutes line gets: an unknown key
    // is skipped and a run shorter than twelve leaves the degrees past its end where the
    // project was created with them, which is on. So a hand written `scale notes=0`
    // takes out the C and says nothing about the other eleven.
    static void ReadScale(Scale scale, string[] tokens)
    {
        for (var i = 1; i < tokens.Length; i++)
        {
            var (key, text) = Split(tokens[i]);
            if (key != "notes") continue;

            for (var degree = 0;
                 degree < Scale.Degrees && degree < text.Length; degree++)
                scale.SetAllowed(degree, text[degree] == '1');
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

    // A number that is not one reads as nothing rather than as itself. NumberStyles
    // .Float accepts "NaN" and "Infinity" as gladly as it accepts a digit, and a file is
    // the widest way into this program: everything else on the way in is a clamp, and a
    // clamp lets a NaN through untouched. See ParamTargets.Set for what one costs.
    static float ReadFloat(string text)
      => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var value) && float.IsFinite(value) ? value : 0.0f;

    static FormatException Fail(int line, string message)
      => new FormatException("line " + (line + 1) + ": " + message);
}

} // namespace Jacquard
