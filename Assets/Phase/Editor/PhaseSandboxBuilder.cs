using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhaseSandboxBuilder
{
    [MenuItem("PHASE/Create gear demo")]
    public static void CreateGearDemo()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PHASE"); var sandbox = root.AddComponent<PhaseSandbox>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 90;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        sandbox.pixelCamera = camera;
        sandbox.pixelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Phase/PixelSurface.mat");
        Add(root, "Safe approach", 0, 160, 88, 24, -1);
        Add(root, "Blue recovery", 208, 136, 24, 8, 1);
        Add(root, "Green landing", 208, 80, 24, 8, 2);
        Add(root, "Red landing", 256, 56, 24, 8, 0);
        Add(root, "Rest", 296, 32, 24, 8, -1);
        var drive = new GameObject("White gear drive"); drive.transform.SetParent(root.transform);
        drive.transform.position = new Vector3(72, -104, 0);
        var gear = drive.AddComponent<PhaseClimbGear>();
        gear.wallSize = new Vector2Int(8, 56); gear.wallTop = 64; gear.platformEndX = 144;
        var moving = new GameObject("Dash recovery platform"); moving.transform.SetParent(root.transform);
        var mover = moving.AddComponent<PhaseMovingPlatform>();
        mover.size = new Vector2Int(24, 8); mover.nodes = new[] { new Vector2Int(232, 112), new Vector2Int(264, 112) }; mover.returns = true;
        const string path = "Assets/Scenes/PhaseGearDemo.unity";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
    [MenuItem("PHASE/Create minimal sandbox")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateRoom(false);
        CreateRoom(true);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/PhaseSandbox.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/PhaseRoom2.unity", true)
        };
        EditorSceneManager.OpenScene("Assets/Scenes/PhaseSandbox.unity");
        PlayerSettings.defaultScreenWidth = 960; PlayerSettings.defaultScreenHeight = 540;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        AssetDatabase.SaveAssets();
    }
    static void CreateRoom(bool second)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PHASE"); var sandbox = root.AddComponent<PhaseSandbox>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(160, -90, -10); camera.orthographic = true; camera.orthographicSize = 90;
        sandbox.pixelCamera = camera;
        if (second) sandbox.leftScene = "PhaseSandbox"; else sandbox.rightScene = "PhaseRoom2";
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
        Add(root, "Climb wall", second ? 80 : 240, second ? 96 : 80, 8, second ? 64 : 80, -1);
        Add(root, "Red", second ? 120 : 48, second ? 128 : 136, 24, 8, 0);
        Add(root, "Blue", second ? 160 : 88, second ? 112 : 120, 24, 8, 1);
        Add(root, "Green", second ? 200 : 128, second ? 96 : 104, 24, 8, 2);
        var platform = new GameObject("Yellow platform"); platform.transform.SetParent(root.transform);
        var mover = platform.AddComponent<PhaseMovingPlatform>();
        mover.nodes = second ? new[] { new Vector2Int(248, 144), new Vector2Int(280, 144) } : new[] { new Vector2Int(160, 144), new Vector2Int(192, 144) }; mover.returns = true;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), second ? "Assets/Scenes/PhaseRoom2.unity" : "Assets/Scenes/PhaseSandbox.unity");
    }
    static void Add(GameObject root, string name, int x, int y, int w, int h, int phase)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = new Vector3(x, -y, 0);
        var terrain = go.AddComponent<PhaseTerrain>(); terrain.size = new Vector2Int(w, h); terrain.phase = phase;
    }
}
