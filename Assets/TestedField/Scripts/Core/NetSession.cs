using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Сетевая сессия (P0-01): listen server — хост один из игроков, подключение по IP, до 4 игроков.
    /// Аналог связки BP_GameInstance + open/servertravel из UE.
    /// </summary>
    public static class NetSession
    {
        public const int MaxPlayers = 4;
        public const ushort DefaultPort = 7777;

        public const string SceneMainMenu = "MainMenu";
        public const string SceneLobby = "Lobby";
        public const string SceneRun = "Run";
        public const string SceneTestField = "TestField";

        /// <summary>Сообщение для главного меню после возврата («Хост покинул игру» и т.п.).</summary>
        public static string PendingMessage;

        static bool intentionalLeave;
        static bool wasConnected;
        static bool callbacksHooked;

        public static NetworkManager Net => NetworkManager.Singleton;

        public static bool IsServer => Net != null && Net.IsServer;

        public static string LocalPlayerName
        {
            get
            {
                string name = PlayerPrefs.GetString("PlayerName", "");
                if (string.IsNullOrWhiteSpace(name))
                    name = System.Environment.UserName;
                if (string.IsNullOrWhiteSpace(name))
                    name = "Игрок";
                return name.Length > 24 ? name.Substring(0, 24) : name;
            }
            set => PlayerPrefs.SetString("PlayerName", value ?? "");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PendingMessage = null;
            intentionalLeave = false;
            wasConnected = false;
            callbacksHooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            GameSettings.ApplySaved();
            GameInput.Ensure();
            EnsureNetworkManager();
            DevConsole.Ensure();
        }

        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                HookCallbacks(NetworkManager.Singleton);
                return NetworkManager.Singleton;
            }

            var prefab = Resources.Load<GameObject>("NetworkManager");
            if (prefab == null)
            {
                Debug.LogError("[Net] Resources/NetworkManager prefab not found. Run menu TestedField → Setup Project.");
                return null;
            }

            var go = Object.Instantiate(prefab);
            go.name = "NetworkManager";
            var nm = go.GetComponent<NetworkManager>();
            // Singleton выставляется в OnEnable — к этому моменту он уже есть.
            HookCallbacks(nm);
            return nm;
        }

        static void HookCallbacks(NetworkManager nm)
        {
            if (callbacksHooked || nm == null)
                return;
            callbacksHooked = true;

            nm.ConnectionApprovalCallback = ApproveConnection;
            nm.OnClientConnectedCallback += id =>
            {
                if (nm.IsClient && id == nm.LocalClientId)
                    wasConnected = true;
            };
            nm.OnClientStopped += OnClientStopped;
        }

        static void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = Net;
            response.CreatePlayerObject = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            bool full = nm.ConnectedClientsIds.Count >= MaxPlayers;
            response.Approved = !full;
            if (full)
                response.Reason = "Сервер заполнен (максимум 4 игрока)";
        }

        static void OnClientStopped(bool wasHost)
        {
            if (intentionalLeave)
            {
                intentionalLeave = false;
                wasConnected = false;
                return;
            }

            if (!wasHost)
            {
                string reason = Net != null ? Net.DisconnectReason : null;
                if (!string.IsNullOrEmpty(reason))
                    PendingMessage = reason;
                else
                    PendingMessage = wasConnected ? "Хост покинул игру" : "Не удалось подключиться";
            }

            wasConnected = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneMainMenu);
        }

        /// <summary>«Создать игру»: хост открывает лобби (аналог open Map_Lobby?listen).</summary>
        public static bool HostGame()
        {
            var nm = EnsureNetworkManager();
            if (nm == null || nm.IsListening)
                return false;

            var transport = nm.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
            if (!nm.StartHost())
            {
                PendingMessage = $"Не удалось запустить хост (порт {DefaultPort} занят?)";
                return false;
            }

            nm.SceneManager.LoadScene(SceneLobby, UnityEngine.SceneManagement.LoadSceneMode.Single);
            return true;
        }

        /// <summary>«Подключиться» по IP (аналог open IP:port).</summary>
        public static bool JoinGame(string address)
        {
            var nm = EnsureNetworkManager();
            if (nm == null || nm.IsListening)
                return false;

            ParseAddress(address, out string ip, out ushort port);
            var transport = nm.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, port);
            wasConnected = false;
            return nm.StartClient();
        }

        public static void ParseAddress(string address, out string ip, out ushort port)
        {
            ip = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            port = DefaultPort;
            int colon = ip.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(ip.Substring(colon + 1), out ushort parsed))
            {
                port = parsed;
                ip = ip.Substring(0, colon);
            }
            if (ip == "localhost")
                ip = "127.0.0.1";
        }

        /// <summary>«Начать игру» в лобби (аналог servertravel /Game/Maps/Map_Run).</summary>
        public static void StartRun()
        {
            if (!IsServer)
                return;
            Net.SceneManager.LoadScene(SceneRun, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        /// <summary>Выход в главное меню (Esc / кнопка «Выйти в меню»).</summary>
        public static void LeaveToMenu()
        {
            if (Net != null && Net.IsListening)
            {
                intentionalLeave = true;
                Net.Shutdown();
            }
            GameInput.InventoryOpen = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneMainMenu);
        }

        /// <summary>
        /// Игровая сцена открыта напрямую в редакторе (Play на Run/TestField) — стартуем хост соло,
        /// как PIE в UE, который запускается сразу на игровой карте.
        /// </summary>
        public static void EnsureHostForDirectPlay()
        {
            var nm = EnsureNetworkManager();
            if (nm == null || nm.IsListening)
                return;
            var transport = nm.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", DefaultPort, "0.0.0.0");
            if (!nm.StartHost())
                Debug.LogError("[Net] Failed to start host for direct play.");
        }

        public static double ServerTime => Net != null && Net.IsListening ? Net.ServerTime.Time : Time.timeAsDouble;
    }
}
