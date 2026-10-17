using UnityEngine;

namespace SpiritOfTheSamuraiUltrawide
{
    // Drawn by the scene-owned input host; never takes focus or changes game input/time.
    internal sealed class ShortcutFeedback : MonoBehaviour
    {
        private static ShortcutFeedback window;
        private const float Duration = 5f;
        private static string message;
        private static float expires;
        private static GUIStyle style;
        private static GUIStyle detail_style;
        private static string detail;
        private static int theme_revision = -1;

        internal static void Init(GameObject host)
        {
            Clear();
            window = host.AddComponent<ShortcutFeedback>();
            window.enabled = false;
        }

        private void Update()
        {
            if (Time.unscaledTime >= expires) Clear();
        }

        private void OnGUI() => Draw();

        internal static void Show(string text, string explanation = null)
        {
            if (window == null) return;
            message = text;
            detail = explanation;
            window.enabled = true;
            expires = Time.unscaledTime + Duration;
        }

        internal static void Clear()
        {
            message = null;
            if (window != null) window.enabled = false;
        }

        internal static void Draw()
        {
            if (message == null) return;
            float remaining = expires - Time.unscaledTime;
            if (remaining <= 0f) { Clear(); return; }
            if (Event.current.type != EventType.Repaint) return;
            if (style == null || theme_revision != GameUiTheme.Revision)
            {
                style = GameUiTheme.TextStyle(25, Color.white, true);
                style.alignment = TextAnchor.MiddleCenter;
                detail_style = GameUiTheme.TextStyle(21, GameUiTheme.Ink, false, true);
                detail_style.alignment = TextAnchor.MiddleCenter;
                theme_revision = GameUiTheme.Revision;
            }

            Matrix4x4 previous_matrix = GUI.matrix;
            Color previous_color = GUI.color;
            int previous_depth = GUI.depth;
            try
            {
                float scale = Screen.height / 1080f;
                if (scale <= 0f) return;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.depth = -200;
                float width = Mathf.Min(680f, Screen.width / scale - 32f);
                float detail_height = string.IsNullOrEmpty(detail) ? 0f : detail_style.CalcHeight(new GUIContent(detail), width - 48f);
                var rect = new Rect((Screen.width / scale - width) * 0.5f, 65f, width, 70f + detail_height);
                float alpha = Mathf.Clamp01(remaining / 0.35f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GameUiTheme.Panel(rect, false);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 12f, width - 48f, 40f), message, style);
                if (detail_height > 0f)
                    GUI.Label(new Rect(rect.x + 24f, rect.y + 54f, width - 48f, detail_height), detail, detail_style);
            }
            finally
            {
                GUI.matrix = previous_matrix;
                GUI.color = previous_color;
                GUI.depth = previous_depth;
            }
        }
    }
}
