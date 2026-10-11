using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_MobData: все числа и внешний вид моба (P0-03). Новый тип моба без кода: создать ассет,
    /// заполнить числа и выбрать его в MobBase.MobData (или сделать вариант префаба).
    /// Расстояния — в метрах (в UE — сантиметры).
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Mobs/Mob Data", fileName = "DA_Mob_")]
    public class MobData : ScriptableObject
    {
        [Header("Stats")]
        public float MaxHealth = 60f;
        [Tooltip("Скорость отбрасывания, м/с (0 — моба не отбрасывает)")]
        public float KnockbackSpeed = 3f;

        [Header("Movement")]
        public float PatrolSpeed = 1.8f;
        public float ChaseSpeed = 3.8f;
        public float PatrolRadius = 6f;
        public float PatrolWaitTime = 2f;

        [Header("Perception")]
        public float DetectionRadius = 9f;
        public float LoseTargetRadius = 15f;
        public float LeashRadius = 25f;
        public float ThinkInterval = 0.2f;

        [Header("Attack")]
        public float AttackDamage = 15f;
        [Tooltip("Дистанция начала атаки")]
        public float AttackRange = 1.7f;
        [Tooltip("Центр круга удара перед мобом")]
        public float AttackHitDistance = 1.1f;
        public float AttackHitRadius = 1.1f;
        public float AttackWindup = 0.8f;
        public float AttackRecovery = 0.6f;
        public float AttackCooldown = 1f;

        [Header("Death")]
        public float CorpseLifetime = 3f;

        [Header("Visual")]
        public Material BodyMaterial;

        [Header("Loot")]
        [Tooltip("Таблица дропа (P0-12)")]
        public LootTable LootTable;
    }
}
