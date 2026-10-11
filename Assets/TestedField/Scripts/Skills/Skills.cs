using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BPC_Skills: слоты активных навыков от надетых предметов, перезарядка, мана, эффекты (P0-17).
    /// Всё решает хост; клиент шлёт только ServerUseSkillRpc(SlotIndex).
    /// </summary>
    public class Skills : NetworkBehaviour
    {
        public SkillConfig Config;
        [Tooltip("Снаряд навыков типа Projectile; пусто — GameAssets.SkillProjectilePrefab")]
        public GameObject ProjectilePrefab;

        readonly NetworkList<FixedString32Bytes> slotSkills = new NetworkList<FixedString32Bytes>();
        readonly NetworkList<FixedString32Bytes> cooldownSkillIds = new NetworkList<FixedString32Bytes>();
        readonly NetworkList<double> cooldownEndTimes = new NetworkList<double>();
        readonly NetworkVariable<int> castCount = new NetworkVariable<int>();
        readonly NetworkVariable<FixedString32Bytes> lastCastSkillId = new NetworkVariable<FixedString32Bytes>();

        /// <summary>Слоты изменились (у всех).</summary>
        public event Action OnSkillsChanged;

        HeroController hero;
        HeroStats stats;
        Inventory inventory;
        readonly List<HeroStats> overlapResults = new List<HeroStats>();
        readonly HashSet<HeroStats> hitSet = new HashSet<HeroStats>();
        readonly List<string> wanted = new List<string>();
        AudioClip fallbackCastSound;

        public SkillConfig ActiveConfig => Config != null ? Config : GameAssets.I.SkillConfig;
        public int SlotCount => slotSkills.Count;
        public string GetSlotSkill(int slot) => slot >= 0 && slot < slotSkills.Count ? slotSkills[slot].ToString() : "";

        void Awake()
        {
            hero = GetComponent<HeroController>();
            stats = GetComponent<HeroStats>();
            inventory = GetComponent<Inventory>();
        }

        public override void OnNetworkSpawn()
        {
            slotSkills.OnListChanged += HandleSlotsChanged;
            castCount.OnValueChanged += OnRepCastCount;

            if (IsServer)
            {
                slotSkills.Clear();
                int count = ActiveConfig != null ? ActiveConfig.SlotCount : 4;
                for (int i = 0; i < count; i++)
                    slotSkills.Add(default);
                if (inventory != null)
                    inventory.OnInventoryChanged += RebuildSlots;
                RebuildSlots();
            }
        }

        public override void OnNetworkDespawn()
        {
            slotSkills.OnListChanged -= HandleSlotsChanged;
            castCount.OnValueChanged -= OnRepCastCount;
            if (inventory != null)
                inventory.OnInventoryChanged -= RebuildSlots;
        }

        void HandleSlotsChanged(NetworkListEvent<FixedString32Bytes> e)
        {
            OnSkillsChanged?.Invoke();
        }

        /// <summary>
        /// Хост: навыки надетых предметов. Пропавший навык убирается из своего слота, новый встаёт
        /// в первый свободный, остальные остаются на своих клавишах.
        /// </summary>
        public void RebuildSlots()
        {
            if (!IsServer || inventory == null || ActiveConfig == null)
                return;

            wanted.Clear();
            var db = inventory.Database;
            foreach (var itemId in inventory.EquippedItemIds())
            {
                var item = db != null ? db.GetItemDefinition(itemId, false) : null;
                if (item == null || string.IsNullOrEmpty(item.ActiveSkill))
                    continue;
                if (ActiveConfig.GetSkillDefinition(item.ActiveSkill) == null)
                    continue;
                if (!wanted.Contains(item.ActiveSkill))
                    wanted.Add(item.ActiveSkill);
            }

            for (int i = 0; i < slotSkills.Count; i++)
            {
                string current = slotSkills[i].ToString();
                if (!string.IsNullOrEmpty(current) && !wanted.Contains(current))
                    slotSkills[i] = default;
            }

            foreach (var skillId in wanted)
            {
                if (FindSlot(skillId) >= 0)
                    continue;
                int free = FindSlot("");
                if (free < 0)
                {
                    Debug.LogWarning($"[Skills] No free skill slot for: {skillId}");
                    continue;
                }
                slotSkills[free] = NetStrings.To32(skillId);
            }
        }

        int FindSlot(string skillId)
        {
            for (int i = 0; i < slotSkills.Count; i++)
                if (slotSkills[i].ToString() == skillId)
                    return i;
            return -1;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || GameInput.GameplayBlocked || ActiveConfig == null)
                return;
            HandleSkillInput();
        }

        void HandleSkillInput()
        {
            var keys = ActiveConfig.SlotKeys;
            for (int i = 0; i < slotSkills.Count && i < keys.Count; i++)
                if (GameInput.WasKeyPressedThisFrame(keys[i]))
                    RequestUseSkill(i);
        }

        public void RequestUseSkill(int slot)
        {
            if (!CanUseSkill(slot))
                return;
            if (IsServer)
                TryUseSkill(slot);
            else
                ServerUseSkillRpc(slot);
        }

        [Rpc(SendTo.Server)]
        void ServerUseSkillRpc(int slot, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                TryUseSkill(slot);
        }

        public bool CanUseSkill(int slot)
        {
            var skill = ActiveConfig != null ? ActiveConfig.GetSkillDefinition(GetSlotSkill(slot), false) : null;
            if (skill == null || skill.EffectType == SkillEffectType.None)
                return false;
            if (hero != null && hero.IsDowned)
                return false;
            if (stats.IsDead)
                return false;
            if (GetCooldownRemaining(skill.SkillId) > 0f)
                return false;
            return stats.Mana >= skill.ManaCost;
        }

        /// <summary>Сколько секунд перезарядки осталось (по навыку, а не по слоту).</summary>
        public float GetCooldownRemaining(string skillId)
        {
            for (int i = 0; i < cooldownSkillIds.Count; i++)
                if (cooldownSkillIds[i].ToString() == skillId)
                    return Mathf.Max(0f, (float)(cooldownEndTimes[i] - NetSession.ServerTime));
            return 0f;
        }

        bool TryUseSkill(int slot)
        {
            if (!IsServer || !CanUseSkill(slot))
                return false;
            var skill = ActiveConfig.GetSkillDefinition(GetSlotSkill(slot));
            if (!stats.TrySpendMana(skill.ManaCost))
                return false;

            SetCooldown(skill.SkillId, NetSession.ServerTime + skill.Cooldown);
            lastCastSkillId.Value = NetStrings.To32(skill.SkillId);
            castCount.Value++;

            switch (skill.EffectType)
            {
                case SkillEffectType.Dash: DoDash(skill); break;
                case SkillEffectType.Heal: DoHeal(skill); break;
                case SkillEffectType.Projectile: DoProjectile(skill); break;
            }
            return true;
        }

        void SetCooldown(string skillId, double endTime)
        {
            for (int i = 0; i < cooldownSkillIds.Count; i++)
            {
                if (cooldownSkillIds[i].ToString() == skillId)
                {
                    cooldownEndTimes[i] = endTime;
                    return;
                }
            }
            cooldownSkillIds.Add(NetStrings.To32(skillId));
            cooldownEndTimes.Add(endTime);
        }

        // ---------------- Эффекты (хост) ----------------

        /// <summary>Рывок вперёд на Distance; стена останавливает раньше. Всем целям в полосе — Damage (каждой один раз).</summary>
        void DoDash(SkillData skill)
        {
            const float bodyRadius = 0.4f;
            var start = transform.position;
            var dir = transform.forward;
            dir.y = 0f;
            dir.Normalize();

            float distance = skill.Distance;
            var origin = start + Vector3.up * 1f;
            var hits = Physics.SphereCastAll(origin, bodyRadius * 0.8f, dir, skill.Distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (!Combat.IsWorldStatic(h.collider))
                    continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, h.distance - 0.05f));
            }

            var end = start + dir * distance;

            hitSet.Clear();
            Combat.OverlapCapsule(start + Vector3.up, end + Vector3.up, Mathf.Max(0.1f, skill.Radius), overlapResults);
            bool friendlyFire = hero != null && hero.FriendlyFire;
            foreach (var target in overlapResults)
            {
                if (!Combat.CanHitTarget(gameObject, target, hitSet, friendlyFire))
                    continue;
                hitSet.Add(target);
                target.ApplyDamage(skill.Damage, gameObject);
            }

            hero?.TeleportTo(end, transform.rotation);
        }

        /// <summary>Все живые герои в Radius (включая кастующего) получают HealAmount. Radius = 0 — только себя.</summary>
        void DoHeal(SkillData skill)
        {
            if (skill.Radius <= 0f)
            {
                stats.Heal(skill.HealAmount);
                return;
            }
            foreach (var other in HeroController.All)
            {
                if (other == null || other.Stats.IsDead)
                    continue;
                if (Combat.FlatDistance(other.transform.position, transform.position) <= skill.Radius)
                    other.Stats.Heal(skill.HealAmount);
            }
        }

        void DoProjectile(SkillData skill)
        {
            var prefab = ProjectilePrefab != null ? ProjectilePrefab : GameAssets.I.SkillProjectilePrefab;
            if (prefab == null)
            {
                Debug.LogError("[Skills] No SkillProjectile prefab");
                return;
            }
            var dir = transform.forward;
            dir.y = 0f;
            var pos = transform.position + dir.normalized * 0.7f + Vector3.up * 1.1f;
            var go = Instantiate(prefab, pos, Quaternion.LookRotation(dir));
            var projectile = go.GetComponent<SkillProjectile>();
            projectile.Init(skill.SkillId, gameObject);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        // ---------------- Визуал каста (у всех) ----------------

        void OnRepCastCount(int previous, int current)
        {
            var skill = ActiveConfig != null ? ActiveConfig.GetSkillDefinition(lastCastSkillId.Value.ToString(), false) : null;
            if (skill == null)
                return;
            var root = hero != null && hero.Visual != null ? hero.Visual.FlashRoot : gameObject;
            var mat = skill.CastFlashMaterial != null ? skill.CastFlashMaterial : GameAssets.I.HitFlashMaterial;
            OverlayFlash.Play(root, mat, skill.CastFlashDuration);

            var clip = skill.CastSound;
            if (clip == null)
            {
                if (fallbackCastSound == null)
                    fallbackCastSound = ProceduralAudio.MakeCast(1f);
                clip = fallbackCastSound;
            }
            ProceduralAudio.PlayAt(clip, transform.position + Vector3.up, 0.6f);
        }
    }
}
