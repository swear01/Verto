using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhaseSandboxBuilder
{
    [MenuItem("PHASE/Create minimal sandbox")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PHASE"); var sandbox = root.AddComponent<PhaseSandbox>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(160, -90, -10); camera.orthographic = true; camera.orthographicSize = 90;
        sandbox.pixelCamera = camera;
        const string materialPath = "Assets/Phase/PixelSurface.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        sandbox.pixelMaterial = material;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        Add(root, "Floor", 0, 160, 320, 24, -1);
        Add(root, "Climb wall", 240, 80, 8, 80, -1);
        Add(root, "Red", 48, 136, 24, 8, 0);
        Add(root, "Blue", 88, 120, 24, 8, 1);
        Add(root, "Green", 128, 104, 24, 8, 2);
        var platform = new GameObject("Yellow platform"); platform.transform.SetParent(root.transform);
        var mover = platform.AddComponent<PhaseMovingPlatform>();
        mover.nodes = new[] { new Vector2Int(160, 144), new Vector2Int(192, 144) }; mover.returns = true;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/PhaseSandbox.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/PhaseSandbox.unity", true) };
        PlayerSettings.defaultScreenWidth = 960; PlayerSettings.defaultScreenHeight = 540;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        AssetDatabase.SaveAssets();
    }
    static void Add(GameObject root, string name, int x, int y, int w, int h, int phase)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = new Vector3(x, -y, 0);
        var terrain = go.AddComponent<PhaseTerrain>(); terrain.size = new Vector2Int(w, h); terrain.phase = phase;
    }
}
