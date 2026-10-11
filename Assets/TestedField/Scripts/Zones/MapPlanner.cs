using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    public class PlannedZone
    {
        public ZoneData Data;
        public ZoneLayout Layout;
        public Vector3 Origin;
    }

    public class PlannedGate
    {
        public int LinkIndex;
        public int OwnerZone;
        public string PassageId;
        public Vector3 Position;
        public Vector3 Outward;
        public float Width;
        public GateUnlockCondition Condition;
    }

    public class PlannedContent
    {
        public int ZoneIndex;
        public int SlotIndex;
        public string SlotType;
        public SlotContent Prefab;
        public Vector3 Position;
        public float Radius;
    }

    /// <summary>План карты — результат одной попытки генерации.</summary>
    public class MapPlan
    {
        public int Seed;
        public int Attempt;
        public readonly List<PlannedZone> Zones = new List<PlannedZone>();
        public readonly List<PlannedGate> Gates = new List<PlannedGate>();
        public readonly List<PlannedContent> Contents = new List<PlannedContent>();
        public string Error;
        public bool Success => Error == null;

        public int FindZone(ZoneData data)
        {
            for (int i = 0; i < Zones.Count; i++)
                if (Zones[i].Data == data)
                    return i;
            return -1;
        }
    }

    /// <summary>
    /// Генерация карты по паттернам (P0-06) без игровых объектов: один seed → один план.
    /// Все случайные решения идут из одного потока в фиксированном порядке (зоны в порядке связей,
    /// правила в порядке FillRules, слоты в порядке слотов заготовки), поэтому хост и клиенты
    /// получают одинаковую карту, а клиентам достаточно знать seed и номер попытки.
    /// </summary>
    public static class MapPlanner
    {
        public static RandomStream CreateStream(int seed, int attempt)
        {
            var rng = new RandomStream(unchecked(seed * 7919 + attempt * 104729 + 1));
            // Три «холостых» броска: без них у соседних seed первые значения почти совпадают.
            rng.GetFraction();
            rng.GetFraction();
            rng.GetFraction();
            return rng;
        }

        public static MapPlan TryBuild(RunMapData data, int seed, int attempt, Vector3 origin)
        {
            var plan = new MapPlan { Seed = seed, Attempt = attempt };
            if (data == null || data.StartZone == null)
            {
                plan.Error = "RunMapData or StartZone is not set";
                return plan;
            }

            var rng = CreateStream(seed, attempt);

            // 1. Стартовая зона — в точке RunMap.
            var startLayout = PickLayout(rng, data.StartZone);
            if (startLayout == null)
            {
                plan.Error = $"Zone {data.StartZone.ZoneId} has no layouts";
                return plan;
            }
            plan.Zones.Add(new PlannedZone { Data = data.StartZone, Layout = startLayout, Origin = origin });

            // 2. Связи: стыковка по проходам + ворота в стыке.
            for (int i = 0; i < data.Links.Count; i++)
            {
                var link = data.Links[i];
                int fromIndex = plan.FindZone(link.FromZone);
                if (fromIndex < 0)
                {
                    plan.Error = $"Link {i}: zone {(link.FromZone ? link.FromZone.ZoneId : "None")} is not built yet";
                    return plan;
                }
                var from = plan.Zones[fromIndex];
                if (!from.Layout.FindPassage(link.FromPassage, out var fromLocal, out var fromOut))
                {
                    plan.Error = $"Passage {link.FromPassage} not found in {from.Layout.name}";
                    return plan;
                }
                var fromWorld = from.Origin + fromLocal;

                if (link.ToZone != null && plan.FindZone(link.ToZone) < 0)
                {
                    var layout = PickLayout(rng, link.ToZone);
                    if (layout == null)
                    {
                        plan.Error = $"Zone {link.ToZone.ZoneId} has no layouts";
                        return plan;
                    }
                    if (!layout.FindPassage(link.ToPassage, out var toLocal, out var toOut))
                    {
                        plan.Error = $"Passage {link.ToPassage} not found in {layout.name}";
                        return plan;
                    }
                    if (Vector3.Dot(fromOut, toOut) > -0.99f)
                    {
                        plan.Error = $"Passages {link.FromPassage} and {link.ToPassage} do not face each other";
                        return plan;
                    }
                    plan.Zones.Add(new PlannedZone { Data = link.ToZone, Layout = layout, Origin = fromWorld - toLocal });
                }

                plan.Gates.Add(new PlannedGate
                {
                    LinkIndex = i,
                    OwnerZone = fromIndex,
                    PassageId = link.FromPassage,
                    Position = fromWorld,
                    Outward = fromOut,
                    Width = from.Layout.CellSize,
                    Condition = link.UnlockCondition,
                });
            }

            // Зоны не должны пересекаться (заготовки только сдвигаются).
            string overlap = FindOverlap(plan);
            if (overlap != null)
            {
                plan.Error = overlap;
                return plan;
            }

            // 3. Наполнение слотов по правилам.
            var slotIndices = new List<int>();
            var weights = new List<float>();
            for (int z = 0; z < plan.Zones.Count; z++)
            {
                var zone = plan.Zones[z];
                foreach (var rule in zone.Data.FillRules)
                {
                    if (rule == null)
                        continue;
                    slotIndices.Clear();
                    for (int s = 0; s < zone.Layout.Slots.Count; s++)
                        if (zone.Layout.Slots[s].SlotType == rule.SlotType)
                            slotIndices.Add(s);

                    if (rule.MinCount > slotIndices.Count)
                    {
                        plan.Error = $"Not enough slots of type {rule.SlotType} in zone {zone.Data.ZoneId}";
                        return plan;
                    }

                    int count = rng.RandRange(rule.MinCount, Mathf.Max(rule.MinCount, rule.MaxCount));
                    count = Mathf.Min(count, slotIndices.Count);
                    rng.Shuffle(slotIndices);

                    weights.Clear();
                    foreach (var c in rule.Contents)
                        weights.Add(c.Prefab != null ? c.Weight : 0f);

                    for (int k = 0; k < count; k++)
                    {
                        int contentIndex = rng.PickWeighted(weights);
                        var slot = zone.Layout.Slots[slotIndices[k]];
                        plan.Contents.Add(new PlannedContent
                        {
                            ZoneIndex = z,
                            SlotIndex = slotIndices[k],
                            SlotType = slot.SlotType,
                            Prefab = contentIndex >= 0 ? rule.Contents[contentIndex].Prefab : null,
                            Position = zone.Origin + slot.Position,
                            Radius = slot.Radius,
                        });
                    }
                }
            }

            return plan;
        }

        static ZoneLayout PickLayout(RandomStream rng, ZoneData zone)
        {
            if (zone == null || zone.Layouts.Count == 0)
                return null;
            return zone.Layouts[rng.RandHelper(zone.Layouts.Count)];
        }

        /// <summary>Пересечение площадей зон (касание краями допустимо). null — пересечений нет.</summary>
        public static string FindOverlap(MapPlan plan)
        {
            for (int a = 0; a < plan.Zones.Count; a++)
            for (int b = a + 1; b < plan.Zones.Count; b++)
            {
                var za = plan.Zones[a];
                var zb = plan.Zones[b];
                foreach (var ra in za.Layout.Rects)
                foreach (var rb in zb.Layout.Rects)
                {
                    var ba = WorldRect(za, ra);
                    var bb = WorldRect(zb, rb);
                    const float eps = 0.01f;
                    if (ba.xMin < bb.xMax - eps && bb.xMin < ba.xMax - eps && ba.yMin < bb.yMax - eps && bb.yMin < ba.yMax - eps)
                        return $"Zones {za.Data.ZoneId} ({za.Layout.name}) and {zb.Data.ZoneId} ({zb.Layout.name}) overlap";
                }
            }
            return null;
        }

        public static Rect WorldRect(PlannedZone zone, RectInt r)
        {
            float cs = zone.Layout.CellSize;
            return new Rect(zone.Origin.x + r.xMin * cs, zone.Origin.z + r.yMin * cs, r.width * cs, r.height * cs);
        }
    }
}
