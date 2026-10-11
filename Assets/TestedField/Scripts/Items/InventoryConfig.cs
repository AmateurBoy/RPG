using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    [Serializable]
    public struct StartingEquipmentEntry
    {
        public string SlotId;
        public string ItemId;
    }

    /// <summary>BP_InventoryConfig: база, вместимость, стартовый набор.</summary>
    [CreateAssetMenu(menuName = "TestedField/Items/Inventory Config", fileName = "DA_InventoryConfig")]
    public class InventoryConfig : ScriptableObject
    {
        public ItemDatabase ItemDatabase;
        [Tooltip("Число ячеек инвентаря")]
        public int Capacity = 12;
        [Tooltip("Кладутся в инвентарь при старте забега")]
        public List<string> StartingItems = new List<string>();
        [Tooltip("Пары «SlotId → ItemId», сразу надеваются")]
        public List<StartingEquipmentEntry> StartingEquipment = new List<StartingEquipmentEntry>();
    }
}
