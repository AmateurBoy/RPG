using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Общие ссылки на ассеты (Resources/GameAssets). Нужны коду, который собирает геометрию в рантайме
    /// (зоны, телеграфы, лут) и не может получить ссылку через инспектор.
    /// Создаётся и заполняется меню TestedField → Setup Project.
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Game Assets", fileName = "GameAssets")]
    public class GameAssets : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Палитра MI_* (окружение, герой, мобы, лут). Ищется по имени без префикса MI_.")]
        public List<Material> Materials = new List<Material>();
        [Tooltip("Overlay-материал вспышки попадания (M_HitFlash в UE)")]
        public Material HitFlashMaterial;
        [Tooltip("Материал круга-телеграфа атак мобов и боссов")]
        public Material TelegraphMaterial;

        [Header("Audio")]
        [Tooltip("Звук попадания; пусто — генерируется заглушка")]
        public AudioClip HitSound;

        [Header("Data")]
        public ItemDatabase ItemDatabase;
        public SkillConfig SkillConfig;

        [Header("Network prefabs")]
        public GameObject HeroPrefab;
        public GameObject LootPickupPrefab;
        public GameObject ChestPrefab;
        public GameObject SkillProjectilePrefab;

        static GameAssets instance;
        Dictionary<string, Material> materialLookup;
        static Material fallbackMaterial;

        public static GameAssets I
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameAssets>("GameAssets");
                    if (instance == null)
                    {
                        Debug.LogError("[GameAssets] Resources/GameAssets not found. Run menu TestedField → Setup Project.");
                        instance = CreateInstance<GameAssets>();
                    }
                }
                return instance;
            }
        }

        /// <summary>Сбросить кэш (редактор: после пересоздания ассета в Setup Project).</summary>
        public static void ClearCache()
        {
            instance = null;
        }

        /// <summary>Материал палитры по имени («Stone» или «MI_Stone»).</summary>
        public Material Mat(string name)
        {
            if (materialLookup == null)
            {
                materialLookup = new Dictionary<string, Material>();
                foreach (var m in Materials)
                {
                    if (m == null)
                        continue;
                    string key = m.name.StartsWith("MI_") ? m.name.Substring(3) : m.name;
                    materialLookup[key] = m;
                }
            }

            if (name != null && name.StartsWith("MI_"))
                name = name.Substring(3);
            if (name != null && materialLookup.TryGetValue(name, out var mat))
                return mat;

            Debug.LogWarning($"[GameAssets] Material not found: {name}");
            return Fallback;
        }

        static Material Fallback
        {
            get
            {
                if (fallbackMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    fallbackMaterial = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Fallback" };
                    fallbackMaterial.color = new Color(0.8f, 0.2f, 0.8f);
                }
                return fallbackMaterial;
            }
        }

        AudioClip generatedHitSound;

        public AudioClip GetHitSound()
        {
            if (HitSound != null)
                return HitSound;
            if (generatedHitSound == null)
                generatedHitSound = ProceduralAudio.MakeHit();
            return generatedHitSound;
        }

        void OnValidate()
        {
            materialLookup = null;
        }
    }
}
