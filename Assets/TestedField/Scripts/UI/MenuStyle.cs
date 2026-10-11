using UnityEngine;

namespace TestedField
{
    /// <summary>Общий стиль IMGUI-меню (главное меню, лобби), масштаб по высоте экрана.</summary>
    public static class MenuStyle
    {
        static GUIStyle button, field, label, title;
        static float builtFor;

        public static float S => Screen.height / 1080f;

        static void Ensure()
        {
            float s = S;
            if (button != null && Mathf.Approximately(builtFor, s))
                return;
            builtFor = s;

            var bg = MakeTex(new Color(0.16f, 0.18f, 0.23f, 0.95f));
            var bgHover = MakeTex(new Color(0.26f, 0.3f, 0.4f, 0.98f));
            var bgActive = MakeTex(new Color(0.35f, 0.42f, 0.58f, 1f));
            var fieldBg = MakeTex(new Color(0.08f, 0.09f, 0.12f, 0.95f));

            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(24 * s),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            button.normal.background = bg;
            button.hover.background = bgHover;
            button.active.background = bgActive;
            button.focused.background = bg;
            button.normal.textColor = button.hover.textColor = button.active.textColor = button.focused.textColor = Color.white;

            field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = Mathf.RoundToInt(24 * s),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(Mathf.RoundToInt(12 * s), 4, 4, 4),
            };
            field.normal.background = field.focused.background = field.hover.background = fieldBg;
            field.normal.textColor = field.focused.textColor = field.hover.textColor = Color.white;

            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            label.normal.textColor = new Color(0.9f, 0.9f, 0.92f);

            title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(64 * s), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1f, 0.88f, 0.55f);
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static GUIStyle Button { get { Ensure(); return button; } }
        public static GUIStyle Field { get { Ensure(); return field; } }
        public static GUIStyle Label { get { Ensure(); return label; } }
        public static GUIStyle Title { get { Ensure(); return title; } }

        public static void Background()
        {
            HudDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.06f, 0.55f));
        }
    }
}
