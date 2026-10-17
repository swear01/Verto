using NUnit.Framework;
using Phase;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GridGeometryTests
{
    [Test]
    public void NativeGridAndBoxColliderDefineThePixelCollisionRectangle()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Grid probe"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1);
            var grid = root.GetComponent<Grid>();
            Assert.That(PhaseGrid.CellTopLeft(grid, new Vector3(6, -6)), Is.EqualTo(Vector3.zero));
            Assert.That(PhaseGrid.CellTopLeft(grid, new Vector3(8, -8)), Is.EqualTo(new Vector3(8, -8)));
            Assert.That(PhaseGrid.CellTopLeft(grid, new Vector3(-.1f, -8.1f)), Is.EqualTo(new Vector3(-8, -8)));
            var go = new GameObject("Box"); go.transform.SetParent(root.transform); go.transform.position = new Vector3(25, -39);
            var terrain = go.AddComponent<PhaseTerrain>(); terrain.size = new Vector2Int(24, 8); terrain.phaseMask = 3;
            var block = terrain.CreateBlock(); var shape = go.GetComponent<BoxCollider2D>();
            Assert.That(shape, Is.Not.Null, "authoring creates the missing native collider");
            shape.enabled = false; block = terrain.CreateBlock();
            Assert.That((block.x, block.y, block.w, block.h, block.Mask), Is.EqualTo((24d, 40d, 24d, 8d, 3)));
            Assert.That(shape.enabled, Is.True);
            Assert.That(shape.bounds.min.x, Is.EqualTo(block.x)); Assert.That(-shape.bounds.max.y, Is.EqualTo(block.y));
            go.transform.position = new Vector3(25, -39);
            var emitter = go.AddComponent<PhaseLaser>(); var laser = emitter.CreateLaser();
            Assert.That((laser.x, laser.y), Is.EqualTo((24d, 40d)), "emitters share the terrain Grid");
            root.transform.localScale = new Vector3(2, 1, 1);
            Assert.Throws<System.InvalidOperationException>(() => terrain.CreateBlock());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test]
    public void DemoAuthoringRejectsOffGridCollisionGeometry()
    {
        var data = JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText("Assets/Phase/DemoRooms.json"));
        foreach (var room in data.rooms) room.CreateController();
        data.rooms[0].blocks[0].x = 1;
        Assert.Throws<System.InvalidOperationException>(() => data.rooms[0].CreateController());
    }
}
