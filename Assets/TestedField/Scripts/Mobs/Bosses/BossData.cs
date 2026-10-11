using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_BossData: данные босса — все поля моба + поля босса (P0-09).
    /// Из полей моба боссу нужны MaxHealth, KnockbackSpeed, PatrolSpeed, ChaseSpeed, AttackRange
    /// (на какую дистанцию подходит), AttackCooldown, ThinkInterval, CorpseLifetime, BodyMaterial, LootTable.
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Mobs/Boss Data", fileName = "DA_Boss_")]
    public class BossData : MobData
    {
        [Header("Boss")]
        [Tooltip("Имя на HUD")]
        public string DisplayName;
        [Tooltip("Масштаб модели и капсулы")]
        public float VisualScale = 1.5f;
        [Tooltip("Материал с фазы 2 (пусто — без смены)")]
        public Material EnragedMaterial;
        public List<BossAttack> Attacks = new List<BossAttack>();
        [Tooltip("Доли HP, на которых начинается следующая фаза ([0.5] — 2 фазы)")]
        public List<float> PhaseHealthThresholds = new List<float> { 0.5f };
        [Tooltip("По фазам, начиная с 1-й")]
        public List<float> PhaseSpeedMultipliers = new List<float> { 1f, 1.3f };
        public List<float> PhaseDamageMultipliers = new List<float> { 1f, 1.2f };
        [Tooltip("По числу игроков 1–4. Сейчас все 1.0 — таблица в P1-09")]
        public List<float> PlayerCountHealthMultipliers = new List<float> { 1f, 1f, 1f, 1f };
        public List<float> PlayerCountDamageMultipliers = new List<float> { 1f, 1f, 1f, 1f };

        public int PhaseCount => PhaseHealthThresholds.Count + 1;

        public static float At(List<float> list, int index, float fallback = 1f)
        {
            if (list == null || list.Count == 0)
                return fallback;
            return list[Mathf.Clamp(index, 0, list.Count - 1)];
        }
    }
}
