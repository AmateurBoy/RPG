using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TestedField
{
    /// <summary>
    /// Отладочная консоль (` или F1) и экранный лог — замена консоли UE (`ke * DebugXxx`) и PrintString.
    /// Команды регистрирует <see cref="DebugCommands"/>; большинство работает только у хоста.
    /// </summary>
    public class DevConsole : MonoBehaviour
    {
        struct Command
        {
            public string Help;
            public Action<string[]> Run;
        }

        struct LogLine
        {
            public string Text;
            public Color Color;
            public float Until;
        }

        static DevConsole instance;
        static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
        static readonly List<LogLine> screenLog = new List<LogLine>();
        static readonly List<string> history = new List<string>();

        public static bool IsOpen { get; private set; }

        string input = "";
        bool focusField;
        readonly List<string> output = new List<string>();
        GUIStyle logStyle;

        public static void Ensure()
        {
            if (instance != null)
                return;
            var go = new GameObject("DevConsole");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevConsole>();
            DebugCommands.RegisterAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            IsOpen = false;
            commands.Clear();
            screenLog.Clear();
            history.Clear();
        }

        public static void Register(string name, string help, Action<string[]> run)
        {
            commands[name] = new Command { Help = help, Run = run };
        }

        /// <summary>Сообщение на экране этого игрока (аналог PrintString) + в лог.</summary>
        public static void Print(string text, float duration = 5f, Color? color = null)
        {
            Debug.Log(text);
            screenLog.Add(new LogLine { Text = text, Color = color ?? new Color(0.4f, 0.9f, 1f), Until = Time.unscaledTime + duration });
            if (screenLog.Count > 12)
                screenLog.RemoveAt(0);
            instance?.output.Add(text);
        }

        public static void Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            history.Add(line);
            var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0];
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            instance?.output.Add("> " + line);
            if (!commands.TryGetValue(name, out var cmd))
            {
                Print($"Unknown command: {name}. Type 'help'.", 4f, Color.yellow);
                return;
            }

            try
            {
                cmd.Run(args);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Print($"Command failed: {e.Message}", 5f, Color.red);
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> ListCommands()
        {
            foreach (var kv in commands)
                yield return new KeyValuePair<string, string>(kv.Key, kv.Value.Help);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)
            {
                IsOpen = !IsOpen;
                focusField = IsOpen;
                input = "";
            }

            float now = Time.unscaledTime;
            screenLog.RemoveAll(l => l.Until < now);
        }

        void OnGUI()
        {
            GUI.depth = -100;
            if (logStyle == null)
            {
                logStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = false };
            }

            // Ниже панелей союзников (левый верхний угол занят HUD).
            float y = IsOpen ? Mathf.Min(320f, Screen.height * 0.45f) + 8f : Screen.height * 0.3f;
            foreach (var line in screenLog)
            {
                var shadow = new Rect(9f, y + 1f, Screen.width - 20f, 22f);
                logStyle.normal.textColor = Color.black;
                GUI.Label(shadow, line.Text, logStyle);
                logStyle.normal.textColor = line.Color;
                GUI.Label(new Rect(8f, y, Screen.width - 20f, 22f), line.Text, logStyle);
                y += 20f;
            }

            if (!IsOpen)
                return;

            float h = Mathf.Min(320f, Screen.height * 0.45f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float lineH = 18f;
            int visible = Mathf.FloorToInt((h - 34f) / lineH);
            int start = Mathf.Max(0, output.Count - visible);
            logStyle.normal.textColor = Color.white;
            for (int i = start; i < output.Count; i++)
                GUI.Label(new Rect(8f, 4f + (i - start) * lineH, Screen.width - 16f, lineH), output[i], logStyle);

            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                Execute(input);
                input = "";
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow && history.Count > 0)
            {
                input = history[history.Count - 1];
                e.Use();
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.BackQuote || e.keyCode == KeyCode.F1))
            {
                e.Use();
            }

            GUI.SetNextControlName("DevConsoleInput");
            input = GUI.TextField(new Rect(4f, h - 28f, Screen.width - 8f, 24f), input ?? "");
            input = input.Replace("`", "").Replace("ё", "").Replace("Ё", "");
            if (focusField)
            {
                GUI.FocusControl("DevConsoleInput");
                focusField = false;
            }
        }
    }
}
