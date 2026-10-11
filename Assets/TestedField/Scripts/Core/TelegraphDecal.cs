using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Красный круг на земле перед ударом (телеграф мобов и боссов): внешний контур и заливка,
    /// которая растёт до края к моменту удара.
    /// </summary>
    public class TelegraphDecal : MonoBehaviour
    {
        Transform outer;
        Transform fill;
        Material outerMat;
        Material fillMat;
        float startTime;
        float duration;
        float radius;

        public static TelegraphDecal Create(string name)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<TelegraphDecal>();
            t.Build();
            go.SetActive(false);
            return t;
        }

        void Build()
        {
            var baseMat = GameAssets.I.TelegraphMaterial;
            outerMat = new Material(baseMat);
            fillMat = new Material(baseMat);
            OverlayFlash.SetColor(outerMat, new Color(1f, 0.1f, 0.05f, 0.28f));
            OverlayFlash.SetColor(fillMat, new Color(1f, 0.15f, 0.05f, 0.45f));
            outer = Shapes.Cylinder(transform, "Outer", new Vector3(0f, 0.03f, 0f), 1f, 0.02f, outerMat, false).transform;
            fill = Shapes.Cylinder(transform, "Fill", new Vector3(0f, 0.05f, 0f), 1f, 0.02f, fillMat, false).transform;
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Show(Vector3 center, float circleRadius, float windup)
        {
            transform.position = new Vector3(center.x, center.y, center.z);
            radius = circleRadius;
            duration = Mathf.Max(0.01f, windup);
            startTime = Time.time;
            outer.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            fill.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (Time.time - startTime > duration + 0.05f)
            {
                Hide();
                return;
            }
            float k = Mathf.Clamp01((Time.time - startTime) / duration);
            float d = Mathf.Max(0.01f, radius * 2f * k);
            fill.localScale = new Vector3(d, 0.01f, d);
        }

        void OnDestroy()
        {
            if (outerMat != null) Destroy(outerMat);
            if (fillMat != null) Destroy(fillMat);
        }
    }
}
