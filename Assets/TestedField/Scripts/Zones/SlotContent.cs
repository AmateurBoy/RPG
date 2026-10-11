using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_SlotContent: контент слота. Базовый класс — заглушка (столбик и подпись с типом слота).
    /// Настоящие POI, лагеря НПС, входы в данж и квестовые объекты (P1-02, P1-03, P1-01, P0-15) —
    /// наследники, их префаб прописывается в правиле наполнения.
    /// </summary>
    public class SlotContent : MonoBehaviour
    {
        [Tooltip("Строить столбик-заглушку с подписью")]
        public bool BuildPlaceholder = true;
        public Color PlaceholderColor = new Color(0.9f, 0.8f, 0.3f);
        [Tooltip("Подпись над столбиком; пусто — тип слота")]
        public string PlaceholderText;

        static readonly System.Collections.Generic.Dictionary<Color, Material> placeholderMaterials = new System.Collections.Generic.Dictionary<Color, Material>();

        public string SlotType { get; private set; }
        public ZoneInstance Zone { get; private set; }
        public float SlotRadius { get; private set; }
        public int ContentIndex { get; private set; }

        /// <summary>Столбик и подпись заглушки (null, если BuildPlaceholder выключен).</summary>
        protected Renderer PillarRenderer { get; private set; }
        protected TextMesh Label { get; private set; }

        public virtual void Initialize(string slotType, ZoneInstance zone, float slotRadius, int contentIndex)
        {
            SlotType = slotType;
            Zone = zone;
            SlotRadius = slotRadius;
            ContentIndex = contentIndex;
            if (BuildPlaceholder)
                BuildPlaceholderVisual();
        }

        void BuildPlaceholderVisual()
        {
            if (!placeholderMaterials.TryGetValue(PlaceholderColor, out var mat) || mat == null)
            {
                mat = new Material(GameAssets.I.Mat("Prop")) { name = "Placeholder_" + ColorUtility.ToHtmlStringRGB(PlaceholderColor) };
                OverlayFlash.SetColor(mat, PlaceholderColor);
                placeholderMaterials[PlaceholderColor] = mat;
            }
            PillarRenderer = Shapes.Cylinder(transform, "Pillar", new Vector3(0f, 0.75f, 0f), 0.6f, 1.5f, mat).GetComponent<Renderer>();
            Shapes.Cylinder(transform, "Base", new Vector3(0f, 0.05f, 0f), 1.4f, 0.1f, mat, false);
            Label = Shapes.Label(transform, string.IsNullOrEmpty(PlaceholderText) ? SlotType : PlaceholderText, new Vector3(0f, 2.3f, 0f), PlaceholderColor, 0.5f);
        }

        /// <summary>Хост: карта собрана, сетевые объекты можно спавнить.</summary>
        public virtual void OnMapBuiltServer()
        {
        }

        /// <summary>Хост: карта разбирается.</summary>
        public virtual void OnMapClearedServer()
        {
        }
    }
}
