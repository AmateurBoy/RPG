using Unity.AI.Navigation;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Строит navmesh по дочерней геометрии при запуске уровня (тестовая карта). Строится у всех участников
    /// (сеть в Start может быть ещё не запущена), но нужен только хосту — ИИ работает там.
    /// Карта забега строит свой navmesh сама (RunMap, только на хосте) после сборки зон.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMesh : MonoBehaviour
    {
        void Start()
        {
            NavMeshBaker.Build(GetComponent<NavMeshSurface>());
        }
    }
}
