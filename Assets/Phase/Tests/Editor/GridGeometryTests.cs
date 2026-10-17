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
    public void SavedDemoPrefabInstancesPreserveAllElevenRoomMechanisms()
    {
        var data = JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText("Assets/Phase/DemoRooms.json"));
        var rooms = data.rooms.Concat(JsonUtility.FromJson<PhaseDemoRooms>(System.IO.File.ReadAllText("Assets/Phase/CrystalRooms.json")).rooms).ToArray();
        for (int i = 0; i < rooms.Length; i++)
        {
            var name = i < 8 ? "PhaseDemo" + (i + 1) : "PhaseCrystal" + (i - 7);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                var sandbox = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<PhaseSandbox>()).Single();
                var expected = rooms[i].CreateController();
                var movers = sandbox.GetComponentsInChildren<PhaseMovingPlatform>();
                Assert.That(movers.Length, Is.EqualTo(expected.movers.Length));
                for (int j = 0; j < movers.Length; j++)
                {
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(movers[j]), Is.True);
                    var model = movers[j].CreatePlatform(); var source = expected.movers[j];
                    Assert.That(model.nodes, Is.EqualTo(source.nodes));
                    Assert.That((model.w, model.h, model.returns), Is.EqualTo((source.w, source.h, source.returns)));
                }
                var gears = sandbox.GetComponentsInChildren<PhaseClimbGear>();
                Assert.That(gears.Length, Is.EqualTo(expected.gears.Length));
                for (int j = 0; j < gears.Length; j++)
                {
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(gears[j]), Is.True);
                    var model = gears[j].CreateGear(); var source = expected.gears[j];
                    Assert.That((model.wall.x, model.wall.y, model.wall.w, model.wall.h, model.top),
                        Is.EqualTo((source.wall.x, source.wall.y, source.wall.w, source.wall.h, source.top)));
                    Assert.That((model.platform.x, model.platform.y, model.platform.w, model.platform.h, model.endX),
                        Is.EqualTo((source.platform.x, source.platform.y, source.platform.w, source.platform.h, source.endX)));
                }
                var crystals = sandbox.GetComponentsInChildren<PhaseCrystal>();
                Assert.That(crystals.All(PrefabUtility.IsPartOfPrefabInstance), Is.True);
                Assert.That(crystals.Select(c => c.CreateCrystal()).Select(c => (c.x, c.y, c.kind, c.Mask)),
                    Is.EqualTo(expected.crystals.Select(c => (c.x, c.y, c.kind, c.Mask))));
                var lasers = sandbox.GetComponentsInChildren<PhaseLaser>();
                Assert.That(lasers.All(PrefabUtility.IsPartOfPrefabInstance), Is.True);
                Assert.That(lasers.Select(l => l.CreateLaser()).Select(l => (l.x, l.y, l.direction, l.Mask)),
                    Is.EqualTo(expected.lasers.Select(l => (l.x, l.y, l.direction, l.Mask))));
                var spikes = sandbox.GetComponentsInChildren<PhaseSpike>();
                Assert.That(spikes.All(PrefabUtility.IsPartOfPrefabInstance), Is.True);
                Assert.That(spikes.Select(s => s.CreateSpike()).Select(s => (s.x, s.y, s.w, s.h, s.direction, s.Mask)),
                    Is.EqualTo(expected.spikes.Select(s => (s.x, s.y, s.w, s.h, s.direction, s.Mask))));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }

    [Test]
    public void DuplicatedPlatformPrefabsTranslateTheirOwnRoutesOnTheGrid()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Prefab Grid probe"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Phase/Prefabs/DashPlatformVertical.prefab");
            Assert.That(asset, Is.Not.Null);
            var first = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            first.transform.position = new Vector3(80, -144);
            var second = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            second.transform.position = new Vector3(217, -111);
            var a = first.GetComponentInChildren<PhaseMovingPlatform>();
            var b = second.GetComponentInChildren<PhaseMovingPlatform>();
            b.nodes[1] = new Vector2Int(56, 0);
            var m = a.CreatePlatform(); var n = b.CreatePlatform();
            Assert.That(m.nodes[0], Is.EqualTo(new[] { 80d, 144d }));
            Assert.That(m.nodes[1], Is.EqualTo(new[] { 80d, 80d }));
            Assert.That(n.nodes[0], Is.EqualTo(new[] { 216d, 112d }));
            Assert.That(n.nodes[1], Is.EqualTo(new[] { 272d, 112d }));
            first.transform.position += new Vector3(16, 8);
            m = a.CreatePlatform();
            Assert.That(m.nodes[0], Is.EqualTo(new[] { 96d, 136d }));
            Assert.That(m.nodes[1], Is.EqualTo(new[] { 96d, 72d }));
            Assert.That(b.CreatePlatform().nodes, Is.EqualTo(n.nodes));
            Assert.That(a.GetComponent<BoxCollider2D>().size, Is.EqualTo(new Vector2(40, 8)));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(first), Is.EqualTo(asset));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(second), Is.EqualTo(asset));
            Assert.That(asset.GetComponentInChildren<PhaseMovingPlatform>().nodes[1], Is.EqualTo(new Vector2Int(0, -64)));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void GearLaserAndSpikePrefabsCanBePlacedMoreThanOnce()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Mechanism Grid probe"); SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Grid>().cellSize = new Vector3(8, 8, 1);
            foreach (var name in new[] { "ClimbGear", "Laser", "Spikes" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Phase/Prefabs/" + name + ".prefab");
                Assert.That(asset, Is.Not.Null);
                for (int i = 0; i < 2; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                    instance.transform.position = new Vector3(72 + 96 * i, -104);
                    var gear = instance.GetComponentInChildren<PhaseClimbGear>();
                    if (gear != null)
                    {
                        var model = gear.CreateGear();
                        Assert.That((model.wall.x, model.wall.y, model.top), Is.EqualTo((72d + 96 * i, 104d, 80d)));
                        Assert.That((model.platform.x, model.platform.y, model.endX), Is.EqualTo((88d + 96 * i, 80d, 112d + 96 * i)));
                    }
                    var laser = instance.GetComponent<PhaseLaser>();
                    if (laser != null) Assert.That(laser.CreateLaser().x, Is.EqualTo(72 + 96 * i));
                    var spike = instance.GetComponent<PhaseSpike>();
                    if (spike != null) Assert.That(spike.CreateSpike().x, Is.EqualTo(72 + 96 * i));
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void VerticalReturnNodesKeepTheNativeColliderHorizontal()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Platform Grid probe");SceneManager.MoveGameObjectToScene(root,scene);
            root.AddComponent<Grid>().cellSize=new Vector3(8,8,1);
            var go=new GameObject("Vertical route");go.transform.SetParent(root.transform);
            var platform=go.AddComponent<PhaseMovingPlatform>();platform.size=new Vector2Int(48,8);
            platform.nodes=new[]{new Vector2Int(80,144),new Vector2Int(80,80)};platform.returns=true;
            var model=platform.CreatePlatform();
            Assert.That((model.x,model.y,model.w,model.h),Is.EqualTo((80d,144d,48d,8d)));
            Assert.That(go.GetComponent<BoxCollider2D>().size,Is.EqualTo(new Vector2(48,8)));
            Assert.That(go.transform.rotation,Is.EqualTo(Quaternion.identity));
            platform.nodes[1]=new Vector2Int(88,80);
            Assert.Throws<System.ArgumentException>(()=>platform.CreatePlatform());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
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
            var pickup = go.AddComponent<PhaseCrystal>(); pickup.kind = CrystalKind.Dash; pickup.phaseMask = 5;
            var crystal = pickup.CreateCrystal();
            Assert.That((crystal.x, crystal.y, crystal.w, crystal.h, crystal.Mask, crystal.kind),
                Is.EqualTo((24d, 40d, 8d, 8d, 5, CrystalKind.Dash)), "pickups share the terrain Grid and native bounds");
            Assert.That(shape.isTrigger, Is.True); Assert.That(shape.size, Is.EqualTo(new Vector2(8, 8)));
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
        double originalX = data.rooms[0].blocks[0].x;
        data.rooms[0].blocks[0].x = 1;
        Assert.Throws<System.InvalidOperationException>(() => data.rooms[0].CreateController());
        data.rooms[0].blocks[0].x = originalX;
        data.rooms[0].crystals = new[] { new DemoCrystal { x = 40, y = 81, kind = CrystalKind.Jump, phaseMask = 7 } };
        Assert.Throws<System.InvalidOperationException>(() => data.rooms[0].CreateController());
    }
}
