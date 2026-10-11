using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Overlay-вспышка на всех мешах объекта (аналог SetOverlayMaterial в UE): материал добавляется
    /// вторым проходом к каждому рендереру на заданное время. Используется вспышкой попадания и каста.
    /// </summary>
    public class OverlayFlash : MonoBehaviour
    {
        readonly List<Renderer> renderers = new List<Renderer>();
        Material activeOverlay;
        Material tintedInstance;
        float endTime;
        bool applied;

        public static void Play(GameObject root, Material overlay, float duration, Color? tint = null)
        {
            if (root == null || overlay == null || duration <= 0f)
                return;
            var flash = root.GetComponent<OverlayFlash>();
            if (flash == null)
                flash = root.AddComponent<OverlayFlash>();
            flash.Begin(overlay, duration, tint);
        }

        void Begin(Material overlay, float duration, Color? tint)
        {
            Remove();

            Material mat = overlay;
            if (tint.HasValue)
            {
                if (tintedInstance == null)
                    tintedInstance = new Material(overlay);
                tintedInstance.CopyPropertiesFromMaterial(overlay);
                SetColor(tintedInstance, tint.Value);
                mat = tintedInstance;
            }

            activeOverlay = mat;
            endTime = Time.time + duration;

            renderers.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null || !r.enabled)
                    continue;
                var mats = r.sharedMaterials;
                renderers.Add(r);
                var withOverlay = new Material[mats.Length + 1];
                mats.CopyTo(withOverlay, 0);
                withOverlay[mats.Length] = activeOverlay;
                r.sharedMaterials = withOverlay;
            }
            applied = true;
        }

        void Update()
        {
            if (applied && Time.time >= endTime)
                Remove();
        }

        void Remove()
        {
            if (!applied)
                return;
            // Убираем только overlay: материалы, сменённые во время вспышки (например, «ярость» босса), сохраняются.
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                var mats = r.sharedMaterials;
                if (mats.Length > 0 && mats[mats.Length - 1] == activeOverlay)
                {
                    var restored = new Material[mats.Length - 1];
                    System.Array.Copy(mats, restored, restored.Length);
                    r.sharedMaterials = restored;
                }
            }
            renderers.Clear();
            applied = false;
        }

        void OnDisable()
        {
            Remove();
        }

        void OnDestroy()
        {
            if (tintedInstance != null)
                Destroy(tintedInstance);
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }
    }
}
