using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase
{
    public sealed partial class PhaseSandbox
    {
        public static string LastMapError { get; private set; } = "";
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PhaseMapResult(int success, string message);
#endif
        static void MapResult(bool success, string message = "")
        {
            LastMapError = success ? "" : message;
#if UNITY_WEBGL && !UNITY_EDITOR
            PhaseMapResult(success ? 1 : 0, message);
#endif
        }
        public void SelectDemoRoom(string value)
        {
            if (transitioning || InTransition) { MapResult(false,"請等轉場結束後再選擇地圖。"); return; }
            try
            {
                if (value == null || value.Length != 1 || value[0] < '1' || value[0] > '8')
                    throw new ArgumentException("請選擇第1至8關。");
                string scene = "PhaseDemo" + value;
                if (!Application.CanStreamedLevelBeLoaded(scene)) throw new ArgumentException("此關卡尚未包含在遊戲中。");
                ClearTravel(); jumpQueued = dashQueued = false; accumulator = 0;
                transitioning = true;
                var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                if (operation == null) throw new InvalidOperationException("關卡載入失敗。");
                operation.completed += _ => { controlsOpen = false; releaseControls = true; MapResult(true); };
            }
            catch (Exception error) { transitioning = false; MapResult(false, error.Message); }
        }
        public void LoadCustomMap(string json)
        {
            if (transitioning || InTransition) { MapResult(false,"請等轉場結束後再選擇地圖。"); return; }
            GameObject root = null;
            try
            {
                var room = PhaseCustomMap.Parse(json);
                root = new GameObject("PHASE"); root.SetActive(false);
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
                var sandbox = root.AddComponent<PhaseSandbox>();
                sandbox.pixelCamera = pixelCamera; sandbox.pixelMaterial = pixelMaterial;
                sandbox.spawn = new Vector2Int((int)room.spawnX,(int)room.spawnY);
                sandbox.checkpointOnEntry = true;
                if (pixelCamera.transform.IsChildOf(transform)) pixelCamera.transform.SetParent(null,true);
                ClearTravel(); jumpQueued = dashQueued = false; accumulator = 0;
                gameObject.SetActive(false); root.SetActive(true);
                if (sandbox.Controller == null || sandbox.Frame == null) throw new InvalidOperationException("自訂地圖載入失敗。");
                controlsOpen = false; releaseControls = true;
                Destroy(gameObject); root = null;
                MapResult(true);
            }
            catch (Exception error)
            {
                if (root != null) { root.SetActive(false); Destroy(root); }
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                MapResult(false,error.Message);
            }
        }
    }
}
