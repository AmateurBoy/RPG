using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>BP_ZoneData: зона забега — заготовки, правила наполнения, боссы.</summary>
    [CreateAssetMenu(menuName = "TestedField/Zones/Zone Data", fileName = "DA_Zone_")]
    public class ZoneData : ScriptableObject
    {
        public string ZoneId;
        public string DisplayName;
        [Tooltip("Заготовки; выбирается одна, равновероятно по seed")]
        public List<ZoneLayout> Layouts = new List<ZoneLayout>();
        [Tooltip("Правила наполнения слотов (одно правило на тип слота)")]
        public List<SlotFillRule> FillRules = new List<SlotFillRule>();
        [Tooltip("Боссы зоны (префабы BossBase); арена ставит первого (P2-01 — случайный выбор)")]
        public List<BossBase> BossPrefabs = new List<BossBase>();
        [Tooltip("Финальная локация: победа над её боссом = победа в забеге (P0-10)")]
        public bool IsFinalZone;
    }
}
