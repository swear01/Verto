using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PhaseGridPainter : EditorWindow
{
    bool painting;
    int color;
    static readonly int[] masks = { 7, 1, 2, 4, 5, 3, 6 };
    static readonly string[] colors = { "永久白", "紅", "藍", "綠", "黃", "洋紅", "青" };
    [MenuItem("PHASE/Paint Grid terrain")]
    static void Open() => GetWindow<PhaseGridPainter>("PHASE Grid");
    void OnEnable() => SceneView.duringSceneGui += Paint;
    void OnDisable() => SceneView.duringSceneGui -= Paint;
    void OnGUI()
    {
        painting = EditorGUILayout.Toggle("畫地磚", painting);
        color = EditorGUILayout.Popup("顏色", color, colors);
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
        var grid = root.GetComponent<Grid>();
        var point = PhaseGrid.CellTopLeft(grid, ray.GetPoint(distance));
        // Screen Y points down; select the cell below the snapped upper-left corner.
        foreach (var terrain in root.GetComponentsInChildren<PhaseTerrain>())
            if (point.x >= terrain.transform.position.x && point.x < terrain.transform.position.x + terrain.size.x &&
                point.y <= terrain.transform.position.y && point.y > terrain.transform.position.y - terrain.size.y) { e.Use(); return; }
        var go = new GameObject("Terrain"); Undo.RegisterCreatedObjectUndo(go, "Paint PHASE cell");
        go.transform.SetParent(root.transform); go.transform.position = point;
        var block = go.AddComponent<PhaseTerrain>(); block.phaseMask = masks[color]; block.CreateBlock();
        EditorSceneManager.MarkSceneDirty(go.scene); e.Use(); view.Repaint();
    }
}
