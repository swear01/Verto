using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase
{
    public sealed partial class PhaseSandbox
    {
        public static string LastMapError { get; private set; } = "";
        public static int MapResultCount { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PhaseMapResult(int success, string message);
#endif
        internal const string Busy = "請等轉場結束後再選擇地圖。";
        internal static void MapResult(bool success, string message = "")
        {
            LastMapError = success ? "" : message; MapResultCount++;
#if UNITY_WEBGL && !UNITY_EDITOR
            PhaseMapResult(success ? 1 : 0, message);
#endif
        }
        // Only a pending scene load blocks; the arrival slide is visual and stays frozen while a dialog pauses play.
        public void SelectDemoRoom(string value)
        {
            if (transitioning) { MapResult(false, Busy); return; }
            try { jumpQueued = false; accumulator = 0; transitioning = true; LoadLevel(value); }
            catch (Exception error) { transitioning = false; MapResult(false, error.Message); }
        }
        // Validation throws before anything changes; success is reported once the scene has loaded.
        internal static void LoadLevel(string value)
        {
            if (!int.TryParse(value, out int index) || index < 1 || index > Levels.Length || value != index.ToString())
                throw new ArgumentException("請選擇有效的關卡。");
            string scene = Levels[index - 1];
            if (!Application.CanStreamedLevelBeLoaded(scene)) throw new ArgumentException("此關卡尚未包含在遊戲中。");
            ClearTravel();
            var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("關卡載入失敗。");
            operation.completed += _ => { controlsOpen = false; releaseControls = true; MapResult(true); };
        }
        public void LoadCustomMap(string json)
        {
            if (transitioning) { MapResult(false, Busy); return; }
            try
            {
                var room = PhaseCustomMap.Parse(json);
                if (pixelCamera.transform.IsChildOf(transform)) pixelCamera.transform.SetParent(null,true);
                jumpQueued = false; accumulator = 0;
                gameObject.SetActive(false);
                CreateCustom(room, pixelCamera, pixelMaterial);
                controlsOpen = false; releaseControls = true;
                Destroy(gameObject);
                MapResult(true);
            }
            catch (Exception error)
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                MapResult(false,error.Message);
            }
        }
        // A parented room is an editor playtest; it is renamed so the "PHASE" bridge object stays unique.
        internal static PhaseSandbox CreateCustom(PhaseDemoRoom room, Camera camera, Material material, Transform parent = null)
        {
            var root = new GameObject(parent == null ? "PHASE" : "Playtest"); root.SetActive(false);
            try
            {
                root.transform.SetParent(parent,false);
                var grid = root.AddComponent<Grid>(); grid.cellSize = new Vector3(8,8,1);
                root.AddComponent<PhaseGrid>();
                foreach (var block in room.blocks)
                {
                    var tile = new GameObject("Terrain"); tile.transform.SetParent(root.transform,false);
                    tile.transform.localPosition = new Vector3((float)block.x,-(float)block.y,0);
                    var terrain = tile.AddComponent<PhaseTerrain>(); terrain.size = new Vector2Int((int)block.w,(int)block.h);
                    terrain.phase = block.phase; terrain.phaseMask = block.phaseMask;
                }
                foreach (var spike in room.spikes ?? Array.Empty<DemoSpike>())
                {
                    var tile = new GameObject("Phase spikes"); tile.transform.SetParent(root.transform,false);
                    tile.transform.localPosition = new Vector3((float)spike.x,-(float)spike.y,0);
                    var hazard = tile.AddComponent<PhaseSpike>(); hazard.length = (int)spike.length / 8;
                    hazard.direction = spike.direction; hazard.phaseMask = spike.phaseMask;
                }
                foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>())
                {
                    var tile = new GameObject("Phase laser"); tile.transform.SetParent(root.transform,false);
                    tile.transform.localPosition = new Vector3((float)laser.x,-(float)laser.y,0);
                    var emitter = tile.AddComponent<PhaseLaser>(); emitter.direction = laser.direction; emitter.phaseMask = laser.phaseMask;
                }
                foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>())
                {
                    var tile = new GameObject("Phase crystal"); tile.transform.SetParent(root.transform,false);
                    tile.transform.localPosition = new Vector3((float)crystal.x,-(float)crystal.y,0);
                    var pickup = tile.AddComponent<PhaseCrystal>(); pickup.phaseMask = crystal.phaseMask;
                }
                var sandbox = root.AddComponent<PhaseSandbox>(); sandbox.custom = true;
                sandbox.pixelCamera = camera; sandbox.pixelMaterial = material;
                sandbox.spawn = new Vector2Int((int)room.spawnX,(int)room.spawnY);
                sandbox.checkpointOnEntry = true;
                sandbox.roomSize = new Vector2Int(room.GridWidth,room.RoomHeight);
                ClearTravel();
                root.SetActive(true);
                if (sandbox.Controller == null || sandbox.Frame == null) throw new InvalidOperationException("自訂地圖載入失敗。");
                return sandbox;
            }
            catch { root.SetActive(false); Destroy(root); throw; }
        }
    }
}
