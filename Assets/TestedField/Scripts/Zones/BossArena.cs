using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    public enum ArenaState : byte
    {
        /// <summary>Босс ждёт, стены опущены.</summary>
        Idle,
        /// <summary>Идёт бой, стены подняты — вход закрыт.</summary>
        Fight,
        /// <summary>Босс побеждён, стены опущены навсегда.</summary>
        Defeated,
    }

    /// <summary>
    /// BP_BossArena: контент слота BossArena (P0-09). Хост спавнит босса зоны в центре, следит за входом
    /// героев (начало боя), поражением группы внутри (сброс) и смертью босса (победа).
    /// Состояние арены хранит RunMap (реплицируется), стены у всех строятся по нему.
    /// </summary>
    public class BossArena : SlotContent
    {
        [Tooltip("Радиус арены, м (слот BossArena — 8 м; финальная арена — 10.5 м)")]
        public float ArenaRadius = 7.5f;
        public float WallHeight = 2.6f;
        public float OpenWallHeight = 0.15f;
        public float WallThickness = 0.5f;
        [Tooltip("Бой начинается, когда живой герой ближе ArenaRadius − EnterMargin к центру")]
        public float EnterMargin = 1.5f;
        public float CheckInterval = 0.25f;
        public int WallSegments = 16;
        public float BossSpawnDelay = 0.1f;

        public int ArenaIndex { get; set; }
        public BossBase Boss { get; private set; }
        public ArenaState State => RunMap.Instance != null ? RunMap.Instance.GetArenaState(ArenaIndex) : ArenaState.Idle;

        readonly List<Transform> walls = new List<Transform>();
        readonly List<Collider> wallColliders = new List<Collider>();

        public override void Initialize(string slotType, ZoneInstance zone, float slotRadius, int contentIndex)
        {
            base.Initialize(slotType, zone, slotRadius, contentIndex);
            BuildWalls();
            ApplyState(ArenaState.Idle);
        }

        void BuildWalls()
        {
            var mat = GameAssets.I.Mat("StoneDark");
            var root = Shapes.Group(transform, "Walls");
            float arc = 2f * Mathf.PI * ArenaRadius / WallSegments;
            for (int i = 0; i < WallSegments; i++)
            {
                float a = (i + 0.5f) / WallSegments * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Sin(a) * ArenaRadius, 0f, Mathf.Cos(a) * ArenaRadius);
                var rot = Quaternion.LookRotation(-pos.normalized, Vector3.up);
                var wall = Shapes.Box(root, $"ArenaWall_{i}", pos, rot, new Vector3(arc * 1.08f, 1f, WallThickness), mat);
                walls.Add(wall.transform);
                wallColliders.Add(wall.GetComponent<Collider>());
            }
        }

        /// <summary>У всех: стены по состоянию.</summary>
        public void ApplyState(ArenaState state)
        {
            bool closed = state == ArenaState.Fight;
            float h = closed ? WallHeight : OpenWallHeight;
            foreach (var w in walls)
            {
                var s = w.localScale;
                w.localScale = new Vector3(s.x, h, s.z);
                var p = w.localPosition;
                w.localPosition = new Vector3(p.x, h * 0.5f, p.z);
            }
            foreach (var c in wallColliders)
                c.enabled = closed;
        }

        public bool IsInside(Vector3 position) => Combat.FlatDistance(position, transform.position) <= ArenaRadius;

        // ---------------- Хост ----------------

        public override void OnMapBuiltServer()
        {
            StartCoroutine(SpawnBossAfterDelay());
        }

        public override void OnMapClearedServer()
        {
            StopAllCoroutines();
            if (Boss != null)
            {
                Boss.Died -= OnBossDied;
                if (Boss.IsSpawned)
                    Boss.NetworkObject.Despawn(true);
                Boss = null;
            }
        }

        IEnumerator SpawnBossAfterDelay()
        {
            yield return new WaitForSeconds(BossSpawnDelay);

            var prefab = Zone != null && Zone.Data != null && Zone.Data.BossPrefabs.Count > 0 ? Zone.Data.BossPrefabs[0] : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[Arena] Zone {(Zone != null ? Zone.ZoneId : "?")} has no BossPrefabs");
                yield break;
            }

            var go = Instantiate(prefab.gameObject, transform.position, transform.rotation);
            Boss = go.GetComponent<BossBase>();
            Boss.Arena = this;
            Boss.Died += OnBossDied;
            go.GetComponent<NetworkObject>().Spawn(true);

            StartCoroutine(CheckLoop());
        }

        IEnumerator CheckLoop()
        {
            var wait = new WaitForSeconds(CheckInterval);
            while (Boss != null && State != ArenaState.Defeated)
            {
                CheckArena();
                yield return wait;
            }
        }

        void CheckArena()
        {
            var runMap = RunMap.Instance;
            if (runMap == null || Boss == null || Boss.IsDead)
                return;

            if (State == ArenaState.Idle)
            {
                float enter = ArenaRadius - EnterMargin;
                foreach (var hero in HeroController.All)
                {
                    if (Combat.IsHeroAlive(hero) && Combat.FlatDistance(hero.transform.position, transform.position) <= enter)
                    {
                        int players = Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count);
                        runMap.SetArenaState(ArenaIndex, ArenaState.Fight);
                        Boss.StartFight(players);
                        DevConsole.Print($"[Arena] Fight started: {Boss.DisplayName}, players: {players}", 4f);
                        return;
                    }
                }
            }
            else if (State == ArenaState.Fight)
            {
                foreach (var hero in HeroController.All)
                    if (Combat.IsHeroAlive(hero) && IsInside(hero.transform.position))
                        return;

                // Внутри не осталось живых героев — бой сбрасывается.
                runMap.SetArenaState(ArenaIndex, ArenaState.Idle);
                Boss.ResetFight();
                DevConsole.Print($"[Arena] Fight reset: {Boss.DisplayName}", 4f);
            }
        }

        void OnBossDied(MobBase mob)
        {
            var runMap = RunMap.Instance;
            if (runMap == null)
                return;
            runMap.SetArenaState(ArenaIndex, ArenaState.Defeated);
            runMap.NotifyZoneBossDefeated(Zone);
        }

        /// <summary>Отладка: засчитать босса без боя.</summary>
        public void DebugDefeat()
        {
            if (Boss != null && !Boss.IsDead)
            {
                Boss.Died -= OnBossDied;
                if (Boss.IsSpawned)
                    Boss.NetworkObject.Despawn(true);
                Boss = null;
            }
            OnBossDied(null);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, ArenaRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, ArenaRadius - EnterMargin);
        }
    }
}
