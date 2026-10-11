using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Примитивы рисования для IMGUI-HUD (аналог DrawRect/DrawText на Canvas в UE).
    /// Все размеры передаются уже умноженными на масштаб s = высота экрана / 1080.
    /// </summary>
    public static class HudDraw
    {
        static GUIStyle textStyle;
        static Texture2D circleTexture;

        public static float Scale => Screen.height / 1080f;

        public static void Rect(Rect r, Color c)
        {
            if (c.a <= 0f)
                return;
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Outline(Rect r, Color c, float thickness)
        {
            Rect(new Rect(r.x, r.y, r.width, thickness), c);
            Rect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            Rect(new Rect(r.x, r.y, thickness, r.height), c);
            Rect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        public static void Line(Vector2 a, Vector2 b, Color c, float thickness)
        {
            var delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f)
                return;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var prevMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            Rect(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), c);
            GUI.matrix = prevMatrix;
        }

        public static void Circle(Vector2 center, float radius, Color c)
        {
            if (circleTexture == null)
                circleTexture = MakeCircle(64);
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circleTexture);
            GUI.color = prev;
        }

        static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Полоса: фон, заполнение, контур, подпись по центру.</summary>
        public static void Bar(Rect r, float fraction, Color fill, Color background, string label, float fontSize)
        {
            Rect(r, background);
            fraction = Mathf.Clamp01(fraction);
            Rect(new Rect(r.x, r.y, r.width * fraction, r.height), fill);
            Outline(r, new Color(0f, 0f, 0f, 0.8f), Mathf.Max(1f, fontSize * 0.08f));
            if (!string.IsNullOrEmpty(label))
                Text(r, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        }

        public static void Text(Rect r, string text, float fontSize, Color color, TextAnchor anchor, bool bold = false, bool shadow = true, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var style = Style(fontSize, anchor, bold);
            style.wordWrap = wrap;
            if (shadow)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
                float o = Mathf.Max(1f, fontSize * 0.07f);
                GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        /// <summary>Текст с центром в точке.</summary>
        public static void TextAt(Vector2 center, string text, float fontSize, Color color, bool bold = false)
        {
            var size = Measure(text, fontSize, bold);
            Text(new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y), text, fontSize, color, TextAnchor.MiddleCenter, bold);
        }

        public static Vector2 Measure(string text, float fontSize, bool bold = false)
        {
            var style = Style(fontSize, TextAnchor.UpperLeft, bold);
            return style.CalcSize(new GUIContent(text));
        }

        /// <summary>Высота текста с переносом по ширине.</summary>
        public static float MeasureWrappedHeight(string text, float fontSize, float width, bool bold = false)
        {
            var style = Style(fontSize, TextAnchor.UpperLeft, bold);
            style.wordWrap = true;
            float h = style.CalcHeight(new GUIContent(text), width);
            style.wordWrap = false;
            return h;
        }

        public static GUIStyle Style(float fontSize, TextAnchor anchor, bool bold = false)
        {
            if (textStyle == null)
                textStyle = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, richText = false };
            textStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(fontSize));
            textStyle.alignment = anchor;
            textStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textStyle.wordWrap = false;
            return textStyle;
        }

        /// <summary>Мировая точка → координаты IMGUI (y вниз). false, если за камерой.</summary>
        public static bool WorldToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            gui = default;
            if (cam == null)
                return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f)
                return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }
    }
}
