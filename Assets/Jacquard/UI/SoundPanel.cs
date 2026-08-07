using UnityEngine;
using UnityEngine.UIElements;

namespace Jacquard.App {

// One channel's timbre.
//
// The synth keeps no patch of its own, so this edits the value that every note
// event of that channel is stamped from. The ten parameter lock targets are listed
// first, in their own order, because those are the ones a lock can reach — seeing
// them here is what makes a lock's amount mean something.
//
// Which channel is shown follows the cursor onto a lane, so a tweak lands on the
// sound of the lane being worked on rather than on whichever channel happened to be
// open. The chooser is still there, for a channel that has no lane yet.
//
// There is no explanatory hint at the foot of this panel, unlike the others: the
// list of parameters is long enough that the panel is already close to as tall as a
// small window can show, and what a channel's timbre is stands on the CHAN tile's
// own hint instead.

sealed class SoundPanel
{
    public VisualElement Root { get; }

    public SoundPanel(ScoreEditor editor)
    {
        _editor = editor;

        Root = Controls.Panel("Sound", () => Root.style.display = DisplayStyle.None);
        Root.style.right = 12;
        Root.style.bottom = 12;
        Root.style.display = DisplayStyle.None;

        _body = new VisualElement();
        Root.Add(_body);

        _channel = _editor.Channel;
        Build();
    }

    public void Toggle()
      => Root.style.display = Root.style.display == DisplayStyle.None
         ? DisplayStyle.Flex : DisplayStyle.None;

    public bool IsOpen => Root.style.display != DisplayStyle.None;

    // Called when the cursor moves and when the score changes: a CHAN tile can be
    // renumbered under the panel, which switches the sound being edited just as
    // moving onto another lane does.
    //
    // Empty ground away from every lane leaves the panel alone rather than sending
    // it back to channel one, so a channel picked by hand — the reason the chooser
    // exists at all — survives a click that was not aimed at a lane.
    public void Refresh()
    {
        // The patch on screen can also have been replaced wholesale by a load, which
        // leaves the channel it is shown for unchanged, so the bars are pulled back in
        // line with the bank before anything else.
        ValueBar.SyncAll(_body);

        if (_editor.SelectedLane == null) return;

        var channel = _editor.Channel;
        if (channel == _channel) return;

        _channel = channel;
        Build();
    }

    // Private members

    readonly ScoreEditor _editor;
    readonly VisualElement _body;

    int _channel;

    // A row is bound to the channel it was made for, so switching channels means
    // building the body again rather than nudging what is on it.
    void Build()
    {
        _body.Clear();

        _body.Add(Controls.Chooser("Channel", ChannelNames, () => _channel - 1,
                                   index => { _channel = index + 1; Build(); }));

        _body.Add(Controls.Caption("Lock targets"));
        _body.Add(Controls.Divider());

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var index = target;
            _body.Add(Controls.Bar(ParamTargets.Name(index), ParamRanges.Of(index),
                                   () => ParamTargets.Get(Patch, index),
                                   value => Set(index, value)));
        }

        _body.Add(Controls.Divider());
        _body.Add(Controls.Caption("Not lockable"));

        // The carrier's release is all that is left over: every other field of the
        // flattened patch is a lock target, so it is the one thing a step cannot
        // reach and the panel is the only place to set it.
        _body.Add(Controls.Bar("Car release", ParamRanges.CarrierRelease,
                               () => Patch.carrierRelease,
                               value => { Patch.carrierRelease = Mathf.Max(value, 0.0f);
                                          Changed(); }));

        _body.Add(Controls.Divider());
        _body.Add(Controls.Push("Audition", Audition, 70));
    }

    // The bank hands out a reference, which is what lets a field be written in
    // place and a lock target be pointed at.
    ref FmPatch Patch => ref _editor.Project.Patches[_channel];

    void Set(int target, float value)
    {
        ParamTargets.Set(ref Patch, target, value);
        Changed();
    }

    // Nothing to tell the sequencer: it reads the bank afresh every instant, since
    // a lock never outlives one.
    void Changed() => Audition();

    // Through the channel being edited, not the one the cursor is on: the two are
    // the same until the chooser is used.
    void Audition() => _editor.Preview(60, _channel);

    static readonly string[] ChannelNames = NewChannelNames();

    static string[] NewChannelNames()
    {
        var names = new string[PatchBank.Channels];
        for (var i = 0; i < names.Length; i++) names[i] = "Ch " + (i + 1);
        return names;
    }
}

} // namespace Jacquard.App
