using NUnit.Framework;
using System.Linq;
using Phase;
using UnityEditor;
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
            go.transform.position = new Vector3(25, -39);
            var pickup = go.AddComponent<PhaseCrystal>(); pickup.phaseMask = 5;
            var crystal = pickup.CreateCrystal();
            Assert.That((crystal.x, crystal.y, crystal.w, crystal.h, crystal.Mask),
                Is.EqualTo((24d, 40d, 8d, 8d, 5)), "pickups share the terrain Grid and native bounds");
            Assert.That(shape.isTrigger, Is.True); Assert.That(shape.size, Is.EqualTo(new Vector2(8, 8)));
            root.transform.localScale = new Vector3(2, 1, 1);
            Assert.Throws<System.InvalidOperationException>(() => terrain.CreateBlock());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test]
    public void TerrainAndPrefabsSnapAnywhereInsideABigRoom()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Big room probe"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1);
            var sandbox = root.AddComponent<PhaseSandbox>(); sandbox.roomSize = new Vector2Int(640, 360);
            var tile = new GameObject("Terrain"); tile.transform.SetParent(root.transform); tile.transform.position = new Vector3(633, -351);
            var floor = tile.AddComponent<PhaseTerrain>().CreateBlock();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Phase/Prefabs/Laser.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            instance.transform.position = new Vector3(601, -339);
            var emitter = instance.GetComponent<PhaseLaser>(); emitter.direction = LaserDirection.Down;
            var laser = emitter.CreateLaser();
            Assert.That((floor.x, floor.y, laser.x, laser.y), Is.EqualTo((632d, 352d, 600d, 336d)));
            var room = new PhaseController(new[] { floor }, 32, 149, lasers: new[] { laser }, width: sandbox.roomSize.x, height: sandbox.roomSize.y);
            var beam = room.LaserBeam(laser);
            Assert.That((beam.y, beam.y + beam.h), Is.EqualTo((344d, 360d)), "the beam reaches the bottom of the big room");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test]
    public void DemoAuthoringRejectsOffGridCollisionGeometry()
    {
        var data = JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText("Assets/Phase/DemoRooms.json"));
        foreach (var room in data.rooms) room.CreateController();
        double originalX = data.rooms[0].blocks[0].x;
        data.rooms[0].blocks[0].x = 1;
        Assert.Throws<System.InvalidOperationException>(() => data.rooms[0].CreateController());
        data.rooms[0].blocks[0].x = originalX;
        data.rooms[0].crystals = new[] { new DemoCrystal { x = 40, y = 81, phaseMask = 7 } };
        Assert.Throws<System.InvalidOperationException>(() => data.rooms[0].CreateController());
    }

    [Test]
    public void JumpPlatformComponentBuildsCenterCellPivotsAndSpikeRowsAndSanitizesItself()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Grid probe"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1);
            var go = new GameObject("Platform"); go.transform.SetParent(root.transform); go.transform.position = new Vector3(24, -40);
            var component = go.AddComponent<PhaseJumpPlatform>();
            component.length = 3; component.reverse = true; component.spikes = PlatformSides.Up | PlatformSides.Right;
            component.pivots = new[] { Vector2Int.zero, new Vector2Int(3, 0), new Vector2Int(3, -2) };
            var platform = component.CreatePlatform();
            Assert.That(platform.pivots, Is.EqualTo(new[] { (4, 5), (7, 5), (7, 3) }), "offsets become centre cells, with Y down");
            Assert.That((platform.x, platform.y, platform.w, platform.h, platform.reverse), Is.EqualTo((24d, 40d, 24d, 8d, true)));
            Assert.That(platform.attached.Select(s => s.direction), Is.EquivalentTo(new[] { SpikeDirection.Up, SpikeDirection.Right }));
            component.length = 4; component.phaseMask = 0; component.pivots = null; component.vertical = true;
            platform = component.CreatePlatform();
            Assert.That((platform.length, platform.Mask, platform.pivots.Length), Is.EqualTo((5, 7, 2)), "an odd length, a color and two stops are restored");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
