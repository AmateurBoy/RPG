using UnityEngine;

namespace TestedField
{
    /// <summary>Механика навыка (EffectType в UE). None — навык не срабатывает.</summary>
    public enum SkillEffectType
    {
        None,
        Dash,
        Heal,
        Projectile,
    }

    /// <summary>BP_SkillData: один активный навык (P0-17). Поля, не нужные механике, игнорируются.</summary>
    [CreateAssetMenu(menuName = "TestedField/Skills/Skill", fileName = "DA_Skill_")]
    public class SkillData : ScriptableObject
    {
        [Tooltip("Уникальный ID; им ссылается предмет, он же ходит по сети")]
        public string SkillId;
        public string DisplayName;
        [TextArea] public string Description;
        [Tooltip("Иконка слота; пусто — в слоте пишется название")]
        public Texture2D Icon;
        public float ManaCost;
        [Tooltip("Перезарядка, с")]
        public float Cooldown;
        public SkillEffectType EffectType;

        [Header("Effect")]
        [Tooltip("Урон (Dash, Projectile)")]
        public float Damage;
        [Tooltip("Длина рывка (Dash) / дальность полёта (Projectile), м")]
        public float Distance;
        [Tooltip("Ширина полосы урона (Dash), радиус лечения (Heal), радиус снаряда (Projectile), м")]
        public float Radius;
        [Tooltip("Сколько HP восстанавливает (Heal)")]
        public float HealAmount;
        [Tooltip("Скорость снаряда (Projectile), м/с")]
        public float Speed;

        [Header("Visual")]
        [Tooltip("Вспышка на модели героя при касте (видят все)")]
        public Material CastFlashMaterial;
        public float CastFlashDuration = 0.25f;
        [Tooltip("Звук каста (пусто — без звука)")]
        public AudioClip CastSound;
        [Tooltip("Материал снаряда (пусто — материал по умолчанию из SkillProjectile)")]
        public Material ProjectileMaterial;
    }
}
