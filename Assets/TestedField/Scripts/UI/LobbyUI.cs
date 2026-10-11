using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace TestedField
{
    /// <summary>WBP_Lobby: список игроков, «Начать игру» только у хоста, «Выйти в меню».</summary>
    public class LobbyUI : MonoBehaviour
    {
        string localAddresses;

        void Start()
        {
            localAddresses = string.Join(", ", GetLocalAddresses());
        }

        static IEnumerable<string> GetLocalAddresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            result.Add(ua.Address.ToString());
                }
            }
            catch (System.Exception)
            {
                // Нет доступа к интерфейсам — просто не показываем адреса.
            }
            return result;
        }

        void Update()
        {
            if (GameInput.Menu != null && GameInput.Menu.WasPressedThisFrame())
                NetSession.LeaveToMenu();

            // -autostart N: хост сам начинает забег, когда собралось N игроков (тест из командной строки).
            var lobby = LobbyState.Instance;
            int needed = CommandLineLaunch.AutoStartPlayers;
            if (needed > 0 && NetSession.IsServer && lobby != null && lobby.Players.Count >= needed && !autoStarted)
            {
                autoStarted = true;
                NetSession.StartRun();
            }
        }

        bool autoStarted;

        void OnGUI()
        {
            float s = MenuStyle.S;
            MenuStyle.Background();
            GUI.Label(new Rect(0, Screen.height * 0.08f, Screen.width, 90f * s), "Лобби", MenuStyle.Title);

            var nm = NetSession.Net;
            bool isHost = nm != null && nm.IsServer;
            float w = 620f * s, h = 52f * s;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.22f;

            if (isHost && !string.IsNullOrEmpty(localAddresses))
            {
                GUI.Label(new Rect(x, y, w, h), $"Адрес для подключения: {localAddresses} (порт {NetSession.DefaultPort})", MenuStyle.Label);
                y += h;
            }

            var lobby = LobbyState.Instance;
            int count = lobby != null ? lobby.Players.Count : 0;
            GUI.Label(new Rect(x, y, w, h), $"Игроки ({count} / {NetSession.MaxPlayers}):", MenuStyle.Label);
            y += h;

            for (int i = 0; i < count; i++)
            {
                var p = lobby.Players[i];
                var r = new Rect(x, y, w, h);
                HudDraw.Rect(r, new Color(0.1f, 0.12f, 0.16f, 0.9f));
                HudDraw.Rect(new Rect(r.x, r.y, 6f * s, r.height), HeroVisual.PlayerColors[(int)(p.ClientId % 4)]);
                string suffix = p.ClientId == NetworkManagerHostId ? "  (хост)" : "";
                if (nm != null && p.ClientId == nm.LocalClientId)
                    suffix += "  — вы";
                GUI.Label(new Rect(r.x + 20f * s, r.y, r.width - 20f * s, r.height), p.Name + suffix, MenuStyle.Label);
                y += h + 6f * s;
            }

            y += 20f * s;
            if (isHost)
            {
                if (GUI.Button(new Rect(x, y, w, 60f * s), "Начать игру", MenuStyle.Button))
                    NetSession.StartRun();
                y += 60f * s + 14f * s;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "Ожидание хоста…", MenuStyle.Label);
                y += h + 14f * s;
            }

            if (GUI.Button(new Rect(x, y, w, 60f * s), "Выйти в меню", MenuStyle.Button))
                NetSession.LeaveToMenu();
        }

        static ulong NetworkManagerHostId => Unity.Netcode.NetworkManager.ServerClientId;
    }
}
