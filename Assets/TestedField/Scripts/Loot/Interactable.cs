using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_Interactable: объект, с которым можно взаимодействовать по E (лут, сундук).
    /// Радиус — от центра героя до корня объекта (корень на земле).
    /// </summary>
    public abstract class Interactable : NetworkBehaviour, IInteractTarget
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public float InteractRadius = 1.5f;

        public bool IsInteractTargetActive => this != null && IsSpawned;
        public Vector3 InteractPosition => transform.position;
        public float InteractRange => InteractRadius;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
        }

        public override void OnDestroy()
        {
            All.Remove(this);
            base.OnDestroy();
        }

        public virtual bool CanInteract(HeroController hero) => hero != null && !hero.IsDowned;

        /// <summary>Только хост. true — взаимодействие состоялось.</summary>
        public abstract bool Interact(HeroController hero);

        /// <summary>Подсказка HUD («E — …»).</summary>
        public abstract string GetPromptText(HeroController hero);
    }
}
