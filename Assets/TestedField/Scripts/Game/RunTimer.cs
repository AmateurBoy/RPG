using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    [Serializable]
    public struct DayCost
    {
        public string Reason;
        public int Days;

        public DayCost(string reason, int days)
        {
            Reason = reason;
            Days = days;
        }
    }

    /// <summary>Запись лога списаний (для экрана итогов).</summary>
    public struct RunLogEntry : INetworkSerializeByMemcpy, IEquatable<RunLogEntry>
    {
        /// <summary>День после списания.</summary>
        public int Day;
        public FixedString32Bytes Reason;
        /// <summary>Сколько дней списано.</summary>
        public int Days;

        public bool Equals(RunLogEntry other) => Day == other.Day && Days == other.Days && Reason.Equals(other.Reason);
        public override int GetHashCode() => Day ^ (Days << 8) ^ Reason.GetHashCode();
    }

    /// <summary>Последнее списание — клиенты по изменению Count показывают всплывающее сообщение.</summary>
    public struct SpendEvent : INetworkSerializeByMemcpy, IEquatable<SpendEvent>
    {
        public int Count;
        public int Days;
        public int NewDay;
        public FixedString32Bytes Reason;

        public bool Equals(SpendEvent other) => Count == other.Count && Days == other.Days && NewDay == other.NewDay && Reason.Equals(other.Reason);
        public override int GetHashCode() => Count;
    }

    /// <summary>
    /// BPC_RunTimer: таймер забега на 30 дней (P0-08). День меняет только хост, состояние реплицируется.
    /// Использование (хост): RunTimer.Instance.SpendDaysForReason("Quest").
    /// </summary>
    public class RunTimer : NetworkBehaviour
    {
        public static RunTimer Instance { get; private set; }

        public int MaxDays = 30;
        public int StartDay = 1;
        [Tooltip("Сколько дней списывает причина. TODO: реальные значения не определены")]
        public List<DayCost> DayCosts = new List<DayCost>
        {
            new DayCost("PartyWipe", 1),
            new DayCost("Quest", 1),
            new DayCost("Dungeon", 1),
        };
        [Tooltip("Решение не принято; при включении каждые SecondsPerDay списывается 1 день с причиной Time")]
        public bool DaysPassAutomatically;
        public float SecondsPerDay = 600f;

        readonly NetworkVariable<int> currentDay = new NetworkVariable<int>(1);
        readonly NetworkVariable<bool> timeUp = new NetworkVariable<bool>();
        readonly NetworkVariable<SpendEvent> lastSpend = new NetworkVariable<SpendEvent>();
        readonly NetworkList<RunLogEntry> log = new NetworkList<RunLogEntry>();

        public int CurrentDay => currentDay.Value;
        public bool TimeUp => timeUp.Value;
        public SpendEvent LastSpend => lastSpend.Value;
        public NetworkList<RunLogEntry> Log => log;

        /// <summary>Хост: для каждого наступившего дня.</summary>
        public event Action<int> OnNewDay;
        /// <summary>Хост: (дней, причина, новый день) после каждого списания.</summary>
        public event Action<int, string, int> OnDaysSpent;
        /// <summary>Хост: день дошёл до MaxDays.</summary>
        public event Action OnTimeUp;
        /// <summary>У всех: пришло новое списание (для HUD).</summary>
        public event Action<SpendEvent> OnSpendNotified;

        float autoTimer;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                currentDay.Value = StartDay;
                timeUp.Value = false;
                log.Clear();
            }
            lastSpend.OnValueChanged += HandleSpend;
        }

        public override void OnNetworkDespawn()
        {
            lastSpend.OnValueChanged -= HandleSpend;
            if (Instance == this)
                Instance = null;
        }

        void HandleSpend(SpendEvent previous, SpendEvent current)
        {
            if (current.Count != previous.Count)
                OnSpendNotified?.Invoke(current);
        }

        void Update()
        {
            if (!IsSpawned || !IsServer || !DaysPassAutomatically || timeUp.Value)
                return;
            autoTimer += Time.deltaTime;
            if (autoTimer >= SecondsPerDay)
            {
                autoTimer -= SecondsPerDay;
                SpendDays(1, "Time");
            }
        }

        public int GetDaysLeft() => Mathf.Max(0, MaxDays - currentDay.Value);

        /// <summary>Списать столько дней, сколько указано для причины в DayCosts. Неизвестная причина → 0.</summary>
        public int SpendDaysForReason(string reason)
        {
            if (!IsServer)
                return 0;
            foreach (var cost in DayCosts)
                if (cost.Reason == reason)
                    return SpendDays(cost.Days, reason);
            Debug.LogWarning($"[RunTimer] Unknown day cost reason: {reason}");
            return 0;
        }

        /// <summary>Списать явное число дней. Возвращает, сколько реально списано.</summary>
        public int SpendDays(int days, string reason)
        {
            if (!IsServer || days <= 0 || timeUp.Value)
                return 0;

            int before = currentDay.Value;
            int after = Mathf.Min(MaxDays, before + days);
            int spent = after - before;
            if (spent <= 0)
                return 0;

            currentDay.Value = after;
            log.Add(new RunLogEntry { Day = after, Reason = NetStrings.To32(reason), Days = spent });
            var prev = lastSpend.Value;
            lastSpend.Value = new SpendEvent { Count = prev.Count + 1, Days = spent, NewDay = after, Reason = NetStrings.To32(reason) };
            Debug.Log($"[RunTimer] -{spent} day(s): {reason}. Day {after}/{MaxDays}");

            for (int d = before + 1; d <= after; d++)
                OnNewDay?.Invoke(d);
            OnDaysSpent?.Invoke(spent, reason, after);

            if (after >= MaxDays)
            {
                timeUp.Value = true;
                Debug.Log("[RunTimer] Time is up");
                OnTimeUp?.Invoke();
            }
            return spent;
        }
    }
}
