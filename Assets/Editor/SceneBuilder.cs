using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

using Jacquard.App;

namespace Jacquard.Editor {

// Builds the one scene the prototype needs, so that it can be regenerated rather
// than hand-edited: a camera to hang the audio listener on, and the app driving a
// UIDocument. Everything on screen is built in code, so the UXML is only there to
// give the panel a root.

static class SceneBuilder
{
    const string ScenePath = "Assets/Main.unity";

    // The score the app opens on. Replacing it is a matter of writing another file
    // over this one — save the score from the app and copy it here — so the name is
    // worth having in one place, where the self test can read the same one.
    public const string StartupScorePath = "Assets/Jacquard/Scores/Startup.jacquard.txt";

    const string VisualizerShaderPath = "Assets/Jacquard/Visual/Visualizer.shader";

    // The wordmark on the transport row, cut from the same grid as the logo in the
    // README and the app icon; Branding/make_logo_png.py writes it.
    const string LogoPath = "Assets/Branding/Logo.png";

    // The face the whole interface is set in. Jura, under the Open Font License, which
    // is kept beside it. Google ships it as a variable font with a weight axis whose
    // default end is Light; what is checked in is the Regular instance cut out of it,
    // since the importer has no way to ask for a position on that axis and Light at
    // eight pixels on a dark ground is not what the chrome is written for.
    const string FontPath = "Assets/UI/Fonts/Jura-Regular.ttf";

    [MenuItem("Jacquard/Rebuild Main Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                               NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        // What the panel used to paint for itself. The interface is transparent over
        // this now, so this is the background of the whole app rather than the colour
        // behind a panel that covered it. Style.Background as a float, spelled out
        // rather than read, because Style is internal to the runtime assembly.
        camera.backgroundColor = new Color(0.086f, 0.086f, 0.086f);
        camera.orthographic = true;
        // The default layer, which is where the visualizer's mesh is drawn and the only
        // thing there is to see: this was zero for as long as there was nothing at all.
        camera.cullingMask = 1;
        cameraObject.AddComponent<AudioListener>();

        var appObject = new GameObject("Jacquard");

        var document = appObject.AddComponent<UIDocument>();
        document.panelSettings =
          AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultSettings.asset");
        document.visualTreeAsset =
          AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Main.uxml");

        var app = appObject.AddComponent<JacquardApp>();
        app.StartupScore = AssetDatabase.LoadAssetAtPath<TextAsset>(StartupScorePath);
        app.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        app.Font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        // Beside the app rather than on the camera: what it draws it reads off the
        // synth, and the shader is a reference because a shader nothing in a scene
        // points at is a shader that is not in the build.
        var visualizer = appObject.AddComponent<Visualizer>();
        visualizer.Shader = AssetDatabase.LoadAssetAtPath<Shader>(VisualizerShaderPath);
        // Down until something asks for it, which is the state everything the transport
        // row raises starts in. What asks is the System panel, which reads the setting
        // as it is built and hands it straight over — so this is the state of the first
        // frames rather than the default, and the default is written down there.
        visualizer.enabled = false;

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
          { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("Jacquard: scene rebuilt at " + ScenePath);
    }
}

} // namespace Jacquard.Editor
