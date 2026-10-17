using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhaseSandboxBuilder
{
    [MenuItem("PHASE/Create eight-room demo")]
    public static void CreateDemoRooms()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var data = JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText("Assets/Phase/DemoRooms.json"));
        if (data?.rooms == null || data.rooms.Length != 8) throw new System.InvalidOperationException("Demo requires eight rooms.");
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        for (int i = 0; i < data.rooms.Length; i++)
        {
            var room = data.rooms[i]; room.CreateController();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PHASE"); root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1); root.AddComponent<PhaseGrid>(); var sandbox = root.AddComponent<PhaseSandbox>();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 90;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            sandbox.pixelCamera = camera;
            sandbox.pixelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Phase/PixelSurface.mat");
            sandbox.spawn = new Vector2Int((int)room.spawnX, (int)room.spawnY);
            sandbox.checkpointOnEntry = true;
            sandbox.rightScene = i < 7 ? "PhaseDemo" + (i + 2) : "";
            foreach (var b in room.blocks) Add(root, "Terrain", (int)b.x, (int)b.y, (int)b.w, (int)b.h, b.phase, b.phaseMask);
            foreach (var spike in room.CreateSpikes())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Spikes", root.transform, new Vector3((float)spike.x, -(float)spike.y));
                var teeth = go.GetComponent<PhaseSpike>(); teeth.direction = spike.direction; teeth.phaseMask = spike.Mask;
                teeth.length = (int)(spike.direction == SpikeDirection.Up || spike.direction == SpikeDirection.Down ? spike.w : spike.h) / 8;
            }
            var drive = room.CreateGear();
            foreach (var laser in room.CreateLasers())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Laser", root.transform, new Vector3((float)laser.x, -(float)laser.y));
                var emitter = go.GetComponent<PhaseLaser>(); emitter.direction = laser.direction; emitter.phaseMask = laser.Mask;
            }
            if (drive != null)
            {
                var go = PhaseMechanismPrefabs.Instantiate("ClimbGear", root.transform, new Vector3((float)drive.wall.x, -(float)drive.wall.y));
                var gear = go.GetComponentInChildren<PhaseClimbGear>();
                gear.wallSize = new Vector2Int((int)drive.wall.w, (int)drive.wall.h); gear.wallTop = (int)(drive.top - drive.wall.y);
                gear.platformStart = new Vector2Int((int)(drive.platform.x - drive.wall.x), (int)(drive.platform.y - drive.wall.y));
                gear.platformSize = new Vector2Int((int)drive.platform.w, (int)drive.platform.h); gear.platformEndX = (int)(drive.endX - drive.wall.x); gear.CreateGear();
            }
            var platform = room.CreateMover();
            if (platform != null)
            {
                var go = PhaseMechanismPrefabs.Instantiate("DashPlatformHorizontal", root.transform, new Vector3((float)platform.x, -(float)platform.y));
                var mover = go.GetComponentInChildren<PhaseMovingPlatform>();
                mover.size = new Vector2Int((int)platform.w, (int)platform.h);
                mover.nodes = System.Array.ConvertAll(platform.nodes, n => new Vector2Int((int)(n[0] - platform.x), (int)(n[1] - platform.y)));
                mover.returns = platform.returns; mover.CreatePlatform();
            }
            string path = "Assets/Scenes/PhaseDemo" + (i + 1) + ".unity";
            RecordOverrides(root);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        foreach (var scene in EditorBuildSettings.scenes)
            if (!scenes.Exists(s => s.path == scene.path)) scenes.Add(scene);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/PhaseDemo1.unity");
    }
    [MenuItem("PHASE/Create gear demo")]
    public static void CreateGearDemo()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PHASE"); root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1); root.AddComponent<PhaseGrid>(); var sandbox = root.AddComponent<PhaseSandbox>();
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
        var drive = PhaseMechanismPrefabs.Instantiate("ClimbGear", root.transform, new Vector3(72, -104));
        var gear = drive.GetComponentInChildren<PhaseClimbGear>();
        gear.wallSize = new Vector2Int(8, 56); gear.platformSize = new Vector2Int(56, 8); gear.wallTop = -24; gear.platformStart = new Vector2Int(16, -24); gear.platformEndX = 40; gear.CreateGear();
        var moving = PhaseMechanismPrefabs.Instantiate("DashPlatformHorizontal", root.transform, new Vector3(232, -112));
        var mover = moving.GetComponentInChildren<PhaseMovingPlatform>();
        mover.size = new Vector2Int(24, 8); mover.nodes = new[] { Vector2Int.zero, new Vector2Int(32, 0) }; mover.returns = true; mover.CreatePlatform();
        const string path = "Assets/Scenes/PhaseGearDemo.unity";
        RecordOverrides(root);
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
        var root = new GameObject("PHASE"); root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1); root.AddComponent<PhaseGrid>(); var sandbox = root.AddComponent<PhaseSandbox>();
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
        var platform = PhaseMechanismPrefabs.Instantiate("DashPlatformHorizontal", root.transform, new Vector3(second ? 248 : 160, -144));
        var mover = platform.GetComponentInChildren<PhaseMovingPlatform>();
        mover.nodes = new[] { Vector2Int.zero, new Vector2Int(32, 0) }; mover.returns = true; mover.CreatePlatform();
        RecordOverrides(root);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), second ? "Assets/Scenes/PhaseRoom2.unity" : "Assets/Scenes/PhaseSandbox.unity");
    }
    static void RecordOverrides(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>())
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
    static void Add(GameObject root, string name, int x, int y, int w, int h, int phase, int phaseMask = 0)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = new Vector3(x, -y, 0);
        var terrain = go.AddComponent<PhaseTerrain>(); terrain.size = new Vector2Int(w, h); terrain.phase = phase; terrain.phaseMask = phaseMask; terrain.CreateBlock();
    }
}
