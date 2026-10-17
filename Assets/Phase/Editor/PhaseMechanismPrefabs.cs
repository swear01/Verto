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

    [MenuItem("PHASE/Add/Dash platform horizontal")]
    static void Horizontal() => Add("DashPlatformHorizontal");
    [MenuItem("PHASE/Add/Dash platform vertical")]
    static void Vertical() => Add("DashPlatformVertical");
    [MenuItem("PHASE/Add/Climb gear")]
    static void Gear() => Add("ClimbGear");
    [MenuItem("PHASE/Add/Laser")]
    static void Laser() => Add("Laser");
    [MenuItem("PHASE/Add/Spikes")]
    static void Spikes() => Add("Spikes");

    [MenuItem("PHASE/Add/Jump crystal")]
    static void JumpCrystal() => Add("JumpCrystal");
    [MenuItem("PHASE/Add/Dash crystal")]
    static void DashCrystal() => Add("DashCrystal");

    static void Add(string name)
    {
        var root = Object.FindAnyObjectByType<PhaseSandbox>();
        if (root == null) throw new System.InvalidOperationException("Open a PHASE scene first.");
        var instance = Instantiate(name, root.transform, new Vector3(80, -120));
        Undo.RegisterCreatedObjectUndo(instance, "Add PHASE mechanism");
        Selection.activeGameObject = instance;
    }

    [MenuItem("PHASE/Add/Dash platform horizontal", true)]
    [MenuItem("PHASE/Add/Dash platform vertical", true)]
    [MenuItem("PHASE/Add/Climb gear", true)]
    [MenuItem("PHASE/Add/Laser", true)]
    [MenuItem("PHASE/Add/Spikes", true)]
    [MenuItem("PHASE/Add/Jump crystal", true)]
    [MenuItem("PHASE/Add/Dash crystal", true)]
    static bool CanAdd() => !EditorApplication.isPlaying && Object.FindAnyObjectByType<PhaseSandbox>() != null;
}
