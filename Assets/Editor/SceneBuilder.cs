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
        // behind a panel that covered it.
        camera.backgroundColor = new Color(0.086f, 0.086f, 0.102f);
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

        // Beside the app rather than on the camera: what it draws it reads off the
        // synth, and the shader is a reference because a shader nothing in a scene
        // points at is a shader that is not in the build.
        var visualizer = appObject.AddComponent<Visualizer>();
        visualizer.Shader = AssetDatabase.LoadAssetAtPath<Shader>(VisualizerShaderPath);

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
          { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("Jacquard: scene rebuilt at " + ScenePath);
    }
}

} // namespace Jacquard.Editor
