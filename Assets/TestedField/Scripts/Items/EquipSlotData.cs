using UnityEngine;

namespace TestedField
{
    /// <summary>BP_EquipSlotData: слот экипировки.</summary>
    [CreateAssetMenu(menuName = "TestedField/Items/Equip Slot", fileName = "DA_Slot_")]
    public class EquipSlotData : ScriptableObject
    {
        public string SlotId;
        public string DisplayName;
    }
}
