using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Изометрическая камера своего героя (аналог SpringArm + Camera в BP_Hero): фиксированный поворот,
    /// приближение колесом (IA_Zoom). Движение героя считается относительно направления камеры.
    /// </summary>
    public class HeroCamera : MonoBehaviour
    {
        public static HeroCamera Instance { get; private set; }

        [Tooltip("Поворот камеры вокруг вертикали, град (верх экрана = «вперёд»)")]
        public float Yaw = 45f;
        [Tooltip("Наклон камеры, град")]
        public float Pitch = 55f;
        public float Distance = 15f;
        public float MinDistance = 7f;
        public float MaxDistance = 26f;
        public float ZoomStep = 2f;
        public float ZoomSharpness = 10f;
        public float FollowSharpness = 14f;
        public float FieldOfView = 40f;

        HeroController target;
        float targetDistance;
        Vector3 focus;

        public static float CurrentYaw => Instance != null ? Instance.Yaw : 45f;

        public static void Attach(HeroController hero)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            var rig = cam.GetComponent<HeroCamera>();
            if (rig == null)
                rig = cam.gameObject.AddComponent<HeroCamera>();
            rig.target = hero;
            rig.targetDistance = rig.Distance;
            rig.Snap();
        }

        public static void SnapIfFollowing(HeroController hero)
        {
            if (Instance != null && Instance.target == hero)
                Instance.Snap();
        }

        /// <summary>Ввод WASD/стика → направление в мире относительно камеры.</summary>
        public static Vector3 InputToWorld(Vector2 input)
        {
            float yaw = CurrentYaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var dir = right * input.x + forward * input.y;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        void OnEnable()
        {
            Instance = this;
            targetDistance = Distance;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Snap()
        {
            if (target == null)
                return;
            focus = target.transform.position;
            Distance = targetDistance;
            Apply();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            if (!GameInput.GameplayBlocked && GameInput.Zoom != null)
            {
                float scroll = GameInput.Zoom.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.01f)
                    targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * ZoomStep, MinDistance, MaxDistance);
            }

            Distance = Mathf.Lerp(Distance, targetDistance, 1f - Mathf.Exp(-ZoomSharpness * Time.deltaTime));
            focus = Vector3.Lerp(focus, target.transform.position, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            Apply();
        }

        void Apply()
        {
            var cam = GetComponent<Camera>();
            if (cam != null)
                cam.fieldOfView = FieldOfView;
            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.SetPositionAndRotation(focus + Vector3.up * 1f - rotation * Vector3.forward * Distance, rotation);
        }
    }
}
