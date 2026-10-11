using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// WBP_MainMenu: «Создать игру», «Подключиться» (IP), «Настройки», «Выход».
    /// После обрыва связи показывает сообщение («Хост покинул игру»).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        enum Page { Main, Settings }

        Page page;
        string address;
        string playerName;
        string message;
        Color messageColor = new Color(1f, 0.45f, 0.4f);

        // Настройки (черновик до «Применить»)
        int windowMode, resolutionIndex, quality, fpsIndex;
        bool vsync;
        Resolution[] resolutions;

        static readonly FullScreenMode[] WindowModes = { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen };
        static readonly string[] WindowModeNames = { "Полноэкранный в окне", "В окне", "Полноэкранный" };

        void Start()
        {
            CommandLineLaunch.TryHandle();
            address = PlayerPrefs.GetString("LastAddress", "127.0.0.1");
            playerName = NetSession.LocalPlayerName;
            if (!string.IsNullOrEmpty(NetSession.PendingMessage))
            {
                message = NetSession.PendingMessage;
                NetSession.PendingMessage = null;
            }
            GameInput.InventoryOpen = false;
        }

        void OnGUI()
        {
            float s = MenuStyle.S;
            MenuStyle.Background();
            GUI.Label(new Rect(0, Screen.height * 0.1f, Screen.width, 90f * s), "TestedField", MenuStyle.Title);

            float w = 520f * s, h = 60f * s, gap = 16f * s;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.28f;

            if (page == Page.Main)
                DrawMain(x, y, w, h, gap, s);
            else
                DrawSettings(x, y, w, h, gap, s);
        }

        void DrawMain(float x, float y, float w, float h, float gap, float s)
        {
            var nm = NetSession.Net;
            bool connecting = nm != null && nm.IsClient && !nm.IsConnectedClient && !nm.IsHost;

            GUI.Label(new Rect(x, y, 160f * s, h), "Имя:", MenuStyle.Label);
            string newName = GUI.TextField(new Rect(x + 160f * s, y, w - 160f * s, h), playerName ?? "", 24, MenuStyle.Field);
            if (newName != playerName)
            {
                playerName = newName;
                NetSession.LocalPlayerName = newName;
            }
            y += h + gap * 2f;

            GUI.enabled = !connecting;
            if (GUI.Button(new Rect(x, y, w, h), "Создать игру", MenuStyle.Button))
            {
                message = null;
                if (!NetSession.HostGame())
                    message = NetSession.PendingMessage ?? "Не удалось создать игру";
                NetSession.PendingMessage = null;
            }
            y += h + gap;

            address = GUI.TextField(new Rect(x, y, w * 0.58f, h), address ?? "", 64, MenuStyle.Field);
            if (GUI.Button(new Rect(x + w * 0.6f, y, w * 0.4f, h), "Подключиться", MenuStyle.Button))
            {
                message = null;
                PlayerPrefs.SetString("LastAddress", address);
                if (!NetSession.JoinGame(address))
                    message = "Не удалось начать подключение";
            }
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "Настройки", MenuStyle.Button))
                OpenSettings();
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "Выход", MenuStyle.Button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            y += h + gap;
            GUI.enabled = true;

            if (connecting)
            {
                GUI.Label(new Rect(x, y, w, h), $"Подключение к {address}…", MenuStyle.Label);
                y += h;
                if (GUI.Button(new Rect(x, y, w, h), "Отмена", MenuStyle.Button))
                    NetSession.LeaveToMenu();
            }
            else if (!string.IsNullOrEmpty(message))
            {
                var style = new GUIStyle(MenuStyle.Label) { alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = messageColor;
                GUI.Label(new Rect(x - 100f * s, y, w + 200f * s, h), message, style);
            }
        }

        void OpenSettings()
        {
            page = Page.Settings;
            resolutions = Screen.resolutions;
            windowMode = System.Array.IndexOf(WindowModes, GameSettings.WindowMode);
            if (windowMode < 0) windowMode = 0;
            resolutionIndex = 0;
            for (int i = 0; i < resolutions.Length; i++)
                if (resolutions[i].width == GameSettings.ResolutionWidth && resolutions[i].height == GameSettings.ResolutionHeight)
                    resolutionIndex = i;
            quality = Mathf.Clamp(GameSettings.Quality, 0, QualitySettings.names.Length - 1);
            fpsIndex = System.Array.IndexOf(GameSettings.FpsLimits, GameSettings.FpsLimit);
            if (fpsIndex < 0) fpsIndex = GameSettings.FpsLimits.Length - 1;
            vsync = GameSettings.VSync;
        }

        void DrawSettings(float x, float y, float w, float h, float gap, float s)
        {
            float lw = w * 0.45f;
            Row(ref y, x, w, h, gap, lw, "Режим окна", WindowModeNames[windowMode], () => windowMode = (windowMode + 1) % WindowModes.Length);

            string res = resolutions != null && resolutions.Length > 0 ? $"{resolutions[resolutionIndex].width} × {resolutions[resolutionIndex].height}" : "—";
            Row(ref y, x, w, h, gap, lw, "Разрешение", res, () =>
            {
                if (resolutions != null && resolutions.Length > 0)
                    resolutionIndex = (resolutionIndex + 1) % resolutions.Length;
            });

            Row(ref y, x, w, h, gap, lw, "Качество", QualitySettings.names.Length > 0 ? QualitySettings.names[quality] : "—",
                () => quality = (quality + 1) % Mathf.Max(1, QualitySettings.names.Length));

            int fps = GameSettings.FpsLimits[fpsIndex];
            Row(ref y, x, w, h, gap, lw, "Лимит FPS", fps > 0 ? fps.ToString() : "Без лимита", () => fpsIndex = (fpsIndex + 1) % GameSettings.FpsLimits.Length);
            Row(ref y, x, w, h, gap, lw, "V-Sync", vsync ? "Вкл" : "Выкл", () => vsync = !vsync);

            y += gap;
            if (GUI.Button(new Rect(x, y, w * 0.48f, h), "Применить", MenuStyle.Button))
            {
                GameSettings.WindowMode = WindowModes[windowMode];
                if (resolutions != null && resolutions.Length > 0)
                {
                    GameSettings.ResolutionWidth = resolutions[resolutionIndex].width;
                    GameSettings.ResolutionHeight = resolutions[resolutionIndex].height;
                }
                GameSettings.Quality = quality;
                GameSettings.FpsLimit = GameSettings.FpsLimits[fpsIndex];
                GameSettings.VSync = vsync;
                GameSettings.Save();
                if (!Application.isEditor && resolutions != null && resolutions.Length > 0)
                    Screen.SetResolution(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight, GameSettings.WindowMode);
            }
            if (GUI.Button(new Rect(x + w * 0.52f, y, w * 0.48f, h), "Назад", MenuStyle.Button))
                page = Page.Main;
        }

        static void Row(ref float y, float x, float w, float h, float gap, float labelWidth, string label, string value, System.Action next)
        {
            GUI.Label(new Rect(x, y, labelWidth, h), label, MenuStyle.Label);
            if (GUI.Button(new Rect(x + labelWidth, y, w - labelWidth, h), value, MenuStyle.Button))
                next();
            y += h + gap;
        }
    }
}
