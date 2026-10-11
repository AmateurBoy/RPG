using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>Условие открытия ворот на связи (P0-07, P0-10).</summary>
    public enum GateUnlockCondition
    {
        /// <summary>Выполнен квест зоны «откуда».</summary>
        ZoneQuest,
        /// <summary>Побеждены боссы всех зон, кроме финальной.</summary>
        AllZoneBosses,
        /// <summary>Сразу при сборке карты.</summary>
        Open,
        /// <summary>Побеждён босс зоны «откуда».</summary>
        ZoneBoss,
    }

    /// <summary>Связь: FromZone.FromPassage → ToZone.ToPassage. Зона «откуда» должна быть уже построена.</summary>
    [Serializable]
    public struct ZoneLink
    {
        public ZoneData FromZone;
        public string FromPassage;
        public ZoneData ToZone;
        public string ToPassage;
        public GateUnlockCondition UnlockCondition;
    }

    /// <summary>BP_RunMapData: стартовая зона и список связей между зонами.</summary>
    [CreateAssetMenu(menuName = "TestedField/Zones/Run Map Data", fileName = "DA_RunMap_")]
    public class RunMapData : ScriptableObject
    {
        public ZoneData StartZone;
        public List<ZoneLink> Links = new List<ZoneLink>();
        [Tooltip("Какие ворота ставить в стыки; пусто — проходы остаются открытыми")]
        public PassageGate PassageGatePrefab;
    }
}
