using System;
using UnityEngine.UIElements;

namespace Jacquard.App {

// Twelve switches laid out as a keyboard: seven across the bottom and five sitting
// in the gaps above them, with the two gaps a keyboard does not have — E to F and B
// to C — left empty.
//
// That is the whole of the shape, and deliberately. What a player needs from it is
// to find a semitone without counting, which the two missing blacks give: they are
// what turn a run of twelve boxes into somewhere a hand already knows. Anything
// further — narrower blacks, a black overlapping the whites it sits between, a
// drawn key — would be a picture of a keyboard rather than a set of switches, and
// these are switches: a press here sets something, and a note that comes out of it
// is a remark about what was set rather than the box being played.
//
// No captions, for the reason a lap switch has none. Position is what a switch in a
// run means, and here the position is a pitch.
//
// What a press means is the caller's, and the two callers mean different things by
// it: on the Global panel a key is a toggle and what it toggles is whether the piece
// is allowed to land on that semitone, while on a note tile the twelve are one
// choice and the press is the pitch itself. Both want the same twelve boxes in the
// same places with some of them lit, and neither wants this to hold the answer — the
// scale is the project's and the pitch is the tile's. So nothing is kept here: lit is
// a question asked of the caller, once per degree, and Sync is how it is asked again.

sealed class Keys : VisualElement
{
    public Keys(Func<int, bool> lit, Action<int> press)
    {
        _lit = lit;

        var size = Controls.SwitchSize(WhiteKeys);
        var stride = size + Controls.Gap;

        // The two rows and the one gap between them, and then the gap below carried by
        // hand: the height here is explicit, so the bottom margin the white keys came
        // with has nothing left to push into and whatever stands under the keyboard
        // would otherwise be up against it.
        style.height = size * 2.0f + Controls.Gap;
        style.marginBottom = Controls.Gap;

        // The blacks first in the tree and absolutely placed, so that the whites
        // below them lay themselves out in a plain row and neither has to know
        // where the other is standing.
        var blacks = new VisualElement { style = { height = size } };
        blacks.style.position = Position.Absolute;
        blacks.style.left = 0;
        blacks.style.right = 0;
        blacks.style.top = 0;
        Add(blacks);

        var whites = new VisualElement();
        whites.style.flexDirection = FlexDirection.Row;
        whites.style.marginTop = size + Controls.Gap;
        Add(whites);

        for (var i = 0; i < WhiteKeys; i++)
        {
            var degree = White[i];
            _switches[degree] = Controls.Switch(WhiteKeys, () => press(degree));
            whites.Add(_switches[degree]);
        }

        for (var i = 0; i < BlackKeys; i++)
        {
            var degree = Black[i];
            var key = Controls.Switch(WhiteKeys, () => press(degree));

            // Half a step along from the white it follows, which is where the gap
            // between two of them is.
            key.style.position = Position.Absolute;
            key.style.left = stride * (BlackAfter[i] + 0.5f);
            key.style.top = 0;
            key.style.marginRight = 0;
            key.style.marginBottom = 0;

            blacks.Add(key);
            _switches[degree] = key;
        }

        Sync();
    }

    public void Sync()
    {
        for (var degree = 0; degree < Degrees; degree++)
            Controls.SetActive(_switches[degree], _lit(degree));
    }

    // Private members

    const int WhiteKeys = 7;
    const int BlackKeys = 5;

    // Counted off the two runs rather than written down as twelve, since the twelve
    // this holds is exactly the keys it stood up.
    const int Degrees = WhiteKeys + BlackKeys;

    static readonly int[] White = { 0, 2, 4, 5, 7, 9, 11 };
    static readonly int[] Black = { 1, 3, 6, 8, 10 };

    // Which white key each black one stands after, counted across the row: the
    // fourth and the seventh gaps are the ones nothing goes in.
    static readonly int[] BlackAfter = { 0, 1, 3, 4, 5 };

    readonly Func<int, bool> _lit;
    readonly Button[] _switches = new Button[Degrees];
}

} // namespace Jacquard.App
