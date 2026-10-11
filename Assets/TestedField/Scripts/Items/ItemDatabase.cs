using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_ItemDatabase: единственный источник данных о предметах и слотах.
    /// Порядок в Slots = порядок слотов в UI и в массиве Equipped.
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Items/Item Database", fileName = "DA_ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> Items = new List<ItemData>();
        public List<EquipSlotData> Slots = new List<EquipSlotData>();

        Dictionary<string, ItemData> lookup;

        /// <summary>Определение по ItemId. Неизвестный ID — предупреждение в лог и null.</summary>
        public ItemData GetItemDefinition(string itemId, bool logUnknown = true)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, ItemData>(StringComparer.Ordinal);
                foreach (var item in Items)
                    if (item != null && !string.IsNullOrEmpty(item.ItemId))
                        lookup[item.ItemId] = item;
            }
            if (lookup.TryGetValue(itemId, out var data))
                return data;
            if (logUnknown)
                Debug.LogWarning($"[Items] Unknown ItemId: {itemId}");
            return null;
        }

        public int GetSlotIndex(EquipSlotData slot)
        {
            return slot != null ? Slots.IndexOf(slot) : -1;
        }

        public int GetSlotIndexById(string slotId)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i] != null && Slots[i].SlotId == slotId)
                    return i;
            return -1;
        }

        public string GetDisplayName(string itemId)
        {
            var def = GetItemDefinition(itemId, false);
            return def != null ? def.DisplayName : itemId;
        }

        void OnValidate()
        {
            lookup = null;
        }

        void OnEnable()
        {
            lookup = null;
        }
    }
}
