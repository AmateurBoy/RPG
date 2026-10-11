using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BPC_Inventory: инвентарь и экипировка героя (P0-11). Состояние (Cells, Equipped) меняет только хост,
    /// клиенты получают его репликацией и шлют запросы через RPC.
    /// </summary>
    public class Inventory : NetworkBehaviour
    {
        public InventoryConfig Config;

        readonly NetworkList<FixedString32Bytes> cells = new NetworkList<FixedString32Bytes>();
        readonly NetworkList<FixedString32Bytes> equipped = new NetworkList<FixedString32Bytes>();

        /// <summary>Инвентарь или экипировка изменились (у всех).</summary>
        public event Action OnInventoryChanged;

        InventoryModel model;
        HeroStats stats;
        readonly Dictionary<string, float> totalFlat = new Dictionary<string, float>();
        readonly Dictionary<string, float> totalPercent = new Dictionary<string, float>();
        readonly List<string> equippedCache = new List<string>();

        public ItemDatabase Database => Config != null ? Config.ItemDatabase : GameAssets.I.ItemDatabase;
        public int CellCount => cells.Count;
        public int SlotCount => equipped.Count;
        public string GetCell(int index) => index >= 0 && index < cells.Count ? cells[index].ToString() : "";
        public string GetEquipped(int slot) => slot >= 0 && slot < equipped.Count ? equipped[slot].ToString() : "";
        public IReadOnlyDictionary<string, float> TotalFlatModifiers => totalFlat;
        public IReadOnlyDictionary<string, float> TotalPercentModifiers => totalPercent;

        void Awake()
        {
            stats = GetComponent<HeroStats>();
        }

        public override void OnNetworkSpawn()
        {
            cells.OnListChanged += HandleListChanged;
            equipped.OnListChanged += HandleListChanged;

            if (IsServer)
            {
                InitStorage();
                GrantStartingLoadout();
            }
            else
            {
                RecalculateTotals();
            }
        }

        public override void OnNetworkDespawn()
        {
            cells.OnListChanged -= HandleListChanged;
            equipped.OnListChanged -= HandleListChanged;
        }

        void HandleListChanged(NetworkListEvent<FixedString32Bytes> e)
        {
            RecalculateTotals();
            OnInventoryChanged?.Invoke();
        }

        // ---------------- Хост ----------------

        void InitStorage()
        {
            int capacity = Config != null ? Config.Capacity : 12;
            model = new InventoryModel(Database, capacity);
            cells.Clear();
            equipped.Clear();
            foreach (var c in model.Cells)
                cells.Add(NetStrings.To32(c));
            foreach (var s in model.Equipped)
                equipped.Add(NetStrings.To32(s));
        }

        /// <summary>Старт забега: стартовые предметы, стартовая экипировка (тем же путём, что ручное надевание), полные HP/MP/SP.</summary>
        void GrantStartingLoadout()
        {
            if (Config != null)
            {
                foreach (var id in Config.StartingItems)
                    AddItem(id, out _);

                foreach (var pair in Config.StartingEquipment)
                {
                    var def = Database != null ? Database.GetItemDefinition(pair.ItemId) : null;
                    if (def == null)
                        continue;
                    if (def.Slot == null || def.Slot.SlotId != pair.SlotId)
                    {
                        Debug.LogError($"[Inventory] Starting equipment {pair.ItemId} does not fit slot {pair.SlotId}, skipped");
                        continue;
                    }
                    if (AddItem(pair.ItemId, out int cell))
                        EquipFromCell(cell);
                }
            }

            RecalculateStats();
            stats?.RefillAll();
        }

        void SyncToNet()
        {
            for (int i = 0; i < model.Cells.Length; i++)
            {
                var v = NetStrings.To32(model.Cells[i]);
                if (!cells[i].Equals(v))
                    cells[i] = v;
            }
            for (int i = 0; i < model.Equipped.Length; i++)
            {
                var v = NetStrings.To32(model.Equipped[i]);
                if (!equipped[i].Equals(v))
                    equipped[i] = v;
            }
        }

        void Warn()
        {
            if (string.IsNullOrEmpty(model.LastWarning))
                return;
            Debug.LogWarning(model.LastWarning);
            ShowWarningRpc(NetStrings.To128(model.LastWarning));
        }

        [Rpc(SendTo.Owner)]
        void ShowWarningRpc(FixedString128Bytes text)
        {
            DevConsole.Print(text.ToString(), 3f, Color.yellow);
        }

        public bool AddItem(string itemId, out int cellIndex)
        {
            cellIndex = -1;
            if (!IsServer || model == null)
                return false;
            bool added = model.AddItem(itemId, out cellIndex);
            Warn();
            if (added)
                SyncToNet();
            return added;
        }

        public string RemoveItemAt(int cellIndex)
        {
            if (!IsServer || model == null)
                return "";
            string id = model.RemoveItemAt(cellIndex);
            SyncToNet();
            return id;
        }

        public bool HasFreeSpace => IsServer && model != null ? model.HasFreeSpace : FindFreeCellReplicated() >= 0;

        int FindFreeCellReplicated()
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].IsEmpty)
                    return i;
            return -1;
        }

        public bool EquipFromCell(int cellIndex)
        {
            if (!IsServer || model == null)
                return false;
            bool ok = model.EquipFromCell(cellIndex);
            Warn();
            if (ok)
            {
                SyncToNet();
                RecalculateStats();
            }
            return ok;
        }

        public bool UnequipSlot(int slotIndex)
        {
            if (!IsServer || model == null)
                return false;
            bool ok = model.UnequipSlot(slotIndex);
            Warn();
            if (ok)
            {
                SyncToNet();
                RecalculateStats();
            }
            return ok;
        }

        public bool MoveItem(int from, int to)
        {
            if (!IsServer || model == null)
                return false;
            bool ok = model.MoveItem(from, to);
            Warn();
            if (ok)
            {
                SyncToNet();
                RecalculateStats();
            }
            return ok;
        }

        /// <summary>Пересчёт статов от надетых предметов (временный мост до P0-16).</summary>
        public void RecalculateStats()
        {
            RecalculateTotals();
            if (IsServer)
                stats?.ApplyItemModifiers(totalFlat, totalPercent);
        }

        void RecalculateTotals()
        {
            equippedCache.Clear();
            for (int i = 0; i < equipped.Count; i++)
                equippedCache.Add(equipped[i].ToString());
            InventoryModel.SumModifiers(Database, equippedCache, totalFlat, totalPercent);
        }

        public float GetModifiedStat(string stat, float baseValue)
        {
            return StatMath.Modified(baseValue, stat, totalFlat, totalPercent);
        }

        public IEnumerable<string> EquippedItemIds()
        {
            for (int i = 0; i < equipped.Count; i++)
                yield return equipped[i].ToString();
        }

        // ---------------- Запросы от UI ----------------

        public void RequestEquipFromCell(int cellIndex)
        {
            if (IsServer) EquipFromCell(cellIndex);
            else ServerEquipFromCellRpc(cellIndex);
        }

        public void RequestUnequipSlot(int slotIndex)
        {
            if (IsServer) UnequipSlot(slotIndex);
            else ServerUnequipSlotRpc(slotIndex);
        }

        public void RequestMoveItem(int from, int to)
        {
            if (IsServer) MoveItem(from, to);
            else ServerMoveItemRpc(from, to);
        }

        [Rpc(SendTo.Server)]
        void ServerEquipFromCellRpc(int cellIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                EquipFromCell(cellIndex);
        }

        [Rpc(SendTo.Server)]
        void ServerUnequipSlotRpc(int slotIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                UnequipSlot(slotIndex);
        }

        [Rpc(SendTo.Server)]
        void ServerMoveItemRpc(int from, int to, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                MoveItem(from, to);
        }
    }
}
