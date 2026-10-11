using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TestedField
{
    /// <summary>BP_SkillConfig: все навыки игры, число слотов и клавиши слотов.</summary>
    [CreateAssetMenu(menuName = "TestedField/Skills/Skill Config", fileName = "DA_SkillConfig")]
    public class SkillConfig : ScriptableObject
    {
        public List<SkillData> Skills = new List<SkillData>();
        [Tooltip("Число слотов навыков")]
        public int SlotCount = 4;
        [Tooltip("Клавиша каждого слота (индекс = слот). Слоты без клавиши не нажимаются")]
        public List<Key> SlotKeys = new List<Key> { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

        Dictionary<string, SkillData> lookup;

        public SkillData GetSkillDefinition(string skillId, bool logUnknown = true)
        {
            if (string.IsNullOrEmpty(skillId))
                return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, SkillData>(StringComparer.Ordinal);
                foreach (var s in Skills)
                    if (s != null && !string.IsNullOrEmpty(s.SkillId))
                        lookup[s.SkillId] = s;
            }
            if (lookup.TryGetValue(skillId, out var data))
                return data;
            if (logUnknown)
                Debug.LogWarning($"[Skills] Unknown SkillId: {skillId}");
            return null;
        }

        public string GetKeyLabel(int slot)
        {
            if (slot < 0 || slot >= SlotKeys.Count || SlotKeys[slot] == Key.None)
                return "";
            var key = SlotKeys[slot];
            if (key >= Key.Digit1 && key <= Key.Digit0)
                return key == Key.Digit0 ? "0" : ((int)(key - Key.Digit1) + 1).ToString();
            return key.ToString();
        }

        void OnValidate() => lookup = null;
        void OnEnable() => lookup = null;
    }
}
