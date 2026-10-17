using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SpiritOfTheSamuraiUltrawide
{
    // The game locks itself to 16:9 in two places:
    //  - InitManager / globalValues map the Quality setting to fixed 16:9 Screen.SetResolution presets.
    //  - LetterBoxer and UIResolutionManager reset camera.rect to a centered 16:9 viewport every Update.
    // Widening those exposes scene-authored 16:9 assumptions:
    //  - CanvasScalers scale by width, so the UI grows and runs off the top/bottom.
    //  - Videos are 16:9 RawImages, so the scene shows beside them instead of black bars.
    //  - Menus park their hidden pages one 16:9 screen-width off-center, which is now partly on screen.
    //  - Camera-attached backdrops and overlays (sky, vignette, damage border) are sized to the 16:9 view.
    [BepInPlugin("renodx.spiritofthesamurai.ultrawide", "Spirit of the Samurai Ultrawide", "1.12.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Rect FullViewport = new Rect(0f, 0f, 1f, 1f);

        // UI roots whose off-screen parking spots assume a 16:9 view.
        private static readonly string[] ClippedRoots = { "MenuManager", "PopupWindow" };

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<bool> EnableSceneInventory;
        internal static ConfigEntry<bool> LimitResolutionToDisplay;
        internal static ConfigEntry<bool> SkipIntros;

        // Start-up scenes driven by videoEnd that SkipIntros ends immediately. Story cutscenes and the ending are not listed.
        private static readonly string[] IntroScenes = { "VideointroLogos", "SentimentGA" };

        private void Awake()
        {
            Log = Logger;
            LimitResolutionToDisplay = Config.Bind("Display", "LimitResolutionToDisplay", true,
                "Cap fitted resolution presets to the display size (recommended for Steam Deck). Disable to allow supersampling.");
            EnableSceneInventory = Config.Bind("Diagnostics", "EnableSceneInventory", false,
                "Write scene inventories automatically for troubleshooting. F9 writes an inventory manually even when this is disabled.");
            SkipIntros = Config.Bind("General", "SkipIntros", true,
                "Skip the start-up logo videos and go straight to the main menu.");
            if (SkipIntros.Value)
            {
                try
                {
                    if (!UnityEngine.Rendering.SplashScreen.isFinished)
                    {
                        UnityEngine.Rendering.SplashScreen.Stop(UnityEngine.Rendering.SplashScreen.StopBehavior.StopImmediate);
                        Logger.LogInfo("[intro] stopped Unity splash screen.");
                    }
                }
                catch (System.Exception exception)
                {
                    Logger.LogWarning($"[intro] could not stop Unity splash: {exception.Message}");
                }
            }
            DialogueAdvance.Init(Config);
            DialogueHistory.Init(Config);
            PlayerLight.Init(Config);
            ModMenu.Init(Config);
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(DialogueAdvance));
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo($"Display {Display.main.systemWidth}x{Display.main.systemHeight}, ultrawide patches applied.");
        }

        private void OnDestroy() => ModMenu.Shutdown();
        private void OnApplicationQuit()
        {
            ModMenu.FlushConfig();
        }

        internal static void WriteSceneInventory(string phase)
        {
            if (!EnableSceneInventory.Value && phase != "manual") return;
            Log.LogInfo($"Scene inventory BEGIN phase={phase}, frame={Time.frameCount}.");
            try
            {
                var report = new StringBuilder();
                report.AppendLine($"Phase={phase}; frame={Time.frameCount}; screen={Screen.width}x{Screen.height}");
                int count = 0;
                foreach (Transform target in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (!target.gameObject.scene.IsValid()) continue;
                    ++count;
                    report.AppendLine($"OBJECT scene={target.gameObject.scene.name} path={GetPath(target)} active={target.gameObject.activeInHierarchy} scale={target.lossyScale}");
                    report.AppendLine($"  components={string.Join(", ", target.GetComponents<Component>().Select(component => component == null ? "missing" : component.GetType().FullName))}");
                    Graphic graphic = target.GetComponent<Graphic>();
                    if (graphic != null)
                    {
                        report.AppendLine($"  GRAPHIC enabled={graphic.enabled} color={graphic.color} texture={graphic.mainTexture?.name} shader={graphic.material?.shader?.name} rect={graphic.rectTransform.rect}");
                    }
                    foreach (Renderer renderer in target.GetComponents<Renderer>())
                    {
                        report.AppendLine($"  RENDERER type={renderer.GetType().Name} enabled={renderer.enabled} forcedOff={renderer.forceRenderingOff} sorting={renderer.sortingLayerName}/{renderer.sortingOrder} bounds={renderer.bounds}");
                        if (renderer is SpriteRenderer sprite)
                            report.AppendLine($"    sprite={sprite.sprite?.name} texture={sprite.sprite?.texture?.name} color={sprite.color}");
                        foreach (Material material in renderer.sharedMaterials)
                            if (material != null)
                                report.AppendLine($"    material={material.name} shader={material.shader?.name} texture={(material.HasProperty("_MainTex") ? material.mainTexture?.name : "")}");
                    }
                    Camera camera = target.GetComponent<Camera>();
                    if (camera != null)
                        report.AppendLine($"  CAMERA enabled={camera.enabled} hdr={camera.allowHDR} msaa={camera.allowMSAA} rect={camera.rect} mask={camera.cullingMask} depth={camera.depth} target={camera.targetTexture?.name}");
                    Canvas canvas = target.GetComponent<Canvas>();
                    if (canvas != null)
                        report.AppendLine($"  CANVAS enabled={canvas.enabled} mode={canvas.renderMode} camera={canvas.worldCamera?.name} sorting={canvas.sortingLayerName}/{canvas.sortingOrder}");
                }
                string directory = Path.Combine(Paths.BepInExRootPath, "diagnostics");
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, $"scene-inventory-{phase}.txt");
                File.WriteAllText(file, report.ToString());
                Log.LogInfo($"Scene inventory COMPLETE objects={count}, file='{file}'.");
            }
            catch (System.Exception exception)
            {
                Log.LogError($"Scene inventory FAILED: {exception}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ModMenu.Shutdown();
            PlayerLight.SceneLoaded();
            WriteSceneInventory("loaded");
            DialogueAdvance.SceneLoaded();
            if (mode == LoadSceneMode.Single)
            {
                var diagnostics = new GameObject("SamuraiSceneDiagnostics");
                diagnostics.AddComponent<SceneInventoryDiagnostic>();
                ModMenu.Attach(diagnostics);
                diagnostics.AddComponent<ModInputController>();
            }
            foreach (Transform root in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (root.name == "StaticUI" && root.gameObject.scene.IsValid() && root.parent != null &&
                    root.parent.name == "InGameUI" && root.GetComponent<RightHudAnchor>() == null)
                {
                    root.gameObject.AddComponent<RightHudAnchor>();
                    Logger.LogInfo($"[{scene.name}] anchored arrow/bag HUD toward the right edge.");
                }
                // The hold-to-skip sprite is authored at x=973 from the left of a 1920-wide canvas.
                // Re-anchor it once; leave the game's 0..270 fill scale and vertical layout untouched.
                if (root.name == "Skip" && root.gameObject.scene.IsValid() && root.parent != null &&
                    root.parent.GetComponent<Canvas>() != null && root.GetComponent<SpriteRenderer>() != null)
                {
                    RectTransform rect = root as RectTransform;
                    CanvasScaler scaler = root.GetComponentInParent<CanvasScaler>();
                    float reference_width = scaler != null ? scaler.referenceResolution.x : 1920f;
                    if (rect != null && reference_width > 0f &&
                        Mathf.Approximately(rect.anchorMin.x, 0f) && Mathf.Approximately(rect.anchorMax.x, 0f))
                    {
                        Vector2 position = rect.anchoredPosition;
                        position.x -= reference_width * 0.5f;
                        rect.anchorMin = new Vector2(0.5f, rect.anchorMin.y);
                        rect.anchorMax = new Vector2(0.5f, rect.anchorMax.y);
                        rect.anchoredPosition = position;
                        Logger.LogInfo($"[{scene.name}] centered cutscene skip-bar anchor: '{GetPath(root)}'.");
                    }
                }
                if (ClippedRoots.Contains(root.name) && root.gameObject.scene.IsValid() && root.GetComponent<SafeAreaClipper>() == null)
                {
                    root.gameObject.AddComponent<SafeAreaClipper>();
                    Logger.LogInfo($"[{scene.name}] clipping '{GetPath(root)}' to 16:9");
                }
            }
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera.gameObject.scene.IsValid() && camera.GetComponent<FullscreenSpriteFitter>() == null)
                {
                    camera.gameObject.AddComponent<FullscreenSpriteFitter>();
                }
            }
            foreach (VideoPlayer player in Resources.FindObjectsOfTypeAll<VideoPlayer>())
            {
                if (player.targetTexture == null || !player.gameObject.scene.IsValid()) continue;
                foreach (RawImage image in Resources.FindObjectsOfTypeAll<RawImage>())
                {
                    if (image.texture == player.targetTexture && image.gameObject.scene.IsValid()
                        && image.GetComponent<VideoBackdrop>() == null)
                    {
                        image.gameObject.AddComponent<VideoBackdrop>().Player = player;
                        Logger.LogInfo($"[{scene.name}] video backdrop behind '{GetPath(image.transform)}'");
                    }
                }
            }
        }

        internal static string GetPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

        // Both bool overloads are patched in case Mono inlines the 3-argument wrapper into game code.
        // Fitting to the display aspect is idempotent, so the nested call is a no-op on the second pass.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Screen), nameof(Screen.SetResolution), typeof(int), typeof(int), typeof(bool))]
        [HarmonyPatch(typeof(Screen), nameof(Screen.SetResolution), typeof(int), typeof(int), typeof(bool), typeof(int))]
        private static void FitResolutionToDisplay(ref int width, ref int height)
        {
            int displayWidth = Display.main.systemWidth;
            int displayHeight = Display.main.systemHeight;
            if (displayWidth < 2 || displayHeight < 2 || width < 2 || height < 2) return;
            float display_aspect = (float)displayWidth / displayHeight;
            if ((float)width / height < display_aspect)
            {
                width = Mathf.RoundToInt(height * display_aspect) & ~1;
            }
            else
            {
                height = Mathf.RoundToInt(width / display_aspect) & ~1;
            }
            // High quality presets can exceed the Deck's 1280x800 display. Keep lower
            // presets available, but avoid rendering above native resolution by default.
            if (LimitResolutionToDisplay.Value && (width > displayWidth || height > displayHeight))
            {
                width = displayWidth & ~1;
                height = displayHeight & ~1;
            }
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LetterBoxer), "PerformSizing")]
        private static bool LetterBoxerFullViewport(Camera ___cam)
        {
            ___cam.rect = FullViewport;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIResolutionManager), nameof(UIResolutionManager.ScaleUI))]
        private static bool UIResolutionManagerFullViewport(Camera ____renderCamera)
        {
            if (____renderCamera != null)
            {
                ____renderCamera.rect = FullViewport;
            }
            return false;
        }

        // videoEnd plays a scene's video and then loads the next scene from Exit(). Calling Exit() once Start() has
        // set up its fader and input leaves the same fade and load as the game's own skip, without playing the video.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(videoEnd), "Start")]
        private static void SkipIntroVideo(videoEnd __instance)
        {
            string scene = __instance.gameObject.scene.name;
            if (!SkipIntros.Value || !IntroScenes.Contains(scene)) return;
            Log.LogInfo($"[{scene}] skipping intro video.");
            VideoPlayer player = __instance.GetComponent<VideoPlayer>();
            if (player != null) player.enabled = false;
            AccessTools.Method(typeof(videoEnd), "Exit").Invoke(__instance, null);
        }

        // Expand scales by the smaller axis: height on wide screens, unchanged at 16:9 and narrower.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CanvasScaler), "HandleScaleWithScreenSize")]
        private static void CanvasScalerExpand(CanvasScaler __instance)
        {
            __instance.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }

    public class SceneInventoryDiagnostic : MonoBehaviour
    {
        private int capture_frame;
        private bool captured;

        private void Awake()
        {
            capture_frame = Time.frameCount + 120;
            Plugin.Log.LogInfo($"Scene diagnostic controller attached; delayed inventory scheduled at frame={capture_frame}.");
        }

        private void Update()
        {
            if (!captured && Time.frameCount >= capture_frame)
            {
                captured = true;
                Plugin.WriteSceneInventory("settled");
            }
            if (Input.GetKeyDown(KeyCode.F9))
                Plugin.WriteSceneInventory("manual");
        }
    }

    // Horizontal screen-space extent of renderers and UI graphics, and the centered 16:9 area vanilla showed.
    internal static class ScreenSpan
    {
        private static readonly Vector3[] Corners = new Vector3[8];

        public static float SafeLeft => (Screen.width - Screen.height * 16f / 9f) / 2f;
        public static float SafeRight => Screen.width - SafeLeft;

        public static Vector2 Of(Camera camera, Renderer renderer)
        {
            Bounds b = renderer.bounds;
            for (int i = 0; i < 8; ++i)
            {
                Corners[i] = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, ((i & 4) >> 1) - 1));
            }
            return Project(camera, 8);
        }

        public static Vector2 Of(Camera camera, Graphic graphic)
        {
            graphic.rectTransform.GetWorldCorners(Corners);
            return Project(camera, 4);
        }

        private static Vector2 Project(Camera camera, int count)
        {
            var span = new Vector2(float.MaxValue, float.MinValue);
            for (int i = 0; i < count; ++i)
            {
                float x = RectTransformUtility.WorldToScreenPoint(camera, Corners[i]).x;
                span.x = Mathf.Min(span.x, x);
                span.y = Mathf.Max(span.y, x);
            }
            return span;
        }
    }

    // Cull whole page groups, never individual labels/buttons. Their bounds can be stale or narrow
    // while a page is parked, and manually culled Graphics need a rebuild when shown again.
    public class SafeAreaClipper : MonoBehaviour
    {
        private class Page
        {
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<Graphic> Graphics = new List<Graphic>();
        }

        private readonly List<Page> pages = new List<Page>();
        private readonly HashSet<Renderer> hidden_renderers = new HashSet<Renderer>();
        private readonly HashSet<Graphic> hidden_graphics = new HashSet<Graphic>();
        private static Camera[] camera_buffer = new Camera[0];

        private void LateUpdate()
        {
            if (Time.frameCount % 30 == 0 || pages.Count == 0)
            {
                RestoreVisibility();
                int count = transform.childCount;
                while (pages.Count < count) pages.Add(new Page());
                if (pages.Count > count) pages.RemoveRange(count, pages.Count - count);
                for (int i = 0; i < count; i++)
                {
                    Transform child = transform.GetChild(i);
                    child.GetComponentsInChildren(true, pages[i].Renderers);
                    child.GetComponentsInChildren(true, pages[i].Graphics);
                }
            }
            Canvas canvas = GetComponentInParent<Canvas>();
            bool overlay = canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Camera camera = overlay ? null
                : canvas != null && canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera
                : FallbackCamera();
            if (!overlay && camera == null)
            {
                RestoreVisibility();
                return;
            }
            foreach (Page page in pages)
            {
                Vector2 span = new Vector2(float.MaxValue, float.MinValue);
                bool has_bounds = false;
                foreach (Renderer renderer in page.Renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Vector2 extent = ScreenSpan.Of(camera, renderer);
                    span.x = Mathf.Min(span.x, extent.x);
                    span.y = Mathf.Max(span.y, extent.y);
                    has_bounds = true;
                }
                foreach (Graphic graphic in page.Graphics)
                {
                    if (graphic == null || !graphic.isActiveAndEnabled) continue;
                    Vector2 extent = ScreenSpan.Of(camera, graphic);
                    span.x = Mathf.Min(span.x, extent.x);
                    span.y = Mathf.Max(span.y, extent.y);
                    has_bounds = true;
                }
                bool outside = has_bounds && Screen.width > Screen.height * 16f / 9f
                    && (span.y <= ScreenSpan.SafeLeft || span.x >= ScreenSpan.SafeRight);
                foreach (Renderer renderer in page.Renderers)
                {
                    if (renderer == null) continue;
                    if (outside && !renderer.forceRenderingOff)
                    {
                        renderer.forceRenderingOff = true;
                        hidden_renderers.Add(renderer);
                    }
                    else if (!outside && hidden_renderers.Remove(renderer))
                    {
                        renderer.forceRenderingOff = false;
                    }
                }
                foreach (Graphic graphic in page.Graphics)
                {
                    if (graphic == null) continue;
                    if (outside && !graphic.canvasRenderer.cull)
                    {
                        graphic.canvasRenderer.cull = true;
                        hidden_graphics.Add(graphic);
                    }
                    else if (!outside && hidden_graphics.Remove(graphic))
                    {
                        graphic.canvasRenderer.cull = false;
                        graphic.SetVerticesDirty();
                    }
                }
            }
        }

        private static Camera FallbackCamera()
        {
            Camera main = Camera.main;
            if (main != null) return main;
            int count = Camera.allCamerasCount;
            if (count == 0) return null;
            if (camera_buffer.Length < count) camera_buffer = new Camera[count];
            int copied = Camera.GetAllCameras(camera_buffer);
            return copied > 0 ? camera_buffer[0] : null;
        }

        private void RestoreVisibility()
        {
            foreach (Renderer renderer in hidden_renderers)
            {
                if (renderer != null) renderer.forceRenderingOff = false;
            }
            hidden_renderers.Clear();
            foreach (Graphic graphic in hidden_graphics)
            {
                if (graphic == null) continue;
                graphic.canvasRenderer.cull = false;
                graphic.SetVerticesDirty();
            }
            hidden_graphics.Clear();
        }

        private void OnDisable()
        {
            RestoreVisibility();
        }
    }
    // Widens camera-attached and UI sprites that were sized to cover the 16:9 view (sky backdrop, damage border)
    // so they cover the full screen, scaling around their own pivot.
    public class FullscreenSpriteFitter : MonoBehaviour
    {
        private class Fit
        {
            public float Factor;
            public float AppliedScaleX;
        }

        private const int RefreshFrames = 30;
        private readonly Dictionary<SpriteRenderer, Fit> fits = new Dictionary<SpriteRenderer, Fit>();
        private Camera owner;

        private void Awake()
        {
            owner = GetComponent<Camera>();
        }

        // Another camera at (nearly) the same pose and lens as this one, e.g. a camera that renders
        // the UI canvas separately and copies this camera each frame, projects sprites as this one does.
        private bool SharesViewWith(Camera other)
        {
            return other != null && other != owner && other.orthographic == owner.orthographic
                && Vector3.SqrMagnitude(other.transform.position - owner.transform.position) < 1f
                && Quaternion.Angle(other.transform.rotation, owner.transform.rotation) < 1f
                && Mathf.Abs(other.fieldOfView - owner.fieldOfView) < 0.5f
                && Mathf.Abs(other.orthographicSize - owner.orthographicSize) < 0.5f;
        }

        private void LateUpdate()
        {
            // These are horizontal expansion fixes; 16:10 handhelds do not need them.
            if (Screen.width <= Screen.height * 16f / 9f && fits.Count == 0) return;
            // Animated sprites (the pulsing damage border) get their scale rewritten every frame, so reapply the
            // stored factor after animation whenever the scale is no longer the one applied last.
            foreach (KeyValuePair<SpriteRenderer, Fit> entry in fits)
            {
                if (entry.Key == null) continue;
                Transform t = entry.Key.transform;
                if (Mathf.Approximately(t.localScale.x, entry.Value.AppliedScaleX)) continue;
                Vector3 local = t.localScale;
                local.x *= entry.Value.Factor;
                t.localScale = local;
                entry.Value.AppliedScaleX = local.x;
            }
            if (Time.frameCount % RefreshFrames != 0 || !owner.isActiveAndEnabled) return;
            IEnumerable<SpriteRenderer> sprites = GetComponentsInChildren<SpriteRenderer>();
            foreach (Canvas canvas in FindObjectsOfType<Canvas>())
            {
                if (canvas.isRootCanvas && (canvas.worldCamera == owner || SharesViewWith(canvas.worldCamera)))
                {
                    sprites = sprites.Concat(canvas.GetComponentsInChildren<SpriteRenderer>());
                }
            }
            float width = Screen.width;
            float safe_width = ScreenSpan.SafeRight - ScreenSpan.SafeLeft;
            float margin = safe_width * 0.05f;
            // Widen to reach this far past each screen edge. The camera's lens and position keep
            // moving after the first fit, so a thin margin leaves strips at the edges later.
            float overscan = width * 0.06f;
            float regrow_below = width * 0.02f;
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.sprite == null || !sprite.enabled || sprite.GetComponentInParent<SafeAreaClipper>() != null) continue;
                Transform t = sprite.transform;
                Vector2 span = ScreenSpan.Of(owner, sprite);
                bool known = fits.TryGetValue(sprite, out Fit fit);
                if (known && span.x <= -regrow_below && span.y >= width + regrow_below) continue;
                if (!known)
                {
                    bool covers_safe_area = span.x <= ScreenSpan.SafeLeft + margin && span.y >= ScreenSpan.SafeRight - margin;
                    bool covers_screen = span.x <= 0f && span.y >= width;
                    if (!covers_safe_area || covers_screen) continue;
                }

                float pivot = owner.WorldToScreenPoint(t.position).x;
                if (pivot <= span.x || pivot >= span.y) continue;
                float scale = Mathf.Max((pivot + overscan) / (pivot - span.x), (width + overscan - pivot) / (span.y - pivot));
                if (scale <= 1f) continue;
                Vector3 local = t.localScale;
                local.x *= scale;
                t.localScale = local;
                if (known)
                {
                    fit.Factor *= scale;
                    fit.AppliedScaleX = local.x;
                }
                else
                {
                    fits[sprite] = new Fit { Factor = scale, AppliedScaleX = local.x };
                }
                Plugin.Log.LogInfo($"{(known ? "re-widened" : "widened")} '{Plugin.GetPath(t)}' x{scale:F2} (was {span.x:F0}..{span.y:F0} of {width:F0})");
            }
        }
    }

    // Overlay bars cover only the area outside a full-screen video. A separate root canvas avoids
    // ScreenSpaceCamera sorting against world sprites, including videos on the root canvas itself.
    public class VideoBackdrop : MonoBehaviour
    {
        public VideoPlayer Player;
        private RawImage video;
        private Rect original_uv;
        private Canvas backdrop_canvas;
        private readonly Image[] bars = new Image[4];
        private readonly Vector3[] corners = new Vector3[4];
        private bool was_visible;
        private static VideoPlayer[] players = new VideoPlayer[0];
        private static int players_scan_frame = -1;
        private static int players_scene = -1;

        private void Awake()
        {
            video = GetComponent<RawImage>();
            original_uv = video.uvRect;
            var backdrop_object = new GameObject("UltrawideVideoBars", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(backdrop_object, gameObject.scene);
            backdrop_canvas = backdrop_object.GetComponent<Canvas>();
            backdrop_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            backdrop_canvas.sortingOrder = 32760;
            backdrop_canvas.enabled = false;
            for (int i = 0; i < bars.Length; ++i)
            {
                var bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(backdrop_object.transform, false);
                bars[i] = bar.GetComponent<Image>();
                bars[i].raycastTarget = false;
            }
        }

        private void LateUpdate()
        {
            // Different cutscene objects reuse the same RenderTexture/RawImage. Never retain the first
            // matching player as the sole owner, and exclude idle black-video placeholder playback.
            int scene = SceneManager.GetActiveScene().handle;
            if (players_scan_frame != Time.frameCount &&
                (players_scene != scene || players.Length == 0 || Time.frameCount % 30 == 0))
            {
                players = Resources.FindObjectsOfTypeAll<VideoPlayer>();
                players_scan_frame = Time.frameCount;
                players_scene = scene;
            }
            VideoPlayer active_player = null;
            foreach (VideoPlayer candidate in players)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid() || !candidate.isActiveAndEnabled
                    || candidate.targetTexture == null || candidate.targetTexture != video.texture
                    || !(candidate.isPlaying || candidate.isPaused)) continue;
                videoPlay cutscene = candidate.GetComponent<videoPlay>();
                videoPlayConversations conversation = candidate.GetComponent<videoPlayConversations>();
                if (cutscene != null && (cutscene.rawimage != video || candidate.clip == cutscene.videoBlack)) continue;
                if (conversation != null && conversation.rawimage != video) continue;
                active_player = candidate;
                break;
            }
            if (Player != active_player) was_visible = false;
            Player = active_player;
            Canvas root = video.canvas != null ? video.canvas.rootCanvas : null;
            bool visible = Player != null && (Player.isPlaying || Player.isPaused)
                && video.isActiveAndEnabled && root != null && root.isActiveAndEnabled
                && video.texture == Player.targetTexture && !video.canvasRenderer.cull;
            Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            if (visible && (root.renderMode == RenderMode.ScreenSpaceOverlay || camera != null))
            {
                video.rectTransform.GetWorldCorners(corners);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                // Save-slot thumbnails share the RenderTexture; require a centered, screen-height video.
                visible = max.y - min.y >= Screen.height * 0.9f
                    && min.x < Screen.width * 0.5f && max.x > Screen.width * 0.5f
                    && min.y < Screen.height * 0.5f && max.y > Screen.height * 0.5f;
                if (visible)
                {
                    // The RawImage often fills the whole (wider) canvas, which stretches the video. Show it at its
                    // own aspect instead: remap the UVs into a centered rect and let the bars cover the rest.
                    Rect uv = original_uv;
                    Vector2 size = max - min;
                    float texture_aspect = (float)video.texture.width / video.texture.height;
                    float box_aspect = size.x / size.y;
                    if (box_aspect > texture_aspect * 1.01f)
                    {
                        float fraction = texture_aspect / box_aspect;
                        float inset = size.x * (1f - fraction) * 0.5f;
                        min.x += inset;
                        max.x -= inset;
                        uv = new Rect(-(1f - fraction) / (2f * fraction), 0f, 1f / fraction, 1f);
                    }
                    else if (box_aspect < texture_aspect / 1.01f)
                    {
                        float fraction = box_aspect / texture_aspect;
                        float inset = size.y * (1f - fraction) * 0.5f;
                        min.y += inset;
                        max.y -= inset;
                        uv = new Rect(0f, -(1f - fraction) / (2f * fraction), 1f, 1f / fraction);
                    }
                    if (video.uvRect != uv) video.uvRect = uv;
                    min = new Vector2(Mathf.Clamp01(min.x / Screen.width), Mathf.Clamp01(min.y / Screen.height));
                    max = new Vector2(Mathf.Clamp01(max.x / Screen.width), Mathf.Clamp01(max.y / Screen.height));
                    SetBar(0, Vector2.zero, new Vector2(min.x, 1f));
                    SetBar(1, new Vector2(max.x, 0f), Vector2.one);
                    SetBar(2, new Vector2(min.x, 0f), new Vector2(max.x, min.y));
                    SetBar(3, new Vector2(min.x, max.y), new Vector2(max.x, 1f));
                    if (!was_visible)
                    {
                        Plugin.Log.LogInfo($"video bars active for '{Plugin.GetPath(transform)}' via '{Plugin.GetPath(Player.transform)}' ({Screen.width}x{Screen.height}, video {min}..{max})");
                    }
                }
            }
            else
            {
                visible = false;
            }
            if (!visible) RestoreUv();
            backdrop_canvas.enabled = visible;
            was_visible = visible;
        }

        private void SetBar(int index, Vector2 min, Vector2 max)
        {
            RectTransform rect = bars[index].rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            bars[index].color = new Color(0f, 0f, 0f, video.color.a * video.canvasRenderer.GetInheritedAlpha());
        }

        private void RestoreUv()
        {
            if (video != null && video.uvRect != original_uv) video.uvRect = original_uv;
        }

        private void OnDisable()
        {
            if (backdrop_canvas != null) backdrop_canvas.enabled = false;
            RestoreUv();
            was_visible = false;
        }

        private void OnDestroy()
        {
            if (backdrop_canvas != null) Destroy(backdrop_canvas.gameObject);
        }
    }
}



