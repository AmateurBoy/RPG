using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_SkillProjectile: снаряд навыков типа Projectile. По сети приходит только SkillId —
    /// каждая сторона берёт скорость/дальность/материал из SkillConfig и двигает снаряд сама;
    /// попадание считает только хост, уничтожение реплицируется.
    /// </summary>
    public class SkillProjectile : NetworkBehaviour
    {
        public SkillConfig Config;
        [Tooltip("Материал по умолчанию, если у навыка не задан ProjectileMaterial")]
        public Material DefaultMaterial;
        [Tooltip("Модель снаряда (масштабируется по радиусу навыка)")]
        public Transform Model;

        readonly NetworkVariable<FixedString32Bytes> skillId = new NetworkVariable<FixedString32Bytes>();

        SkillData skill;
        GameObject caster;
        float traveled;
        bool finished;

        SkillConfig ActiveConfig => Config != null ? Config : GameAssets.I.SkillConfig;

        string pendingSkillId;

        /// <summary>Хост, до Spawn: SkillId уходит в сеть в OnNetworkSpawn (вместе с сообщением о спавне).</summary>
        public void Init(string id, GameObject casterObject)
        {
            pendingSkillId = id;
            caster = casterObject;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingSkillId != null)
                skillId.Value = NetStrings.To32(pendingSkillId);
            skill = ActiveConfig != null ? ActiveConfig.GetSkillDefinition(skillId.Value.ToString()) : null;
            ApplyVisual();
        }

        void ApplyVisual()
        {
            if (Model == null || skill == null)
                return;
            float d = Mathf.Max(0.1f, skill.Radius * 2f);
            Model.localScale = Vector3.one * d;
            var mat = skill.ProjectileMaterial != null ? skill.ProjectileMaterial : DefaultMaterial;
            var r = Model.GetComponent<Renderer>();
            if (r != null && mat != null)
                r.sharedMaterial = mat;
        }

        void Update()
        {
            if (!IsSpawned || finished || skill == null)
                return;

            float step = skill.Speed * Time.deltaTime;
            if (traveled + step > skill.Distance)
                step = Mathf.Max(0f, skill.Distance - traveled);

            if (IsServer && CheckHit(step))
                return;

            transform.position += transform.forward * step;
            traveled += step;

            if (IsServer && traveled >= skill.Distance - 0.001f)
                Finish();
        }

        /// <summary>Хост: первая цель на пути получает урон, стена — снаряд исчезает.</summary>
        bool CheckHit(float step)
        {
            float radius = Mathf.Max(0.05f, skill.Radius);
            var hits = Physics.SphereCastAll(transform.position, radius, transform.forward, step, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var hero = caster != null ? caster.GetComponent<HeroController>() : null;
            bool friendlyFire = hero != null && hero.FriendlyFire;

            foreach (var h in hits)
            {
                var target = h.collider.GetComponentInParent<HeroStats>();
                if (target != null)
                {
                    if (!Combat.CanHitTarget(caster, target, null, friendlyFire))
                        continue;
                    target.ApplyDamage(skill.Damage, caster != null ? caster : gameObject);
                    Finish();
                    return true;
                }
                if (Combat.IsWorldStatic(h.collider))
                {
                    Finish();
                    return true;
                }
            }
            return false;
        }

        void Finish()
        {
            if (finished)
                return;
            finished = true;
            if (IsServer && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }
    }
}
