using UnityEngine;
using UnityEngine.AI;

namespace TestedField
{
    /// <summary>
    /// BP_PassageGate: ворота в стыке зон (P0-07). Закрыто — барьер видим, блокирует героев и навигацию
    /// (мобы тоже не проходят); открыто — барьер скрыт. Состояние хранит RunMap (реплицируется списком),
    /// сами ворота строятся локально у каждого вместе с картой.
    /// </summary>
    public class PassageGate : MonoBehaviour
    {
        [Tooltip("Барьер (масштабируется по ширине прохода)")]
        public Transform Barrier;
        public float BarrierHeight = 3f;
        public float BarrierThickness = 0.4f;

        public int Index { get; private set; }
        public string PassageId { get; private set; }
        public ZoneInstance OwnerZone { get; private set; }
        public GateUnlockCondition UnlockCondition { get; private set; }
        public float Width { get; private set; }
        public bool IsOpen { get; private set; }

        Collider barrierCollider;
        Renderer barrierRenderer;
        NavMeshObstacle obstacle;

        public void Setup(int index, PlannedGate planned, ZoneInstance owner)
        {
            Index = index;
            PassageId = planned.PassageId;
            OwnerZone = owner;
            UnlockCondition = planned.Condition;
            Width = planned.Width;

            // Поперёк коридора: локальная Z — направление от центра зоны к проходу.
            transform.SetPositionAndRotation(planned.Position, Quaternion.LookRotation(planned.Outward, Vector3.up));

            if (Barrier == null)
                Barrier = Shapes.Box(transform, "Barrier", Vector3.zero, Vector3.one, GameAssets.I.Mat("Wood")).transform;
            Barrier.localPosition = new Vector3(0f, BarrierHeight * 0.5f, 0f);
            Barrier.localRotation = Quaternion.identity;
            Barrier.localScale = new Vector3(Width, BarrierHeight, BarrierThickness);

            barrierCollider = Barrier.GetComponent<Collider>();
            barrierRenderer = Barrier.GetComponent<Renderer>();
            obstacle = Barrier.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
                obstacle = Barrier.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            SetOpenVisual(false);
        }

        /// <summary>У всех: показать состояние (вызывает RunMap по реплицированному списку).</summary>
        public void SetOpenVisual(bool open)
        {
            IsOpen = open;
            if (barrierRenderer != null) barrierRenderer.enabled = !open;
            if (barrierCollider != null) barrierCollider.enabled = !open;
            if (obstacle != null) obstacle.enabled = !open;
        }
    }
}
