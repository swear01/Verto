using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Phase
{
    public sealed class PhaseSandbox : MonoBehaviour
    {
        public Camera pixelCamera;
        public Material pixelMaterial;
        public string leftScene, rightScene;
        public Vector2Int spawn = new Vector2Int(32, 149);
        public PhaseController Controller { get; private set; }
        public Texture2D Frame => pixels?.Texture;
        PhasePixels pixels;
        Sprite sprite;
        double accumulator;
        bool jumpQueued, dashQueued, transitioning;
        public const float TransitionDuration = .18f;
        public bool InTransition => transitioning || departingFrame != null;
        Color32[] departingFrame, arrivingFrame;
        float transitionElapsed = -1;
        int transitionDirection;
        static PhaseController traveler;
        static Color32[] travelFrame;
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
        static void ClearTravel() { traveler = null; travelFrame = null; arrivalScene = null; arrivalDirection = 0; }

        void Awake()
        {
            var platform = GetComponentInChildren<PhaseMovingPlatform>();
            Controller = new PhaseController(GetComponentsInChildren<PhaseTerrain>().Select(t => t.CreateBlock()), spawn.x, spawn.y, platform != null ? platform.CreatePlatform() : null);
            Controller.exitLeft = !string.IsNullOrEmpty(leftScene);
            Controller.exitRight = !string.IsNullOrEmpty(rightScene);
            if (traveler != null && gameObject.scene.name == arrivalScene)
            {
                Controller.EnterFrom(traveler, arrivalDirection);
                departingFrame = travelFrame; transitionDirection = arrivalDirection;
            }
            ClearTravel();
            pixels = new PhasePixels();
            pixels.Draw(Controller);
            if (departingFrame != null)
            {
                arrivingFrame = Frame.GetPixels32();
                pixels.Slide(departingFrame, arrivingFrame, 0, transitionDirection);
            }
            var surface = new GameObject("Pixel surface"); surface.transform.SetParent(transform);
            surface.transform.position = new Vector3(160, -90, 0);
            sprite = Sprite.Create(Frame, new Rect(0, 0, 320, 180), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            var renderer = surface.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = pixelMaterial;
            ResizeCamera();
        }
        void ResizeCamera()
        {
            var viewport = Viewport(Screen.width, Screen.height);
            pixelCamera.pixelRect = new Rect(viewport.x, Screen.height - viewport.y - viewport.height, viewport.width, viewport.height);
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
                Controller.player.x = direction < 0 ? 0 : 320 - Controller.player.w;
                return;
            }
            traveler = Controller; arrivalScene = destination; arrivalDirection = direction;
            pixels.Draw(Controller, false); travelFrame = Frame.GetPixels32();
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
            KeyOverrides map;
            try { map = JsonUtility.FromJson<KeyOverrides>(json); }
            catch (ArgumentException) { return; }
            if (map?.keys == null || map.keys.Length != defaultKeys.Length) return;
            var next = new Key[defaultKeys.Length][];
            for (int i = 0; i < next.Length; i++)
            {
                if (map.keys[i] == "") { next[i] = defaultKeys[i]; continue; }
                if (!Enum.TryParse(map.keys[i], out Key key) || !Enum.IsDefined(typeof(Key), key) ||
                    key == Key.None || key == Key.Escape || key == Key.Tab || (int)key >= Keyboard.KeyCount ||
                    key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftMeta || key == Key.RightMeta) return;
                next[i] = new[] { key };
            }
            var all = next.SelectMany(action => action).ToArray();
            if (all.Distinct().Count() != all.Length) return;
            keys = next;
            jumpQueued = dashQueued = false; accumulator = 0;
        }
        public static Rect Viewport(int width, int height)
        {
            int scale = Mathf.Max(1, Mathf.Min(width / 320, height / 180));
            return new Rect(Mathf.Floor((width - 320 * scale) / 2f), Mathf.Floor((height - 180 * scale) / 2f), 320 * scale, 180 * scale);
        }
        void OnDestroy() { if (sprite != null) Destroy(sprite); pixels?.Dispose(); }
    }
}
