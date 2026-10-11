using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestedField
{
    /// <summary>
    /// BP_TestGameMode: спавн героев, смерть группы (P0-04), дроп лута и правило дележа (P0-12).
    /// Логика работает только на хосте; объект стоит в игровых сценах (Run, TestField) вместе с
    /// RunTimer и RunGameState.
    /// </summary>
    public class GameMode : NetworkBehaviour
    {
        public static GameMode Instance { get; private set; }

        [Header("Heroes")]
        [Tooltip("Префаб героя; пусто — GameAssets.HeroPrefab")]
        public GameObject HeroPrefab;

        [Header("PartyDeath")]
        public float PartyWipeRespawnDelay = 3f;
        [Range(0f, 1f)] public float PartyWipeRespawnHealthFraction = 1f;
        [Tooltip("Причина списания дней (сколько — RunTimer.DayCosts)")]
        public string PartyWipeReason = "PartyWipe";

        [Header("Loot")]
        [Tooltip("Правило дележа: FirstPickup — забирает тот, кто первым подобрал")]
        public string LootRule = "FirstPickup";

        bool respawnPending;
        int spawnedCount;
        readonly List<string> lootBuffer = new List<string>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer)
                return;

            NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            NetworkManager.SceneManager.OnSynchronizeComplete += HandleSynchronizeComplete;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            StartCoroutine(SpawnInitialHeroes());
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
                NetworkManager.SceneManager.OnSynchronizeComplete -= HandleSynchronizeComplete;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            if (Instance == this)
                Instance = null;
        }

        IEnumerator SpawnInitialHeroes()
        {
            // Ждём, пока карта забега соберётся (точки появления — слоты PlayerSpawn стартовой зоны).
            var runMap = RunMap.Instance;
            float wait = 0f;
            while (runMap != null && !runMap.IsBuilt && wait < 5f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            EnsureHero(NetworkManager.ServerClientId);
        }

        void HandleLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != gameObject.scene.name)
                return;
            foreach (var clientId in completed)
                EnsureHero(clientId);
        }

        void HandleSynchronizeComplete(ulong clientId)
        {
            // Игрок подключился к уже идущему забегу.
            EnsureHero(clientId);
        }

        void HandleClientDisconnected(ulong clientId)
        {
            // Ушёл последний стоящий игрок — оставшиеся упавшие иначе лежали бы вечно.
            if (IsServer)
                StartCoroutine(CheckPartyWipeNextFrame());
        }

        IEnumerator CheckPartyWipeNextFrame()
        {
            yield return null; // герой ушедшего игрока деспавнится к следующему кадру
            NotifyHeroDowned(null);
        }

        /// <summary>Заспавнить героя игроку, если его ещё нет.</summary>
        public void EnsureHero(ulong clientId)
        {
            if (!IsServer || !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
                return;
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
                return;

            var prefab = HeroPrefab != null ? HeroPrefab : GameAssets.I.HeroPrefab;
            if (prefab == null)
            {
                Debug.LogError("[GameMode] Hero prefab is not set");
                return;
            }

            var points = GetSpawnPoints();
            var point = points[spawnedCount % points.Count];
            spawnedCount++;

            var hero = Instantiate(prefab, point.position, point.rotation);
            hero.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }

        /// <summary>Точки появления: слоты PlayerSpawn стартовой зоны, иначе PlayerStart на сцене.</summary>
        public List<Pose> GetSpawnPoints()
        {
            var result = new List<Pose>();
            var runMap = RunMap.Instance;
            if (runMap != null && runMap.IsBuilt)
                runMap.GetPlayerSpawnPoints(result);

            if (result.Count == 0)
                foreach (var ps in PlayerStart.All)
                    result.Add(new Pose(ps.transform.position, ps.transform.rotation));

            if (result.Count == 0)
                result.Add(new Pose(Vector3.zero, Quaternion.identity));
            return result;
        }

        // ---------------- Смерть группы (P0-04) ----------------

        public void NotifyHeroDowned(HeroController hero)
        {
            if (!IsServer || respawnPending || !AreAllHeroesDowned())
                return;

            respawnPending = true;
            DevConsole.Print("[Party] All heroes are down — party wipe", 4f, new Color(1f, 0.5f, 0.4f));
            RunTimer.Instance?.SpendDaysForReason(PartyWipeReason);
            StartCoroutine(RespawnPartyAfterDelay());
        }

        public bool AreAllHeroesDowned()
        {
            int count = 0;
            foreach (var hero in HeroController.All)
            {
                if (hero == null || !hero.IsSpawned)
                    continue;
                count++;
                if (!hero.IsDowned)
                    return false;
            }
            return count > 0;
        }

        IEnumerator RespawnPartyAfterDelay()
        {
            yield return new WaitForSeconds(PartyWipeRespawnDelay);
            RespawnParty();
        }

        /// <summary>Герои телепортируются на точки появления (по кругу) и поднимаются с долей HP.</summary>
        public void RespawnParty()
        {
            respawnPending = false;
            var points = GetSpawnPoints();
            int i = 0;
            foreach (var hero in HeroController.All)
            {
                if (hero == null || !hero.IsSpawned)
                    continue;
                var p = points[i++ % points.Count];
                hero.RespawnAt(p.position, p.rotation, PartyWipeRespawnHealthFraction);
            }
        }

        // ---------------- Лут (P0-12) ----------------

        /// <summary>Rolls раз бросить таблицу; каждая копия — отдельный LootPickup в квадрате ±ScatterRadius вокруг точки.</summary>
        public void DropLoot(LootTable table, Vector3 origin)
        {
            if (!IsServer || table == null)
                return;
            table.Roll(() => Random.value, lootBuffer);
            foreach (var itemId in lootBuffer)
            {
                var offset = new Vector3(Random.Range(-table.ScatterRadius, table.ScatterRadius), 0f, Random.Range(-table.ScatterRadius, table.ScatterRadius));
                SpawnLootPickup(itemId, origin + offset);
            }
        }

        public LootPickup SpawnLootPickup(string itemId, Vector3 position)
        {
            if (!IsServer)
                return null;
            var prefab = GameAssets.I.LootPickupPrefab;
            if (prefab == null)
            {
                Debug.LogError("[GameMode] LootPickup prefab is not set");
                return null;
            }
            var go = Instantiate(prefab, position, Quaternion.identity);
            var pickup = go.GetComponent<LootPickup>();
            pickup.Init(itemId, ChooseLootOwner());
            go.GetComponent<NetworkObject>().Spawn(true);
            return pickup;
        }

        /// <summary>
        /// Точка расширения правила дележа: кому зарезервирован предмет (LootPickup.NoOwner — любому).
        /// Новое правило = новое значение LootRule + ветка здесь.
        /// </summary>
        public ulong ChooseLootOwner()
        {
            switch (LootRule)
            {
                case "FirstPickup":
                    return LootPickup.NoOwner;
                default:
                    DevConsole.Print($"[Loot] Unknown LootRule '{LootRule}', using FirstPickup", 4f, Color.yellow);
                    return LootPickup.NoOwner;
            }
        }

        /// <summary>Отладка: сундук в 2.5 м перед героем хоста, лицом к нему.</summary>
        public void DebugSpawnChest()
        {
            if (!IsServer)
                return;
            var hero = HeroController.Local;
            var prefab = GameAssets.I.ChestPrefab;
            if (hero == null || prefab == null)
                return;
            var fwd = hero.transform.forward;
            fwd.y = 0f;
            var pos = hero.transform.position + fwd.normalized * 2.5f;
            pos.y = hero.transform.position.y;
            var go = Instantiate(prefab, pos, Quaternion.LookRotation(-fwd));
            go.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}
