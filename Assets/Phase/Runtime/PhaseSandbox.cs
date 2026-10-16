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
        static PhaseController traveler;
        static string arrivalScene;
        static int arrivalDirection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearTravel() { traveler = null; arrivalScene = null; arrivalDirection = 0; }

        void Awake()
        {
            var platform = GetComponentInChildren<PhaseMovingPlatform>();
            Controller = new PhaseController(GetComponentsInChildren<PhaseTerrain>().Select(t => t.CreateBlock()), spawn.x, spawn.y, platform != null ? platform.CreatePlatform() : null);
            Controller.exitLeft = !string.IsNullOrEmpty(leftScene);
            Controller.exitRight = !string.IsNullOrEmpty(rightScene);
            if (traveler != null && gameObject.scene.name == arrivalScene) Controller.EnterFrom(traveler, arrivalDirection);
            ClearTravel();
            pixels = new PhasePixels();
            pixels.Draw(Controller);
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
            var input = ReadControls(k);
            jumpQueued |= k != null && (k.zKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
            dashQueued |= k != null && k.xKey.wasPressedThisFrame;
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
            transitioning = true;
            SceneManager.LoadSceneAsync(destination);
        }
        void OnApplicationFocus(bool focused)
        { if (!focused) jumpQueued = dashQueued = false; }
        public static Controls ReadControls(Keyboard k)
        {
            if (k == null) return default;
            return new Controls {
                x = (k.rightArrowKey.isPressed || k.dKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed || k.aKey.isPressed ? 1 : 0),
                y = (k.downArrowKey.isPressed || k.sKey.isPressed ? 1 : 0) - (k.upArrowKey.isPressed || k.wKey.isPressed ? 1 : 0),
                jumpHeld = k.zKey.isPressed || k.spaceKey.isPressed,
                dashHeld = k.xKey.isPressed,
                grab = k.cKey.isPressed || k.leftShiftKey.isPressed || k.rightShiftKey.isPressed
            };
        }
        public static Rect Viewport(int width, int height)
        {
            int scale = Mathf.Max(1, Mathf.Min(width / 320, height / 180));
            return new Rect(Mathf.Floor((width - 320 * scale) / 2f), Mathf.Floor((height - 180 * scale) / 2f), 320 * scale, 180 * scale);
        }
        void OnDestroy() { if (sprite != null) Destroy(sprite); pixels?.Dispose(); }
    }
}
