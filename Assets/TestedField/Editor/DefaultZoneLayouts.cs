using System.Collections.Generic;
using UnityEngine;

namespace TestedField.EditorTools
{
    /// <summary>
    /// Тестовые заготовки зон (аналог BP_Zone_*_NN из UE): сетка клеток 4 × 4 м, коридоры в одну клетку.
    /// Все сочетания заготовок проверены на непересечение (тест MapGenerationTests).
    /// Координаты слотов — метры от угла клетки (0, 0).
    /// </summary>
    public static class DefaultZoneLayouts
    {
        public static void Fill(ZoneLayout l, string name)
        {
            l.CellSize = 4f;
            l.WallHeight = 2.2f;
            l.WallThickness = 0.4f;
            l.Rects = new List<RectInt>();
            l.Passages = new List<PassageDef>();
            l.Slots = new List<SlotDef>();
            l.Props = new List<PropDef>();

            switch (name)
            {
                case "Layout_Start_01": Start01(l); break;
                case "Layout_Start_02": Start02(l); break;
                case "Layout_A_01": A01(l); break;
                case "Layout_A_02": A02(l); break;
                case "Layout_B_01": B01(l); break;
                case "Layout_B_02": B02(l); break;
                case "Layout_Final_01": Final01(l); break;
                default: Debug.LogError($"Unknown layout {name}"); break;
            }
        }

        static void Spawns(ZoneLayout l, float x, float z)
        {
            l.Slots.Add(new SlotDef("PlayerSpawn", x, z, 1f));
            l.Slots.Add(new SlotDef("PlayerSpawn", x + 3f, z, 1f));
            l.Slots.Add(new SlotDef("PlayerSpawn", x, z - 3f, 1f));
            l.Slots.Add(new SlotDef("PlayerSpawn", x + 3f, z - 3f, 1f));
        }

        // Стартовая: зал 44 × 44 м, выходы на восток (x = 36), север (z = 32), запад (x = −24).
        static void Start01(ZoneLayout l)
        {
            l.FloorMaterial = "Grass";
            l.WallMaterial = "Stone";
            l.Rects.Add(new RectInt(-4, -5, 11, 11));
            l.Rects.Add(new RectInt(7, 0, 2, 1));
            l.Rects.Add(new RectInt(0, 6, 1, 2));
            l.Rects.Add(new RectInt(-6, 0, 2, 1));
            l.Passages.Add(new PassageDef("Exit_East", 8, 0, PassageSide.East));
            l.Passages.Add(new PassageDef("Exit_North", 0, 7, PassageSide.North));
            l.Passages.Add(new PassageDef("Exit_West", -6, 0, PassageSide.West));

            Spawns(l, 4f, -14f);
            l.Slots.Add(new SlotDef("BossArena", -7f, 14f, 8f));
            l.Slots.Add(new SlotDef("POI", 20f, 16f, 2f));
            l.Slots.Add(new SlotDef("POI", 20f, -14f, 2f));
            l.Slots.Add(new SlotDef("POI", -10f, -14f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", -10f, -2f, 3f));
            l.Slots.Add(new SlotDef("QuestObject", 22f, 4f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 10f, 18f, 1.5f));
        }

        // Стартовая: два зала (L-форма), выходы на восток (x = 36), север (z = 32), запад (x = −20).
        static void Start02(ZoneLayout l)
        {
            l.FloorMaterial = "GrassDry";
            l.WallMaterial = "Stone";
            l.Rects.Add(new RectInt(-3, -6, 9, 9));
            l.Rects.Add(new RectInt(-3, 3, 6, 4));
            l.Rects.Add(new RectInt(6, -1, 3, 1));
            l.Rects.Add(new RectInt(0, 7, 1, 1));
            l.Rects.Add(new RectInt(-5, -2, 2, 1));
            l.Passages.Add(new PassageDef("Exit_East", 8, -1, PassageSide.East));
            l.Passages.Add(new PassageDef("Exit_North", 0, 7, PassageSide.North));
            l.Passages.Add(new PassageDef("Exit_West", -5, -2, PassageSide.West));

            Spawns(l, 4f, -18f);
            l.Slots.Add(new SlotDef("BossArena", 15f, 3f, 8f));
            l.Slots.Add(new SlotDef("POI", -6f, -16f, 2f));
            l.Slots.Add(new SlotDef("POI", -6f, 20f, 2f));
            l.Slots.Add(new SlotDef("POI", 18f, -18f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", -6f, 2f, 3f));
            l.Slots.Add(new SlotDef("QuestObject", 6f, 22f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 20f, -10f, 1.5f));
        }

        // Зона A: вход с запада, зал 44 × 44 м.
        static void A01(ZoneLayout l)
        {
            l.FloorMaterial = "Dirt";
            l.WallMaterial = "StoneMossy";
            l.Rects.Add(new RectInt(0, 0, 2, 1));
            l.Rects.Add(new RectInt(2, -5, 11, 11));
            l.Passages.Add(new PassageDef("Entrance", 0, 0, PassageSide.West));

            l.Slots.Add(new SlotDef("BossArena", 38f, 10f, 8f));
            l.Slots.Add(new SlotDef("POI", 16f, -14f, 2f));
            l.Slots.Add(new SlotDef("POI", 44f, -14f, 2f));
            l.Slots.Add(new SlotDef("POI", 16f, 18f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", 28f, -6f, 3f));
            l.Slots.Add(new SlotDef("DungeonEntrance", 46f, -4f, 2.5f));
            l.Slots.Add(new SlotDef("QuestObject", 24f, 6f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 12f, -4f, 1.5f));
        }

        // Зона A: длинный коридор, зал 36 × 44 м и боковая комната на востоке.
        static void A02(ZoneLayout l)
        {
            l.FloorMaterial = "Dirt";
            l.WallMaterial = "StoneMossy";
            l.Rects.Add(new RectInt(0, 0, 3, 1));
            l.Rects.Add(new RectInt(3, -2, 9, 11));
            l.Rects.Add(new RectInt(12, 2, 3, 4));
            l.Passages.Add(new PassageDef("Entrance", 0, 0, PassageSide.West));

            l.Slots.Add(new SlotDef("BossArena", 30f, 24f, 8f));
            l.Slots.Add(new SlotDef("POI", 18f, -4f, 2f));
            l.Slots.Add(new SlotDef("POI", 42f, -4f, 2f));
            l.Slots.Add(new SlotDef("POI", 54f, 11f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", 18f, 30f, 3f));
            l.Slots.Add(new SlotDef("DungeonEntrance", 55f, 21f, 2.5f));
            l.Slots.Add(new SlotDef("QuestObject", 30f, 4f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 42f, 12f, 1.5f));
        }

        // Зона B: вход с юга, зал 44 × 44 м.
        static void B01(ZoneLayout l)
        {
            l.FloorMaterial = "Grass";
            l.WallMaterial = "StoneDark";
            l.Rects.Add(new RectInt(0, 0, 1, 2));
            l.Rects.Add(new RectInt(-5, 2, 11, 11));
            l.Passages.Add(new PassageDef("Entrance", 0, 0, PassageSide.South));

            l.Slots.Add(new SlotDef("BossArena", -8f, 38f, 8f));
            l.Slots.Add(new SlotDef("POI", -14f, 14f, 2f));
            l.Slots.Add(new SlotDef("POI", 18f, 14f, 2f));
            l.Slots.Add(new SlotDef("POI", 16f, 46f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", 8f, 26f, 3f));
            l.Slots.Add(new SlotDef("DungeonEntrance", 18f, 32f, 2.5f));
            l.Slots.Add(new SlotDef("QuestObject", -12f, 24f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 6f, 46f, 1.5f));
        }

        // Зона B: длинный коридор, зал 44 × 36 м и боковая комната на западе.
        static void B02(ZoneLayout l)
        {
            l.FloorMaterial = "Grass";
            l.WallMaterial = "StoneDark";
            l.Rects.Add(new RectInt(0, 0, 1, 3));
            l.Rects.Add(new RectInt(-3, 3, 11, 9));
            l.Rects.Add(new RectInt(-6, 6, 3, 4));
            l.Passages.Add(new PassageDef("Entrance", 0, 0, PassageSide.South));

            l.Slots.Add(new SlotDef("BossArena", 16f, 30f, 8f));
            l.Slots.Add(new SlotDef("POI", -18f, 32f, 2f));
            l.Slots.Add(new SlotDef("POI", -6f, 16f, 2f));
            l.Slots.Add(new SlotDef("POI", 26f, 44f, 2f));
            l.Slots.Add(new SlotDef("NpcCamp", 0f, 40f, 3f));
            l.Slots.Add(new SlotDef("DungeonEntrance", 28f, 16f, 2.5f));
            l.Slots.Add(new SlotDef("QuestObject", -6f, 44f, 1.5f));
            l.Slots.Add(new SlotDef("QuestObject", 6f, 22f, 1.5f));
        }

        // Финальная локация: комната 28 × 28 м с четырьмя колоннами и коридором-входом с востока.
        static void Final01(ZoneLayout l)
        {
            l.FloorMaterial = "StoneDark";
            l.WallMaterial = "StoneDark";
            l.WallHeight = 3f;
            l.Rects.Add(new RectInt(-2, 0, 2, 1));
            l.Rects.Add(new RectInt(-9, -3, 7, 7));
            l.Passages.Add(new PassageDef("Entrance", -1, 0, PassageSide.East));

            l.Slots.Add(new SlotDef("BossArena", -22f, 2f, 11f));
            foreach (var c in new[] { new Vector2(-33f, -9f), new Vector2(-11f, -9f), new Vector2(-33f, 13f), new Vector2(-11f, 13f) })
                l.Props.Add(new PropDef { Name = "Column", Center = new Vector3(c.x, 2f, c.y), Size = new Vector3(2f, 4f, 2f), Material = "Stone" });
        }
    }
}
