using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>BP_RunGameState: состояние забега — победа (P0-10). Таймер дней — компонент RunTimer рядом.</summary>
    public class RunGameState : NetworkBehaviour
    {
        public static RunGameState Instance { get; private set; }

        readonly NetworkVariable<bool> runWon = new NetworkVariable<bool>();
        readonly NetworkVariable<FixedString32Bytes> victoryReason = new NetworkVariable<FixedString32Bytes>();

        public bool RunWon => runWon.Value;
        public string VictoryReason => victoryReason.Value.ToString();

        /// <summary>Хост: забег выигран — сюда подписывается экран итогов (P0-13).</summary>
        public event Action OnRunVictory;

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        public void DeclareVictory(string reason)
        {
            if (!IsServer || runWon.Value)
                return;
            runWon.Value = true;
            victoryReason.Value = NetStrings.To32(reason);
            DevConsole.Print($"[Run] VICTORY: {reason}", 8f, Color.green);
            OnRunVictory?.Invoke();
        }
    }
}
