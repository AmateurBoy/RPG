using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Модель моба/босса из примитивов: замах дубиной (по телеграфу), поза смерти, смена материала
    /// («ярость» босса), масштаб (VisualScale босса).
    /// </summary>
    public class MobVisual : MonoBehaviour
    {
        public Transform Model;
        public Transform Pivot;
        public Transform ClubPivot;
        [Tooltip("Рендереры тела — меняют материал (BodyMaterial / EnragedMaterial)")]
        public List<Renderer> BodyRenderers = new List<Renderer>();

        const float PivotHeight = 0.9f;
        static readonly Quaternion ClubRest = Quaternion.Euler(20f, 0f, 0f);

        float attackStart = -10f, windup = 0.8f, recovery = 0.6f;
        bool dead;

        public GameObject FlashRoot => Pivot != null ? Pivot.gameObject : gameObject;

        void Awake()
        {
            if (Pivot == null)
                Build(this, false);
        }

        public static void Build(MobVisual v, bool boss)
        {
            var assets = GameAssets.I;
            var body = assets.Mat(boss ? "Boss_Brute" : "Mob_Body");
            var accent = assets.Mat("Mob_Accent");
            var wood = assets.Mat("Wood");

            var old = v.transform.Find("Model");
            if (old != null)
                DestroyImmediate(old.gameObject);

            var model = Shapes.Group(v.transform, "Model");
            v.Model = model;
            var pivot = Shapes.Group(model, "Pivot", new Vector3(0f, PivotHeight, 0f));
            v.Pivot = pivot;
            v.BodyRenderers.Clear();

            v.BodyRenderers.Add(Shapes.Capsule(pivot, "Torso", new Vector3(0f, 0f, 0f), 0.7f, 1.1f, body, false).GetComponent<Renderer>());
            v.BodyRenderers.Add(Shapes.Cylinder(pivot, "LegL", new Vector3(-0.15f, -0.62f, 0f), 0.22f, 0.55f, body, false).GetComponent<Renderer>());
            v.BodyRenderers.Add(Shapes.Cylinder(pivot, "LegR", new Vector3(0.15f, -0.62f, 0f), 0.22f, 0.55f, body, false).GetComponent<Renderer>());
            v.BodyRenderers.Add(Shapes.Sphere(pivot, "Head", new Vector3(0f, 0.72f, 0.05f), 0.42f, body, false).GetComponent<Renderer>());
            Shapes.Box(pivot, "Eyes", new Vector3(0f, 0.76f, 0.24f), new Vector3(0.28f, 0.06f, 0.04f), accent, false);
            Shapes.Box(pivot, "Belt", new Vector3(0f, -0.2f, 0f), new Vector3(0.74f, 0.12f, 0.6f), accent, false);
            if (boss)
            {
                Shapes.Box(pivot, "HornL", new Vector3(-0.17f, 0.98f, 0f), Quaternion.Euler(0f, 0f, 25f), new Vector3(0.08f, 0.28f, 0.08f), accent, false);
                Shapes.Box(pivot, "HornR", new Vector3(0.17f, 0.98f, 0f), Quaternion.Euler(0f, 0f, -25f), new Vector3(0.08f, 0.28f, 0.08f), accent, false);
                Shapes.Box(pivot, "Pauldrons", new Vector3(0f, 0.42f, 0f), new Vector3(0.95f, 0.18f, 0.45f), accent, false);
            }

            var club = Shapes.Group(pivot, "ClubPivot", new Vector3(0.45f, 0.2f, 0.05f));
            club.localRotation = ClubRest;
            Shapes.Cylinder(club, "Handle", new Vector3(0f, 0f, 0.35f), Quaternion.Euler(90f, 0f, 0f), 0.08f, 0.7f, wood, false);
            Shapes.Cylinder(club, "Head", new Vector3(0f, 0f, 0.8f), Quaternion.Euler(90f, 0f, 0f), 0.22f, 0.4f, wood, false);
            v.ClubPivot = club;
        }

        public void SetBodyMaterial(Material mat)
        {
            if (mat == null)
                return;
            foreach (var r in BodyRenderers)
                if (r != null)
                    r.sharedMaterial = mat;
        }

        public void SetScale(float scale)
        {
            if (Model != null)
                Model.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        public void PlayAttack(float windupTime, float recoveryTime)
        {
            attackStart = Time.time;
            windup = Mathf.Max(0.05f, windupTime);
            recovery = Mathf.Max(0.05f, recoveryTime);
        }

        public void StopAttack()
        {
            attackStart = -10f;
        }

        public void SetDead(bool value)
        {
            dead = value;
        }

        void LateUpdate()
        {
            if (Pivot == null)
                return;

            Pivot.localRotation = dead ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            Pivot.localPosition = new Vector3(0f, dead ? 0.35f : PivotHeight, 0f);

            if (ClubPivot == null)
                return;
            float t = Time.time - attackStart;
            const float strike = 0.12f;
            if (!dead && t >= 0f && t < windup + strike + recovery)
            {
                float pitch;
                if (t < windup)
                    pitch = Mathf.Lerp(20f, -110f, Mathf.SmoothStep(0f, 1f, t / windup));
                else if (t < windup + strike)
                    pitch = Mathf.Lerp(-110f, 75f, (t - windup) / strike);
                else
                    pitch = Mathf.Lerp(75f, 20f, (t - windup - strike) / recovery);
                ClubPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            else
            {
                ClubPivot.localRotation = ClubRest;
            }
        }
    }
}
