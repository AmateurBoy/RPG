using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_LootPickup: предмет, лежащий в мире. Хранит только ItemId. Подбор — по E на хосте:
    /// Inventory.AddItem, при успехе объект исчезает; инвентарь полон — предмет остаётся лежать.
    /// </summary>
    public class LootPickup : Interactable
    {
        public const ulong NoOwner = ulong.MaxValue;

        [Tooltip("Вращающийся кристалл")]
        public Transform Gem;
        public float SpinSpeed = 90f;
        public float BobHeight = 0.12f;

        readonly NetworkVariable<FixedString32Bytes> itemId = new NetworkVariable<FixedString32Bytes>();
        readonly NetworkVariable<bool> pickedUp = new NetworkVariable<bool>();
        readonly NetworkVariable<ulong> reservedFor = new NetworkVariable<ulong>(NoOwner);

        public string ItemId => itemId.Value.ToString();
        public bool PickedUp => pickedUp.Value;
        /// <summary>ClientId игрока, которому зарезервирован предмет (NoOwner — любому).</summary>
        public ulong ReservedFor => reservedFor.Value;

        Vector3 gemBase;

        void Awake()
        {
            if (Gem != null)
                gemBase = Gem.localPosition;
        }

        string pendingItemId;
        ulong pendingReservedFor = NoOwner;

        /// <summary>Хост, до Spawn: значения уходят в сеть в OnNetworkSpawn (вместе с сообщением о спавне).</summary>
        public void Init(string id, ulong reserved)
        {
            pendingItemId = id;
            pendingReservedFor = reserved;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && pendingItemId != null)
            {
                itemId.Value = NetStrings.To32(pendingItemId);
                reservedFor.Value = pendingReservedFor;
            }
        }

        public string GetDisplayName()
        {
            var db = GameAssets.I.ItemDatabase;
            return db != null ? db.GetDisplayName(ItemId) : ItemId;
        }

        public override bool CanInteract(HeroController hero)
        {
            if (!base.CanInteract(hero) || pickedUp.Value)
                return false;
            return reservedFor.Value == NoOwner || reservedFor.Value == hero.OwnerClientId;
        }

        public override bool Interact(HeroController hero)
        {
            if (!IsServer || !CanInteract(hero) || hero.Inventory == null)
                return false;
            if (!hero.Inventory.AddItem(ItemId, out _))
                return false;
            pickedUp.Value = true;
            NetworkObject.Despawn(true);
            return true;
        }

        public override string GetPromptText(HeroController hero)
        {
            string name = GetDisplayName();
            bool hasSpace = hero != null && hero.Inventory != null && hero.Inventory.HasFreeSpace;
            return hasSpace ? $"E — подобрать: {name}" : $"Инвентарь полон: {name}";
        }

        void Update()
        {
            if (Gem == null)
                return;
            Gem.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.World);
            Gem.localPosition = gemBase + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * BobHeight);
        }
    }
}
