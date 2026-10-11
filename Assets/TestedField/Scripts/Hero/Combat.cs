using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>Общие функции боя: поиск целей и фильтр CanHitTarget (P0-02).</summary>
    public static class Combat
    {
        static readonly Collider[] overlapBuffer = new Collider[64];
        static readonly HashSet<HeroStats> uniqueTargets = new HashSet<HeroStats>();

        /// <summary>
        /// Можно ли бить цель: не сам атакующий, ещё не задета в этом замахе, есть HeroStats,
        /// цель не мертва, другой герой — только при friendlyFire.
        /// </summary>
        public static bool CanHitTarget(GameObject attacker, HeroStats target, ICollection<HeroStats> alreadyHit, bool friendlyFire)
        {
            if (target == null || target.gameObject == attacker)
                return false;
            if (alreadyHit != null && alreadyHit.Contains(target))
                return false;
            if (target.IsDead)
                return false;
            if (!friendlyFire && attacker != null && attacker.GetComponent<HeroController>() != null && target.GetComponent<HeroController>() != null)
                return false;
            return true;
        }

        /// <summary>Все HeroStats, чьи коллайдеры пересекают сферу (каждая цель один раз).</summary>
        public static List<HeroStats> OverlapSphere(Vector3 center, float radius, List<HeroStats> results)
        {
            results.Clear();
            uniqueTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var stats = overlapBuffer[i].GetComponentInParent<HeroStats>();
                if (stats != null && uniqueTargets.Add(stats))
                    results.Add(stats);
            }
            return results;
        }

        /// <summary>Все HeroStats в капсуле от a до b (полоса рывка).</summary>
        public static List<HeroStats> OverlapCapsule(Vector3 a, Vector3 b, float radius, List<HeroStats> results)
        {
            results.Clear();
            uniqueTargets.Clear();
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var stats = overlapBuffer[i].GetComponentInParent<HeroStats>();
                if (stats != null && uniqueTargets.Add(stats))
                    results.Add(stats);
            }
            return results;
        }

        /// <summary>Коллайдер — часть окружения (не персонаж и не триггер).</summary>
        public static bool IsWorldStatic(Collider c)
        {
            return c != null && !c.isTrigger && c.GetComponentInParent<HeroStats>() == null && c.GetComponentInParent<Interactable>() == null;
        }

        /// <summary>Живые герои (не упали и не мертвы).</summary>
        public static bool IsHeroAlive(HeroController hero)
        {
            return hero != null && hero.IsSpawned && !hero.IsDowned && !hero.Stats.IsDead;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
