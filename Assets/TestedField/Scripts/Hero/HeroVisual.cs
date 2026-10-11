using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Модель героя из примитивов и процедурные «анимации»: замах мечом, кувырок, поза «упал».
    /// Модель собирается в префабе (TestedField → Setup Project) и может быть заменена художником —
    /// код использует только ссылки ниже.
    /// </summary>
    public class HeroVisual : MonoBehaviour
    {
        
       


        [Tooltip("Корень модели на высоте бёдер — поворачивается при кувырке и падении")]
        public Transform Pivot;
        [Tooltip("Ось меча (правая рука)")]
        public Transform SwordPivot;
        [Tooltip("Рендереры акцента — красятся в цвет игрока")]
        public List<Renderer> AccentRenderers = new List<Renderer>();

        public static readonly Color[] PlayerColors =
        {
            new Color(0.95f, 0.75f, 0.2f),
            new Color(0.3f, 0.85f, 0.4f),
            new Color(0.75f, 0.4f, 0.95f),
            new Color(0.25f, 0.85f, 0.95f),
        };
        const float PivotHeight = 0.9f;
        static readonly Quaternion SwordRest = Quaternion.Euler(40f, 15f, 0f);

        float swingStart = -10f, swingDuration = 0.4f;
        float rollStart = -10f, rollDuration = 0.45f;
        bool downed;
        float downedRoll = 90f, downedOffset = -0.6f;

        public GameObject FlashRoot => Pivot != null ? Pivot.gameObject : gameObject;

        void Awake()
        {
            if (Pivot == null)
                Build(this);
        }

        /// <summary>Собрать модель (вызывается из Setup Project и как запасной вариант в рантайме).</summary>
        public static void Build(HeroVisual v)
        {
            var assets = GameAssets.I;
            var body = assets.Mat("Hero_Body");
            var head = assets.Mat("Hero_Head");
            var accent = assets.Mat("Hero_Accent");
            var steel = assets.Mat("Hero_Steel");

            var old = v.transform.Find("Model");
            if (old != null)
                DestroyImmediate(old.gameObject);

            var model = Shapes.Group(v.transform, "Model");
            var pivot = Shapes.Group(model, "Pivot", new Vector3(0f, PivotHeight, 0f));
            v.Pivot = pivot;
            v.AccentRenderers.Clear();

            Shapes.Capsule(pivot, "Torso", new Vector3(0f, 0.05f, 0f), 0.55f, 1.0f, body, false);
            Shapes.Cylinder(pivot, "LegL", new Vector3(-0.12f, -0.6f, 0f), 0.18f, 0.6f, body, false);
            Shapes.Cylinder(pivot, "LegR", new Vector3(0.12f, -0.6f, 0f), 0.18f, 0.6f, body, false);
            Shapes.Sphere(pivot, "Head", new Vector3(0f, 0.78f, 0f), 0.36f, head, false);
            Shapes.Box(pivot, "Visor", new Vector3(0f, 0.8f, 0.15f), new Vector3(0.26f, 0.08f, 0.1f), steel, false);
            v.AccentRenderers.Add(Shapes.Cylinder(pivot, "Belt", new Vector3(0f, -0.15f, 0f), 0.58f, 0.1f, accent, false).GetComponent<Renderer>());
            v.AccentRenderers.Add(Shapes.Box(pivot, "Shoulders", new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.14f, 0.32f), accent, false).GetComponent<Renderer>());

            var sword = Shapes.Group(pivot, "SwordPivot", new Vector3(0.36f, 0.15f, 0.1f));
            sword.localRotation = SwordRest;
            Shapes.Cylinder(sword, "Grip", new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), 0.06f, 0.2f, accent, false);
            Shapes.Box(sword, "Guard", new Vector3(0f, 0f, 0.1f), new Vector3(0.26f, 0.05f, 0.06f), steel, false);
            Shapes.Box(sword, "Blade", new Vector3(0f, 0f, 0.6f), new Vector3(0.07f, 0.03f, 0.9f), steel, false);
            v.SwordPivot = sword;

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void SetPlayerIndex(int index)
        {
            var color = PlayerColors[Mathf.Abs(index) % PlayerColors.Length];
            foreach (var r in AccentRenderers)
            {
                if (r == null)
                    continue;
                // Отдельный экземпляр материала (не PropertyBlock), чтобы не перекрашивать overlay-вспышку.
                OverlayFlash.SetColor(r.material, color);
            }
        }

        public void PlaySwing(float duration)
        {
            swingStart = Time.time;
            swingDuration = Mathf.Max(0.05f, duration);
        }

        public void PlayRoll(float duration)
        {
            rollStart = Time.time;
            rollDuration = Mathf.Max(0.05f, duration);
        }

        public void SetDowned(bool value, float roll, float offset)
        {
            downed = value;
            downedRoll = roll;
            downedOffset = offset;
        }

        void LateUpdate()
        {
            if (Pivot == null)
                return;

            // Поза «упал» / кувырок
            float rt = (Time.time - rollStart) / rollDuration;
            if (downed)
            {
                Pivot.localRotation = Quaternion.Euler(0f, 0f, downedRoll);
                Pivot.localPosition = new Vector3(0f, PivotHeight + downedOffset, 0f);
            }
            else if (rt >= 0f && rt < 1f)
            {
                float ease = Mathf.SmoothStep(0f, 1f, rt);
                Pivot.localRotation = Quaternion.Euler(360f * ease, 0f, 0f);
                Pivot.localPosition = new Vector3(0f, PivotHeight - 0.35f * Mathf.Sin(rt * Mathf.PI), 0f);
            }
            else
            {
                Pivot.localRotation = Quaternion.identity;
                Pivot.localPosition = new Vector3(0f, PivotHeight, 0f);
            }

            // Замах: отвести вправо → рубящий удар влево → вернуть
            if (SwordPivot != null)
            {
                float t = (Time.time - swingStart) / swingDuration;
                if (t >= 0f && t < 1f && !downed)
                {
                    float yaw, pitch;
                    if (t < 0.2f)
                    {
                        float k = t / 0.2f;
                        yaw = Mathf.Lerp(15f, 85f, k);
                        pitch = Mathf.Lerp(40f, 5f, k);
                    }
                    else if (t < 0.6f)
                    {
                        float k = Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.4f);
                        yaw = Mathf.Lerp(85f, -95f, k);
                        pitch = Mathf.Lerp(5f, 25f, k);
                    }
                    else
                    {
                        float k = (t - 0.6f) / 0.4f;
                        yaw = Mathf.Lerp(-95f, 15f, k);
                        pitch = Mathf.Lerp(25f, 40f, k);
                    }
                    SwordPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                }
                else
                {
                    SwordPivot.localRotation = SwordRest;
                }
            }
        }
    }
}
