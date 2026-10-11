using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TestedField.EditorTools
{
    public static partial class ProjectSetup
    {
        public class PrefabSet
        {
            public GameObject Hero, MobGrunt, BossBrute, BossCaster, BossWarden, BossFinal, Dummy, LootPickup, Chest, Projectile;
            public PassageGate Gate;
            public SlotContent Poi, NpcCamp, Dungeon, Quest;
            public BossArena Arena, ArenaFinal;
            public readonly Dictionary<string, GameObject> Env = new Dictionary<string, GameObject>();

            public IEnumerable<GameObject> NetworkPrefabs()
            {
                yield return Hero;
                yield return MobGrunt;
                yield return BossBrute;
                yield return BossCaster;
                yield return BossWarden;
                yield return BossFinal;
                yield return Dummy;
                yield return LootPickup;
                yield return Chest;
                yield return Projectile;
            }
        }

        /// <param name="isUpToDate">Существующий префаб пересоздаётся и без force, если вернёт false (миграция).</param>
        static GameObject SavePrefab(string path, Func<GameObject> build, Func<GameObject, bool> isUpToDate = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !force && (isUpToDate == null || isUpToDate(existing)))
                return existing;
            var go = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AddNetworkTransform(GameObject go, bool ownerAuthority)
        {
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = ownerAuthority ? NetworkTransform.AuthorityModes.Owner : NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            nt.Interpolate = true;
        }

        static PrefabSet CreatePrefabs(DataSet d)
        {
            var p = new PrefabSet();
            string net = PrefabDir + "/Network";

            p.Projectile = SavePrefab($"{net}/SkillProjectile.prefab", () =>
            {
                var go = new GameObject("SkillProjectile");
                go.AddComponent<NetworkObject>();
                var proj = go.AddComponent<SkillProjectile>();
                proj.Config = d.SkillConfig;
                proj.DefaultMaterial = mats["MI_Skill_Fireball"];
                proj.Model = Shapes.Sphere(go.transform, "Model", Vector3.zero, 0.6f, mats["MI_Skill_Fireball"], false).transform;
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.2f);
                light.range = 4f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
                return go;
            });

            p.Hero = SavePrefab($"{net}/Hero.prefab", () =>
            {
                var go = new GameObject("Hero");
                go.AddComponent<NetworkObject>();
                var cc = go.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.stepOffset = 0.3f;
                cc.slopeLimit = 45f;
                cc.skinWidth = 0.05f;
                var stats = go.AddComponent<HeroStats>();
                go.AddComponent<HeroController>();
                var inv = go.AddComponent<Inventory>();
                inv.Config = d.InventoryConfig;
                var skills = go.AddComponent<Skills>();
                skills.Config = d.SkillConfig;
                skills.ProjectilePrefab = p.Projectile;
                var visual = go.AddComponent<HeroVisual>();
                HeroVisual.Build(visual);
                stats.FlashRoot = visual.Pivot.gameObject;
                stats.HitFlashMaterial = mats["M_HitFlash"];
                AddNetworkTransform(go, true);
                return go;
            });

            p.MobGrunt = SavePrefab($"{net}/Mob_Grunt.prefab", () => BuildMob("Mob_Grunt", d.MobGrunt, false));
            p.BossBrute = SavePrefab($"{net}/Boss_Brute.prefab", () => BuildMob("Boss_Brute", d.BossBrute, true));
            p.BossCaster = SavePrefab($"{net}/Boss_Caster.prefab", () => BuildMob("Boss_Caster", d.BossCaster, true));
            p.BossWarden = SavePrefab($"{net}/Boss_Warden.prefab", () => BuildMob("Boss_Warden", d.BossWarden, true));
            p.BossFinal = SavePrefab($"{net}/Boss_Final.prefab", () => BuildMob("Boss_Final", d.BossFinal, true));

            p.Dummy = SavePrefab($"{net}/TrainingDummy.prefab", () =>
            {
                var go = new GameObject("TrainingDummy");
                go.AddComponent<NetworkObject>();
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = 0.4f;
                cap.height = 2.2f;
                cap.center = new Vector3(0f, 1.1f, 0f);
                var stats = go.AddComponent<HeroStats>();
                stats.MaxHealth = 100f;
                stats.MaxMana = 0f;
                stats.MaxStamina = 0f;
                stats.ManaRegenRate = 0f;
                stats.StaminaRegenRate = 0f;
                stats.KnockbackSpeed = 0f;
                var dummy = go.AddComponent<TrainingDummy>();
                var model = EnvKit.DummyModel(go.transform);
                dummy.Model = model.gameObject;
                stats.FlashRoot = model.gameObject;
                return go;
            });

            p.LootPickup = SavePrefab($"{net}/LootPickup.prefab", () =>
            {
                var go = new GameObject("LootPickup");
                go.AddComponent<NetworkObject>();
                var pickup = go.AddComponent<LootPickup>();
                EnvKit.LootModel(go.transform, out var gem);
                pickup.Gem = gem;
                return go;
            });

            p.Chest = SavePrefab($"{net}/Chest.prefab", () =>
            {
                var go = new GameObject("Chest");
                go.AddComponent<NetworkObject>();
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.4f, 0f);
                col.size = new Vector3(1.1f, 0.8f, 0.7f);
                var chest = go.AddComponent<Chest>();
                chest.InteractRadius = 1.8f;
                chest.LootTable = d.LootChest;
                EnvKit.ChestModel(go.transform, out var lid);
                chest.Lid = lid;
                return go;
            });

            // ---- Локальные префабы карты забега ----
            string zones = PrefabDir + "/Zones";
            p.Gate = SavePrefab($"{zones}/PassageGate.prefab", () =>
            {
                var go = new GameObject("PassageGate");
                var gate = go.AddComponent<PassageGate>();
                var barrier = Shapes.Box(go.transform, "Barrier", new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 0.4f), mats["MI_Wood"]);
                var obstacle = barrier.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                gate.Barrier = barrier.transform;
                return go;
            }).GetComponent<PassageGate>();

            SlotContent Content<T>(string name, Color color, string text) where T : SlotContent => SavePrefab($"{zones}/{name}.prefab", () =>
            {
                var go = new GameObject(name);
                var c = go.AddComponent<T>();
                c.PlaceholderColor = color;
                c.PlaceholderText = text;
                return go;
            }, existing => existing.TryGetComponent<SlotContent>(out var c) && c.GetType() == typeof(T) && c.PlaceholderText == text)
                .GetComponent<SlotContent>();
            p.Poi = Content<PointOfInterest>("SlotContent_POI", new Color(0.95f, 0.85f, 0.3f), "Точка интереса");
            p.NpcCamp = Content<SlotContent>("SlotContent_NpcCamp", new Color(0.4f, 0.9f, 0.4f), "Лагерь НПС\n(не сделано, P1-03)");
            p.Dungeon = Content<SlotContent>("SlotContent_DungeonEntrance", new Color(0.7f, 0.45f, 0.95f), "Вход в данж\n(не сделано, P1-01)");
            p.Quest = Content<QuestObject>("SlotContent_QuestObject", new Color(0.35f, 0.85f, 1f), "Квест зоны (E)");

            BossArena Arena(string name, float radius) => SavePrefab($"{zones}/{name}.prefab", () =>
            {
                var go = new GameObject(name);
                var a = go.AddComponent<BossArena>();
                a.BuildPlaceholder = false;
                a.ArenaRadius = radius;
                return go;
            }).GetComponent<BossArena>();
            p.Arena = Arena("BossArena", 7.5f);
            p.ArenaFinal = Arena("BossArena_Final", 10.5f);

            // ---- Окружение тестовой карты ----
            string env = PrefabDir + "/Environment";
            void Env(string name, Func<Transform> build) => p.Env[name] = SavePrefab($"{env}/{name}.prefab", () => build().gameObject);
            Env("House", () => EnvKit.House(null));
            Env("House_Thatch", () => EnvKit.House(null, 5f, 4.5f, true));
            Env("Well", () => EnvKit.Well(null));
            Env("Barrel", () => EnvKit.Barrel(null));
            Env("Crate", () => EnvKit.Crate(null));
            Env("Fence", () => EnvKit.Fence(null));
            Env("Haystack", () => EnvKit.Haystack(null));
            Env("CastleWall", () => EnvKit.CastleWall(null));
            Env("CastleWallRuin", () => EnvKit.CastleWall(null, 8f, true));
            Env("CastleTower", () => EnvKit.CastleTower(null));
            Env("CastleTowerRuin", () => EnvKit.CastleTower(null, true));
            Env("CastleGate", () => EnvKit.CastleGate(null));
            Env("CastleKeep", () => EnvKit.CastleKeep(null));
            Env("Oak", () => EnvKit.Oak(null));
            Env("Oak_Autumn", () => EnvKit.Oak(null, 1f, true));
            Env("Pine", () => EnvKit.Pine(null));
            Env("Bush", () => EnvKit.Bush(null));
            Env("Rock", () => EnvKit.Rock(null));
            Env("Grass", () => EnvKit.GrassTuft(null));
            Env("GrassDry", () => EnvKit.GrassTuft(null, true));

            AssetDatabase.SaveAssets();
            foreach (var prefab in p.NetworkPrefabs())
                RefreshNetworkObjectHash(prefab);
            return p;
        }

        static GameObject BuildMob(string name, MobData data, bool boss)
        {
            var go = new GameObject(name);
            go.AddComponent<NetworkObject>();
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = 0.4f;
            cap.height = 1.8f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = data.PatrolSpeed;
            agent.angularSpeed = 540f;
            agent.acceleration = 24f;
            agent.stoppingDistance = 0.3f;
            agent.enabled = false; // включает MobBase на хосте, когда navmesh построен
            var stats = go.AddComponent<HeroStats>();
            stats.MaxHealth = data.MaxHealth;
            stats.MaxMana = 0f;
            stats.MaxStamina = 0f;
            stats.ManaRegenRate = 0f;
            stats.StaminaRegenRate = 0f;
            stats.KnockbackSpeed = data.KnockbackSpeed;
            stats.HitFlashMaterial = mats["M_HitFlash"];

            MobBase mob = boss ? go.AddComponent<BossBase>() : go.AddComponent<MobBase>();
            mob.MobData = data;

            var visual = go.AddComponent<MobVisual>();
            MobVisual.Build(visual, boss);
            visual.SetBodyMaterial(data.BodyMaterial);
            if (data is BossData bossData)
                visual.SetScale(bossData.VisualScale);
            stats.FlashRoot = visual.Pivot.gameObject;

            AddNetworkTransform(go, false);
            return go;
        }

        /// <summary>
        /// GlobalObjectIdHash сетевого префаба считается в NetworkObject.OnValidate по GUID ассета —
        /// у только что созданного префаба его ещё нет, поэтому пересчитываем явно.
        /// </summary>
        static void RefreshNetworkObjectHash(GameObject prefab)
        {
            if (prefab == null)
                return;
            var no = prefab.GetComponent<NetworkObject>();
            if (no == null)
                return;
            InvokeOnValidate(no);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        static void InvokeOnValidate(NetworkObject no)
        {
            var method = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(no, null);
        }

        // ================= Связи данных и префабов =================

        static void LinkData(DataSet d, PrefabSet p, GameAssets ga)
        {
            void Bosses(ZoneData zone, GameObject boss)
            {
                if (zone.BossPrefabs.Count > 0 && !force)
                    return;
                zone.BossPrefabs = new List<BossBase> { boss.GetComponent<BossBase>() };
                EditorUtility.SetDirty(zone);
            }
            Bosses(d.ZoneStart, p.BossBrute);
            Bosses(d.ZoneA, p.BossCaster);
            Bosses(d.ZoneB, p.BossWarden);
            Bosses(d.ZoneFinal, p.BossFinal);

            void Contents(SlotFillRule rule, SlotContent prefab)
            {
                // Перепривязать и после пересоздания префаба (ссылка на старый компонент становится пустой).
                bool broken = rule.Contents.Exists(c => c.Prefab == null);
                if (rule.Contents.Count > 0 && !force && !broken)
                    return;
                rule.Contents = new List<SlotContentOption> { new SlotContentOption { Prefab = prefab, Weight = 1f } };
                EditorUtility.SetDirty(rule);
            }
            Contents(d.FillPoi, p.Poi);
            Contents(d.FillNpcCamp, p.NpcCamp);
            Contents(d.FillDungeon, p.Dungeon);
            Contents(d.FillQuest, p.Quest);
            Contents(d.FillBossArena, p.Arena);
            Contents(d.FillFinalArena, p.ArenaFinal);

            if (d.RunMap.PassageGatePrefab == null || force)
            {
                d.RunMap.PassageGatePrefab = p.Gate;
                EditorUtility.SetDirty(d.RunMap);
            }

            ga.ItemDatabase = d.ItemDatabase;
            ga.SkillConfig = d.SkillConfig;
            ga.HeroPrefab = p.Hero;
            ga.LootPickupPrefab = p.LootPickup;
            ga.ChestPrefab = p.Chest;
            ga.SkillProjectilePrefab = p.Projectile;
            EditorUtility.SetDirty(ga);
            AssetDatabase.SaveAssets();
        }

        // ================= Сеть =================

        const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

        static NetworkPrefabsList GetNetworkPrefabsList()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);
            }
            return list;
        }

        static void RegisterNetworkPrefabs(PrefabSet p)
        {
            var list = GetNetworkPrefabsList();
            foreach (var prefab in p.NetworkPrefabs())
            {
                bool present = false;
                foreach (var entry in list.PrefabList)
                    if (entry.Prefab == prefab)
                        present = true;
                if (!present)
                    list.Add(new NetworkPrefab { Prefab = prefab });
            }
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        static void CreateNetworkManagerPrefab()
        {
            SavePrefab($"{ResourcesDir}/NetworkManager.prefab", () =>
            {
                var go = new GameObject("NetworkManager");
                var nm = go.AddComponent<NetworkManager>();
                var transport = go.AddComponent<UnityTransport>();
                transport.SetConnectionData("127.0.0.1", NetSession.DefaultPort, "0.0.0.0");
                nm.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    PlayerPrefab = null,
                    ConnectionApproval = true,
                    EnableSceneManagement = true,
                    TickRate = 30,
                };
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { GetNetworkPrefabsList() };
                return go;
            });
        }
    }
}
