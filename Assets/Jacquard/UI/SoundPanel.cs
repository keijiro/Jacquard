using UnityEngine.UIElements;

namespace Jacquard.App {

// One channel's timbre.
//
// The synth keeps no patch of its own, so this edits the value that every note
// event of that channel is stamped from. What it lists is the parameter lock targets
// in their own order, and that is the whole patch: seeing what a lock can reach, and
// where the channel currently sits inside each range, is what makes a lock's amount
// mean something.
//
// It stands up under the Tile panel while the cursor is on a CHAN tile and nowhere
// else, which is the same rule the Tile panel follows: a panel shows what the cursor
// is on. A timbre belongs to a channel and a CHAN tile is what a channel is on the
// plane, so the tile that names the sound is also the one that opens it. A JDST head
// shows nothing here, since a branch lane borrows its channel rather than owning one.
//
// This is why there is no channel chooser: the tile under the cursor decides which
// sound is being edited, and a chooser could only disagree with it. A channel with
// no lane of its own therefore cannot be edited, which costs nothing — it has no way
// of sounding either, and its patch is still saved and loaded with the rest.

sealed class SoundPanel
{
    public VisualElement Root { get; }

    public SoundPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Sound", out _title);

        _body = new VisualElement();
        Root.Add(_body);

        Refresh();
    }

    // Called when the cursor moves and when the score changes: a CHAN tile can be
    // renumbered under the panel, which switches the sound being edited just as
    // moving onto another one does.
    public void Refresh()
    {
        var channel = _editor.Selected is ChannelTile tile ? tile.Channel : 0;

        Root.style.display = channel > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        if (channel == 0) return;

        if (channel != _channel)
        {
            _channel = channel;
            Build();
            return;
        }

        // The patch can also have been replaced wholesale by a load, which leaves the
        // channel it is shown for unchanged, so the bars are pulled back in line with
        // the bank.
        ValueBar.SyncAll(_body);
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly Label _title;
    readonly VisualElement _body;

    int _channel;

    // A row is bound to the channel it was made for, so switching channels means
    // building the body again rather than nudging what is on it.
    void Build()
    {
        _body.Clear();

        // The subject of the panel is in the header, the way every other panel names
        // its own: the Tile panel says which tile it is showing, and this says which
        // channel's sound. It is also the only place the number appears now that there
        // is no chooser.
        _title.text = "Channel " + _channel + " Sound";

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var index = target;
            _body.Add(Controls.Bar(ParamTargets.Name(index), ParamRanges.Of(index),
                                   () => ParamTargets.Get(Patch, index),
                                   value => Set(index, value),
                                   Audition));
        }

        // On a row of its own rather than loose in the column, so that it leaves the
        // gap under it that every other row does and the panel closes on the same
        // inset it opened with — and on a foot rather than a plain row, since a button
        // that sounds a note is not the last of the parameters above it.
        var row = Controls.Foot();
        row.Add(Controls.Push("Audition", Audition, 70));
        _body.Add(row);
    }

    // The bank hands out a reference, which is what lets a field be written in
    // place and a lock target be pointed at.
    ref FmPatch Patch => ref _editor.Project.Patches[_channel];

    // Nothing to tell the sequencer either way: it reads the bank afresh every
    // instant, since a lock never outlives one.
    void Set(int target, float value) => ParamTargets.Set(ref Patch, target, value);

    // Sounded on the channel being edited, which is the one under the cursor.
    //
    // Every bar asks for this once its value has settled rather than at every value
    // it passes through, which is what makes a drag down a bar one note instead of a
    // burst of them. The button is the same note asked for on demand, for a parameter
    // that has been left where it was.
    void Audition() => _editor.Preview(60, _channel);
}

} // namespace Jacquard.App
