using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TestedField.EditorTools
{
    public static partial class ProjectSetup
    {
        static readonly string[] SceneNames = { NetSession.SceneMainMenu, NetSession.SceneLobby, NetSession.SceneRun, NetSession.SceneTestField };

        static string ScenePath(string name) => $"{SceneDir}/{name}.unity";

        static void CreateScenes(PrefabSet p, DataSet d)
        {
            // Build Settings до создания сцен: NetworkObject на сцене считается in-scene placed только в сценах из списка.
            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneNames)
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath(name), true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            MakeScene(NetSession.SceneMainMenu, () =>
            {
                SetupCamera(true);
                new GameObject("MainMenuUI").AddComponent<MainMenuUI>();
            });

            MakeScene(NetSession.SceneLobby, () =>
            {
                SetupCamera(true);
                var state = new GameObject("LobbyState");
                state.AddComponent<NetworkObject>();
                state.AddComponent<LobbyState>();
                new GameObject("LobbyUI").AddComponent<LobbyUI>();
            });

            MakeScene(NetSession.SceneRun, () =>
            {
                SetupCamera(false);
                CreateGameObjects();
                var map = new GameObject("RunMap");
                map.AddComponent<NetworkObject>();
                var runMap = map.AddComponent<RunMap>();
                runMap.RunMapData = d.RunMap;
                // Запасная точка появления, если карта не собралась.
                var ps = new GameObject("PlayerStart_Fallback");
                ps.AddComponent<PlayerStart>();
                ps.transform.position = new Vector3(6f, 0f, -12f);
            });

            MakeScene(NetSession.SceneTestField, () =>
            {
                SetupCamera(false);
                CreateGameObjects();
                BuildTestField(p);
            });

            // Пересчёт GlobalObjectIdHash для NetworkObject на сценах (нужен GUID сохранённой сцены).
            foreach (var name in SceneNames)
                FixInScenePlacedObjects(ScenePath(name));

            EditorSceneManager.OpenScene(ScenePath(NetSession.SceneTestField), OpenSceneMode.Single);
        }

        static void MakeScene(string name, Action build)
        {
            string path = ScenePath(name);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null && !force)
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            build();
            EditorSceneManager.SaveScene(scene, path);
        }

        static void FixInScenePlacedObjects(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool any = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var no in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    InvokeOnValidate(no);
                    if (PrefabUtility.IsPartOfPrefabInstance(no))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(no);
                    EditorUtility.SetDirty(no);
                    any = true;
                }
            }
            if (any)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static void SetupCamera(bool menu)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                if (menu)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                }
                cam.transform.SetPositionAndRotation(new Vector3(-8f, 14f, -18f), Quaternion.Euler(50f, 45f, 0f));
                cam.fieldOfView = 40f;
                cam.farClipPlane = 400f;
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                light.intensity = 1.6f;
                light.shadows = LightShadows.Soft;
                light.color = new Color(1f, 0.96f, 0.88f);
            }
        }

        /// <summary>Объект «Game»: GameMode + RunTimer + RunGameState (аналог GameMode/GameState UE), HUD, автохост.</summary>
        static void CreateGameObjects()
        {
            var game = new GameObject("Game");
            game.AddComponent<NetworkObject>();
            game.AddComponent<GameMode>();
            game.AddComponent<RunTimer>();
            game.AddComponent<RunGameState>();

            new GameObject("HUD").AddComponent<HeroHUD>();
            new GameObject("DirectPlayHost").AddComponent<DirectPlayHost>();
        }

        // ================= Тестовая карта (EmptyMap в UE) =================

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (!Mathf.Approximately(scale, 1f))
                go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void BuildTestField(PrefabSet p)
        {
            // Окружение: navmesh строится при запуске по коллайдерам потомков.
            var env = new GameObject("Environment");
            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            env.AddComponent<RuntimeNavMesh>();

            var ground = Shapes.Box(env.transform, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(140f, 0.2f, 140f), mats["MI_Ground"]);
            ground.isStatic = true;
            var bounds = Shapes.Group(env.transform, "Bounds");
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(0f, 2f, 70.5f), new Vector3(142f, 4f, 1f)),
                         (new Vector3(0f, 2f, -70.5f), new Vector3(142f, 4f, 1f)),
                         (new Vector3(70.5f, 2f, 0f), new Vector3(1f, 4f, 142f)),
                         (new Vector3(-70.5f, 2f, 0f), new Vector3(1f, 4f, 142f)),
                     })
            {
                var wall = new GameObject("Bound");
                wall.transform.SetParent(bounds, false);
                wall.transform.position = pos;
                wall.AddComponent<BoxCollider>().size = size;
            }

            var e = p.Env;
            var village = Shapes.Group(env.transform, "Village");
            Place(e["House"], village, new Vector3(28f, 0f, -12f));
            Place(e["House"], village, new Vector3(40f, 0f, -12f));
            Place(e["House"], village, new Vector3(28f, 0f, 12f), 180f);
            Place(e["House_Thatch"], village, new Vector3(41f, 0f, 12f), 180f);
            Place(e["House_Thatch"], village, new Vector3(51f, 0f, 0f), -90f);
            Place(e["Well"], village, new Vector3(36f, 0f, 0f));
            Place(e["Barrel"], village, new Vector3(31.5f, 0f, -7.5f));
            Place(e["Barrel"], village, new Vector3(32.4f, 0f, -7.9f));
            Place(e["Barrel"], village, new Vector3(44.5f, 0f, 7.5f));
            Place(e["Crate"], village, new Vector3(45f, 0f, -7f), 15f);
            Place(e["Crate"], village, new Vector3(46f, 0f, -6.6f), -10f);
            Place(e["Crate"], village, new Vector3(45.5f, 0f, -6.8f), 30f, 0.7f).transform.position += Vector3.up * 0.9f;
            Place(e["Haystack"], village, new Vector3(23f, 0f, 5f));
            for (int i = 0; i < 5; i++)
                Place(e["Fence"], village, new Vector3(22f + i * 4f, 0f, -20f));
            for (int i = 0; i < 4; i++)
                Place(e["Fence"], village, new Vector3(22f + i * 4f, 0f, 20f));

            var field = Shapes.Group(env.transform, "Field");
            for (int i = 0; i < 7; i++)
                Place(e["Fence"], field, new Vector3(-14f + i * 4f, 0f, -52f));
            for (int i = 0; i < 4; i++)
                Place(e["Fence"], field, new Vector3(-16f, 0f, -50f + i * 4f), 90f);
            for (int i = 0; i < 4; i++)
                Place(e["Fence"], field, new Vector3(16f, 0f, -50f + i * 4f), 90f);
            Place(e["Haystack"], field, new Vector3(-8f, 0f, -44f));
            Place(e["Haystack"], field, new Vector3(7f, 0f, -47f));
            Place(e["Haystack"], field, new Vector3(-2f, 0f, -38f));

            var castle = Shapes.Group(env.transform, "Castle");
            foreach (float x in new[] { -16f, -8f, 8f, 16f })
                Place(e["CastleWall"], castle, new Vector3(x, 0f, 55f));
            Place(e["CastleGate"], castle, new Vector3(0f, 0f, 55f));
            Place(e["CastleTower"], castle, new Vector3(-22f, 0f, 55f));
            Place(e["CastleTower"], castle, new Vector3(22f, 0f, 55f));
            Place(e["CastleWall"], castle, new Vector3(-22f, 0f, 61f), 90f);
            Place(e["CastleWall"], castle, new Vector3(22f, 0f, 61f), 90f);
            Place(e["CastleKeep"], castle, new Vector3(0f, 0f, 64f));
            Place(e["CastleWallRuin"], castle, new Vector3(-38f, 0f, 46f), 20f);
            Place(e["CastleTowerRuin"], castle, new Vector3(36f, 0f, 48f));

            // Роща на западе и разбросанная природа (фиксированный seed — сцена одинаковая при пересборке).
            var nature = Shapes.Group(env.transform, "Nature");
            var rng = new RandomStream(42);
            bool Free(float x, float z)
            {
                if (Mathf.Abs(x) < 14f && z > -26f && z < -4f) return false;   // старт и манекены
                if (x > 18f && x < 56f && z > -22f && z < 22f) return false;   // деревня
                if (Mathf.Abs(x) < 26f && z > 44f) return false;               // замок
                if (Mathf.Abs(x) < 18f && z < -30f && z > -54f) return false;  // поле
                return Mathf.Abs(x) < 66f && Mathf.Abs(z) < 66f;
            }
            for (int i = 0; i < 40; i++)
            {
                float x = rng.FRandRange(-64f, -26f), z = rng.FRandRange(-30f, 34f);
                if (!Free(x, z)) continue;
                string kind = rng.GetFraction() < 0.5f ? "Pine" : (rng.GetFraction() < 0.3f ? "Oak_Autumn" : "Oak");
                Place(e[kind], nature, new Vector3(x, 0f, z), rng.FRandRange(0f, 360f), rng.FRandRange(0.8f, 1.25f));
            }
            for (int i = 0; i < 26; i++)
            {
                float x = rng.FRandRange(-66f, 66f), z = rng.FRandRange(-66f, 66f);
                if (!Free(x, z) || (x < -24f && z > -32f && z < 36f)) continue;
                Place(e[rng.GetFraction() < 0.4f ? "Oak" : "Pine"], nature, new Vector3(x, 0f, z), rng.FRandRange(0f, 360f), rng.FRandRange(0.8f, 1.2f));
            }
            for (int i = 0; i < 40; i++)
            {
                float x = rng.FRandRange(-66f, 66f), z = rng.FRandRange(-66f, 66f);
                if (!Free(x, z)) continue;
                Place(e[rng.GetFraction() < 0.6f ? "Bush" : "Rock"], nature, new Vector3(x, 0f, z), rng.FRandRange(0f, 360f), rng.FRandRange(0.7f, 1.4f));
            }
            for (int i = 0; i < 160; i++)
            {
                float x = rng.FRandRange(-68f, 68f), z = rng.FRandRange(-68f, 68f);
                bool inField = Mathf.Abs(x) < 16f && z < -32f && z > -52f;
                if (!inField && !Free(x, z)) continue;
                Place(e[inField ? "GrassDry" : "Grass"], nature, new Vector3(x, 0f, z), rng.FRandRange(0f, 360f), rng.FRandRange(0.8f, 1.4f));
            }

            // Игровые объекты
            var gameplay = new GameObject("Gameplay").transform;
            for (int i = 0; i < 4; i++)
            {
                var ps = new GameObject($"PlayerStart_{i + 1}");
                ps.transform.SetParent(gameplay, false);
                ps.transform.position = new Vector3(-4.5f + i * 3f, 0f, -20f);
                ps.AddComponent<PlayerStart>();
            }
            Place(p.Dummy, gameplay, new Vector3(-4f, 0f, -11f), 180f).name = "Dummy_1";
            Place(p.Dummy, gameplay, new Vector3(0f, 0f, -11f), 180f).name = "Dummy_2";
            Place(p.Dummy, gameplay, new Vector3(4f, 0f, -11f), 180f).name = "Dummy_3";
            Place(p.Chest, gameplay, new Vector3(9f, 0f, -19f), -90f).name = "Chest_Test";
            Place(p.MobGrunt, gameplay, new Vector3(0f, 0f, -42f)).name = "Mob_Field";
            Place(p.MobGrunt, gameplay, new Vector3(35f, 0f, 6f)).name = "Mob_Village";
            Place(p.MobGrunt, gameplay, new Vector3(-38f, 0f, 4f)).name = "Mob_Grove";
        }
    }
}
