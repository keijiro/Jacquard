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

    [MenuItem("Jacquard/Rebuild Main Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                               NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.086f, 0.086f, 0.102f);
        camera.orthographic = true;
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();

        var appObject = new GameObject("Jacquard");

        var document = appObject.AddComponent<UIDocument>();
        document.panelSettings =
          AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultSettings.asset");
        document.visualTreeAsset =
          AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Main.uxml");

        appObject.AddComponent<JacquardApp>();

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
          { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("Jacquard: scene rebuilt at " + ScenePath);
    }
}

} // namespace Jacquard.Editor
