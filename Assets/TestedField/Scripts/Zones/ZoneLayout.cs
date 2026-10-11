using System;
using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    public enum PassageSide
    {
        North,
        East,
        South,
        West,
    }

    /// <summary>Сокет прохода (BPC_ZonePassage): открытая сторона клетки на краю зоны.</summary>
    [Serializable]
    public struct PassageDef
    {
        public string PassageId;
        public Vector2Int Cell;
        public PassageSide Side;

        public PassageDef(string id, int x, int y, PassageSide side)
        {
            PassageId = id;
            Cell = new Vector2Int(x, y);
            Side = side;
        }
    }

    /// <summary>Слот (BPC_ZoneSlot): тип и радиус свободного места, позиция — локальная, в метрах.</summary>
    [Serializable]
    public struct SlotDef
    {
        [Tooltip("PlayerSpawn, POI, NpcCamp, DungeonEntrance, BossArena, QuestObject…")]
        public string SlotType;
        public Vector3 Position;
        public float Radius;

        public SlotDef(string type, float x, float z, float radius)
        {
            SlotType = type;
            Position = new Vector3(x, 0f, z);
            Radius = radius;
        }
    }

    /// <summary>Декоративный блок (колонны и т.п.).</summary>
    [Serializable]
    public struct PropDef
    {
        public string Name;
        public Vector3 Center;
        public Vector3 Size;
        public string Material;
    }

    /// <summary>
    /// Заготовка зоны (BP_Zone_*_NN в UE). Геометрия — набор прямоугольников на сетке клеток CellSize;
    /// пол и стены по контуру строятся автоматически, проходы — открытые стороны клеток.
    /// Ограничение: сборщик только сдвигает зоны — проходы «откуда» и «куда» должны смотреть друг на друга.
    /// </summary>
    [CreateAssetMenu(menuName = "TestedField/Zones/Zone Layout", fileName = "Layout_")]
    public class ZoneLayout : ScriptableObject
    {
        [Tooltip("Размер клетки, м (в UE — 400 см)")]
        public float CellSize = 4f;
        [Tooltip("Прямоугольники зоны в клетках (не должны пересекаться)")]
        public List<RectInt> Rects = new List<RectInt>();
        public string FloorMaterial = "Grass";
        public string WallMaterial = "Stone";
        [Tooltip("Стены 1.8–2.4 м не пропускают героев и отбрасывание")]
        public float WallHeight = 2.2f;
        public float WallThickness = 0.4f;
        public List<PassageDef> Passages = new List<PassageDef>();
        public List<SlotDef> Slots = new List<SlotDef>();
        public List<PropDef> Props = new List<PropDef>();

        public bool ContainsCell(int x, int y)
        {
            foreach (var r in Rects)
                if (x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax)
                    return true;
            return false;
        }

        public bool ContainsLocal(Vector3 p)
        {
            return ContainsCell(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        }

        /// <summary>Границы зоны в локальных координатах (BoundsCenter / BoundsExtent).</summary>
        public Bounds GetLocalBounds()
        {
            if (Rects.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);
            var b = RectBounds(Rects[0]);
            for (int i = 1; i < Rects.Count; i++)
                b.Encapsulate(RectBounds(Rects[i]));
            return b;
        }

        public Bounds RectBounds(RectInt r)
        {
            var min = new Vector3(r.xMin * CellSize, 0f, r.yMin * CellSize);
            var max = new Vector3(r.xMax * CellSize, WallHeight, r.yMax * CellSize);
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        public bool FindPassage(string passageId, out Vector3 localPosition, out Vector3 outward)
        {
            foreach (var p in Passages)
            {
                if (p.PassageId != passageId)
                    continue;
                GetPassagePose(p, out localPosition, out outward);
                return true;
            }
            localPosition = default;
            outward = default;
            return false;
        }

        public void GetPassagePose(PassageDef p, out Vector3 localPosition, out Vector3 outward)
        {
            float cs = CellSize;
            float x = p.Cell.x * cs, z = p.Cell.y * cs;
            switch (p.Side)
            {
                case PassageSide.North: localPosition = new Vector3(x + cs * 0.5f, 0f, z + cs); outward = Vector3.forward; break;
                case PassageSide.East: localPosition = new Vector3(x + cs, 0f, z + cs * 0.5f); outward = Vector3.right; break;
                case PassageSide.South: localPosition = new Vector3(x + cs * 0.5f, 0f, z); outward = Vector3.back; break;
                default: localPosition = new Vector3(x, 0f, z + cs * 0.5f); outward = Vector3.left; break;
            }
        }

        public bool IsPassageEdge(int x, int y, PassageSide side)
        {
            foreach (var p in Passages)
                if (p.Cell.x == x && p.Cell.y == y && p.Side == side)
                    return true;
            return false;
        }

        public int CountSlots(string slotType)
        {
            int n = 0;
            foreach (var s in Slots)
                if (s.SlotType == slotType)
                    n++;
            return n;
        }
    }
}
