using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace TestedField
{
    public enum MobAIState : byte
    {
        Patrol,
        Chase,
        Attack,
        Return,
        Dead,
    }

    /// <summary>
    /// BP_MobBase: моб с ИИ (P0-03). ИИ работает только на хосте (таймер Think с шагом ThinkInterval),
    /// состояние AIState реплицируется — клиенты показывают телеграф, замах и смерть.
    /// Методы состояний виртуальные — боссы (BossBase) переопределяют их.
    /// </summary>
    [RequireComponent(typeof(HeroStats))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class MobBase : NetworkBehaviour, IKnockbackReceiver
    {
        public static readonly List<MobBase> All = new List<MobBase>();

        /// <summary>Хост: моб умер (квесты, счётчики).</summary>
        public static event Action<MobBase> OnMobDied;

        public MobData MobData;
        [Tooltip("Торможение после отбрасывания, м/с²")]
        public float KnockbackDeceleration = 20f;

        /// <summary>Хост: при смерти, перед выпадением лута (Mob, точка).</summary>
        public event Action<MobBase, Vector3> OnDropRequested;
        /// <summary>Хост: этот моб умер.</summary>
        public event Action<MobBase> Died;

        readonly NetworkVariable<MobAIState> aiState = new NetworkVariable<MobAIState>(MobAIState.Patrol);
        readonly NetworkVariable<Vector3> telegraphCenter = new NetworkVariable<Vector3>();
        readonly NetworkVariable<float> telegraphRadius = new NetworkVariable<float>();
        readonly NetworkVariable<float> currentWindup = new NetworkVariable<float>();
        readonly NetworkVariable<float> currentRecovery = new NetworkVariable<float>();
        readonly NetworkVariable<int> attackSerial = new NetworkVariable<int>();

        public MobAIState AIState => aiState.Value;
        public HeroStats Stats { get; private set; }
        public bool IsDead => aiState.Value == MobAIState.Dead;
        public Vector3 HomePosition => homePosition;

        protected NavMeshAgent agent;
        protected MobVisual visual;
        protected Vector3 homePosition;
        protected Quaternion homeRotation;
        protected HeroController target;
        protected float lastAttackEndTime = -100f;
        protected bool navReady;

        TelegraphDecal telegraph;
        Vector3 knockbackVelocity;
        float patrolWaitUntil;
        bool patrolMoving;
        float attackStartTime;
        bool attackHitDone;
        float attackDamage;
        Vector3 attackCenter;
        float attackRadius;

        protected virtual void Awake()
        {
            Stats = GetComponent<HeroStats>();
            agent = GetComponent<NavMeshAgent>();
            visual = GetComponent<MobVisual>();
            agent.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            aiState.OnValueChanged += OnRepAIState;
            attackSerial.OnValueChanged += OnRepAttackSerial;

            if (visual != null && Stats.FlashRoot == null)
                Stats.FlashRoot = visual.FlashRoot;

            ApplyMobData();
            visual?.SetDead(aiState.Value == MobAIState.Dead);

            if (IsServer)
            {
                homePosition = transform.position;
                homeRotation = transform.rotation;
                Stats.OnDeath += HandleDeath;
                StartCoroutine(ThinkLoop());
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            aiState.OnValueChanged -= OnRepAIState;
            attackSerial.OnValueChanged -= OnRepAttackSerial;
            if (IsServer)
                Stats.OnDeath -= HandleDeath;
            if (telegraph != null)
                Destroy(telegraph.gameObject);
        }

        /// <summary>Применить данные (числа и внешний вид). Вызывается у всех при спавне.</summary>
        protected virtual void ApplyMobData()
        {
            if (MobData == null)
                return;
            if (visual != null)
                visual.SetBodyMaterial(MobData.BodyMaterial);
            if (IsServer)
            {
                Stats.KnockbackSpeed = MobData.KnockbackSpeed;
                Stats.SetMaxHealth(MobData.MaxHealth);
            }
            agent.speed = MobData.PatrolSpeed;
        }

        protected void SetState(MobAIState state)
        {
            if (IsServer)
                aiState.Value = state;
        }

        // ================= Хост: цикл ИИ =================

        IEnumerator ThinkLoop()
        {
            while (IsSpawned && aiState.Value != MobAIState.Dead)
            {
                if (!navReady)
                    TryEnableNavigation();
                if (navReady)
                    Think();
                yield return new WaitForSeconds(MobData != null ? Mathf.Max(0.05f, MobData.ThinkInterval) : 0.2f);
            }
        }

        void TryEnableNavigation()
        {
            if (!NavMeshBaker.IsOnNavMesh(transform.position, 3f, out var snapped))
                return;
            agent.enabled = true;
            agent.Warp(snapped);
            navReady = true;
        }

        protected virtual void Think()
        {
            switch (aiState.Value)
            {
                case MobAIState.Patrol: TickPatrol(); break;
                case MobAIState.Chase: TickChase(); break;
                case MobAIState.Return: TickReturn(); break;
            }
        }

        protected virtual void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            if (aiState.Value == MobAIState.Attack)
                UpdateAttack();

            float kb = knockbackVelocity.magnitude;
            if (kb > 0.01f && agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(knockbackVelocity * Time.deltaTime);
                knockbackVelocity = knockbackVelocity.normalized * Mathf.Max(0f, kb - KnockbackDeceleration * Time.deltaTime);
            }
        }

        /// <summary>Ближайший живой герой в радиусе (упавших и мёртвых игнорирует).</summary>
        protected virtual HeroController FindTarget(float radius)
        {
            HeroController best = null;
            float bestDist = radius;
            foreach (var hero in HeroController.All)
            {
                if (!Combat.IsHeroAlive(hero))
                    continue;
                float d = Combat.FlatDistance(hero.transform.position, transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = hero;
                }
            }
            return best;
        }

        protected virtual void TickPatrol()
        {
            var found = FindTarget(MobData.DetectionRadius);
            if (found != null)
            {
                target = found;
                EnterChase();
                return;
            }

            if (!agent.enabled)
                return;
            agent.speed = MobData.PatrolSpeed;
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
            if (patrolMoving && arrived)
            {
                patrolMoving = false;
                patrolWaitUntil = Time.time + MobData.PatrolWaitTime;
            }
            if (!patrolMoving && Time.time >= patrolWaitUntil)
            {
                var random = homePosition + UnityEngine.Random.insideUnitSphere * MobData.PatrolRadius;
                if (NavMesh.SamplePosition(random, out var hit, MobData.PatrolRadius, NavMesh.AllAreas))
                {
                    var path = new NavMeshPath();
                    if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetPath(path);
                        patrolMoving = true;
                    }
                }
            }
        }

        protected virtual void EnterChase()
        {
            SetState(MobAIState.Chase);
            if (agent.enabled)
            {
                agent.isStopped = false;
                agent.speed = GetChaseSpeed();
            }
        }

        protected virtual float GetChaseSpeed() => MobData.ChaseSpeed;

        protected virtual void TickChase()
        {
            target = FindTarget(MobData.LoseTargetRadius);
            if (target == null || Combat.FlatDistance(transform.position, homePosition) > MobData.LeashRadius)
            {
                EnterReturn();
                return;
            }

            float dist = Combat.FlatDistance(transform.position, target.transform.position);
            if (dist <= MobData.AttackRange && Time.time >= lastAttackEndTime + MobData.AttackCooldown)
            {
                StartAttack();
                return;
            }

            if (agent.enabled)
            {
                agent.speed = GetChaseSpeed();
                agent.isStopped = false;
                agent.SetDestination(target.transform.position);
            }
        }

        /// <summary>Удар моба: круг AttackHitRadius на AttackHitDistance перед мобом.</summary>
        protected virtual void StartAttack()
        {
            FaceTarget();
            var center = transform.position + transform.forward * MobData.AttackHitDistance;
            BeginAttack(center, MobData.AttackHitRadius, MobData.AttackWindup, MobData.AttackRecovery, MobData.AttackDamage);
        }

        protected void FaceTarget()
        {
            if (target == null)
                return;
            var dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        /// Общий телеграфированный удар: место фиксируется в начале замаха, через windup — урон всем живым
        /// героям, чей центр внутри круга; потом recovery и снова Chase.
        /// </summary>
        protected void BeginAttack(Vector3 center, float radius, float windup, float recovery, float damage)
        {
            SetState(MobAIState.Attack);
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            center.y = transform.position.y;
            attackCenter = center;
            attackRadius = radius;
            attackDamage = damage;
            attackStartTime = Time.time;
            attackHitDone = false;

            telegraphCenter.Value = center;
            telegraphRadius.Value = radius;
            currentWindup.Value = windup;
            currentRecovery.Value = recovery;
            attackSerial.Value++;
        }

        void UpdateAttack()
        {
            float t = Time.time - attackStartTime;
            if (!attackHitDone && t >= currentWindup.Value)
            {
                attackHitDone = true;
                AttackHit();
            }
            if (t >= currentWindup.Value + currentRecovery.Value)
            {
                lastAttackEndTime = Time.time;
                if (aiState.Value == MobAIState.Attack)
                    EnterChase();
            }
        }

        protected virtual void AttackHit()
        {
            foreach (var hero in HeroController.All)
            {
                if (!Combat.IsHeroAlive(hero))
                    continue;
                if (Combat.FlatDistance(hero.transform.position, attackCenter) <= attackRadius)
                    hero.Stats.ApplyDamage(attackDamage, gameObject);
            }
        }

        protected virtual void EnterReturn()
        {
            SetState(MobAIState.Return);
            target = null;
            if (agent.enabled)
            {
                agent.isStopped = false;
                agent.speed = MobData.PatrolSpeed;
                agent.SetDestination(homePosition);
            }
        }

        protected virtual void TickReturn()
        {
            bool arrived = !agent.enabled || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f);
            if (arrived || Combat.FlatDistance(transform.position, homePosition) < 1f)
                EnterPatrol();
        }

        protected virtual void EnterPatrol()
        {
            SetState(MobAIState.Patrol);
            patrolMoving = false;
            patrolWaitUntil = Time.time + (MobData != null ? MobData.PatrolWaitTime : 1f);
        }

        // ================= Смерть =================

        protected virtual void HandleDeath()
        {
            SetState(MobAIState.Dead);
            StopAllCoroutines();
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = false;

            SpawnDrop();
            OnMobDied?.Invoke(this);
            Died?.Invoke(this);
            StartCoroutine(DespawnCorpse());
        }

        /// <summary>Дроп при смерти: событие OnDropRequested и лут по MobData.LootTable (P0-12).</summary>
        protected virtual void SpawnDrop()
        {
            var location = transform.position;
            OnDropRequested?.Invoke(this, location);
            if (MobData != null && MobData.LootTable != null)
                GameMode.Instance?.DropLoot(MobData.LootTable, location);
        }

        IEnumerator DespawnCorpse()
        {
            yield return new WaitForSeconds(MobData != null ? MobData.CorpseLifetime : 3f);
            if (IsSpawned)
                NetworkObject.Despawn(true);
        }

        public void ApplyKnockback(Vector3 velocity)
        {
            if (IsServer && aiState.Value != MobAIState.Dead)
                knockbackVelocity = velocity;
        }

        // ================= Визуал (у всех) =================

        protected virtual void OnRepAIState(MobAIState previous, MobAIState current)
        {
            if (current == MobAIState.Dead)
            {
                visual?.SetDead(true);
                visual?.StopAttack();
                telegraph?.Hide();
                foreach (var c in GetComponentsInChildren<Collider>())
                    c.enabled = false;
            }
            else if (current != MobAIState.Attack)
            {
                visual?.StopAttack();
                telegraph?.Hide();
            }
        }

        void OnRepAttackSerial(int previous, int current)
        {
            if (telegraph == null)
                telegraph = TelegraphDecal.Create($"{name}_Telegraph");
            telegraph.Show(telegraphCenter.Value, telegraphRadius.Value, currentWindup.Value);
            visual?.PlayAttack(currentWindup.Value, currentRecovery.Value);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (MobData == null)
                return;
            var home = Application.isPlaying ? homePosition : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, MobData.DetectionRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(home, MobData.PatrolRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(home, MobData.LeashRadius);
        }
    }
}
