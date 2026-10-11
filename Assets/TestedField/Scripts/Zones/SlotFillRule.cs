using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    [Serializable]
    public struct SlotContentOption
    {
        [Tooltip("Префаб с компонентом SlotContent (или наследником — арена босса)")]
        public SlotContent Prefab;
        public float Weight;
    }

    /// <summary>
    /// BP_SlotFillRule: как заполнять слоты одного типа в зоне (P0-06).
    /// MinCount ≥ 1 = объект обязательный: если слотов меньше — попытка генерации проваливается.
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Zones/Slot Fill Rule", fileName = "DA_Fill_")]
    public class SlotFillRule : ScriptableObject
    {
        public string SlotType;
        public int MinCount;
        public int MaxCount = 1;
        [Tooltip("Что ставить в слот и вес (выбор по весам)")]
        public List<SlotContentOption> Contents = new List<SlotContentOption>();
    }
}
