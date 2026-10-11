using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    [Serializable]
    public struct LootEntry
    {
        [Tooltip("ItemId из ItemDatabase")]
        public string ItemId;
        [Tooltip("Вес записи; ≤ 0 — не выпадает")]
        public float Weight;
        [Tooltip("Сколько штук (случайно в диапазоне)")]
        public int MinCount;
        [Tooltip("Меньше MinCount — считается равным MinCount")]
        public int MaxCount;

        public LootEntry(string itemId, float weight, int minCount = 1, int maxCount = 1)
        {
            ItemId = itemId;
            Weight = weight;
            MinCount = minCount;
            MaxCount = maxCount;
        }
    }

    /// <summary>BP_LootTable: таблица дропа (P0-12).</summary>
    [CreateAssetMenu(menuName = "TestedField/Loot/Loot Table", fileName = "DA_Loot_")]
    public class LootTable : ScriptableObject
    {
        [Tooltip("Сколько раз бросать таблицу")]
        public int Rolls = 1;
        [Tooltip("Шанс (0–1), что бросок вообще что-то даст")]
        [Range(0f, 1f)] public float DropChance = 1f;
        public List<LootEntry> Entries = new List<LootEntry>();
        [Tooltip("Разброс предметов вокруг точки выпадения, м")]
        public float ScatterRadius = 0.6f;

        readonly List<float> weights = new List<float>();

        /// <summary>Выбор записи по весам; -1 — нечего выбирать.</summary>
        public int PickEntry(Func<float> random01)
        {
            float total = 0f;
            foreach (var e in Entries)
                if (e.Weight > 0f)
                    total += e.Weight;
            if (total <= 0f)
                return -1;
            float roll = random01() * total;
            for (int i = 0; i < Entries.Count; i++)
            {
                float w = Entries[i].Weight;
                if (w <= 0f)
                    continue;
                if (roll < w)
                    return i;
                roll -= w;
            }
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].Weight > 0f)
                    return i;
            return -1;
        }

        /// <summary>Сколько штук выпадает для записи.</summary>
        public int RollCount(int entryIndex, Func<float> random01)
        {
            var e = Entries[entryIndex];
            int min = Mathf.Max(1, e.MinCount);
            int max = Mathf.Max(min, e.MaxCount);
            return min + Mathf.Min(max - min, Mathf.FloorToInt(random01() * (max - min + 1)));
        }

        /// <summary>Rolls раз: при RandomFloat &lt; DropChance выбрать запись и RollCount копий.</summary>
        public List<string> Roll(Func<float> random01, List<string> result = null)
        {
            result ??= new List<string>();
            result.Clear();
            for (int r = 0; r < Rolls; r++)
            {
                if (random01() >= DropChance)
                    continue;
                int entry = PickEntry(random01);
                if (entry < 0)
                    continue;
                int count = RollCount(entry, random01);
                for (int c = 0; c < count; c++)
                    result.Add(Entries[entry].ItemId);
            }
            return result;
        }
    }
}
