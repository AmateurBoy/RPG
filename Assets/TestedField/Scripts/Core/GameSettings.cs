using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Настройки графики из главного меню (режим окна, разрешение, качество, лимит FPS, V-Sync).
    /// Сохраняются в PlayerPrefs между запусками — аналог GameUserSettings.
    /// </summary>
    public static class GameSettings
    {
        public static readonly int[] FpsLimits = { 30, 60, 120, 144, 0 };

        public static FullScreenMode WindowMode
        {
            get => (FullScreenMode)PlayerPrefs.GetInt("Settings.WindowMode", (int)Screen.fullScreenMode);
            set => PlayerPrefs.SetInt("Settings.WindowMode", (int)value);
        }

        public static int ResolutionWidth
        {
            get => PlayerPrefs.GetInt("Settings.Width", Screen.currentResolution.width);
            set => PlayerPrefs.SetInt("Settings.Width", value);
        }

        public static int ResolutionHeight
        {
            get => PlayerPrefs.GetInt("Settings.Height", Screen.currentResolution.height);
            set => PlayerPrefs.SetInt("Settings.Height", value);
        }

        public static int Quality
        {
            get => PlayerPrefs.GetInt("Settings.Quality", QualitySettings.GetQualityLevel());
            set => PlayerPrefs.SetInt("Settings.Quality", value);
        }

        /// <summary>0 — без лимита.</summary>
        public static int FpsLimit
        {
            get => PlayerPrefs.GetInt("Settings.FpsLimit", 0);
            set => PlayerPrefs.SetInt("Settings.FpsLimit", value);
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt("Settings.VSync", 1) != 0;
            set => PlayerPrefs.SetInt("Settings.VSync", value ? 1 : 0);
        }

        public static bool HasSaved => PlayerPrefs.HasKey("Settings.Width");

        public static void ApplySaved()
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;

            if (HasSaved && !Application.isEditor)
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, WindowMode);
        }

        public static void Save()
        {
            PlayerPrefs.Save();
            ApplySaved();
        }
    }
}
