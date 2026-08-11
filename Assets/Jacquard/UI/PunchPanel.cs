using System;
using UnityEngine.UIElements;

namespace Jacquard.App {

// The twelve punch-in effects, on twelve buttons that are on only while they are held.
//
// The other four panels set something and leave it set. This one sets nothing: what a
// button here does lasts exactly as long as a finger is on it, which is why they are
// held rather than toggled and why there is no state on this panel for a load or a
// cursor to disagree with. It never has to be refreshed.
//
// It stands along the bottom edge, in the middle, and not in either column. The
// columns are where things are read — the cursor's panels on the right saying what the
// score holds, the project's on the left — and reading is done at arm's length from
// what is being said. This is played, so it goes where a hand already is: two thumbs
// on a tablet held in both hands reach the bottom corners and nothing else, and a
// panel across the bottom is the only place on this screen both of them can be.
//
// Six columns of two, and the top of a column is always the smaller of the pair: the
// two sends, the short gate over the long one, down over up, falling over rising, and
// then the four rolls in length order, reading down each column and then across. So
// where a button is says what it does before the word on it does.
//
// The names are what a player calls these and not what the code does. Stab is a gate
// of a tenth of a step with the tail cut back to match, Sustain is both doubled, Rise
// and Fall are a semitone a step in either direction. Reverb and Delay name what
// receives, the same way the Send FX button does: a send is what a channel does, and
// what these two throw everything into is the effect.
//
// The rolls are named by their length, because the length is the only thing that tells
// them apart and a note value is the thing itself rather than a code standing in for
// one — the same reason a note tile shows A4. Roll and not Loop, since a lane already
// loops at its terminator and that is a different thing entirely; a roll is the one
// that is held.

sealed class PunchPanel
{
    public VisualElement Root { get; }

    // released is what hands the keyboard back to the plane. It is the panel's caller
    // that knows where focus belongs, and the lift is the only moment it can be given
    // back — a press is settled by the focus controller after this sees it.
    public PunchPanel(PunchFx punch, Func<long> clock, Action released)
    {
        (_punch, _clock, _released) = (punch, clock, released);

        Root = Controls.Panel("Punch-in FX");

        // As wide as the two rows it holds and no wider. Every other panel is one
        // column of rows, so PanelWidth is the answer for all of them; this one is
        // five buttons across, and letting it measure itself is what keeps the inset
        // equal on both sides without this having to know what a border costs.
        Root.style.width = StyleKeyword.Auto;

        Root.Add(Controls.Divider());

        Root.Add(Buttons(PunchEffect.Reverb, PunchEffect.Stab, PunchEffect.OctaveDown,
                         PunchEffect.Fall, PunchEffect.Roll1, PunchEffect.Roll3));

        Root.Add(Buttons(PunchEffect.Delay, PunchEffect.Sustain, PunchEffect.OctaveUp,
                         PunchEffect.Rise, PunchEffect.Roll2, PunchEffect.Roll4));
    }

    // Private members

    readonly PunchFx _punch;
    readonly Func<long> _clock;
    readonly Action _released;

    // Wide enough for Roll 3/16, which is the longest label here.
    const float ButtonWidth = 70.0f;

    VisualElement Buttons(params PunchEffect[] effects)
    {
        var row = Controls.Row();

        foreach (var fx in effects)
            row.Add(Controls.Hold(Name(fx),
                                  () => _punch.Press(fx, _clock()),
                                  () => { _punch.Release(fx); _released(); },
                                  ButtonWidth));

        // The last button in a row carries a gap to its right that has nothing on the
        // other side of it, and the panel is fitted to its contents, so it would show
        // up as a right inset a gap wider than the left one.
        row.ElementAt(row.childCount - 1).style.marginRight = 0;

        return row;
    }

    static string Name(PunchEffect fx)
      => fx switch
        { PunchEffect.Reverb => "Reverb",
          PunchEffect.Delay => "Delay",
          PunchEffect.Stab => "Stab",
          PunchEffect.Sustain => "Sustain",
          // A plain hyphen and plus, which is what the steppers already use: the
          // typographic pair would be the only two glyphs on the chrome that are not
          // on a keyboard.
          PunchEffect.OctaveDown => "Oct -",
          PunchEffect.OctaveUp => "Oct +",
          PunchEffect.Fall => "Fall",
          PunchEffect.Rise => "Rise",
          // Reduced, since that is how a note value is written everywhere else and
          // 2/16 is not one. Three sixteenths has no shorter spelling of its own.
          PunchEffect.Roll1 => "Roll 1/16",
          PunchEffect.Roll2 => "Roll 1/8",
          PunchEffect.Roll3 => "Roll 3/16",
          _ => "Roll 1/4" };
}

} // namespace Jacquard.App
