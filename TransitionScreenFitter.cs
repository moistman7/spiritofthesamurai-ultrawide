using UnityEngine;

namespace SpiritOfTheSamuraiUltrawide
{
    // Only the native loading/fade surface. Never changes its color, visibility or animation.
    internal sealed class TransitionScreenFitter : MonoBehaviour
    {
        private SpriteRenderer surface;
        private UnityEngine.UI.Graphic graphic;
        private readonly Vector3[] corners = new Vector3[4];
        private Canvas canvas;
        private Camera camera;
        private static Camera[] cameras = new Camera[0];

        internal static void Attach(GameObject target)
        {
            if (target == null || (target.GetComponent<SpriteRenderer>() == null && target.GetComponent<UnityEngine.UI.Graphic>() == null) || target.GetComponent<TransitionScreenFitter>() != null) return;
            target.AddComponent<TransitionScreenFitter>().Fit();
            Plugin.Log.LogInfo("[transition] fitting loading fade to screen: " + Plugin.GetPath(target.transform));
        }

        private void Awake()
        {
            surface = GetComponent<SpriteRenderer>();
            graphic = GetComponent<UnityEngine.UI.Graphic>();
            canvas = GetComponentInParent<Canvas>();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (surface != null ? surface.sprite == null || !surface.enabled || surface.color.a <= 0f :
                graphic == null || !graphic.enabled || graphic.color.a <= 0f) return;
            bool overlay = graphic != null && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            if (canvas != null && canvas.worldCamera != null) camera = canvas.worldCamera;
            if (!overlay && (camera == null || !camera.isActiveAndEnabled))
            {
                int count = Camera.allCamerasCount;
                if (cameras.Length < count) cameras = new Camera[count];
                count = Camera.GetAllCameras(cameras);
                camera = null;
                for (int index = 0; index < count; index++)
                {
                    Camera candidate = cameras[index];
                    if (candidate != null && candidate.isActiveAndEnabled && candidate.targetTexture == null &&
                        (candidate.cullingMask & (1 << gameObject.layer)) != 0) { camera = candidate; break; }
                }
            }
            if (!overlay && (camera == null || !camera.isActiveAndEnabled)) return;
            Camera projection = overlay ? null : camera;
            if (graphic != null) graphic.rectTransform.GetWorldCorners(corners);
            else
            {
                // Project the actual sprite plane, not its inflated world-space AABB.
                Bounds bounds = surface.sprite.bounds;
                for (int index = 0; index < 4; index++)
                {
                    Vector3 local = bounds.center + new Vector3((index & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                        (index & 2) == 0 ? -bounds.extents.y : bounds.extents.y, 0f);
                    corners[index] = transform.TransformPoint(local);
                }
            }
            var span = new Vector2(float.MaxValue, float.MinValue);
            for (int index = 0; index < 4; index++)
            {
                float x = RectTransformUtility.WorldToScreenPoint(projection, corners[index]).x;
                span.x = Mathf.Min(span.x, x);
                span.y = Mathf.Max(span.y, x);
            }
            float pivot = RectTransformUtility.WorldToScreenPoint(projection, transform.position).x;
            float factor = ModLogic.FullscreenWidthFactor(span.x, span.y, pivot, Screen.width);
            if (factor <= 1.0001f) return;
            Vector3 scale = transform.localScale;
            scale.x *= factor;
            transform.localScale = scale;
        }
    }
}
