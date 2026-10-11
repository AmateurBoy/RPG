using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>Точка появления игроков (аналог PlayerStart). В Run точки берутся из слотов PlayerSpawn стартовой зоны.</summary>
    public class PlayerStart : MonoBehaviour
    {
        public static readonly List<PlayerStart> All = new List<PlayerStart>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward);
        }
    }
}
