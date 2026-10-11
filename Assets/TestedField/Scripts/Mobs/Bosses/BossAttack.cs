using UnityEngine;

namespace TestedField
{
    /// <summary>Где рисуется круг атаки босса.</summary>
    public enum BossAttackShape
    {
        /// <summary>На HitDistance перед боссом.</summary>
        Front,
        /// <summary>Вокруг босса.</summary>
        Self,
        /// <summary>Под целью (место фиксируется в начале замаха).</summary>
        Target,
    }

    /// <summary>BP_BossAttack: данные одной атаки босса (P0-09). Расстояния — в метрах.</summary>
    [CreateAssetMenu(menuName = "TestedField/Mobs/Boss Attack", fileName = "DA_BossAttack_")]
    public class BossAttack : ScriptableObject
    {
        public string AttackId;
        public BossAttackShape Shape;
        [Tooltip("Атака доступна, если цель не дальше")]
        public float UseRange = 2.5f;
        [Tooltip("Front: расстояние до центра круга")]
        public float HitDistance = 1.5f;
        public float HitRadius = 1.5f;
        public float Damage = 20f;
        public float Windup = 1f;
        public float Recovery = 0.8f;
        [Tooltip("Вес при выборе по весам")]
        public float Weight = 1f;
        [Tooltip("С какой фазы доступна (1 — сразу)")]
        public int MinPhase = 1;
    }
}
