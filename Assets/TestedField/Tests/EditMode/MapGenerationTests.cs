using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TestedField.Tests
{
    /// <summary>Генерация карты (P0-05…P0-07, P0-10) на данных проекта.</summary>
    public class MapGenerationTests
    {
        const string RunMapPath = "Assets/TestedField/Data/Zones/DA_RunMap_Default.asset";

        RunMapData data;

        [SetUp]
        public void SetUp()
        {
            data = AssetDatabase.LoadAssetAtPath<RunMapData>(RunMapPath);
            Assert.IsNotNull(data, "Run menu TestedField → Setup Project first");
        }

        static string Describe(MapPlan plan)
        {
            var zones = string.Join(";", plan.Zones.Select(z => $"{z.Data.ZoneId}:{z.Layout.name}@{z.Origin}"));
            var contents = string.Join(";", plan.Contents.Select(c => $"{c.ZoneIndex}/{c.SlotIndex}/{(c.Prefab ? c.Prefab.name : "-")}"));
            return zones + "|" + contents;
        }

        [Test]
        public void SameSeedGivesSameMap()
        {
            foreach (int seed in new[] { 1, 3, 111, 222, 333, 1647690880 })
            {
                var a = MapPlanner.TryBuild(data, seed, 0, Vector3.zero);
                var b = MapPlanner.TryBuild(data, seed, 0, Vector3.zero);
                Assert.IsTrue(a.Success, a.Error);
                Assert.AreEqual(Describe(a), Describe(b), $"seed {seed}");
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentMaps()
        {
            var maps = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
                maps.Add(Describe(MapPlanner.TryBuild(data, seed, 0, Vector3.zero)));
            Assert.Greater(maps.Count, 10);
        }

        [Test]
        public void EveryZoneHasRequiredContent()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var plan = MapPlanner.TryBuild(data, seed, 0, Vector3.zero);
                Assert.IsTrue(plan.Success, $"seed {seed}: {plan.Error}");
                Assert.AreEqual(4, plan.Zones.Count);
                Assert.AreEqual(3, plan.Gates.Count);

                for (int z = 0; z < plan.Zones.Count; z++)
                {
                    var zone = plan.Zones[z];
                    var types = plan.Contents.Where(c => c.ZoneIndex == z).Select(c => c.SlotType).ToList();
                    Assert.AreEqual(1, types.Count(t => t == "BossArena"), $"seed {seed} zone {zone.Data.ZoneId}: BossArena");
                    if (zone.Data.IsFinalZone)
                        continue;
                    Assert.AreEqual(1, types.Count(t => t == "QuestObject"), $"seed {seed} zone {zone.Data.ZoneId}: QuestObject");
                    Assert.GreaterOrEqual(types.Count(t => t == "POI"), 1, $"seed {seed} zone {zone.Data.ZoneId}: POI");
                    Assert.LessOrEqual(types.Count(t => t == "POI"), 2);
                }
            }
        }

        [Test]
        public void GatesHaveConditionsFromLinks()
        {
            var plan = MapPlanner.TryBuild(data, 111, 0, Vector3.zero);
            Assert.IsTrue(plan.Success, plan.Error);
            CollectionAssert.AreEqual(
                new[] { GateUnlockCondition.ZoneQuest, GateUnlockCondition.ZoneQuest, GateUnlockCondition.AllZoneBosses },
                plan.Gates.Select(g => g.Condition).ToArray());
            Assert.IsTrue(plan.Gates.All(g => g.OwnerZone == 0), "все проходы выходят из стартовой зоны");
        }

        [Test]
        public void AllLayoutCombinationsDoNotOverlap()
        {
            var start = data.StartZone;
            var zoneA = data.Links[0].ToZone;
            var zoneB = data.Links[1].ToZone;
            var final = data.Links[2].ToZone;
            int checkedCombos = 0;

            foreach (var ls in start.Layouts)
            foreach (var la in zoneA.Layouts)
            foreach (var lb in zoneB.Layouts)
            foreach (var lf in final.Layouts)
            {
                var copy = Object.Instantiate(data);
                var map = new Dictionary<ZoneData, ZoneData>();
                ZoneData Single(ZoneData src, ZoneLayout layout)
                {
                    var z = Object.Instantiate(src);
                    z.Layouts = new List<ZoneLayout> { layout };
                    map[src] = z;
                    return z;
                }
                copy.StartZone = Single(start, ls);
                Single(zoneA, la);
                Single(zoneB, lb);
                Single(final, lf);
                copy.Links = data.Links.Select(l => new ZoneLink
                {
                    FromZone = map[l.FromZone], FromPassage = l.FromPassage,
                    ToZone = map[l.ToZone], ToPassage = l.ToPassage, UnlockCondition = l.UnlockCondition,
                }).ToList();

                var plan = MapPlanner.TryBuild(copy, 7, 0, Vector3.zero);
                Assert.IsTrue(plan.Success, $"{ls.name}+{la.name}+{lb.name}+{lf.name}: {plan.Error}");
                checkedCombos++;
            }

            Assert.AreEqual(start.Layouts.Count * zoneA.Layouts.Count * zoneB.Layouts.Count * final.Layouts.Count, checkedCombos);
            Assert.GreaterOrEqual(checkedCombos, 8);
        }

        [Test]
        public void SlotsAreInsideTheirZones()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ZoneLayout"))
            {
                var layout = AssetDatabase.LoadAssetAtPath<ZoneLayout>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var slot in layout.Slots)
                {
                    Assert.IsTrue(layout.ContainsLocal(slot.Position), $"{layout.name}: slot {slot.SlotType} at {slot.Position} is outside");
                    // Свободное место слота (радиус) тоже внутри зоны.
                    foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                        Assert.IsTrue(layout.ContainsLocal(slot.Position + dir * (slot.Radius - 0.01f)), $"{layout.name}: slot {slot.SlotType} radius {slot.Radius} leaves the zone");
                }
                foreach (var p in layout.Passages)
                {
                    Assert.IsTrue(layout.ContainsCell(p.Cell.x, p.Cell.y), $"{layout.name}: passage {p.PassageId} cell is outside");
                }
            }
        }

        [Test]
        public void ImpossibleRuleFailsGeneration()
        {
            var copy = Object.Instantiate(data);
            var start = Object.Instantiate(data.StartZone);
            var rule = ScriptableObject.CreateInstance<SlotFillRule>();
            rule.SlotType = "BossArena";
            rule.MinCount = 2;
            rule.MaxCount = 2;
            start.FillRules = new List<SlotFillRule> { rule };
            copy.StartZone = start;
            copy.Links = data.Links.Select(l => { l.FromZone = l.FromZone == data.StartZone ? start : l.FromZone; return l; }).ToList();

            var plan = MapPlanner.TryBuild(copy, 1, 0, Vector3.zero);
            Assert.IsFalse(plan.Success);
            StringAssert.Contains("Not enough slots of type BossArena in zone Start", plan.Error);
        }

        [Test]
        public void StartZoneHasFourPlayerSpawns()
        {
            foreach (var layout in data.StartZone.Layouts)
                Assert.AreEqual(4, layout.CountSlots("PlayerSpawn"), layout.name);
        }
    }
}
