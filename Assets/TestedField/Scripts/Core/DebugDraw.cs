using UnityEngine;

namespace TestedField
{
    /// <summary>Отладочная отрисовка в мире (аналог DrawDebugSphere/DrawDebugBox).</summary>
    public static class DebugDraw
    {
        public static void Sphere(Vector3 center, float radius, Color color, float duration)
        {
            Circle(center, Vector3.up, radius, color, duration);
            Circle(center, Vector3.right, radius, color, duration);
            Circle(center, Vector3.forward, radius, color, duration);
        }

        public static void Circle(Vector3 center, Vector3 normal, float radius, Color color, float duration, int segments = 24)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, normal);
            Vector3 prev = center + rot * new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + rot * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Debug.DrawLine(prev, next, color, duration);
                prev = next;
            }
        }

        public static void Box(Vector3 center, Vector3 halfExtents, Quaternion rotation, Color color, float duration)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                c[i] = center + rotation * Vector3.Scale(corner, halfExtents);
            }
            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            for (int i = 0; i < edges.GetLength(0); i++)
                Debug.DrawLine(c[edges[i, 0]], c[edges[i, 1]], color, duration);
        }
    }
}
