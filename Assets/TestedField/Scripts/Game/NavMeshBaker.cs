using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TestedField
{
    /// <summary>
    /// Navmesh строится при запуске уровня по заспавненной геометрии (аналог RuntimeGeneration = Dynamic):
    /// карта забега собирается из заготовок в рантайме. ИИ работает только на хосте, поэтому строит хост.
    /// </summary>
    public static class NavMeshBaker
    {
        public static void Build(NavMeshSurface surface)
        {
            if (surface == null)
                return;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        /// <summary>Есть ли navmesh под точкой (агент можно включать).</summary>
        public static bool IsOnNavMesh(Vector3 position, float maxDistance, out Vector3 snapped)
        {
            if (NavMesh.SamplePosition(position, out var hit, maxDistance, NavMesh.AllAreas))
            {
                snapped = hit.position;
                return true;
            }
            snapped = position;
            return false;
        }
    }
}
