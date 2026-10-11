using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_Hero: движение, удар, кувырок, спринт, состояние «упал», поднятие союзника, взаимодействие.
    /// Движение — у владельца (NetworkTransform в режиме Owner, аналог предсказания CharacterMovement),
    /// урон, стамина и состояния — на хосте (P0-01, P0-02, P0-04).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(HeroStats))]
    public class HeroController : NetworkBehaviour, IKnockbackReceiver
    {
        public static readonly List<HeroController> All = new List<HeroController>();
        public static HeroController Local { get; private set; }

        [Header("Movement")]
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        [Tooltip("Расход стамины в секунду при спринте (0 — спринт бесплатный)")]
        public float SprintStaminaPerSecond = 10f;
        [Tooltip("После полного истощения спринт снова доступен с этого запаса стамины")]
        public float SprintResumeStamina = 20f;
        public float TurnSpeed = 720f;
        public float Gravity = -20f;
        [Tooltip("Множитель скорости во время удара")]
        public float AttackMoveSpeedMultiplier = 0.5f;
        [Tooltip("Торможение после отбрасывания, м/с²")]
        public float KnockbackDeceleration = 20f;

        [Header("Combat")]
        [Tooltip("Базовый урон (до P0-16); бонусы предметов — через Inventory")]
        public float AttackDamage = 25f;
        [Tooltip("Центр сферы хитбокса перед героем, м")]
        public float AttackRange = 0.9f;
        public float AttackRadius = 0.8f;
        public float AttackHeight = 1f;
        [Tooltip("Длительность замаха, с")]
        public float AttackDuration = 0.4f;
        [Tooltip("Окно активного хитбокса, доля замаха")]
        public float AttackHitWindowStart = 0.2f;
        public float AttackHitWindowEnd = 0.55f;
        public float AttackStaminaCost = 10f;
        [Tooltip("Урон по другим героям")]
        public bool FriendlyFire;
        [Tooltip("Рисовать сферу хитбокса (Gizmos/Debug)")]
        public bool DebugDrawHitbox;

        [Header("Roll")]
        public float RollStaminaCost = 25f;
        public float RollDuration = 0.45f;
        public float RollDistance = 4f;
        [Tooltip("Неуязвимость с начала кувырка, с")]
        public float IFrameDuration = 0.3f;

        [Header("State")]
        public float DownedVisualRoll = 90f;
        public float DownedVisualOffsetY = -0.6f;

        [Header("Revive")]
        public float ReviveDuration = 3f;
        public float ReviveHealthFraction = 0.3f;
        public float ReviveRadius = 2f;
        public float ReviveTickInterval = 0.05f;

        readonly NetworkVariable<int> attackCount = new NetworkVariable<int>();
        readonly NetworkVariable<int> rollCount = new NetworkVariable<int>();
        readonly NetworkVariable<bool> isSprinting = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> isDowned = new NetworkVariable<bool>();
        readonly NetworkVariable<float> reviveProgress = new NetworkVariable<float>();
        readonly NetworkVariable<ulong> currentReviverId = new NetworkVariable<ulong>();
        readonly NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>();

        public HeroStats Stats { get; private set; }
        public Inventory Inventory { get; private set; }
        public Skills Skills { get; private set; }
        public HeroVisual Visual { get; private set; }

        public bool IsDowned => isDowned.Value;
        public bool IsSprinting => isSprinting.Value;
        public float ReviveProgress => reviveProgress.Value;
        public bool IsBeingRevived => currentReviverId.Value != 0;
        /// <summary>NetworkObjectId героя, который поднимает этого (0 — никто).</summary>
        public ulong ReviverId => currentReviverId.Value;
        public string PlayerName => playerName.Value.ToString();
        public bool IsAttacking => attacking;
        public bool IsRolling => rolling;

        CharacterController controller;
        NetworkTransform netTransform;

        // Локальное состояние (у всех)
        bool attacking;
        float attackStartTime;
        bool rolling;
        float rollStartTime;
        Vector3 rollDirection;

        // Владелец
        float verticalVelocity;
        Vector3 knockbackVelocity;
        bool sentSprintState;
        bool interactSent;

        // Хост
        readonly HashSet<HeroStats> hitActorsThisSwing = new HashSet<HeroStats>();
        readonly List<HeroStats> overlapResults = new List<HeroStats>();
        bool interactHeld;
        bool sprintRequested;
        bool serverSprintExhausted;
        bool ownerSprintExhausted;
        HeroController reviveTarget;
        float reviveTickTimer;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            netTransform = GetComponent<NetworkTransform>();
            Stats = GetComponent<HeroStats>();
            Inventory = GetComponent<Inventory>();
            Skills = GetComponent<Skills>();
            Visual = GetComponent<HeroVisual>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            attackCount.OnValueChanged += OnRepAttackCount;
            rollCount.OnValueChanged += OnRepRollCount;
            isDowned.OnValueChanged += OnRepIsDowned;

            if (IsServer)
            {
                Stats.OnDeath += HandleDeath;
                Stats.OnDamaged += HandleDamaged;
                if (playerName.Value.IsEmpty)
                    playerName.Value = NetStrings.To64($"Игрок {OwnerClientId + 1}");
            }

            if (IsOwner)
            {
                Local = this;
                SetPlayerNameServerRpc(NetStrings.To64(NetSession.LocalPlayerName));
                HeroCamera.Attach(this);
            }

            Visual?.SetPlayerIndex((int)(OwnerClientId % 4));
            Debug.Log($"[Hero] Spawned hero of client {OwnerClientId} (local: {IsOwner}) at {transform.position}");
            ApplyDownedVisual(isDowned.Value);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            attackCount.OnValueChanged -= OnRepAttackCount;
            rollCount.OnValueChanged -= OnRepRollCount;
            isDowned.OnValueChanged -= OnRepIsDowned;
            if (IsServer)
            {
                Stats.OnDeath -= HandleDeath;
                Stats.OnDamaged -= HandleDamaged;
                CancelRevive();
            }
            if (Local == this)
                Local = null;
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            if (IsOwner)
                OwnerUpdate();

            UpdateLocalTimers();

            if (IsServer)
                ServerUpdate();
        }

        // ================= Владелец: ввод и движение =================

        void OwnerUpdate()
        {
            bool blocked = GameInput.GameplayBlocked || isDowned.Value;
            Vector2 input = blocked ? Vector2.zero : GameInput.Move.ReadValue<Vector2>();
            Vector3 moveDir = HeroCamera.InputToWorld(input);

            if (!blocked)
            {
                if (GameInput.Attack.WasPressedThisFrame())
                    TryAttack();
                if (GameInput.Roll.WasPressedThisFrame())
                    TryRoll(moveDir);
                if (GameInput.Interact.WasPressedThisFrame())
                {
                    interactSent = true;
                    InteractPressedServerRpc();
                }
            }
            if (interactSent && (blocked || !GameInput.Interact.IsPressed()))
            {
                interactSent = false;
                InteractReleasedServerRpc();
            }

            bool wantSprint = !blocked && GameInput.Sprint.IsPressed() && moveDir.sqrMagnitude > 0.01f && !rolling;
            if (wantSprint != sentSprintState)
            {
                sentSprintState = wantSprint;
                SetSprintingServerRpc(wantSprint);
            }

            Vector3 velocity;
            if (rolling)
            {
                velocity = rollDirection * (RollDistance / Mathf.Max(0.05f, RollDuration));
            }
            else
            {
                bool canSprint = HasSprintStamina(ref ownerSprintExhausted) && wantSprint;
                float speed = canSprint ? SprintSpeed : WalkSpeed;
                if (attacking)
                    speed *= AttackMoveSpeedMultiplier;
                velocity = moveDir * speed;
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += Gravity * Time.deltaTime;

            float kb = knockbackVelocity.magnitude;
            if (kb > 0f)
            {
                velocity += knockbackVelocity;
                knockbackVelocity = knockbackVelocity.normalized * Mathf.Max(0f, kb - KnockbackDeceleration * Time.deltaTime);
            }

            velocity.y = verticalVelocity;
            if (controller.enabled)
                controller.Move(velocity * Time.deltaTime);

            Vector3 face = rolling ? rollDirection : (attacking ? Vector3.zero : moveDir);
            if (face.sqrMagnitude > 0.0001f && !isDowned.Value)
            {
                var target = Quaternion.LookRotation(new Vector3(face.x, 0f, face.z));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * Time.deltaTime);
            }
        }

        /// <summary>Хватает ли стамины на спринт (с гистерезисом после полного истощения).</summary>
        bool HasSprintStamina(ref bool exhausted)
        {
            if (SprintStaminaPerSecond <= 0f)
                return true;
            if (Stats.Stamina <= 0.5f)
                exhausted = true;
            else if (Stats.Stamina >= SprintResumeStamina)
                exhausted = false;
            return !exhausted;
        }

        void TryAttack()
        {
            if (attacking || rolling || isDowned.Value || !Stats.HasStamina(AttackStaminaCost))
                return;
            AttackServerRpc();
        }

        void TryRoll(Vector3 moveDir)
        {
            if (rolling || isDowned.Value || !Stats.HasStamina(RollStaminaCost))
                return;
            var dir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : transform.forward;
            dir.y = 0f;
            // Кувырок начинается у владельца сразу (как предсказание движения), хост списывает стамину и даёт i-frames.
            StartRollLocal(dir.normalized);
            RollServerRpc(dir.normalized);
        }

        void UpdateLocalTimers()
        {
            if (attacking && Time.time - attackStartTime >= AttackDuration)
                attacking = false;
            if (rolling && Time.time - rollStartTime >= RollDuration)
                rolling = false;
        }

        // ================= RPC (клиент → хост) =================

        [Rpc(SendTo.Server)]
        void SetPlayerNameServerRpc(FixedString64Bytes name, RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            if (!name.IsEmpty)
                playerName.Value = name;
        }

        [Rpc(SendTo.Server)]
        void AttackServerRpc(RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            if (attacking || isDowned.Value || Stats.IsDead)
                return;
            if (!Stats.TrySpendStamina(AttackStaminaCost))
                return;
            attackCount.Value++;
        }

        [Rpc(SendTo.Server)]
        void RollServerRpc(Vector3 direction, RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            if (isDowned.Value || Stats.IsDead)
                return;
            if (!Stats.TrySpendStamina(RollStaminaCost))
                return;
            StartRollIFrames();
            rollDirection = direction;
            rollCount.Value++;
        }

        [Rpc(SendTo.Server)]
        void SetSprintingServerRpc(bool value, RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            sprintRequested = value;
        }

        [Rpc(SendTo.Server)]
        void InteractPressedServerRpc(RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            interactHeld = true;
            OnServerInteractPressed();
        }

        [Rpc(SendTo.Server)]
        void InteractReleasedServerRpc(RpcParams rpcParams = default)
        {
            if (!FromOwner(rpcParams))
                return;
            interactHeld = false;
            CancelRevive();
        }

        /// <summary>Запрос пришёл от владельца героя (чужим героем управлять нельзя).</summary>
        bool FromOwner(RpcParams rpcParams) => rpcParams.Receive.SenderClientId == OwnerClientId;

        // ================= Хост → владелец =================

        [Rpc(SendTo.Owner)]
        void KnockbackRpc(Vector3 velocity)
        {
            knockbackVelocity = velocity;
        }

        [Rpc(SendTo.Owner)]
        void TeleportRpc(Vector3 position, Quaternion rotation)
        {
            ApplyTeleport(position, rotation);
        }

        void ApplyTeleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            if (netTransform != null && netTransform.CanCommitToTransform)
                netTransform.Teleport(position, rotation, transform.localScale);
            else
                transform.SetPositionAndRotation(position, rotation);
            controller.enabled = wasEnabled;
            knockbackVelocity = Vector3.zero;
            verticalVelocity = 0f;
            HeroCamera.SnapIfFollowing(this);
        }

        /// <summary>Телепорт героя (хост): двигает его владелец, т.к. движение у владельца.</summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
                return;
            if (IsOwner)
                ApplyTeleport(position, rotation);
            else
                TeleportRpc(position, rotation);
        }

        public void ApplyKnockback(Vector3 velocity)
        {
            if (!IsServer || isDowned.Value)
                return;
            if (IsOwner)
                knockbackVelocity = velocity;
            else
                KnockbackRpc(velocity);
        }

        // ================= Репликация визуала =================

        void OnRepAttackCount(int previous, int current)
        {
            StartAttackLocal();
        }

        void StartAttackLocal()
        {
            attacking = true;
            attackStartTime = Time.time;
            hitActorsThisSwing.Clear();
            Visual?.PlaySwing(AttackDuration);
        }

        void OnRepRollCount(int previous, int current)
        {
            if (IsOwner)
                return; // владелец уже кувыркается
            StartRollLocal(transform.forward);
        }

        void StartRollLocal(Vector3 direction)
        {
            rolling = true;
            rollStartTime = Time.time;
            rollDirection = direction;
            attacking = false;
            Visual?.PlayRoll(RollDuration);
        }

        void OnRepIsDowned(bool previous, bool current)
        {
            if (current)
            {
                attacking = false;
                rolling = false;
                knockbackVelocity = Vector3.zero;
            }
            ApplyDownedVisual(current);
        }

        void ApplyDownedVisual(bool downed)
        {
            Visual?.SetDowned(downed, DownedVisualRoll, DownedVisualOffsetY);
        }

        // ================= Хост: удар, спринт, поднятие =================

        void ServerUpdate()
        {
            if (attacking)
                UpdateAttack();

            // Спринт: владелец сообщает, что держит Shift и двигается; хост списывает стамину, пока она есть
            // (у владельца скорость спринта — тоже только при стамине > 0, поэтому после восстановления
            // спринт снова платный без повторного нажатия).
            bool sprinting = sprintRequested && !isDowned.Value && HasSprintStamina(ref serverSprintExhausted);
            if (isSprinting.Value != sprinting)
                isSprinting.Value = sprinting;
            if (sprinting && SprintStaminaPerSecond > 0f)
                Stats.DrainStamina(SprintStaminaPerSecond * Time.deltaTime);

            if (interactHeld || reviveTarget != null)
            {
                reviveTickTimer += Time.deltaTime;
                while (reviveTickTimer >= ReviveTickInterval)
                {
                    reviveTickTimer -= ReviveTickInterval;
                    TickRevive();
                }
            }
        }

        void UpdateAttack()
        {
            float t = (Time.time - attackStartTime) / Mathf.Max(0.01f, AttackDuration);
            if (t >= AttackHitWindowStart && t <= AttackHitWindowEnd)
                DoAttackHit();
        }

        public Vector3 HitboxCenter => transform.position + transform.forward * AttackRange + Vector3.up * AttackHeight;

        void DoAttackHit()
        {
            var center = HitboxCenter;
            if (DebugDrawHitbox)
                DebugDraw.Sphere(center, AttackRadius, Color.red, 0.1f);

            Combat.OverlapSphere(center, AttackRadius, overlapResults);
            float damage = GetAttackDamage();
            foreach (var target in overlapResults)
            {
                if (!Combat.CanHitTarget(gameObject, target, hitActorsThisSwing, FriendlyFire))
                    continue;
                hitActorsThisSwing.Add(target);
                target.ApplyDamage(damage, gameObject);
            }
        }

        /// <summary>Урон удара: база + модификаторы надетых предметов (P0-11).</summary>
        public float GetAttackDamage()
        {
            return Inventory != null ? Inventory.GetModifiedStat("AttackDamage", AttackDamage) : AttackDamage;
        }

        void StartRollIFrames()
        {
            Stats.GrantInvulnerability(IFrameDuration);
        }

        void HandleDamaged(float amount, GameObject causer)
        {
            // Получил урон — поднятие, которое делал этот герой, прерывается.
            CancelRevive();
        }

        void HandleDeath()
        {
            isDowned.Value = true;
            isSprinting.Value = false;
            sprintRequested = false;
            CancelRevive();
            GameMode.Instance?.NotifyHeroDowned(this);
        }

        /// <summary>Поднять героя (хост): HP = доля от максимума.</summary>
        public void GetRevived(float healthFraction)
        {
            if (!IsServer)
                return;
            Stats.Revive(healthFraction);
            isDowned.Value = false;
            reviveProgress.Value = 0f;
            currentReviverId.Value = 0;
        }

        void OnServerInteractPressed()
        {
            if (isDowned.Value)
                return;
            // Рядом упавший союзник — работает поднятие (приоритет), иначе взаимодействие с объектом.
            if (FindDownedAllyNearby() != null)
                return;
            TryInteract();
        }

        void TickRevive()
        {
            if (!interactHeld || isDowned.Value || Stats.IsDead)
            {
                CancelRevive();
                return;
            }

            if (reviveTarget != null && !IsValidReviveTarget(reviveTarget))
                CancelRevive();

            if (reviveTarget == null)
            {
                reviveTarget = FindDownedAllyNearby();
                if (reviveTarget == null)
                    return;
                reviveTarget.reviveProgress.Value = 0f;
            }

            reviveTarget.currentReviverId.Value = NetworkObjectId;
            reviveTarget.reviveProgress.Value += ReviveTickInterval / Mathf.Max(0.05f, ReviveDuration);
            if (reviveTarget.reviveProgress.Value >= 1f)
            {
                var target = reviveTarget;
                reviveTarget = null;
                target.GetRevived(ReviveHealthFraction);
            }
        }

        bool IsValidReviveTarget(HeroController target)
        {
            if (target == null || !target.IsSpawned || target == this || !target.isDowned.Value)
                return false;
            if (target.currentReviverId.Value != 0 && target.currentReviverId.Value != NetworkObjectId)
                return false;
            return Combat.FlatDistance(transform.position, target.transform.position) <= ReviveRadius;
        }

        /// <summary>Ближайший упавший союзник в ReviveRadius, которого никто другой не поднимает.</summary>
        public HeroController FindDownedAllyNearby()
        {
            HeroController best = null;
            float bestDist = float.MaxValue;
            foreach (var hero in All)
            {
                if (!IsValidReviveTarget(hero))
                    continue;
                float d = Combat.FlatDistance(transform.position, hero.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = hero;
                }
            }
            return best;
        }

        void CancelRevive()
        {
            if (reviveTarget != null)
            {
                if (reviveTarget.IsSpawned && reviveTarget.currentReviverId.Value == NetworkObjectId)
                {
                    reviveTarget.reviveProgress.Value = 0f;
                    reviveTarget.currentReviverId.Value = 0;
                }
                reviveTarget = null;
            }
            reviveTickTimer = 0f;
        }

        /// <summary>Сбросить «упал» и поднять с долей HP (возрождение группы).</summary>
        public void RespawnAt(Vector3 position, Quaternion rotation, float healthFraction)
        {
            if (!IsServer)
                return;
            CancelRevive();
            GetRevived(healthFraction);
            TeleportTo(position, rotation);
        }

        // ================= Взаимодействие (P0-12) =================

        /// <summary>Ближайший объект в его радиусе, с которым можно взаимодействовать (сетевой или объект карты).</summary>
        public IInteractTarget FindInteractTarget()
        {
            IInteractTarget best = null;
            float bestDist = float.MaxValue;
            foreach (var it in Interactable.All)
                Consider(it, ref best, ref bestDist);
            foreach (var it in InteractTargets.Local)
                Consider(it, ref best, ref bestDist);
            return best;
        }

        void Consider(IInteractTarget it, ref IInteractTarget best, ref float bestDist)
        {
            if (it == null || !it.IsInteractTargetActive)
                return;
            float d = Combat.FlatDistance(transform.position, it.InteractPosition);
            if (d > it.InteractRange || d >= bestDist || !it.CanInteract(this))
                return;
            bestDist = d;
            best = it;
        }

        /// <summary>Хост: взаимодействие с ближайшим объектом.</summary>
        public bool TryInteract()
        {
            if (!IsServer || isDowned.Value)
                return false;
            var target = FindInteractTarget();
            return target != null && target.Interact(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(HitboxCenter, AttackRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, ReviveRadius);
        }
    }
}
