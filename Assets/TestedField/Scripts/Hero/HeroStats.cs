using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>Кто умеет получать отбрасывание (герой — через владельца, моб — на хосте).</summary>
    public interface IKnockbackReceiver
    {
        void ApplyKnockback(Vector3 velocity);
    }

    /// <summary>
    /// BPC_HeroStats: здоровье, мана, стамина, смерть, неуязвимость, отклик на попадание.
    /// Общий компонент для героев, манекенов, мобов и боссов. Значения меняет только хост,
    /// клиенты получают их репликацией (P0-01, P0-02).
    /// </summary>
    public class HeroStats : NetworkBehaviour
    {
        [Header("Stats")]
        public float MaxHealth = 100f;
        public float MaxMana = 50f;
        public float MaxStamina = 100f;
        public float HealthRegenRate = 0f;
        public float ManaRegenRate = 2f;
        public float StaminaRegenRate = 25f;
        [Tooltip("Восстановление стамины начинается через столько секунд после последней траты")]
        public float StaminaRegenDelay = 1f;

        [Header("HitReact")]
        [Tooltip("Скорость отбрасывания, м/с (0 — без него)")]
        public float KnockbackSpeed = 4.5f;
        public float HitFlashDuration = 0.12f;
        [Tooltip("Overlay-материал вспышки; пусто — GameAssets.HitFlashMaterial")]
        public Material HitFlashMaterial;
        [Tooltip("Звук попадания; пусто — GameAssets.HitSound")]
        public AudioClip HitSound;
        [Tooltip("Корень моделей для вспышки; пусто — весь объект")]
        public GameObject FlashRoot;

        readonly NetworkVariable<float> health = new NetworkVariable<float>();
        readonly NetworkVariable<float> mana = new NetworkVariable<float>();
        readonly NetworkVariable<float> stamina = new NetworkVariable<float>();
        readonly NetworkVariable<float> maxHealthNet = new NetworkVariable<float>();
        readonly NetworkVariable<float> maxManaNet = new NetworkVariable<float>();
        readonly NetworkVariable<float> maxStaminaNet = new NetworkVariable<float>();
        readonly NetworkVariable<bool> isDead = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> invulnerableFlag = new NetworkVariable<bool>();
        readonly NetworkVariable<double> invulnerableUntil = new NetworkVariable<double>();
        readonly NetworkVariable<int> hitReactCount = new NetworkVariable<int>();

        public float Health => health.Value;
        public float Mana => mana.Value;
        public float Stamina => stamina.Value;
        public float CurrentMaxHealth => maxHealthNet.Value;
        public float CurrentMaxMana => maxManaNet.Value;
        public float CurrentMaxStamina => maxStaminaNet.Value;
        public bool IsDead => isDead.Value;
        public float HealthFraction => maxHealthNet.Value > 0f ? health.Value / maxHealthNet.Value : 0f;

        public bool IsInvulnerable => invulnerableFlag.Value || NetSession.ServerTime < invulnerableUntil.Value;

        /// <summary>(новое HP, максимум) — у всех.</summary>
        public event Action<float, float> OnHealthChanged;
        /// <summary>(урон, источник) — только на хосте.</summary>
        public event Action<float, GameObject> OnDamaged;
        /// <summary>HP дошло до 0 — только на хосте.</summary>
        public event Action OnDeath;

        bool basesCaptured;
        float baseMaxHealth, baseMaxMana, baseMaxStamina, baseHealthRegen, baseManaRegen, baseStaminaRegen;
        float lastStaminaSpendTime = -100f;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                maxHealthNet.Value = MaxHealth;
                maxManaNet.Value = MaxMana;
                maxStaminaNet.Value = MaxStamina;
                health.Value = MaxHealth;
                mana.Value = MaxMana;
                stamina.Value = MaxStamina;
                isDead.Value = false;
            }

            health.OnValueChanged += HandleHealthChanged;
            hitReactCount.OnValueChanged += OnRepHitReactCount;
        }

        public override void OnNetworkDespawn()
        {
            health.OnValueChanged -= HandleHealthChanged;
            hitReactCount.OnValueChanged -= OnRepHitReactCount;
        }

        void HandleHealthChanged(float previous, float current)
        {
            OnHealthChanged?.Invoke(current, maxHealthNet.Value);
        }

        void Update()
        {
            if (!IsSpawned || !IsServer || isDead.Value)
                return;

            float dt = Time.deltaTime;
            if (HealthRegenRate > 0f && health.Value < maxHealthNet.Value)
                health.Value = Mathf.Min(maxHealthNet.Value, health.Value + HealthRegenRate * dt);
            if (ManaRegenRate > 0f && mana.Value < maxManaNet.Value)
                mana.Value = Mathf.Min(maxManaNet.Value, mana.Value + ManaRegenRate * dt);
            if (StaminaRegenRate > 0f && stamina.Value < maxStaminaNet.Value && Time.time - lastStaminaSpendTime >= StaminaRegenDelay)
                stamina.Value = Mathf.Min(maxStaminaNet.Value, stamina.Value + StaminaRegenRate * dt);
        }

        // ---------------- Урон и смерть (хост) ----------------

        /// <summary>Урон. Не проходит, если цель мертва или неуязвима. Возвращает bApplied.</summary>
        public bool ApplyDamage(float amount, GameObject causer)
        {
            if (!IsServer || !IsSpawned || isDead.Value || IsInvulnerable || amount <= 0f)
                return false;

            health.Value = Mathf.Max(0f, health.Value - amount);
            OnDamaged?.Invoke(amount, causer);
            PlayHitReact(causer);

            if (health.Value <= 0f)
            {
                isDead.Value = true;
                OnDeath?.Invoke();
            }
            return true;
        }

        void PlayHitReact(GameObject causer)
        {
            hitReactCount.Value++;
            if (KnockbackSpeed > 0f && causer != null && TryGetComponent<IKnockbackReceiver>(out var receiver))
            {
                var dir = transform.position - causer.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = -transform.forward;
                receiver.ApplyKnockback(dir.normalized * KnockbackSpeed);
            }
        }

        void OnRepHitReactCount(int previous, int current)
        {
            var mat = HitFlashMaterial != null ? HitFlashMaterial : GameAssets.I.HitFlashMaterial;
            OverlayFlash.Play(FlashRoot != null ? FlashRoot : gameObject, mat, HitFlashDuration);
            var clip = HitSound != null ? HitSound : GameAssets.I.GetHitSound();
            ProceduralAudio.PlayAt(clip, transform.position + Vector3.up, 0.7f);
        }

        public bool Heal(float amount)
        {
            if (!IsServer || isDead.Value || amount <= 0f)
                return false;
            health.Value = Mathf.Min(maxHealthNet.Value, health.Value + amount);
            return true;
        }

        /// <summary>Поднятие: HP = доля от максимума.</summary>
        public void Revive(float healthFraction)
        {
            if (!IsServer)
                return;
            isDead.Value = false;
            health.Value = Mathf.Max(1f, maxHealthNet.Value * Mathf.Clamp01(healthFraction));
        }

        public void ResetToFull()
        {
            if (!IsServer)
                return;
            isDead.Value = false;
            RefillAll();
        }

        public void RefillAll()
        {
            if (!IsServer)
                return;
            health.Value = maxHealthNet.Value;
            mana.Value = maxManaNet.Value;
            stamina.Value = maxStaminaNet.Value;
        }

        public void GrantInvulnerability(float duration)
        {
            if (!IsServer || duration <= 0f)
                return;
            double until = NetSession.ServerTime + duration;
            if (until > invulnerableUntil.Value)
                invulnerableUntil.Value = until;
        }

        public void SetInvulnerable(bool value)
        {
            if (IsServer)
                invulnerableFlag.Value = value;
        }

        // ---------------- Ресурсы ----------------

        public bool HasStamina(float cost) => stamina.Value >= cost;
        public bool HasMana(float cost) => mana.Value >= cost;

        public bool TrySpendStamina(float cost)
        {
            if (!IsServer || stamina.Value < cost)
                return false;
            stamina.Value -= cost;
            lastStaminaSpendTime = Time.time;
            return true;
        }

        /// <summary>Непрерывная трата (спринт): списывает сколько есть.</summary>
        public void DrainStamina(float amount)
        {
            if (!IsServer || amount <= 0f)
                return;
            stamina.Value = Mathf.Max(0f, stamina.Value - amount);
            lastStaminaSpendTime = Time.time;
        }

        public bool TrySpendMana(float cost)
        {
            if (!IsServer || mana.Value < cost)
                return false;
            mana.Value -= cost;
            return true;
        }

        /// <summary>Максимум HP из данных (мобы, множитель боссов). Текущее HP = максимум.</summary>
        public void SetMaxHealth(float value, bool refill = true)
        {
            if (!IsServer)
                return;
            MaxHealth = value;
            if (basesCaptured)
                baseMaxHealth = value;
            maxHealthNet.Value = value;
            if (refill)
                health.Value = value;
            else
                health.Value = Mathf.Min(health.Value, value);
        }

        // ---------------- Модификаторы предметов (P0-11) ----------------

        /// <summary>
        /// Временный мост до системы статов (P0-16): при первом вызове запоминает базовые значения
        /// и применяет (База + Σ плоских) × (1 + Σ процентов / 100). Текущие значения обрезаются по новому максимуму.
        /// </summary>
        public void ApplyItemModifiers(IReadOnlyDictionary<string, float> flat, IReadOnlyDictionary<string, float> percent)
        {
            if (!IsServer)
                return;
            if (!basesCaptured)
            {
                basesCaptured = true;
                baseMaxHealth = MaxHealth;
                baseMaxMana = MaxMana;
                baseMaxStamina = MaxStamina;
                baseHealthRegen = HealthRegenRate;
                baseManaRegen = ManaRegenRate;
                baseStaminaRegen = StaminaRegenRate;
            }

            maxHealthNet.Value = StatMath.Modified(baseMaxHealth, "MaxHealth", flat, percent);
            maxManaNet.Value = StatMath.Modified(baseMaxMana, "MaxMana", flat, percent);
            maxStaminaNet.Value = StatMath.Modified(baseMaxStamina, "MaxStamina", flat, percent);
            HealthRegenRate = StatMath.Modified(baseHealthRegen, "HealthRegenRate", flat, percent);
            ManaRegenRate = StatMath.Modified(baseManaRegen, "ManaRegenRate", flat, percent);
            StaminaRegenRate = StatMath.Modified(baseStaminaRegen, "StaminaRegenRate", flat, percent);

            health.Value = Mathf.Min(health.Value, maxHealthNet.Value);
            mana.Value = Mathf.Min(mana.Value, maxManaNet.Value);
            stamina.Value = Mathf.Min(stamina.Value, maxStaminaNet.Value);
        }
    }

    public static class StatMath
    {
        /// <summary>(База + Σ плоских) × (1 + Σ процентов / 100).</summary>
        public static float Modified(float baseValue, string stat, IReadOnlyDictionary<string, float> flat, IReadOnlyDictionary<string, float> percent)
        {
            float f = 0f, p = 0f;
            if (flat != null && flat.TryGetValue(stat, out var fv)) f = fv;
            if (percent != null && percent.TryGetValue(stat, out var pv)) p = pv;
            return (baseValue + f) * (1f + p / 100f);
        }
    }
}
