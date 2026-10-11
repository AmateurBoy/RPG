using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// То, с чем можно взаимодействовать по E. Сетевые объекты (лут, сундук) — наследники <see cref="Interactable"/>;
    /// объекты карты забега, которые каждый участник строит сам (квестовый объект), регистрируются в
    /// <see cref="InteractTargets.Local"/>. Interact вызывается только на хосте.
    /// </summary>
    public interface IInteractTarget
    {
        /// <summary>Объект существует и участвует в поиске целей.</summary>
        bool IsInteractTargetActive { get; }
        /// <summary>Точка, от которой меряется радиус (корень на земле).</summary>
        Vector3 InteractPosition { get; }
        float InteractRange { get; }
        bool CanInteract(HeroController hero);
        /// <summary>Только хост. true — взаимодействие состоялось.</summary>
        bool Interact(HeroController hero);
        /// <summary>Подсказка HUD («E — …»).</summary>
        string GetPromptText(HeroController hero);
    }

    public static class InteractTargets
    {
        /// <summary>Несетевые цели (объекты карты забега).</summary>
        public static readonly List<IInteractTarget> Local = new List<IInteractTarget>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Local.Clear();
    }
}
