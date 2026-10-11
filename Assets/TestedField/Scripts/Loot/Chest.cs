using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>BP_Chest: открывается один раз и выдаёт лут по своей LootTable.</summary>
    public class Chest : Interactable
    {
        public LootTable LootTable;
        [Tooltip("Лут выпадает на столько метров перед сундуком")]
        public float DropDistance = 0.9f;
        [Tooltip("Угол открытой крышки, град")]
        public float OpenLidPitch = 105f;
        public Transform Lid;

        readonly NetworkVariable<bool> opened = new NetworkVariable<bool>();

        public bool Opened => opened.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            opened.OnValueChanged += OnRepOpened;
            ApplyLid(opened.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            opened.OnValueChanged -= OnRepOpened;
        }

        void OnRepOpened(bool previous, bool current) => ApplyLid(current);

        void ApplyLid(bool isOpen)
        {
            if (Lid != null)
                Lid.localRotation = Quaternion.Euler(isOpen ? -OpenLidPitch : 0f, 0f, 0f);
        }

        public override bool CanInteract(HeroController hero) => base.CanInteract(hero) && !opened.Value;

        public override bool Interact(HeroController hero)
        {
            if (!IsServer || !CanInteract(hero))
                return false;
            opened.Value = true;
            ApplyLid(true);
            GameMode.Instance?.DropLoot(LootTable, transform.position + transform.forward * DropDistance);
            return true;
        }

        public override string GetPromptText(HeroController hero) => "E — открыть сундук";
    }
}
