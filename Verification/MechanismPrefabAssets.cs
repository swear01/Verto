using Phase;
using UnityEditor;
using UnityEngine;

public static class MechanismPrefabAssets
{
    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Phase/Prefabs")) AssetDatabase.CreateFolder("Assets/Phase", "Prefabs");
        SavePlatform("DashPlatformHorizontal", new Vector2Int(80, 0));
        SavePlatform("DashPlatformVertical", new Vector2Int(0, -64));
        Save("ClimbGear", root =>
        {
            var body = new GameObject("Mechanism"); body.transform.SetParent(root.transform, false);
            var gear = body.AddComponent<PhaseClimbGear>();
            gear.wallTop = -24; gear.platformStart = new Vector2Int(16, -24); gear.platformEndX = 40;
            Box(body, gear.wallSize);
            var surface = new GameObject("Platform collider"); surface.transform.SetParent(body.transform, false);
            surface.transform.localPosition = new Vector3(16, 24); Box(surface, gear.platformSize);
        });
        Save("Laser", root => root.AddComponent<PhaseLaser>());
        Save("Spikes", root => root.AddComponent<PhaseSpike>());
        foreach (var kind in new[] { CrystalKind.Jump, CrystalKind.Dash })
            Save(kind + "Crystal", root =>
            {
                root.AddComponent<PhaseCrystal>().kind = kind; Box(root, new Vector2Int(8, 8));
            });
        AssetDatabase.SaveAssets();
        return "Ensured seven reusable PHASE mechanism prefabs.";
    }

    static void SavePlatform(string name, Vector2Int end) => Save(name, root =>
    {
        var body = new GameObject("Platform"); body.transform.SetParent(root.transform, false);
        var platform = body.AddComponent<PhaseMovingPlatform>();
        platform.nodes = new[] { Vector2Int.zero, end }; platform.returns = true;
        Box(body, platform.size);
    });

    static void Box(GameObject body, Vector2Int size)
    {
        var collider = body.AddComponent<BoxCollider2D>();
        collider.isTrigger = true; collider.size = size; collider.offset = new Vector2(size.x / 2f, -size.y / 2f);
    }

    static void Save(string name, System.Action<GameObject> setup)
    {
        var path = PhaseMechanismPrefabs.Folder + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;
        var root = new GameObject(name);
        try { setup(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { Object.DestroyImmediate(root); }
    }
}
