using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_BossBase: босс — наследник моба (P0-09). Атаки с телеграфом из данных, фазы по HP,
    /// множители от числа игроков, сброс боя. Бой начинает и сбрасывает арена (BossArena).
    /// </summary>
    public class BossBase : MobBase
    {
        public static readonly List<BossBase> AllBosses = new List<BossBase>();

        readonly NetworkVariable<int> phase = new NetworkVariable<int>(1);
        readonly NetworkVariable<bool> inFight = new NetworkVariable<bool>();

        public BossData BossData => MobData as BossData;
        public int Phase => phase.Value;
        public bool InFight => inFight.Value;
        public string DisplayName => BossData != null && !string.IsNullOrEmpty(BossData.DisplayName) ? BossData.DisplayName : name;

        /// <summary>Хост: арена, в которой стоит босс (null — поставлен вручную).</summary>
        public BossArena Arena { get; set; }

        float playerHealthMultiplier = 1f;
        float playerDamageMultiplier = 1f;
        readonly List<BossAttack> candidates = new List<BossAttack>();
        readonly List<float> candidateWeights = new List<float>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            AllBosses.Add(this);
            phase.OnValueChanged += OnRepPhase;
            ApplyPhaseVisual(phase.Value);
            if (IsServer)
                Stats.OnHealthChanged += CheckPhase;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            AllBosses.Remove(this);
            phase.OnValueChanged -= OnRepPhase;
            if (IsServer)
                Stats.OnHealthChanged -= CheckPhase;
        }

        protected override void ApplyMobData()
        {
            base.ApplyMobData();
            var data = BossData;
            if (data == null)
                return;
            float scale = Mathf.Max(0.1f, data.VisualScale);
            visual?.SetScale(scale);
            if (TryGetComponent<CapsuleCollider>(out var capsule))
            {
                capsule.radius = 0.4f * scale;
                capsule.height = 1.8f * scale;
                capsule.center = new Vector3(0f, 0.9f * scale, 0f);
            }
            agent.radius = 0.4f * scale;
            agent.height = 1.8f * scale;
            agent.stoppingDistance = Mathf.Max(0.5f, data.AttackRange * 0.8f);
        }

        // ---------------- Бой ----------------

        /// <summary>Хост: начало боя. HP × множитель от числа игроков, фаза 1, преследование.</summary>
        public void StartFight(int playerCount)
        {
            if (!IsServer || IsDead || BossData == null)
                return;
            int idx = Mathf.Clamp(playerCount, 1, 4) - 1;
            playerHealthMultiplier = BossData.At(BossData.PlayerCountHealthMultipliers, idx);
            playerDamageMultiplier = BossData.At(BossData.PlayerCountDamageMultipliers, idx);
            Stats.SetMaxHealth(BossData.MaxHealth * playerHealthMultiplier);
            phase.Value = 1;
            inFight.Value = true;
            lastAttackEndTime = Time.time - BossData.AttackCooldown * 0.5f;
            EnterChase();
        }

        /// <summary>Хост: сброс боя — босс на месте, полное HP, фаза 1, ожидание.</summary>
        public void ResetFight()
        {
            if (!IsServer || IsDead)
                return;
            inFight.Value = false;
            phase.Value = 1;
            target = null;
            lastAttackEndTime = -100f;
            if (agent.enabled)
            {
                agent.ResetPath();
                agent.Warp(homePosition);
            }
            else
            {
                transform.position = homePosition;
            }
            transform.rotation = homeRotation;
            Stats.SetMaxHealth(BossData != null ? BossData.MaxHealth : Stats.MaxHealth);
            SetState(MobAIState.Patrol);
        }

        void CheckPhase(float health, float maxHealth)
        {
            if (!IsServer || BossData == null || maxHealth <= 0f || IsDead)
                return;
            float fraction = health / maxHealth;
            int p = phase.Value;
            while (p < BossData.PhaseCount && fraction <= BossData.PhaseHealthThresholds[p - 1])
                p++;
            if (p != phase.Value)
            {
                phase.Value = p;
                if (agent.enabled)
                    agent.speed = GetChaseSpeed();
                Debug.Log($"[Boss] {DisplayName} phase {p}, speed {GetChaseSpeed():0.##}");
            }
        }

        protected override float GetChaseSpeed()
        {
            float baseSpeed = MobData != null ? MobData.ChaseSpeed : 3f;
            return BossData != null ? baseSpeed * BossData.At(BossData.PhaseSpeedMultipliers, phase.Value - 1) : baseSpeed;
        }

        float DamageMultiplier => BossData != null ? BossData.At(BossData.PhaseDamageMultipliers, phase.Value - 1) * playerDamageMultiplier : 1f;

        // ---------------- ИИ ----------------

        protected override void TickPatrol()
        {
            // Ожидание: без патруля и без обнаружения — бой начинает арена.
            if (inFight.Value)
            {
                EnterChase();
                return;
            }
            if (agent.enabled && agent.hasPath)
                agent.ResetPath();
        }

        protected override void EnterReturn()
        {
            // Боссу некуда возвращаться: вне боя он ждёт, в бою — ждёт новую цель (сброс решает арена).
            target = null;
            if (agent.enabled)
                agent.ResetPath();
            if (!inFight.Value)
                SetState(MobAIState.Patrol);
        }

        /// <summary>Ближайший живой герой внутри арены (FightRadius = радиус арены).</summary>
        protected override HeroController FindTarget(float radius)
        {
            if (Arena == null)
                return base.FindTarget(radius);
            HeroController best = null;
            float bestDist = float.MaxValue;
            foreach (var hero in HeroController.All)
            {
                if (!Combat.IsHeroAlive(hero) || !Arena.IsInside(hero.transform.position))
                    continue;
                float d = Combat.FlatDistance(hero.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = hero;
                }
            }
            return best;
        }

        protected override void TickChase()
        {
            if (!inFight.Value)
            {
                SetState(MobAIState.Patrol);
                return;
            }

            target = FindTarget(MobData.LoseTargetRadius);
            if (target == null)
            {
                if (agent.enabled)
                    agent.ResetPath();
                return;
            }

            float dist = Combat.FlatDistance(transform.position, target.transform.position);
            if (Time.time >= lastAttackEndTime + MobData.AttackCooldown)
            {
                var attack = ChooseAttack(dist);
                if (attack != null)
                {
                    StartBossAttack(attack);
                    return;
                }
            }

            if (agent.enabled)
            {
                agent.isStopped = false;
                agent.speed = GetChaseSpeed();
                if (dist > MobData.AttackRange)
                    agent.SetDestination(target.transform.position);
                else
                    agent.ResetPath();
            }
        }

        /// <summary>Атака по весам среди доступных: MinPhase ≤ фаза и UseRange ≥ дистанция.</summary>
        BossAttack ChooseAttack(float distance)
        {
            candidates.Clear();
            candidateWeights.Clear();
            if (BossData == null)
                return null;
            foreach (var a in BossData.Attacks)
            {
                if (a == null || a.MinPhase > phase.Value || a.UseRange < distance)
                    continue;
                candidates.Add(a);
                candidateWeights.Add(a.Weight);
            }
            if (candidates.Count == 0)
                return null;

            float total = 0f;
            foreach (var w in candidateWeights) total += Mathf.Max(0f, w);
            if (total <= 0f)
                return candidates[0];
            float roll = Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                float w = Mathf.Max(0f, candidateWeights[i]);
                if (roll < w)
                    return candidates[i];
                roll -= w;
            }
            return candidates[candidates.Count - 1];
        }

        void StartBossAttack(BossAttack attack)
        {
            FaceTarget();
            Vector3 center;
            switch (attack.Shape)
            {
                case BossAttackShape.Self:
                    center = transform.position;
                    break;
                case BossAttackShape.Target:
                    center = target != null ? target.transform.position : transform.position;
                    break;
                default:
                    center = transform.position + transform.forward * attack.HitDistance;
                    break;
            }
            BeginAttack(center, attack.HitRadius, attack.Windup, attack.Recovery, attack.Damage * DamageMultiplier);
        }

        protected override void StartAttack()
        {
            // Атаки босса выбираются в TickChase из данных.
        }

        // ---------------- Визуал ----------------

        void OnRepPhase(int previous, int current) => ApplyPhaseVisual(current);

        void ApplyPhaseVisual(int currentPhase)
        {
            if (visual == null || BossData == null)
                return;
            if (currentPhase >= 2 && BossData.EnragedMaterial != null)
                visual.SetBodyMaterial(BossData.EnragedMaterial);
            else
                visual.SetBodyMaterial(BossData.BodyMaterial);
        }
    }
}
