using Phase;
using UnityEditor;
using UnityEngine;

public static class PhaseMechanismPrefabs
{
    public const string Folder = "Assets/Phase/Prefabs/";

    public static GameObject Instantiate(string name, Transform parent, Vector3 position)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        if (asset == null) throw new System.InvalidOperationException("Missing PHASE prefab: " + name);
        var point = PhaseGrid.Snap(parent, position);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.transform.position = point;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    [MenuItem("PHASE/Add/Laser")]
    static void Laser() => Add("Laser");
    [MenuItem("PHASE/Add/Spikes")]
    static void Spikes() => Add("Spikes");

    [MenuItem("PHASE/Add/Jump crystal")]
    static void JumpCrystal() => Add("JumpCrystal");

    [MenuItem("PHASE/Add/Jump platform")]
    static void JumpPlatform() => Add("JumpPlatform");

    static void Add(string name)
    {
        var root = Object.FindAnyObjectByType<PhaseSandbox>();
        if (root == null) throw new System.InvalidOperationException("Open a PHASE scene first.");
        var view = SceneView.lastActiveSceneView;
        var instance = Instantiate(name, root.transform, AddPoint(root, view != null ? view.pivot : new Vector3(80, -120)));
        Undo.RegisterCreatedObjectUndo(instance, "Add PHASE mechanism");
        Selection.activeGameObject = instance;
    }

    // New mechanisms appear at the Scene view centre, kept inside the room bounds with room for the largest Prefab.
    public static Vector3 AddPoint(PhaseSandbox root, Vector3 pivot) =>
        new Vector3(Mathf.Clamp(pivot.x, 0, root.roomSize.x - 64), Mathf.Clamp(pivot.y, 64 - root.roomSize.y, 0));

    [MenuItem("PHASE/Add/Laser", true)]
    [MenuItem("PHASE/Add/Spikes", true)]
    [MenuItem("PHASE/Add/Jump crystal", true)]
    [MenuItem("PHASE/Add/Jump platform", true)]
    static bool CanAdd() => !EditorApplication.isPlaying && Object.FindAnyObjectByType<PhaseSandbox>() != null;
}
