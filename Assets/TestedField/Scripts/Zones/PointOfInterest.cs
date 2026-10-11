using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Точка интереса — ЗАГЛУШКА P1-02: метка на карте и сундук с лутом рядом (по loot.md сундуки ставятся в POI).
    /// Сундук — сетевой объект, его спавнит хост после сборки карты и убирает при пересборке.
    /// </summary>
    public class PointOfInterest : SlotContent
    {
        [Tooltip("Префаб сундука (с NetworkObject и Chest); пусто — GameAssets.ChestPrefab")]
        public GameObject ChestPrefab;
        [Tooltip("Таблица лута сундука; пусто — таблица из префаба")]
        public LootTable LootTable;
        [Tooltip("Где стоит сундук относительно метки")]
        public Vector3 ChestOffset = new Vector3(0f, 0f, -1.6f);

        NetworkObject spawnedChest;

        public override void OnMapBuiltServer()
        {
            var prefab = ChestPrefab != null ? ChestPrefab : GameAssets.I.ChestPrefab;
            if (prefab == null)
                return;
            var go = Instantiate(prefab, transform.position + ChestOffset, Quaternion.LookRotation(ChestOffset.sqrMagnitude > 0.01f ? ChestOffset : Vector3.back));
            go.name = $"Chest_{name}";
            if (LootTable != null && go.TryGetComponent<Chest>(out var chest))
                chest.LootTable = LootTable;
            spawnedChest = go.GetComponent<NetworkObject>();
            spawnedChest.Spawn(true);
        }

        public override void OnMapClearedServer()
        {
            if (spawnedChest != null && spawnedChest.IsSpawned)
                spawnedChest.Despawn(true);
            spawnedChest = null;
        }
    }
}
