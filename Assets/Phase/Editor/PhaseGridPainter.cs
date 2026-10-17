using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PhaseGridPainter : EditorWindow
{
    bool painting;
    int mask = 7;
    [MenuItem("PHASE/Paint Grid terrain")]
    static void Open() => GetWindow<PhaseGridPainter>("PHASE Grid");
    void OnEnable() => SceneView.duringSceneGui += Paint;
    void OnDisable() => SceneView.duringSceneGui -= Paint;
    void OnGUI()
    {
        painting = EditorGUILayout.Toggle("Paint 8px cells", painting);
        mask = EditorGUILayout.IntSlider("Phase mask", mask, 1, 7);
    }
    void Paint(SceneView view)
    {
        if (!painting || EditorApplication.isPlaying) return;
        var root = Object.FindAnyObjectByType<PhaseSandbox>();
        if (root == null || root.GetComponent<Grid>() == null) return;
        var e = Event.current;
        if (e.alt || e.control || e.command || e.button != 0) return;
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return;
        var point = PhaseGrid.Snap(root, ray.GetPoint(distance));
        // Screen Y points down; select the cell below the snapped upper-left corner.
        foreach (var terrain in root.GetComponentsInChildren<PhaseTerrain>())
            if (point.x >= terrain.transform.position.x && point.x < terrain.transform.position.x + terrain.size.x &&
                point.y <= terrain.transform.position.y && point.y > terrain.transform.position.y - terrain.size.y) { e.Use(); return; }
        var go = new GameObject("Terrain"); Undo.RegisterCreatedObjectUndo(go, "Paint PHASE cell");
        go.transform.SetParent(root.transform); go.transform.position = point;
        var block = go.AddComponent<PhaseTerrain>(); block.phaseMask = mask; block.CreateBlock();
        EditorSceneManager.MarkSceneDirty(go.scene); e.Use(); view.Repaint();
    }
}
