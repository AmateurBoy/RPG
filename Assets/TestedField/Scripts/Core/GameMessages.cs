using System;
using UnityEngine;

namespace TestedField
{
    /// <summary>Игровые сообщения для HUD (квест выполнен, босс побеждён…): последнее показывается по центру экрана.</summary>
    public static class GameMessages
    {
        public static string Current { get; private set; }
        public static float PostedAt { get; private set; } = -100f;

        public static event Action<string> OnPosted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            PostedAt = -100f;
            OnPosted = null;
        }

        public static void Post(string text)
        {
            Current = text;
            PostedAt = Time.time;
            Debug.Log("[Message] " + text);
            OnPosted?.Invoke(text);
        }
    }
}
