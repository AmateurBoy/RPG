using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Построенная зона на карте (экземпляр BP_ZoneLayout). Состояние (квест, босс) хранит RunMap —
    /// здесь только чтение.
    /// </summary>
    public class ZoneInstance : MonoBehaviour
    {
        public ZoneData Data;
        public ZoneLayout Layout;
        public int Index;
        public Vector3 Origin;

        public string ZoneId => Data != null ? Data.ZoneId : name;
        public string DisplayName => Data != null && !string.IsNullOrEmpty(Data.DisplayName) ? Data.DisplayName : ZoneId;
        public bool IsFinalZone => Data != null && Data.IsFinalZone;

        public bool QuestCompleted => RunMap.Instance != null && RunMap.Instance.IsZoneQuestCompleted(Index);
        public bool BossDefeated => RunMap.Instance != null && RunMap.Instance.IsZoneBossDefeated(Index);

        /// <summary>Границы зоны в мире (BoundsCenter / BoundsExtent).</summary>
        public Bounds WorldBounds
        {
            get
            {
                var b = Layout.GetLocalBounds();
                b.center += Origin;
                return b;
            }
        }

        public bool ContainsLocation(Vector3 world) => Layout != null && Layout.ContainsLocal(world - Origin);

        public void GetSlots(string slotType, List<Vector3> result)
        {
            foreach (var s in Layout.Slots)
                if (s.SlotType == slotType)
                    result.Add(Origin + s.Position);
        }

        public bool FindPassage(string passageId, out Vector3 worldPosition, out Vector3 outward)
        {
            bool found = Layout.FindPassage(passageId, out var local, out outward);
            worldPosition = Origin + local;
            return found;
        }
    }
}
