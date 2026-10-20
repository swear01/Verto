using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Phase
{
    public sealed partial class PhaseSandbox : MonoBehaviour
    {
        public Camera pixelCamera;
        public Material pixelMaterial;
        public string leftScene, rightScene;
        public bool checkpointOnEntry;
        public Vector2Int spawn = new Vector2Int(32, 149);
        public PhaseController Controller { get; private set; }
        public Texture2D Frame => pixels?.Texture;
        PhasePixels pixels;
        Sprite sprite;
        SpriteRenderer surface;
        double accumulator;
        bool jumpQueued, dashQueued, transitioning;
        public const float TransitionDuration = .18f;
        public bool InTransition => transitioning || departingFrame != null;
        public bool Won { get; private set; }
        public static readonly string[] Levels = {
            "PhaseDemo1", "PhaseDemo2", "PhaseDemo3", "PhaseDemo4", "PhaseDemo5", "PhaseDemo6", "PhaseDemo7", "PhaseDemo8",
            "PhaseCrystal1", "PhaseCrystal2", "PhaseCrystal3", "PhaseDemo12"
        };
        int level = -1;
        bool custom;
        Color32[] departingFrame, arrivingFrame;
        float transitionElapsed = -1;
        int transitionDirection;
        static PhaseController traveler;
        static Color32[] travelFrame;
        static Vector2Int travelSize;
        static string arrivalScene;
        static int arrivalDirection;
        static readonly Key[][] defaultKeys = {
            new[] { Key.LeftArrow, Key.A }, new[] { Key.RightArrow, Key.D },
            new[] { Key.UpArrow, Key.W }, new[] { Key.DownArrow, Key.S },
            new[] { Key.Z, Key.Space }, new[] { Key.X }, new[] { Key.C, Key.LeftShift, Key.RightShift }
        };
        static Key[][] keys = defaultKeys;
        static bool controlsOpen, releaseControls;
        [Serializable] public sealed class KeyOverrides { public string[] keys; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { ClearTravel(); keys = defaultKeys; controlsOpen = releaseControls = false; }
        static void ClearTravel() { traveler = null; travelFrame = null; travelSize = default; arrivalScene = null; arrivalDirection = 0; }

        PhaseController Build(Vector2Int at, int width)
        {
            var controller = new PhaseController(GetComponentsInChildren<PhaseTerrain>().Select(t => t.CreateBlock()), at.x, at.y,
                GetComponentsInChildren<PhaseMovingPlatform>().Select(m => m.CreatePlatform()), gears: GetComponentsInChildren<PhaseClimbGear>().Select(g => g.CreateGear()),
                spikes: GetComponentsInChildren<PhaseSpike>().Select(t => t.CreateSpike()),
                lasers: GetComponentsInChildren<PhaseLaser>().Select(t => t.CreateLaser()),
                crystals: GetComponentsInChildren<PhaseCrystal>().Select(t => t.CreateCrystal()));
            controller.exitLeft = !string.IsNullOrEmpty(leftScene);
            controller.exitRight = !string.IsNullOrEmpty(rightScene) || level == Levels.Length - 1;
            controller.ResizeRoom(width);
            return controller;
        }
        // Enter near the current right edge, choosing a solid phase and clear standing cell.
        public static (Vector2Int at, int phase)? RightEntry(PhaseController c, double preferY, int preferPhase = 0)
        {
            int authored = c.phase;
            try
            {
                for (int x = (c.Width - 16) / 8 * 8; x >= c.Width / 2; x -= 8)
                {
                    foreach (int phase in new[] { 0, preferPhase, 1, 2 }.Distinct())
                    {
                        c.phase = phase;
                        var blockers = c.Solid().Concat(c.spikes.Where(s => s.ActiveInPhase(phase)).Select(s => s.Hitbox))
                            .Concat(c.lasers.Where(l => l.ActiveInPhase(phase)).Select(c.LaserBeam)).ToList();
                        var spots = c.blocks.Where((b, i) => b.ActiveInPhase(phase) && !c.blocked.Contains(i) && b.x <= x && b.x + b.w >= x + 8 && b.y >= 11)
                            .Select(b => new Block(x, b.y - 11, 8, 11)).Where(p => !blockers.Any(o => PhaseController.Overlap(p, o)))
                            .OrderBy(p => Math.Abs(p.y - preferY)).ToList();
                        if (spots.Count > 0) return (new Vector2Int(x, (int)spots[0].y), phase);
                    }
                }
                return null;
            }
            finally { c.phase = authored; }
        }
        void Awake()
        {
            level = custom ? -1 : Array.IndexOf(Levels, gameObject.scene.name);
            if (level >= 0)
            {
                leftScene = level > 0 ? Levels[level - 1] : "";
                rightScene = level < Levels.Length - 1 ? Levels[level + 1] : "";
            }
            bool arriving = traveler != null && gameObject.scene.name == arrivalScene;
            var size = arriving && travelFrame != null ? travelSize : ViewSize(Screen.width, Screen.height);
            Controller = Build(spawn, size.x);
            if (arriving)
            {
                if (checkpointOnEntry)
                {
                    if (arrivalDirection < 0 && RightEntry(Controller, traveler.player.y, traveler.phase) is { } entry)
                    {
                        // Red floors become the checkpoint; other floors keep the authored respawn so a reset cannot drop into a void.
                        if (entry.phase == 0) Controller = Build(entry.at, size.x);
                        Controller.player.x = entry.at.x; Controller.player.y = entry.at.y;
                        Controller.phase = entry.phase; Controller.player.facing = -1;
                    }
                    Controller.time = traveler.time; Controller.jumps = traveler.jumps;
                    Controller.dashes = traveler.dashes; Controller.deaths = traveler.deaths;
                }
                else Controller.EnterFrom(traveler, arrivalDirection);
                if (travelFrame?.Length == size.x * size.y) departingFrame = travelFrame;
                transitionDirection = arrivalDirection;
            }
            ClearTravel();
            pixels = new PhasePixels();
            pixels.SetView(size.x, size.y, 0);
            pixels.Draw(Controller);
            if (departingFrame != null)
            {
                arrivingFrame = Frame.GetPixels32();
                pixels.Slide(departingFrame, arrivingFrame, 0, transitionDirection);
            }
            var go = new GameObject("Pixel surface"); go.transform.SetParent(transform);
            surface = go.AddComponent<SpriteRenderer>(); surface.sharedMaterial = pixelMaterial;
            UpdateSurface();
        }
        void UpdateSurface() => sprite = Present(pixels, surface, sprite, pixelCamera);
        internal static Sprite Present(PhasePixels pixels, SpriteRenderer surface, Sprite previous, Camera camera)
        {
            if (previous != null) Destroy(previous);
            var sprite = Sprite.Create(pixels.Texture, new Rect(0, 0, pixels.Width, pixels.Height), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            surface.sprite = sprite; surface.transform.position = new Vector3(pixels.Width / 2f, -pixels.OriginY - pixels.Height / 2f, 0);
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = pixels.Width / (float)pixels.Height;
            camera.orthographicSize = pixels.Height / 2f;
            camera.transform.position = new Vector3(pixels.Width / 2f, -pixels.OriginY - pixels.Height / 2f, -10);
            return sprite;
        }
        void ResizeCamera()
        {
            var size = ViewSize(Screen.width, Screen.height);
            if (size.x != pixels.Width || size.y != pixels.Height)
            {
                Controller.ResizeRoom(size.x);
                departingFrame = arrivingFrame = null;
                pixels.SetView(size.x, size.y, 0);
                Render(); UpdateSurface();
            }
        }
        void Render() { pixels.Draw(Controller, !Won); if (Won) StampVictory(); }
        static readonly string[] victoryText = {
            "...#....#................#..........................",
            "...#...#...........##############.....###########...",
            "..#...#########......#.........................#....",
            ".##...........#......##########...............#.....",
            "..#.......#..#...............................#......",
            "..#.......#............######...............#.......",
            "..#.......#............#....#...............#.......",
            "..#....#..#.#..........######...............#.......",
            "..#...#...#..#..............................#.......",
            "..#..#....#...#...####.#####.####...........#.......",
            "..#.......#.......#..#.#...#.#..#...........#.......",
            "..#.......#.......####.#####.##.#...........#.......",
            "..#.......#.......#..#.#...#.#..#...........#.......",
            "..#.......#.......####.#####.#..#...........#.......",
            "..#.......#.......#..#..#.#..#..#...........#.......",
            "..#.....###.......#.##.#...#.#..##.......####......."
        }, retryIcon = { "..####.#.", ".#....##.", "#....###.", "#........", "#.......#", "#.......#", ".#.....#.", "..#####.." };
        void StampVictory()
        {
            var frame = new PixelFrame(Frame); int top = frame.Banner();
            frame.Blit(victoryText, (frame.Width - victoryText[0].Length) / 2, top + 8, PixelFrame.Light);
            frame.Blit(retryIcon, (frame.Width - retryIcon[0].Length) / 2, top + 29, PixelFrame.Light);
            frame.Apply();
        }
        void Win()
        {
            Won = true; jumpQueued = dashQueued = false; accumulator = 0;
            Render();
            Victory();
        }
        void Update()
        {
            if (transitioning) return;
            ResizeCamera();
            var k = Keyboard.current;
            if (InputPaused(k)) { jumpQueued = dashQueued = false; accumulator = 0; return; }
            if (departingFrame != null)
            {
                jumpQueued = dashQueued = false; accumulator = 0;
                transitionElapsed = transitionElapsed < 0 ? 0 : transitionElapsed + Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(transitionElapsed / TransitionDuration);
                pixels.Slide(departingFrame, arrivingFrame, 1 - Math.Pow(1 - t, 3), transitionDirection);
                if (t >= 1) departingFrame = arrivingFrame = null;
                return;
            }
            if (Won) { if (Pressed(k, 4, true)) SelectDemoRoom("1"); return; }
            var input = ReadControls(k);
            jumpQueued |= Pressed(k, 4, true);
            dashQueued |= Pressed(k, 5, true);
            accumulator += Time.deltaTime;
            while (accumulator >= PhaseController.Dt)
            {
                input.jump = jumpQueued; input.dash = dashQueued;
                Controller.Step(input); jumpQueued = dashQueued = false;
                accumulator -= PhaseController.Dt;
                if (Controller.ExitDirection != 0) { Travel(Controller.ExitDirection); break; }
            }
            if (!Won) pixels.Draw(Controller);
        }
        void Travel(int direction)
        {
            if (direction > 0 && level == Levels.Length - 1) { Win(); return; }
            string destination = direction < 0 ? leftScene : rightScene;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError($"Room scene is missing from the build: {destination}");
                if (direction < 0) Controller.exitLeft = false; else Controller.exitRight = false;
                Controller.player.x = direction < 0 ? 0 : Controller.Width - Controller.player.w;
                return;
            }
            traveler = Controller; arrivalScene = destination; arrivalDirection = direction;
            pixels.Draw(Controller, false); travelFrame = Frame.GetPixels32(); travelSize = new Vector2Int(pixels.Width, pixels.Height);
            transitioning = true;
            SceneManager.LoadSceneAsync(destination);
        }
        void OnApplicationFocus(bool focused)
        { if (!focused) jumpQueued = dashQueued = false; }
        public static Controls ReadControls(Keyboard k)
        {
            if (k == null) return default;
            return new Controls {
                x = (Pressed(k, 1) ? 1 : 0) - (Pressed(k, 0) ? 1 : 0),
                y = (Pressed(k, 3) ? 1 : 0) - (Pressed(k, 2) ? 1 : 0),
                jumpHeld = Pressed(k, 4), dashHeld = Pressed(k, 5), grab = Pressed(k, 6)
            };
        }
        internal static bool Pressed(Keyboard k, int action, bool edge = false) => k != null && keys[action].Any(key => edge ? k[key].wasPressedThisFrame : k[key].isPressed);
        // A dialog pauses input; after it closes, keys held from the dialog stay ignored until released.
        internal static bool InputPaused(Keyboard k)
        {
            if (!controlsOpen && !releaseControls) return false;
            if (!controlsOpen && (k == null || !k.anyKey.isPressed)) releaseControls = false;
            return true;
        }
        internal static void ReleaseInput() => releaseControls = true;
        public void SetControlsOpen(string value) => Pause(value);
        internal static void Pause(string value) { controlsOpen = value == "true"; releaseControls = true; }
        public void ConfigureControls(string json)
        { if (ApplyControls(json)) { jumpQueued = dashQueued = false; accumulator = 0; } }
        internal static bool ApplyControls(string json)
        {
            // JsonUtility turns null string-array entries into empty strings.
            if (json == null || json.Contains("null")) return false;
            KeyOverrides map;
            try { map = JsonUtility.FromJson<KeyOverrides>(json); }
            catch (ArgumentException) { return false; }
            if (map?.keys == null || map.keys.Length != defaultKeys.Length * 2) return false;
            var next = new Key[defaultKeys.Length][];
            for (int i = 0; i < next.Length; i++)
            {
                var action = new List<Key>();
                for (int slot = 0; slot < 2; slot++)
                {
                    string name = map.keys[i * 2 + slot];
                    if (name == "") continue;
                    if (name == "Shift") { action.Add(Key.LeftShift); action.Add(Key.RightShift); continue; }
                    if (!Enum.TryParse(name, out Key key) || !Enum.IsDefined(typeof(Key), key) || key.ToString() != name ||
                        key == Key.None || key == Key.Escape || key == Key.Tab || key == Key.Delete || (int)key >= Keyboard.KeyCount ||
                        key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                        key == Key.LeftMeta || key == Key.RightMeta) return false;
                    action.Add(key);
                }
                if (action.Count == 0) return false;
                next[i] = action.ToArray();
            }
            var all = next.SelectMany(action => action).ToArray();
            if (all.Distinct().Count() != all.Length) return false;
            keys = next;
            return true;
        }
        public static Vector2Int ViewSize(int width, int height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            return new Vector2Int(
                Math.Max(320, (int)Math.Floor(180.0 * width / height + .5)),
                Math.Max(180, (int)Math.Ceiling(320.0 * height / width)));
        }
        void OnDestroy() { if (sprite != null) Destroy(sprite); pixels?.Dispose(); }
    }
}
