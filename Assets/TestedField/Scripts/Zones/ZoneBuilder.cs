using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>Строит геометрию заготовки зоны: пол по прямоугольникам, стены по контуру с проёмами под проходы, декор.</summary>
    public static class ZoneBuilder
    {
        struct EdgeKey
        {
            public PassageSide Side;
            public int Line;
        }

        public static ZoneInstance Build(PlannedZone planned, int index, Transform parent)
        {
            var layout = planned.Layout;
            var root = new GameObject($"Zone_{planned.Data.ZoneId}_{layout.name}");
            root.transform.SetParent(parent, false);
            root.transform.position = planned.Origin;

            var zone = root.AddComponent<ZoneInstance>();
            zone.Data = planned.Data;
            zone.Layout = layout;
            zone.Index = index;
            zone.Origin = planned.Origin;

            var assets = GameAssets.I;
            var floorMat = assets.Mat(layout.FloorMaterial);
            var wallMat = assets.Mat(layout.WallMaterial);
            float cs = layout.CellSize;

            var floors = Shapes.Group(root.transform, "Floor");
            foreach (var r in layout.Rects)
            {
                var center = new Vector3((r.xMin + r.width * 0.5f) * cs, -0.1f, (r.yMin + r.height * 0.5f) * cs);
                var floor = Shapes.Box(floors, $"Floor_{r.xMin}_{r.yMin}", center, new Vector3(r.width * cs, 0.2f, r.height * cs), floorMat);
                floor.isStatic = true;
            }

            BuildWalls(layout, Shapes.Group(root.transform, "Walls"), wallMat);

            if (layout.Props.Count > 0)
            {
                var props = Shapes.Group(root.transform, "Props");
                foreach (var p in layout.Props)
                    Shapes.Box(props, string.IsNullOrEmpty(p.Name) ? "Prop" : p.Name, p.Center, p.Size, assets.Mat(string.IsNullOrEmpty(p.Material) ? layout.WallMaterial : p.Material));
            }

            return zone;
        }

        static void BuildWalls(ZoneLayout layout, Transform parent, Material mat)
        {
            float cs = layout.CellSize;
            float t = layout.WallThickness;
            float h = layout.WallHeight;

            // Граничные стороны клеток, сгруппированные по линии.
            var edges = new Dictionary<EdgeKey, List<int>>();
            foreach (var r in layout.Rects)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                {
                    TryEdge(layout, edges, x, y, PassageSide.North, x, y + 1);
                    TryEdge(layout, edges, x, y, PassageSide.South, x, y - 1);
                    TryEdge(layout, edges, x, y, PassageSide.East, x + 1, y);
                    TryEdge(layout, edges, x, y, PassageSide.West, x - 1, y);
                }
            }

            foreach (var kv in edges)
            {
                var cells = kv.Value;
                cells.Sort();
                int runStart = cells[0];
                for (int i = 1; i <= cells.Count; i++)
                {
                    if (i < cells.Count && cells[i] == cells[i - 1] + 1)
                        continue;
                    int runEnd = cells[i - 1];
                    PlaceWall(layout, parent, mat, kv.Key, runStart, runEnd, cs, t, h);
                    if (i < cells.Count)
                        runStart = cells[i];
                }
            }
        }

        static void TryEdge(ZoneLayout layout, Dictionary<EdgeKey, List<int>> edges, int x, int y, PassageSide side, int nx, int ny)
        {
            if (layout.ContainsCell(nx, ny) || layout.IsPassageEdge(x, y, side))
                return;
            bool horizontal = side == PassageSide.North || side == PassageSide.South;
            var key = new EdgeKey { Side = side, Line = horizontal ? y : x };
            if (!edges.TryGetValue(key, out var list))
                edges[key] = list = new List<int>();
            list.Add(horizontal ? x : y);
        }

        static void PlaceWall(ZoneLayout layout, Transform parent, Material mat, EdgeKey key, int from, int to, float cs, float t, float h)
        {
            bool horizontal = key.Side == PassageSide.North || key.Side == PassageSide.South;
            float start = from * cs;
            float end = (to + 1) * cs;

            // Удлиняем на толщину стены, чтобы закрыть углы, — кроме концов, упирающихся в проход.
            if (!EndTouchesPassage(layout, key, from, -1))
                start -= t;
            if (!EndTouchesPassage(layout, key, to, +1))
                end += t;

            float length = end - start;
            float mid = (start + end) * 0.5f;
            Vector3 center, size;
            switch (key.Side)
            {
                case PassageSide.North:
                    center = new Vector3(mid, h * 0.5f, (key.Line + 1) * cs + t * 0.5f);
                    size = new Vector3(length, h, t);
                    break;
                case PassageSide.South:
                    center = new Vector3(mid, h * 0.5f, key.Line * cs - t * 0.5f);
                    size = new Vector3(length, h, t);
                    break;
                case PassageSide.East:
                    center = new Vector3((key.Line + 1) * cs + t * 0.5f, h * 0.5f, mid);
                    size = new Vector3(t, h, length);
                    break;
                default:
                    center = new Vector3(key.Line * cs - t * 0.5f, h * 0.5f, mid);
                    size = new Vector3(t, h, length);
                    break;
            }
            var wall = Shapes.Box(parent, $"Wall_{key.Side}_{key.Line}_{from}", center, size, mat);
            wall.isStatic = true;
        }

        /// <summary>Конец стены упирается в проход (сама клетка или соседняя по линии имеет проход).</summary>
        static bool EndTouchesPassage(ZoneLayout layout, EdgeKey key, int cell, int dir)
        {
            bool horizontal = key.Side == PassageSide.North || key.Side == PassageSide.South;
            int line = key.Line;
            if (horizontal)
            {
                var endSide = dir > 0 ? PassageSide.East : PassageSide.West;
                return layout.IsPassageEdge(cell, line, endSide) || layout.IsPassageEdge(cell + dir, line, key.Side);
            }
            else
            {
                var endSide = dir > 0 ? PassageSide.North : PassageSide.South;
                return layout.IsPassageEdge(line, cell, endSide) || layout.IsPassageEdge(line, cell + dir, key.Side);
            }
        }
    }
}
