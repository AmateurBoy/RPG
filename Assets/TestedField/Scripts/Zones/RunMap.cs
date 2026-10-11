using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>Что собрано: seed, номер успешной попытки и версия сборки (клиенты пересобирают карту при её смене).</summary>
    public struct MapBuildInfo : INetworkSerializeByMemcpy, IEquatable<MapBuildInfo>
    {
        public int Seed;
        public int Attempt;
        public int Version;

        public bool Equals(MapBuildInfo other) => Seed == other.Seed && Attempt == other.Attempt && Version == other.Version;
        public override int GetHashCode() => Seed ^ (Attempt << 16) ^ (Version << 24);
    }

    /// <summary>
    /// BP_RunMap: строит мир забега из заготовок зон (P0-05…P0-10).
    /// Хост выбирает seed и ищет успешную попытку генерации; в сеть уходят только seed, номер попытки и
    /// состояния (ворота, квесты, боссы, арены). Каждый участник собирает одинаковую карту локально
    /// (MapPlanner детерминирован), сетевые объекты — только боссы.
    /// </summary>
    public class RunMap : NetworkBehaviour
    {
        public static RunMap Instance { get; private set; }

        public RunMapData RunMapData;
        [Header("Generation")]
        public bool UseFixedSeed;
        public int FixedSeed = 111;
        public int MaxGenerationAttempts = 10;
        [Header("Debug")]
        [Tooltip("Сферы слотов и рамки проходов при сборке (Scene view / Gizmos)")]
        public bool DebugDrawSlots;
        public float DebugDrawDuration = 30f;

        readonly NetworkVariable<MapBuildInfo> buildInfo = new NetworkVariable<MapBuildInfo>();
        readonly NetworkList<bool> gateOpen = new NetworkList<bool>();
        readonly NetworkList<bool> zoneQuestCompleted = new NetworkList<bool>();
        readonly NetworkList<bool> zoneBossDefeated = new NetworkList<bool>();
        readonly NetworkList<byte> arenaStates = new NetworkList<byte>();

        /// <summary>Хост: босс зоны побеждён.</summary>
        public event Action<ZoneInstance> OnZoneBossDefeated;
        /// <summary>У всех: карта собрана (или пересобрана).</summary>
        public event Action OnMapBuilt;

        public int Seed => buildInfo.Value.Seed;
        public int GenerationAttempt => buildInfo.Value.Attempt;
        public bool IsBuilt { get; private set; }
        public IReadOnlyList<ZoneInstance> Zones => zones;
        public IReadOnlyList<PassageGate> Gates => gates;
        public IReadOnlyList<SlotContent> Contents => contents;
        public IReadOnlyList<BossArena> Arenas => arenas;
        public ZoneInstance StartZoneLayout => zones.Count > 0 ? zones[0] : null;

        readonly List<ZoneInstance> zones = new List<ZoneInstance>();
        readonly List<PassageGate> gates = new List<PassageGate>();
        readonly List<SlotContent> contents = new List<SlotContent>();
        readonly List<BossArena> arenas = new List<BossArena>();
        Transform zonesRoot, gatesRoot, contentRoot;
        NavMeshSurface navSurface;
        int builtVersion = -1;

        void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            buildInfo.OnValueChanged += HandleBuildInfoChanged;
            gateOpen.OnListChanged += HandleGatesChanged;
            arenaStates.OnListChanged += HandleArenasChanged;

            if (IsServer)
                BuildMap(UseFixedSeed ? FixedSeed : RandomSeed());
            else if (buildInfo.Value.Version > 0)
                BuildLocal(buildInfo.Value);
        }

        public override void OnNetworkDespawn()
        {
            buildInfo.OnValueChanged -= HandleBuildInfoChanged;
            gateOpen.OnListChanged -= HandleGatesChanged;
            arenaStates.OnListChanged -= HandleArenasChanged;
            if (IsServer)
                foreach (var c in contents)
                    if (c != null)
                        c.OnMapClearedServer();
            if (Instance == this)
                Instance = null;
        }

        static int RandomSeed() => UnityEngine.Random.Range(1, int.MaxValue);

        // ================= Сборка =================

        /// <summary>Хост: до MaxGenerationAttempts попыток с разными потоками; успех → состояние в сеть.</summary>
        public bool BuildMap(int seed)
        {
            if (!IsServer)
                return false;

            MapPlan plan = null;
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                plan = MapPlanner.TryBuild(RunMapData, seed, attempt, transform.position);
                if (plan.Success)
                    break;
                Debug.LogWarning($"[RunMap] Seed {seed}, attempt {attempt} failed: {plan.Error}");
            }

            if (plan == null || !plan.Success)
            {
                Debug.LogError($"[RunMap] Map generation failed after {MaxGenerationAttempts} attempts (seed {seed})");
                DevConsole.Print($"Map generation failed (seed {seed}): {plan?.Error}", 10f, Color.red);
                return false;
            }

            ClearMap();
            ApplyPlan(plan);

            gateOpen.Clear();
            foreach (var g in gates)
                gateOpen.Add(g.UnlockCondition == GateUnlockCondition.Open);
            zoneQuestCompleted.Clear();
            zoneBossDefeated.Clear();
            foreach (var z in zones)
            {
                zoneQuestCompleted.Add(false);
                zoneBossDefeated.Add(false);
            }
            arenaStates.Clear();
            foreach (var a in arenas)
                arenaStates.Add((byte)ArenaState.Idle);

            var info = new MapBuildInfo { Seed = seed, Attempt = plan.Attempt, Version = buildInfo.Value.Version + 1 };
            builtVersion = info.Version;
            buildInfo.Value = info;

            ApplyGateStates();
            ApplyArenaStates();

            NavMeshBaker.Build(navSurface);

            foreach (var c in contents)
                c.OnMapBuiltServer();

            PlacePlayersAtStart();
            DevConsole.Print($"Map built. Seed {seed}, attempt {plan.Attempt + 1}", 6f, Color.green);
            return true;
        }

        void HandleBuildInfoChanged(MapBuildInfo previous, MapBuildInfo current)
        {
            if (IsServer || current.Version == builtVersion || current.Version <= 0)
                return;
            BuildLocal(current);
        }

        /// <summary>Клиент: тот же план по seed и номеру попытки.</summary>
        void BuildLocal(MapBuildInfo info)
        {
            ClearMap();
            var plan = MapPlanner.TryBuild(RunMapData, info.Seed, info.Attempt, transform.position);
            if (!plan.Success)
            {
                Debug.LogError($"[RunMap] Client failed to rebuild map (seed {info.Seed}, attempt {info.Attempt}): {plan.Error}");
                return;
            }
            ApplyPlan(plan);
            builtVersion = info.Version;
            ApplyGateStates();
            ApplyArenaStates();
            Debug.Log($"[RunMap] Client built map: seed {info.Seed}, attempt {info.Attempt + 1}, zones {zones.Count}, contents {contents.Count}");
        }

        void ApplyPlan(MapPlan plan)
        {
            EnsureRoots();

            for (int i = 0; i < plan.Zones.Count; i++)
                zones.Add(ZoneBuilder.Build(plan.Zones[i], i, zonesRoot));

            var gatePrefab = RunMapData.PassageGatePrefab;
            if (gatePrefab == null && plan.Gates.Count > 0)
                Debug.LogWarning("[RunMap] PassageGatePrefab is not set — passages stay open");
            for (int i = 0; i < plan.Gates.Count && gatePrefab != null; i++)
            {
                var planned = plan.Gates[i];
                var gate = Instantiate(gatePrefab, gatesRoot);
                gate.name = $"Gate_{zones[planned.OwnerZone].ZoneId}_{planned.PassageId}";
                gate.Setup(gates.Count, planned, zones[planned.OwnerZone]);
                gates.Add(gate);
            }

            foreach (var pc in plan.Contents)
            {
                var zone = zones[pc.ZoneIndex];
                SlotContent content;
                if (pc.Prefab != null)
                {
                    content = Instantiate(pc.Prefab, pc.Position, Quaternion.identity, contentRoot);
                }
                else
                {
                    var go = new GameObject("SlotContent");
                    go.transform.SetParent(contentRoot, false);
                    go.transform.position = pc.Position;
                    content = go.AddComponent<SlotContent>();
                }
                content.name = $"{pc.SlotType}_{zone.ZoneId}_{pc.SlotIndex}";
                content.Initialize(pc.SlotType, zone, pc.Radius, contents.Count);
                contents.Add(content);
                if (content is BossArena arena)
                {
                    arena.ArenaIndex = arenas.Count;
                    arenas.Add(arena);
                }
            }

            if (DebugDrawSlots)
                DrawDebug(plan);

            IsBuilt = true;
            OnMapBuilt?.Invoke();
        }

        void EnsureRoots()
        {
            if (zonesRoot == null)
            {
                zonesRoot = Shapes.Group(transform, "Zones");
                zonesRoot.position = Vector3.zero;
                navSurface = zonesRoot.gameObject.AddComponent<NavMeshSurface>();
                navSurface.collectObjects = CollectObjects.Children;
                navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            }
            if (gatesRoot == null)
            {
                gatesRoot = Shapes.Group(transform, "Gates");
                gatesRoot.position = Vector3.zero;
            }
            if (contentRoot == null)
            {
                contentRoot = Shapes.Group(transform, "Contents");
                contentRoot.position = Vector3.zero;
            }
        }

        /// <summary>Удалить зоны, контент и ворота (хост ещё и деспавнит боссов).</summary>
        public void ClearMap()
        {
            if (IsServer)
                foreach (var c in contents)
                    if (c != null)
                        c.OnMapClearedServer();

            // Destroy отложен до конца кадра — сначала убираем из иерархии, чтобы новый navmesh не собрал старую геометрию.
            foreach (var z in zones) if (z != null) DestroyNow(z.gameObject);
            foreach (var g in gates) if (g != null) DestroyNow(g.gameObject);
            foreach (var c in contents) if (c != null) DestroyNow(c.gameObject);
            zones.Clear();
            gates.Clear();
            contents.Clear();
            arenas.Clear();
            if (navSurface != null)
                navSurface.RemoveData();
            IsBuilt = false;
        }

        /// <summary>Сообщение по центру экрана у всех игроков.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void NotifyAllRpc(Unity.Collections.FixedString512Bytes text)
        {
            GameMessages.Post(text.ToString());
        }

        static void DestroyNow(GameObject go)
        {
            go.SetActive(false);
            go.transform.SetParent(null);
            Destroy(go);
        }

        void DrawDebug(MapPlan plan)
        {
            foreach (var z in plan.Zones)
            {
                foreach (var s in z.Layout.Slots)
                    DebugDraw.Sphere(z.Origin + s.Position + Vector3.up * 0.5f, Mathf.Max(0.5f, s.Radius), Color.cyan, DebugDrawDuration);
                foreach (var p in z.Layout.Passages)
                {
                    z.Layout.GetPassagePose(p, out var local, out var outward);
                    var rot = Quaternion.LookRotation(outward);
                    DebugDraw.Box(z.Origin + local + Vector3.up, new Vector3(z.Layout.CellSize * 0.5f, 1f, 0.3f), rot, Color.yellow, DebugDrawDuration);
                }
            }
        }

        // ================= Запросы =================

        public ZoneInstance GetZoneById(string zoneId)
        {
            foreach (var z in zones)
                if (z.ZoneId == zoneId)
                    return z;
            return null;
        }

        public ZoneInstance GetZoneAtLocation(Vector3 position)
        {
            foreach (var z in zones)
                if (z.ContainsLocation(position))
                    return z;
            return null;
        }

        public ZoneInstance FindZoneByData(ZoneData data)
        {
            foreach (var z in zones)
                if (z.Data == data)
                    return z;
            return null;
        }

        /// <summary>Слоты PlayerSpawn стартовой зоны — точки появления и возрождения группы.</summary>
        public void GetPlayerSpawnPoints(List<Pose> result)
        {
            var start = StartZoneLayout;
            if (start == null)
                return;
            var points = new List<Vector3>();
            start.GetSlots("PlayerSpawn", points);
            var center = start.WorldBounds.center;
            foreach (var p in points)
            {
                var look = center - p;
                look.y = 0f;
                result.Add(new Pose(p, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity));
            }
        }

        public bool IsZoneQuestCompleted(int zoneIndex) => zoneIndex >= 0 && zoneIndex < zoneQuestCompleted.Count && zoneQuestCompleted[zoneIndex];
        public bool IsZoneBossDefeated(int zoneIndex) => zoneIndex >= 0 && zoneIndex < zoneBossDefeated.Count && zoneBossDefeated[zoneIndex];
        public bool IsGateOpen(int gateIndex) => gateIndex >= 0 && gateIndex < gateOpen.Count && gateOpen[gateIndex];
        public ArenaState GetArenaState(int arenaIndex) => arenaIndex >= 0 && arenaIndex < arenaStates.Count ? (ArenaState)arenaStates[arenaIndex] : ArenaState.Idle;

        /// <summary>Зона: данные из реплицируемого ZoneId (аналог ResolveZoneData в UE).</summary>
        public ZoneData ResolveZoneData(ZoneInstance zone) => zone != null ? zone.Data : null;

        // ================= Ворота, квесты, боссы (хост) =================

        public void SetArenaState(int arenaIndex, ArenaState state)
        {
            if (IsServer && arenaIndex >= 0 && arenaIndex < arenaStates.Count)
                arenaStates[arenaIndex] = (byte)state;
        }

        /// <summary>Квест зоны выполнен (P0-07, источник — P0-15). Повторный вызов ничего не делает.</summary>
        public void CompleteZoneQuest(ZoneInstance zone)
        {
            if (!IsServer || zone == null || IsZoneQuestCompleted(zone.Index))
                return;
            zoneQuestCompleted[zone.Index] = true;
            int opened = UpdateGates();
            Debug.Log($"[RunMap] Zone quest completed: {zone.ZoneId}, passages opened: {opened}");
            string text = $"Квест зоны «{zone.DisplayName}» выполнен";
            if (opened > 0)
                text += $" — открыто проходов: {opened}";
            NotifyAllRpc(NetStrings.To512(text));
        }

        /// <summary>Босс зоны побеждён: флаг, событие, пересчёт ворот; финальная зона — победа в забеге.</summary>
        public void NotifyZoneBossDefeated(ZoneInstance zone)
        {
            if (!IsServer || zone == null || IsZoneBossDefeated(zone.Index))
                return;
            zoneBossDefeated[zone.Index] = true;
            Debug.Log($"[RunMap] Zone boss defeated: {zone.ZoneId}");
            OnZoneBossDefeated?.Invoke(zone);
            int opened = UpdateGates();
            if (opened > 0)
                Debug.Log($"[RunMap] Passages opened: {opened}");
            if (!zone.IsFinalZone)
            {
                CountZoneBosses(out int defeated, out int total);
                string text = $"Босс зоны «{zone.DisplayName}» побеждён ({defeated} / {total})";
                if (opened > 0)
                    text += $" — открыто проходов: {opened}";
                else if (defeated < total)
                    text += $". Проход к финалу откроется после всех боссов зон";
                NotifyAllRpc(NetStrings.To512(text));
            }
            if (zone.IsFinalZone)
                RunGameState.Instance?.DeclareVictory("FinalBoss");
        }

        /// <summary>Открыть все закрытые ворота, у которых выполнено условие. Возвращает, сколько открыто.</summary>
        public int UpdateGates()
        {
            if (!IsServer)
                return 0;
            int opened = 0;
            for (int i = 0; i < gates.Count && i < gateOpen.Count; i++)
            {
                if (gateOpen[i] || !IsGateUnlocked(gates[i]))
                    continue;
                gateOpen[i] = true;
                opened++;
            }
            ApplyGateStates();
            return opened;
        }

        /// <summary>Новое условие = новая ветка здесь.</summary>
        public bool IsGateUnlocked(PassageGate gate)
        {
            switch (gate.UnlockCondition)
            {
                case GateUnlockCondition.Open:
                    return true;
                case GateUnlockCondition.ZoneQuest:
                    return gate.OwnerZone != null && IsZoneQuestCompleted(gate.OwnerZone.Index);
                case GateUnlockCondition.ZoneBoss:
                    return gate.OwnerZone != null && IsZoneBossDefeated(gate.OwnerZone.Index);
                case GateUnlockCondition.AllZoneBosses:
                    CountZoneBosses(out int defeated, out int total);
                    return defeated >= total;
            }
            return false;
        }

        /// <summary>Боссы зон для условия AllZoneBosses: зоны с боссами, кроме финальной.</summary>
        public void CountZoneBosses(out int defeated, out int total)
        {
            defeated = 0;
            total = 0;
            foreach (var z in zones)
            {
                if (z.Data == null || z.Data.IsFinalZone || z.Data.BossPrefabs.Count == 0)
                    continue;
                total++;
                if (IsZoneBossDefeated(z.Index))
                    defeated++;
            }
        }

        /// <summary>Есть ли ворота, которые открывает квест этой зоны.</summary>
        public bool ZoneQuestOpensGates(ZoneInstance zone)
        {
            foreach (var g in gates)
                if (g.OwnerZone == zone && g.UnlockCondition == GateUnlockCondition.ZoneQuest)
                    return true;
            return false;
        }

        /// <summary>Почему ворота закрыты — для подсказки HUD.</summary>
        public string GetGateRequirementText(PassageGate gate)
        {
            string zone = gate.OwnerZone != null ? gate.OwnerZone.DisplayName : "?";
            switch (gate.UnlockCondition)
            {
                case GateUnlockCondition.ZoneQuest:
                    return $"Проход закрыт: выполните квест зоны «{zone}» (голубой столб «Квест зоны»)";
                case GateUnlockCondition.ZoneBoss:
                    return $"Проход закрыт: победите босса зоны «{zone}»";
                case GateUnlockCondition.AllZoneBosses:
                    CountZoneBosses(out int defeated, out int total);
                    return $"Проход к финалу закрыт: победите боссов всех зон ({defeated} / {total})";
            }
            return "Проход закрыт";
        }

        public void SetGateOpen(int gateIndex, bool open)
        {
            if (!IsServer || gateIndex < 0 || gateIndex >= gateOpen.Count)
                return;
            gateOpen[gateIndex] = open;
            ApplyGateStates();
        }

        void HandleGatesChanged(NetworkListEvent<bool> e) => ApplyGateStates();
        void HandleArenasChanged(NetworkListEvent<byte> e) => ApplyArenaStates();

        void ApplyGateStates()
        {
            for (int i = 0; i < gates.Count; i++)
                gates[i].SetOpenVisual(IsGateOpen(i));
        }

        void ApplyArenaStates()
        {
            foreach (var a in arenas)
                a.ApplyState(GetArenaState(a.ArenaIndex));
        }

        void PlacePlayersAtStart()
        {
            var points = new List<Pose>();
            GetPlayerSpawnPoints(points);
            if (points.Count == 0)
                return;
            int i = 0;
            foreach (var hero in HeroController.All)
            {
                var p = points[i++ % points.Count];
                hero.TeleportTo(p.position, p.rotation);
            }
        }

        // ================= Отладка (консоль хоста) =================

        public void DebugRegenerate(int seed)
        {
            if (IsServer)
                BuildMap(seed);
        }

        public void DebugRegenerateRandom()
        {
            if (IsServer)
                BuildMap(RandomSeed());
        }

        public void DebugCompleteZoneQuest(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
                DevConsole.Print($"Zone not found: {zoneId}", 4f, Color.yellow);
            else
                CompleteZoneQuest(zone);
        }

        public void DebugDefeatZoneBoss(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                DevConsole.Print($"Zone not found: {zoneId}", 4f, Color.yellow);
                return;
            }
            foreach (var a in arenas)
            {
                if (a.Zone == zone)
                {
                    a.DebugDefeat();
                    return;
                }
            }
            NotifyZoneBossDefeated(zone);
        }
    }
}
