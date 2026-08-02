using UnityEngine.UIElements;

namespace Jacquard.App {

// The project's timbre.
//
// The synth keeps no patch of its own, so this edits the one value that every note
// event is stamped from. The ten parameter lock targets are listed first, in their
// own order, because those are the ones a lock can reach — seeing them here is
// what makes a lock's amount mean something.

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

        Root.Add(Controls.Caption("Lock targets"));
        Root.Add(Controls.Divider());

        for (var target = 0; target < ParamTargets.Count; target++)
        {
            var index = target;
            Root.Add(Controls.Stepper(ParamTargets.Name(index),
                                      () => ParamTargets.Get(_editor.Project.Patch, index),
                                      value => Set(index, value),
                                      ParamTargets.Increment(index) * 5.0f));
        }

        Root.Add(Controls.Divider());
        Root.Add(Controls.Caption("Envelopes"));

        Root.Add(Stepper("Car ratio", () => _editor.Project.Patch.carrierRatio,
                         value => _editor.Project.Patch.carrierRatio = value, 0.25f));
        Root.Add(Stepper("Car release", () => _editor.Project.Patch.carrier.release,
                         value => _editor.Project.Patch.carrier.release = value, 0.02f));
        Root.Add(Stepper("Mod attack", () => _editor.Project.Patch.modulator.attack,
                         value => _editor.Project.Patch.modulator.attack = value, 0.005f));
        Root.Add(Stepper("Mod sustain", () => _editor.Project.Patch.modulator.sustain,
                         value => _editor.Project.Patch.modulator.sustain = value, 0.05f));
        Root.Add(Stepper("Mod release", () => _editor.Project.Patch.modulator.release,
                         value => _editor.Project.Patch.modulator.release = value, 0.02f));

        Root.Add(Controls.Divider());
        Root.Add(Controls.Push("Audition", () => _editor.Preview(60), 70));
    }

    public void Toggle()
      => Root.style.display = Root.style.display == DisplayStyle.None
         ? DisplayStyle.Flex : DisplayStyle.None;

    public bool IsOpen => Root.style.display != DisplayStyle.None;

    // Private members

    readonly ScoreEditor _editor;

    void Set(int target, float value)
    {
        ParamTargets.Set(ref _editor.Project.Patch, target, value);
        Changed();
    }

    VisualElement Stepper(string caption, System.Func<float> get,
                          System.Action<float> set, float step)
      => Controls.Stepper(caption, get,
                          value => { set(UnityEngine.Mathf.Max(value, 0.0f)); Changed(); },
                          step);

    // A channel that an absolute lock had already moved away from the patch is
    // reset, which is the only sane reading of the patch changing underneath.
    void Changed()
    {
        _editor.Sequencer?.RefreshPatch();
        _editor.Preview(60);
    }
}

} // namespace Jacquard.App
