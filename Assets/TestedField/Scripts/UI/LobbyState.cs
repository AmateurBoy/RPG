using System;
using Unity.Collections;
using Unity.Netcode;

namespace TestedField
{
    public struct LobbyPlayer : INetworkSerializeByMemcpy, IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;

        public bool Equals(LobbyPlayer other) => ClientId == other.ClientId && Name.Equals(other.Name);
        public override int GetHashCode() => ClientId.GetHashCode();
    }

    /// <summary>Список игроков лобби (аналог GameState.PlayerArray + PlayerState.PlayerName).</summary>
    public class LobbyState : NetworkBehaviour
    {
        public static LobbyState Instance { get; private set; }

        readonly NetworkList<LobbyPlayer> players = new NetworkList<LobbyPlayer>();

        public NetworkList<LobbyPlayer> Players => players;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                players.Clear();
                foreach (var id in NetworkManager.ConnectedClientsIds)
                    Upsert(id, $"Игрок {id + 1}");
                NetworkManager.OnClientConnectedCallback += HandleConnected;
                NetworkManager.OnClientDisconnectCallback += HandleDisconnected;
            }
            SetNameRpc(NetStrings.To64(NetSession.LocalPlayerName));
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleDisconnected;
            }
            if (Instance == this)
                Instance = null;
        }

        void HandleConnected(ulong clientId)
        {
            // Имя могло прийти раньше (SetNameRpc из OnNetworkSpawn клиента) — не затираем его.
            for (int i = 0; i < players.Count; i++)
                if (players[i].ClientId == clientId)
                    return;
            Upsert(clientId, $"Игрок {clientId + 1}");
        }

        void HandleDisconnected(ulong clientId)
        {
            for (int i = players.Count - 1; i >= 0; i--)
                if (players[i].ClientId == clientId)
                    players.RemoveAt(i);
        }

        void Upsert(ulong clientId, string name)
        {
            var entry = new LobbyPlayer { ClientId = clientId, Name = NetStrings.To64(name) };
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == clientId)
                {
                    players[i] = entry;
                    return;
                }
            }
            players.Add(entry);
        }

        [Rpc(SendTo.Server)]
        void SetNameRpc(FixedString64Bytes name, RpcParams rpcParams = default)
        {
            Upsert(rpcParams.Receive.SenderClientId, name.ToString());
        }
    }
}
