using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Квестовый объект зоны — ЗАГЛУШКА квеста зоны (P0-15, в UE не сделан). Подойти и нажать E:
    /// квест зоны засчитывается (RunMap.CompleteZoneQuest — открываются ворота с условием ZoneQuest)
    /// и списываются дни по причине DayCostReason (RunTimer.DayCosts). Настоящий квест заменит
    /// Interact, а вызов CompleteZoneQuest останется тем же.
    /// Объект строится у каждого участника вместе с картой; состояние «выполнен» берётся из RunMap (реплицируется).
    /// </summary>
    public class QuestObject : SlotContent, IInteractTarget
    {
        public float InteractRadius = 2f;
        [Tooltip("Причина списания дней из RunTimer.DayCosts; пусто — без списания")]
        public string DayCostReason = "Quest";
        public Color CompletedColor = new Color(0.35f, 1f, 0.45f);

        Material completedMaterial;
        bool shownCompleted;

        public bool Completed => Zone != null && Zone.QuestCompleted;

        public bool IsInteractTargetActive => this != null && isActiveAndEnabled && Zone != null;
        public Vector3 InteractPosition => transform.position;
        public float InteractRange => InteractRadius;

        void OnEnable() => InteractTargets.Local.Add(this);
        void OnDisable() => InteractTargets.Local.Remove(this);

        public bool CanInteract(HeroController hero) => hero != null && !hero.IsDowned && !Completed && RunMap.Instance != null;

        public bool Interact(HeroController hero)
        {
            if (!NetSession.IsServer || !CanInteract(hero))
                return false;
            RunMap.Instance.CompleteZoneQuest(Zone);
            if (!string.IsNullOrEmpty(DayCostReason))
                RunTimer.Instance?.SpendDaysForReason(DayCostReason);
            return true;
        }

        public string GetPromptText(HeroController hero) => $"E — выполнить квест зоны «{Zone.DisplayName}»";

        void Update()
        {
            bool completed = Completed;
            if (completed == shownCompleted)
                return;
            shownCompleted = completed;
            if (PillarRenderer != null && completed)
            {
                completedMaterial ??= CreateCompletedMaterial();
                var placeholder = PillarRenderer.sharedMaterial;
                foreach (var r in GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial == placeholder)
                        r.sharedMaterial = completedMaterial;
            }
            if (Label != null)
            {
                Label.text = completed ? "Квест выполнен" : (string.IsNullOrEmpty(PlaceholderText) ? SlotType : PlaceholderText);
                Label.color = completed ? CompletedColor : PlaceholderColor;
            }
        }

        Material CreateCompletedMaterial()
        {
            var mat = new Material(GameAssets.I.Mat("Prop")) { name = "QuestObject_Completed" };
            OverlayFlash.SetColor(mat, CompletedColor);
            return mat;
        }

        void OnDestroy()
        {
            if (completedMaterial != null)
                Destroy(completedMaterial);
        }
    }
}
