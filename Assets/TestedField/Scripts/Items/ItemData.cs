using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    [Serializable]
    public struct StatModifier
    {
        [Tooltip("MaxHealth, MaxMana, MaxStamina, HealthRegenRate, ManaRegenRate, StaminaRegenRate, AttackDamage")]
        public string Stat;
        public float Value;

        public StatModifier(string stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }

    /// <summary>
    /// BP_ItemData: определение предмета. В инвентаре и по сети хранится только ItemId,
    /// статичные данные всегда берутся из базы (ItemDatabase).
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Items/Item", fileName = "DA_Item_")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Уникальный ID, по нему предмет ищут и выдают")]
        public string ItemId;
        public string DisplayName;
        [TextArea] public string Description;
        [Tooltip("Иконка; если пусто — в ячейке рисуется название")]
        public Texture2D Icon;
        [Tooltip("Weapon / Armor / Accessory (пока только информативно)")]
        public string Category;
        [Tooltip("Слот экипировки — ссылка на ассет слота, а не enum")]
        public EquipSlotData Slot;
        [Tooltip("Плоские бонусы: AttackDamage 10 = +10")]
        public List<StatModifier> FlatModifiers = new List<StatModifier>();
        [Tooltip("Процентные бонусы: StaminaRegenRate 10 = +10%")]
        public List<StatModifier> PercentModifiers = new List<StatModifier>();
        [Tooltip("SkillId активного навыка (P0-17); пусто — без навыка")]
        public string ActiveSkill;
        [Tooltip("Условие использования для P2-03 (пока не проверяется)")]
        public string UseCondition;
    }
}
