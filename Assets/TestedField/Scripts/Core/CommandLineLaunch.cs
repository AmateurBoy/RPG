using System;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Запуск из командной строки (для теста кооперативного режима несколькими окнами):
    ///   -host                 создать игру (лобби);
    ///   -autostart N          хост сам начинает забег, когда в лобби N игроков;
    ///   -connect IP[:порт]    подключиться;
    ///   -name Имя             имя игрока.
    /// Пример: TestedField.x86_64 -host -autostart 2   и   TestedField.x86_64 -connect 127.0.0.1
    /// </summary>
    public static class CommandLineLaunch
    {
        static bool handled;

        public static int AutoStartPlayers { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            handled = false;
            AutoStartPlayers = 0;
        }

        /// <summary>Вызывается главным меню один раз за запуск.</summary>
        public static void TryHandle()
        {
            if (handled)
                return;
            handled = true;

            var args = Environment.GetCommandLineArgs();
            string connect = null;
            bool host = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "-host")
                    host = true;
                else if (a == "-connect" && next != null)
                    connect = next;
                else if (a == "-autostart" && next != null && int.TryParse(next, out int n))
                    AutoStartPlayers = Mathf.Clamp(n, 1, NetSession.MaxPlayers);
                else if (a == "-name" && next != null)
                    NetSession.LocalPlayerName = next;
            }

            if (host)
            {
                Debug.Log("[CommandLine] -host");
                NetSession.HostGame();
            }
            else if (!string.IsNullOrEmpty(connect))
            {
                Debug.Log($"[CommandLine] -connect {connect}");
                NetSession.JoinGame(connect);
            }
        }
    }
}
