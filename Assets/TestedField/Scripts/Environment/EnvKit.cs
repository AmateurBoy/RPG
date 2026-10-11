using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Набор окружения из примитивов (Environment/Castle, Nature, Village в UE). Используется
    /// TestedField → Setup Project для префабов тестовой карты. Размеры — метры.
    /// </summary>
    public static class EnvKit
    {
        static Material M(string name) => GameAssets.I.Mat(name);

        static Transform Root(Transform parent, string name)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------------- Деревня ----------------

        public static Transform House(Transform parent, float width = 6f, float depth = 5f, bool thatch = false)
        {
            var root = Root(parent, thatch ? "BP_House_Thatch" : "BP_House");
            float wallH = 3f;
            Shapes.Box(root, "Walls", new Vector3(0f, wallH * 0.5f, 0f), new Vector3(width, wallH, depth), M("Plaster"));
            // Фахверк
            var timber = M("Timber");
            Shapes.Box(root, "BeamBottom", new Vector3(0f, 0.15f, 0f), new Vector3(width + 0.1f, 0.3f, depth + 0.1f), timber, false);
            Shapes.Box(root, "BeamTop", new Vector3(0f, wallH - 0.1f, 0f), new Vector3(width + 0.1f, 0.2f, depth + 0.1f), timber, false);
            foreach (float x in new[] { -width * 0.5f, 0f, width * 0.5f })
            {
                Shapes.Box(root, "Post", new Vector3(x, wallH * 0.5f, depth * 0.5f + 0.03f), new Vector3(0.2f, wallH, 0.08f), timber, false);
                Shapes.Box(root, "PostBack", new Vector3(x, wallH * 0.5f, -depth * 0.5f - 0.03f), new Vector3(0.2f, wallH, 0.08f), timber, false);
            }
            // Двускатная крыша
            var roof = M(thatch ? "RoofThatch" : "RoofTile");
            float roofW = depth * 0.5f / Mathf.Cos(35f * Mathf.Deg2Rad) + 0.5f;
            Shapes.Box(root, "RoofL", new Vector3(0f, wallH + depth * 0.25f * Mathf.Tan(35f * Mathf.Deg2Rad) + 0.2f, depth * 0.25f), Quaternion.Euler(35f, 0f, 0f), new Vector3(width + 0.6f, 0.2f, roofW), roof);
            Shapes.Box(root, "RoofR", new Vector3(0f, wallH + depth * 0.25f * Mathf.Tan(35f * Mathf.Deg2Rad) + 0.2f, -depth * 0.25f), Quaternion.Euler(-35f, 0f, 0f), new Vector3(width + 0.6f, 0.2f, roofW), roof);
            Shapes.Box(root, "Gable", new Vector3(0f, wallH + 0.7f, 0f), new Vector3(width - 0.05f, 1.4f, depth * 0.5f), M("Plaster"), false);
            // Дверь и окна
            Shapes.Box(root, "Door", new Vector3(0f, 1f, depth * 0.5f + 0.05f), new Vector3(1.1f, 2f, 0.1f), M("Wood"), false);
            Shapes.Box(root, "WindowL", new Vector3(-width * 0.3f, 1.8f, depth * 0.5f + 0.05f), new Vector3(0.9f, 0.8f, 0.08f), M("Window"), false);
            Shapes.Box(root, "WindowR", new Vector3(width * 0.3f, 1.8f, depth * 0.5f + 0.05f), new Vector3(0.9f, 0.8f, 0.08f), M("Window"), false);
            Shapes.Box(root, "Chimney", new Vector3(width * 0.3f, wallH + 1.6f, -depth * 0.15f), new Vector3(0.6f, 1.6f, 0.6f), M("StoneDark"), false);
            return root;
        }

        public static Transform Well(Transform parent)
        {
            var root = Root(parent, "BP_Well");
            Shapes.Cylinder(root, "Ring", new Vector3(0f, 0.45f, 0f), 2f, 0.9f, M("Stone"));
            Shapes.Cylinder(root, "Water", new Vector3(0f, 0.8f, 0f), 1.6f, 0.05f, M("Water"), false);
            Shapes.Box(root, "PostL", new Vector3(-0.85f, 1.3f, 0f), new Vector3(0.15f, 1.8f, 0.15f), M("Timber"), false);
            Shapes.Box(root, "PostR", new Vector3(0.85f, 1.3f, 0f), new Vector3(0.15f, 1.8f, 0.15f), M("Timber"), false);
            Shapes.Box(root, "RoofL", new Vector3(0f, 2.4f, 0.4f), Quaternion.Euler(35f, 0f, 0f), new Vector3(2.2f, 0.1f, 1.1f), M("RoofTile"), false);
            Shapes.Box(root, "RoofR", new Vector3(0f, 2.4f, -0.4f), Quaternion.Euler(-35f, 0f, 0f), new Vector3(2.2f, 0.1f, 1.1f), M("RoofTile"), false);
            return root;
        }

        public static Transform Barrel(Transform parent)
        {
            var root = Root(parent, "BP_Barrel");
            Shapes.Cylinder(root, "Body", new Vector3(0f, 0.5f, 0f), 0.75f, 1f, M("Wood"));
            Shapes.Cylinder(root, "HoopTop", new Vector3(0f, 0.85f, 0f), 0.78f, 0.06f, M("StoneDark"), false);
            Shapes.Cylinder(root, "HoopBottom", new Vector3(0f, 0.15f, 0f), 0.78f, 0.06f, M("StoneDark"), false);
            return root;
        }

        public static Transform Crate(Transform parent, float size = 0.9f)
        {
            var root = Root(parent, "BP_Crate");
            Shapes.Box(root, "Body", new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, M("Wood"));
            Shapes.Box(root, "Band", new Vector3(0f, size * 0.5f, 0f), new Vector3(size + 0.02f, 0.12f, size + 0.02f), M("Timber"), false);
            return root;
        }

        public static Transform Fence(Transform parent, float length = 4f)
        {
            var root = Root(parent, "BP_Fence");
            var wood = M("Wood");
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -length * 0.5f + length * i / (posts - 1);
                Shapes.Box(root, "Post", new Vector3(x, 0.55f, 0f), new Vector3(0.15f, 1.1f, 0.15f), wood, false);
            }
            Shapes.Box(root, "RailTop", new Vector3(0f, 0.9f, 0f), new Vector3(length, 0.1f, 0.08f), wood, false);
            Shapes.Box(root, "RailBottom", new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.1f, 0.08f), wood, false);
            // Один общий коллайдер — забор не перепрыгнуть.
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(length, 1.2f, 0.25f);
            return root;
        }

        public static Transform Haystack(Transform parent)
        {
            var root = Root(parent, "BP_Haystack");
            Shapes.Sphere(root, "Hay", new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.6f, 2.2f), M("Hay"));
            Shapes.Cylinder(root, "Top", new Vector3(0f, 1.45f, 0f), 0.8f, 0.4f, M("Hay"), false);
            return root;
        }

        // ---------------- Замок ----------------

        public static Transform CastleWall(Transform parent, float length = 8f, bool ruin = false)
        {
            var root = Root(parent, ruin ? "BP_CastleWallRuin" : "BP_CastleWall");
            var stone = M(ruin ? "StoneMossy" : "StoneDark");
            float h = ruin ? 2.6f : 5f;
            if (ruin)
            {
                Shapes.Box(root, "WallA", new Vector3(-length * 0.25f, h * 0.5f, 0f), new Vector3(length * 0.45f, h, 1.4f), stone);
                Shapes.Box(root, "WallB", new Vector3(length * 0.3f, h * 0.3f, 0f), new Vector3(length * 0.35f, h * 0.6f, 1.4f), stone);
                Shapes.Box(root, "Rubble1", new Vector3(0.4f, 0.3f, 1.2f), Quaternion.Euler(10f, 25f, 5f), new Vector3(1.2f, 0.6f, 0.9f), M("Stone"));
                Shapes.Box(root, "Rubble2", new Vector3(1.6f, 0.25f, -1.1f), Quaternion.Euler(-8f, 50f, 12f), new Vector3(0.9f, 0.5f, 0.8f), M("Stone"));
            }
            else
            {
                Shapes.Box(root, "Wall", new Vector3(0f, h * 0.5f, 0f), new Vector3(length, h, 1.5f), stone);
                int merlons = Mathf.Max(2, Mathf.RoundToInt(length / 1.6f));
                for (int i = 0; i < merlons; i++)
                {
                    float x = -length * 0.5f + (i + 0.5f) * length / merlons;
                    Shapes.Box(root, "Merlon", new Vector3(x, h + 0.4f, 0f), new Vector3(length / merlons * 0.55f, 0.8f, 1.5f), stone, false);
                }
            }
            return root;
        }

        public static Transform CastleTower(Transform parent, bool ruin = false)
        {
            var root = Root(parent, ruin ? "BP_CastleTowerRuin" : "BP_CastleTower");
            var stone = M(ruin ? "StoneMossy" : "Stone");
            float h = ruin ? 3.5f : 8f;
            Shapes.Cylinder(root, "Tower", new Vector3(0f, h * 0.5f, 0f), 4.2f, h, stone);
            if (!ruin)
            {
                Shapes.Cylinder(root, "Top", new Vector3(0f, h + 0.3f, 0f), 4.8f, 0.6f, M("StoneDark"), false);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    Shapes.Box(root, "Merlon", new Vector3(Mathf.Sin(a) * 2.2f, h + 1f, Mathf.Cos(a) * 2.2f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(0.9f, 0.9f, 0.5f), M("StoneDark"), false);
                }
            }
            else
            {
                Shapes.Box(root, "Rubble", new Vector3(2.4f, 0.35f, 0.8f), Quaternion.Euler(12f, 30f, 6f), new Vector3(1.4f, 0.7f, 1.1f), M("Stone"));
            }
            return root;
        }

        public static Transform CastleGate(Transform parent)
        {
            var root = Root(parent, "BP_CastleGate");
            var stone = M("StoneDark");
            Shapes.Box(root, "PillarL", new Vector3(-3f, 3f, 0f), new Vector3(2f, 6f, 2f), stone);
            Shapes.Box(root, "PillarR", new Vector3(3f, 3f, 0f), new Vector3(2f, 6f, 2f), stone);
            Shapes.Box(root, "Arch", new Vector3(0f, 5.5f, 0f), new Vector3(8f, 1.5f, 2f), stone);
            Shapes.Box(root, "DoorL", new Vector3(-1.6f, 2.2f, 0.6f), Quaternion.Euler(0f, 60f, 0f), new Vector3(2f, 4.4f, 0.2f), M("Wood"));
            Shapes.Box(root, "DoorR", new Vector3(1.6f, 2.2f, 0.6f), Quaternion.Euler(0f, -60f, 0f), new Vector3(2f, 4.4f, 0.2f), M("Wood"));
            return root;
        }

        public static Transform CastleKeep(Transform parent)
        {
            var root = Root(parent, "BP_CastleKeep");
            Shapes.Box(root, "Keep", new Vector3(0f, 5f, 0f), new Vector3(10f, 10f, 10f), M("Stone"));
            Shapes.Box(root, "Roof", new Vector3(0f, 10.3f, 0f), new Vector3(10.6f, 0.6f, 10.6f), M("StoneDark"), false);
            Shapes.Box(root, "Door", new Vector3(0f, 1.5f, 5.05f), new Vector3(2f, 3f, 0.1f), M("Wood"), false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var dir = Quaternion.Euler(0f, a, 0f);
                Shapes.Box(root, "Window", dir * new Vector3(0f, 6.5f, 5.05f), dir, new Vector3(1f, 1.6f, 0.08f), M("Window"), false);
            }
            return root;
        }

        // ---------------- Природа ----------------

        public static Transform Oak(Transform parent, float scale = 1f, bool autumn = false)
        {
            var root = Root(parent, autumn ? "BP_Oak_Autumn" : "BP_Oak");
            root.localScale = Vector3.one * scale;
            Shapes.Cylinder(root, "Trunk", new Vector3(0f, 1.6f, 0f), 0.6f, 3.2f, M("Bark"));
            var leaves = M(autumn ? "LeavesAutumn" : "Leaves");
            Shapes.Sphere(root, "Crown", new Vector3(0f, 4.2f, 0f), new Vector3(4f, 3.2f, 4f), leaves, false);
            Shapes.Sphere(root, "Crown2", new Vector3(1.1f, 3.6f, 0.6f), new Vector3(2.6f, 2.2f, 2.6f), leaves, false);
            Shapes.Sphere(root, "Crown3", new Vector3(-0.9f, 3.8f, -0.8f), new Vector3(2.4f, 2f, 2.4f), leaves, false);
            return root;
        }

        public static Transform Pine(Transform parent, float scale = 1f)
        {
            var root = Root(parent, "BP_Pine");
            root.localScale = Vector3.one * scale;
            Shapes.Cylinder(root, "Trunk", new Vector3(0f, 1.2f, 0f), 0.45f, 2.4f, M("Bark"));
            var needles = M("PineNeedles");
            for (int i = 0; i < 4; i++)
            {
                float d = 3.2f - i * 0.7f;
                Shapes.Cylinder(root, "Tier", new Vector3(0f, 2f + i * 1.15f, 0f), d, 1.2f, needles, false);
            }
            Shapes.Cylinder(root, "Tip", new Vector3(0f, 6.6f, 0f), 0.5f, 0.8f, needles, false);
            return root;
        }

        public static Transform Bush(Transform parent, float scale = 1f)
        {
            var root = Root(parent, "BP_Bush");
            root.localScale = Vector3.one * scale;
            var leaves = M("Leaves");
            Shapes.Sphere(root, "A", new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 1.4f), leaves);
            Shapes.Sphere(root, "B", new Vector3(0.6f, 0.4f, 0.3f), new Vector3(1f, 0.8f, 1f), leaves, false);
            Shapes.Sphere(root, "C", new Vector3(-0.5f, 0.35f, -0.3f), new Vector3(0.9f, 0.7f, 0.9f), leaves, false);
            return root;
        }

        public static Transform Rock(Transform parent, float scale = 1f, float yaw = 0f)
        {
            var root = Root(parent, "BP_Rock");
            root.localScale = Vector3.one * scale;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Shapes.Box(root, "Rock", new Vector3(0f, 0.35f, 0f), Quaternion.Euler(12f, 20f, 8f), new Vector3(1.4f, 0.9f, 1.1f), M("Stone"));
            Shapes.Box(root, "Rock2", new Vector3(0.55f, 0.2f, 0.35f), Quaternion.Euler(-6f, 50f, 14f), new Vector3(0.7f, 0.5f, 0.6f), M("StoneMossy"), false);
            return root;
        }

        public static Transform GrassTuft(Transform parent, bool dry = false)
        {
            var root = Root(parent, "BP_Grass");
            var grass = M(dry ? "GrassDry" : "Grass");
            for (int i = 0; i < 3; i++)
                Shapes.Box(root, "Blade", new Vector3(0f, 0.25f, 0f), Quaternion.Euler(0f, i * 60f, 0f), new Vector3(0.6f, 0.5f, 0.04f), grass, false);
            return root;
        }

        // ---------------- Игровые модели ----------------

        /// <summary>Модель манекена (BP_TrainingDummy).</summary>
        public static Transform DummyModel(Transform parent)
        {
            var root = Root(parent, "Model");
            var mat = M("Dummy");
            Shapes.Cylinder(root, "Post", new Vector3(0f, 0.6f, 0f), 0.12f, 1.2f, M("Wood"), false);
            Shapes.Cylinder(root, "Body", new Vector3(0f, 1.25f, 0f), 0.6f, 0.9f, mat, false);
            Shapes.Box(root, "Arms", new Vector3(0f, 1.45f, 0f), new Vector3(1.3f, 0.14f, 0.14f), mat, false);
            Shapes.Sphere(root, "Head", new Vector3(0f, 1.95f, 0f), 0.42f, mat, false);
            Shapes.Cylinder(root, "Base", new Vector3(0f, 0.05f, 0f), 0.8f, 0.1f, M("Wood"), false);
            return root;
        }

        /// <summary>Модель сундука; возвращает шарнир крышки.</summary>
        public static Transform ChestModel(Transform parent, out Transform lid)
        {
            var root = Root(parent, "Model");
            Shapes.Box(root, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f), M("Wood"), false);
            Shapes.Box(root, "Band", new Vector3(0f, 0.3f, 0f), new Vector3(1.12f, 0.1f, 0.72f), M("Hero_Steel"), false);
            lid = Shapes.Group(root, "LidHinge", new Vector3(0f, 0.6f, -0.35f));
            Shapes.Box(lid, "Lid", new Vector3(0f, 0.1f, 0.35f), new Vector3(1.12f, 0.2f, 0.72f), M("Wood"), false);
            Shapes.Box(lid, "Lock", new Vector3(0f, 0.02f, 0.72f), new Vector3(0.16f, 0.2f, 0.06f), M("Hero_Accent"), false);
            return root;
        }

        /// <summary>Модель лута: кристалл, луч, свет. Возвращает кристалл (вращается).</summary>
        public static Transform LootModel(Transform parent, out Transform gem)
        {
            var root = Root(parent, "Model");
            gem = Shapes.Group(root, "Gem", new Vector3(0f, 0.55f, 0f));
            Shapes.Box(gem, "Crystal", Vector3.zero, Quaternion.Euler(45f, 0f, 45f), new Vector3(0.32f, 0.32f, 0.32f), M("LootGlow"), false);
            var beam = Shapes.Cylinder(root, "Beam", new Vector3(0f, 2.2f, 0f), 0.12f, 4.4f, M("LootBeam"), false);
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.8f, 0.35f);
            light.shadows = LightShadows.None;
            return root;
        }
    }
}
