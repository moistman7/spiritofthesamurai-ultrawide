using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpiritOfTheSamuraiUltrawide
{
    // Borrow loaded game assets at runtime. No game textures or fonts are shipped with the mod.
    internal static class GameUiTheme
    {
        internal static readonly Color Ink = new Color(1f, 0.902f, 0.843f);
        internal static readonly Color Gold = new Color(0.961f, 0.616f, 0.141f);
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> wanted = new HashSet<string> { "MapInfoPanelBox", "fondo_tuto", "menuFrame", "menuFrame_hightlighted", "menuFrame_Clicked", "XB1_A", "XB1_B", "XB1_DPadUp" };
        private static readonly List<Image> images = new List<Image>();
        private static readonly List<Text> texts = new List<Text>();
        private static readonly float[] dx = new float[4], dy = new float[4], sx = new float[4], sy = new float[4];
        private static Font font;
        private static bool scanned;
        internal static int Revision { get; private set; }

        internal static void Reset()
        {
            sprites.Clear();
            font = null;
            scanned = false;
            ++Revision;
        }

        internal static void Discover()
        {
            if (scanned) return;
            scanned = true;
            images.Clear();
            texts.Clear();
            // UI assets are already referenced by scene Images; don't pin every loaded game sprite.
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != "UI" && root.name != "Canvas") continue;
                root.GetComponentsInChildren(true, images);
                foreach (Image image in images)
                {
                    Keep(image.sprite);
                    var button = image.GetComponent<UnityEngine.UI.Selectable>();
                    if (button != null)
                    {
                        Keep(button.spriteState.highlightedSprite);
                        Keep(button.spriteState.pressedSprite);
                    }
                }
                root.GetComponentsInChildren(true, texts);
                if (root.name == "UI") break;
            }
            int best = -1;
            foreach (Text text in texts)
            {
                if (text.font == null || !text.gameObject.scene.IsValid()) continue;
                string path = Plugin.GetPath(text.transform);
                int score = path.Contains("PauseMenu/ContentButtons/") ? 100 :
                    path.Contains("PauseMenu/") ? 60 : path.Contains("MenuManager/") ? 40 : 0;
                if (score <= best) continue;
                best = score;
                font = text.font;
                if (score == 100) break;
            }
            images.Clear(); texts.Clear();
            ++Revision;
            Plugin.Log.LogInfo($"[ui] game assets: font='{font?.name ?? "fallback"}', panel='{Find("MapInfoPanelBox", "fondo_tuto")?.name ?? "fallback"}', buttons='{Find("menuFrame")?.name ?? "fallback"}'.");
        }

        private static void Keep(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) return;
            if (wanted.Contains(sprite.name)) sprites[sprite.name] = sprite;
            if (wanted.Contains(sprite.texture.name) && !sprites.ContainsKey(sprite.texture.name)) sprites[sprite.texture.name] = sprite;
        }

        private static Sprite Find(string first, string second = null, string third = null)
        {
            if (sprites.TryGetValue(first, out Sprite sprite) && sprite != null) return sprite;
            if (second != null && sprites.TryGetValue(second, out sprite) && sprite != null) return sprite;
            if (third != null && sprites.TryGetValue(third, out sprite) && sprite != null) return sprite;
            return null;
        }

        internal static GUIStyle TextStyle(int size, Color color, bool bold = false, bool wrap = false)
        {
            Discover();
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font != null ? font : GUI.skin.font,
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wrap,
                richText = true,
                normal = { textColor = color }
            };
            return style;
        }

        internal static GUIStyle ButtonStyle(int size = 23)
        {
            var style = TextStyle(size, Ink);
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(18, 18, 6, 6);
            style.hover.textColor = Gold;
            style.active.textColor = Gold;
            return style;
        }

        internal static void Panel(Rect rect, bool dimBackground = true)
        {
            Discover();
            if (dimBackground)
                Fill(new Rect(0, 0, Screen.width / Mathf.Max(0.1f, Screen.height / 1080f), 1080), new Color(0, 0, 0, 0.65f));
            Fill(rect, new Color(0.045f, 0.025f, 0.02f, 0.97f));
            Sprite panel = Find("MapInfoPanelBox", "fondo_tuto");
            if (panel != null) DrawSprite(rect, panel, Color.white, true);
            else
            {
                Rule(new Rect(rect.x, rect.y, rect.width, 2));
                Rule(new Rect(rect.x, rect.yMax - 2, rect.width, 2));
            }
        }

        internal static void Rule(Rect rect) => Fill(rect, new Color(Gold.r, Gold.g, Gold.b, 0.45f));

        internal static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static bool Button(Rect rect, string label, GUIStyle style, bool selected = false)
        {
            bool highlight = selected || (Cursor.visible && rect.Contains(Event.current.mousePosition));
            Sprite frame = highlight ? Find("menuFrame_hightlighted", "menuFrame_Clicked", "menuFrame") : Find("menuFrame");
            if (frame != null) DrawSprite(rect, frame, Color.white, true);
            else Fill(rect, highlight ? new Color(0.35f, 0.18f, 0.09f, 0.9f) : new Color(0.14f, 0.08f, 0.05f, 0.9f));
            Color previous = GUI.contentColor;
            Color normal = style.normal.textColor;
            Color hover = style.hover.textColor;
            Color active = style.active.textColor;
            Color text_color = highlight ? Color.white : Ink;
            style.normal.textColor = text_color;
            style.hover.textColor = text_color;
            style.active.textColor = text_color;
            GUI.contentColor = Color.white;
            bool clicked = GUI.Button(rect, label, style);
            style.normal.textColor = normal;
            style.hover.textColor = hover;
            style.active.textColor = active;
            GUI.contentColor = previous;
            return clicked;
        }

        internal static bool LayoutButton(string label, GUIStyle style, bool selected, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), style, options);
            return Button(rect, label, style, selected);
        }

        internal static void Hint(Rect rect, string icon, string fallback, string label, GUIStyle style)
        {
            Sprite glyph = Find(icon);
            var icon_rect = new Rect(rect.x, rect.y, 30, 30);
            if (glyph != null) DrawSprite(icon_rect, glyph, Color.white, false);
            else GUI.Label(icon_rect, fallback, style);
            GUI.Label(new Rect(rect.x + 38, rect.y, rect.width - 38, rect.height), label, style);
        }

        private static void DrawSprite(Rect rect, Sprite sprite, Color tint, bool sliced)
        {
            if (Event.current.type != EventType.Repaint) return;
            Rect source;
            try { source = sprite.textureRect; }
            catch (UnityException) { return; }
            Texture2D texture = sprite.texture;
            Color previous = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * previous.a);
            if (!sliced)
            {
                GUI.DrawTextureWithTexCoords(rect, texture, UV(source, texture));
            }
            else
            {
                Vector4 border = sprite.border;
                // Many of the game's frames are Simple images with no authored nine-slice border.
                if (border == Vector4.zero)
                {
                    float edge = Mathf.Min(source.width, source.height) * 0.18f;
                    border = new Vector4(edge, edge, edge, edge);
                }
                float left = Mathf.Min(border.x, rect.width / 3);
                float right = Mathf.Min(border.z, rect.width / 3);
                float top = Mathf.Min(border.w, rect.height / 3);
                float bottom = Mathf.Min(border.y, rect.height / 3);
                dx[0] = rect.x; dx[1] = rect.x + left; dx[2] = rect.xMax - right; dx[3] = rect.xMax;
                dy[0] = rect.y; dy[1] = rect.y + top; dy[2] = rect.yMax - bottom; dy[3] = rect.yMax;
                sx[0] = source.x; sx[1] = source.x + border.x; sx[2] = source.xMax - border.z; sx[3] = source.xMax;
                sy[0] = source.yMax; sy[1] = source.yMax - border.w; sy[2] = source.y + border.y; sy[3] = source.y;
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                        GUI.DrawTextureWithTexCoords(new Rect(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]), texture,
                            UV(new Rect(sx[x], sy[y + 1], sx[x + 1] - sx[x], sy[y] - sy[y + 1]), texture));
            }
            GUI.color = previous;
        }

        private static Rect UV(Rect rect, Texture2D texture) => new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
    }
}
