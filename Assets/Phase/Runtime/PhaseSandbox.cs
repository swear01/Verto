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

        void Awake()
        {
            Controller = new PhaseController(GetComponentsInChildren<PhaseTerrain>().Select(t => t.CreateBlock()), spawn.x, spawn.y,
                GetComponentsInChildren<PhaseMovingPlatform>().Select(m => m.CreatePlatform()), gears: GetComponentsInChildren<PhaseClimbGear>().Select(g => g.CreateGear()),
                spikes: GetComponentsInChildren<PhaseSpike>().Select(t => t.CreateSpike()),
                lasers: GetComponentsInChildren<PhaseLaser>().Select(t => t.CreateLaser()),
                crystals: GetComponentsInChildren<PhaseCrystal>().Select(t => t.CreateCrystal()));
            Controller.exitLeft = !string.IsNullOrEmpty(leftScene);
            Controller.exitRight = !string.IsNullOrEmpty(rightScene);
            bool arriving = traveler != null && gameObject.scene.name == arrivalScene;
            var size = arriving && travelFrame != null ? travelSize : ViewSize(Screen.width, Screen.height);
            Controller.ResizeRoom(size.x);
            if (arriving)
            {
                if (checkpointOnEntry)
                {
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
        void UpdateSurface()
        {
            if (sprite != null) Destroy(sprite);
            sprite = Sprite.Create(Frame, new Rect(0, 0, pixels.Width, pixels.Height), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            surface.sprite = sprite; surface.transform.position = new Vector3(pixels.Width / 2f, -pixels.OriginY - pixels.Height / 2f, 0);
            pixelCamera.rect = new Rect(0, 0, 1, 1);
            pixelCamera.aspect = pixels.Width / (float)pixels.Height;
            pixelCamera.orthographicSize = pixels.Height / 2f;
            pixelCamera.transform.position = new Vector3(pixels.Width / 2f, -pixels.OriginY - pixels.Height / 2f, -10);
        }
        void ResizeCamera()
        {
            var size = ViewSize(Screen.width, Screen.height);
            if (size.x != pixels.Width || size.y != pixels.Height)
            {
                Controller.ResizeRoom(size.x);
                departingFrame = arrivingFrame = null;
                pixels.SetView(size.x, size.y, 0);
                pixels.Draw(Controller); UpdateSurface();
            }
        }
        void Update()
        {
            if (transitioning) return;
            ResizeCamera();
            var k = Keyboard.current;
            if (controlsOpen || releaseControls)
            {
                jumpQueued = dashQueued = false; accumulator = 0;
                if (!controlsOpen && (k == null || !k.anyKey.isPressed)) releaseControls = false;
                return;
            }
            if (departingFrame != null)
            {
                jumpQueued = dashQueued = false; accumulator = 0;
                transitionElapsed = transitionElapsed < 0 ? 0 : transitionElapsed + Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(transitionElapsed / TransitionDuration);
                pixels.Slide(departingFrame, arrivingFrame, 1 - Math.Pow(1 - t, 3), transitionDirection);
                if (t >= 1) departingFrame = arrivingFrame = null;
                return;
            }
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
            pixels.Draw(Controller);
        }
        void Travel(int direction)
        {
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
        static bool Pressed(Keyboard k, int action, bool edge = false) => k != null && keys[action].Any(key => edge ? k[key].wasPressedThisFrame : k[key].isPressed);
        public void SetControlsOpen(string value) { controlsOpen = value == "true"; releaseControls = true; }
        public void ConfigureControls(string json)
        {
            // JsonUtility turns null string-array entries into empty strings.
            if (json == null || json.Contains("null")) return;
            KeyOverrides map;
            try { map = JsonUtility.FromJson<KeyOverrides>(json); }
            catch (ArgumentException) { return; }
            if (map?.keys == null || map.keys.Length != defaultKeys.Length * 2) return;
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
                        key == Key.LeftMeta || key == Key.RightMeta) return;
                    action.Add(key);
                }
                if (action.Count == 0) return;
                next[i] = action.ToArray();
            }
            var all = next.SelectMany(action => action).ToArray();
            if (all.Distinct().Count() != all.Length) return;
            keys = next;
            jumpQueued = dashQueued = false; accumulator = 0;
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
