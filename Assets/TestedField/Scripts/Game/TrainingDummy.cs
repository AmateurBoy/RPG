using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>BP_TrainingDummy: манекен с HeroStats; после смерти исчезает и возрождается через RespawnDelay.</summary>
    [RequireComponent(typeof(HeroStats))]
    public class TrainingDummy : NetworkBehaviour
    {
        public float RespawnDelay = 3f;
        [Tooltip("Модель манекена (прячется после смерти)")]
        public GameObject Model;

        readonly NetworkVariable<bool> isActive = new NetworkVariable<bool>(true);

        HeroStats stats;
        Collider[] colliders;

        void Awake()
        {
            stats = GetComponent<HeroStats>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            isActive.OnValueChanged += OnRepIsActive;
            ApplyActive(isActive.Value);
            if (IsServer)
                stats.OnDeath += HandleDeath;
        }

        public override void OnNetworkDespawn()
        {
            isActive.OnValueChanged -= OnRepIsActive;
            if (IsServer)
                stats.OnDeath -= HandleDeath;
        }

        void HandleDeath()
        {
            isActive.Value = false;
            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(RespawnDelay);
            stats.ResetToFull();
            isActive.Value = true;
        }

        void OnRepIsActive(bool previous, bool current) => ApplyActive(current);

        void ApplyActive(bool active)
        {
            if (Model != null)
                Model.SetActive(active);
            foreach (var c in colliders)
                if (c != null)
                    c.enabled = active;
        }
    }
}
