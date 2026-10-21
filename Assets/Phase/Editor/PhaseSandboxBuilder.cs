using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhaseSandboxBuilder
{
    [MenuItem("PHASE/Create eight-room demo")]
    public static void CreateDemoRooms()
    {
        CreateRooms("Assets/Phase/DemoRooms.json", "PhaseDemo", 8);
    }
    [MenuItem("PHASE/Create crystal rooms")]
    public static void CreateCrystalRooms()
    {
        CreateRooms("Assets/Phase/CrystalRooms.json", "PhaseCrystal", 3);
    }
    static void CreateRooms(string source, string prefix, int count)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var data = JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText(source));
        if (data?.rooms == null || data.rooms.Length != count) throw new System.InvalidOperationException("Unexpected room count: " + source);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
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
            int level = System.Array.IndexOf(PhaseSandbox.Levels, prefix + (i + 1));
            sandbox.leftScene = level > 0 ? PhaseSandbox.Levels[level - 1] : "";
            sandbox.rightScene = level < PhaseSandbox.Levels.Length - 1 ? PhaseSandbox.Levels[level + 1] : "";
            foreach (var b in room.blocks) Add(root, "Terrain", (int)b.x, (int)b.y, (int)b.w, (int)b.h, b.phase, b.phaseMask);
            foreach (var spike in room.CreateSpikes())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Spikes", root.transform, new Vector3((float)spike.x, -(float)spike.y));
                var teeth = go.GetComponent<PhaseSpike>(); teeth.direction = spike.direction; teeth.phaseMask = spike.Mask;
                teeth.length = (int)(spike.direction == SpikeDirection.Up || spike.direction == SpikeDirection.Down ? spike.w : spike.h) / 8;
            }
            foreach (var laser in room.CreateLasers())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Laser", root.transform, new Vector3((float)laser.x, -(float)laser.y));
                var emitter = go.GetComponent<PhaseLaser>(); emitter.direction = laser.direction; emitter.phaseMask = laser.Mask;
            }
            foreach (var crystal in room.CreateCrystals())
            {
                var go = PhaseMechanismPrefabs.Instantiate("JumpCrystal", root.transform, new Vector3((float)crystal.x, -(float)crystal.y));
                var pickup = go.GetComponent<PhaseCrystal>(); pickup.phaseMask = crystal.Mask; pickup.CreateCrystal();
            }
            string path = "Assets/Scenes/" + prefix + (i + 1) + ".unity";
            RecordOverrides(root);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/" + prefix + "1.unity");
    }
    // A standalone 640x360 scrolling room: a long floor run, then a three-step wall climb in the right column.
    [MenuItem("PHASE/Create scroll test")]
    public static void CreateScrollTest()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PHASE"); root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1); root.AddComponent<PhaseGrid>(); var sandbox = root.AddComponent<PhaseSandbox>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 90;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        sandbox.pixelCamera = camera;
        sandbox.pixelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Phase/PixelSurface.mat");
        sandbox.roomSize = new Vector2Int(640, 360); sandbox.spawn = new Vector2Int(16, 333);
        Add(root, "Floor", 0, 344, 640, 16, -1);
        Add(root, "Step A", 560, 248, 80, 96, -1);
        Add(root, "Step B", 592, 152, 48, 96, -1);
        Add(root, "Step C", 624, 56, 16, 96, -1);
        Add(root, "Red ledge", 160, 264, 40, 8, 0);
        Add(root, "Blue ledge", 264, 200, 40, 8, 1);
        Add(root, "Green ledge", 368, 136, 40, 8, 2);
        Add(root, "High ledge", 64, 112, 48, 8, -1, 5);
        var laser = PhaseMechanismPrefabs.Instantiate("Laser", root.transform, new Vector3(0, -16)).GetComponent<PhaseLaser>();
        laser.direction = LaserDirection.Right; laser.phaseMask = 6;
        var spikes = PhaseMechanismPrefabs.Instantiate("Spikes", root.transform, new Vector3(400, -112)).GetComponent<PhaseSpike>();
        spikes.direction = SpikeDirection.Up; spikes.length = 3; spikes.phaseMask = 7;
        Add(root, "Spike base", 400, 120, 24, 8, -1);
        const string path = "Assets/Scenes/Drafts/ScrollTest.unity";
        System.IO.Directory.CreateDirectory("Assets/Scenes/Drafts");
        RecordOverrides(root);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        AssetDatabase.SaveAssets();
    }
    // The title is the first build scene; the editor follows the levels.
    [MenuItem("PHASE/Create title and editor")]
    public static void CreateScreens()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var type in new[] { typeof(PhaseTitle), typeof(PhaseEditor) })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var screen = (PhaseScreen)new GameObject("PHASE").AddComponent(type);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 90;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            screen.pixelCamera = camera;
            screen.pixelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Phase/PixelSurface.mat");
            string path = "Assets/Scenes/" + type.Name + ".unity";
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            scenes.RemoveAll(s => s.path == path);
            if (type == typeof(PhaseTitle)) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            else scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/PhaseTitle.unity");
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
        RecordOverrides(root);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), second ? "Assets/Scenes/PhaseRoom2.unity" : "Assets/Scenes/PhaseSandbox.unity");
    }
    // Imports level-a and level-b and puts every playable scene, including the hand-authored color room, in the build.
    public static string CreateImportedLevels()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new System.OperationCanceledException("Unsaved scenes were kept.");
        var maps = new[] { ("Assets/Phase/Imported/level-a.json", "PhaseDemo13"), ("Assets/Phase/Imported/level-b.json", "PhaseDemo14") };
        foreach (var (source, sceneName) in maps)
        {
            var room = PhaseCustomMap.ParseRoom(System.IO.File.ReadAllText(source)); room.CreateController();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PHASE"); root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1); root.AddComponent<PhaseGrid>(); var sandbox = root.AddComponent<PhaseSandbox>();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 90;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            sandbox.pixelCamera = camera;
            sandbox.pixelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Phase/PixelSurface.mat");
            sandbox.spawn = new Vector2Int((int)room.spawnX, (int)room.spawnY);
            sandbox.checkpointOnEntry = true;
            int level = System.Array.IndexOf(PhaseSandbox.Levels, sceneName);
            if (level < 0) throw new System.InvalidOperationException(sceneName + " is not in the level list.");
            sandbox.leftScene = level > 0 ? PhaseSandbox.Levels[level - 1] : "";
            sandbox.rightScene = level < PhaseSandbox.Levels.Length - 1 ? PhaseSandbox.Levels[level + 1] : "";
            foreach (var b in room.blocks) Add(root, "Terrain", (int)b.x, (int)b.y, (int)b.w, (int)b.h, b.phase, b.phaseMask);
            foreach (var spike in room.CreateSpikes())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Spikes", root.transform, new Vector3((float)spike.x, -(float)spike.y));
                var teeth = go.GetComponent<PhaseSpike>(); teeth.direction = spike.direction; teeth.phaseMask = spike.Mask;
                teeth.length = (int)(spike.direction == SpikeDirection.Up || spike.direction == SpikeDirection.Down ? spike.w : spike.h) / 8;
            }
            foreach (var laser in room.CreateLasers())
            {
                var go = PhaseMechanismPrefabs.Instantiate("Laser", root.transform, new Vector3((float)laser.x, -(float)laser.y));
                var emitter = go.GetComponent<PhaseLaser>(); emitter.direction = laser.direction; emitter.phaseMask = laser.Mask;
            }
            string path = "Assets/Scenes/" + sceneName + ".unity";
            RecordOverrides(root);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        }
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var kept = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var scene in scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            if (scene.path == "Assets/Scenes/PhaseTitle.unity" || scene.path == "Assets/Scenes/PhaseEditor.unity" || System.Array.IndexOf(PhaseSandbox.Levels, name) >= 0) continue;
            kept.Add(scene);
        }
        var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene("Assets/Scenes/PhaseTitle.unity", true) };
        foreach (var level in PhaseSandbox.Levels) ordered.Add(new EditorBuildSettingsScene("Assets/Scenes/" + level + ".unity", true));
        ordered.AddRange(kept);
        ordered.Add(new EditorBuildSettingsScene("Assets/Scenes/PhaseEditor.unity", true));
        EditorBuildSettings.scenes = ordered.ToArray();
        AssetDatabase.SaveAssets();
        return string.Join(",", System.Array.ConvertAll(maps, m => m.Item2));
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
