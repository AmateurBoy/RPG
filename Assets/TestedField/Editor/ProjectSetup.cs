using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TestedField.EditorTools
{
    /// <summary>
    /// Собирает проект из кода: материалы, данные (Data Assets с числами из UE-прототипа), префабы,
    /// список сетевых префабов, NetworkManager, сцены и Build Settings.
    /// «Setup Project» создаёт только недостающее (правки дизайнеров не трогаются),
    /// «Rebuild Generated Assets» пересоздаёт всё заново.
    /// </summary>
    public static partial class ProjectSetup
    {
        public const string Root = "Assets/TestedField";
        const string MatDir = Root + "/Materials";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ResourcesDir = Root + "/Resources";

        static bool force;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("TestedField/Setup Project (create missing)", priority = 1)]
        public static void SetupMissing() => Run(false);

        [MenuItem("TestedField/Rebuild Generated Assets (overwrite)", priority = 2)]
        public static void RebuildAllMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild generated assets",
                    "Пересоздать материалы, данные, префабы и сцены TestedField? Ручные правки в них будут потеряны.", "Пересоздать", "Отмена"))
                Run(true);
        }

        /// <summary>Точка входа и для пакетного запуска (без диалогов).</summary>
        public static void Run(bool overwrite)
        {
            force = overwrite;
            // Не внутри StartAssetEditing: там IsValidFolder не видит только что созданные папки
            // и CreateFolder плодит «Data 1», «Data 2»…
            EnsureFolders();

            CreateMaterials();
            var gameAssets = CreateGameAssets();
            var data = CreateData();
            var prefabs = CreatePrefabs(data);
            LinkData(data, prefabs, gameAssets);
            RegisterNetworkPrefabs(prefabs);
            CreateNetworkManagerPrefab();
            CreateScenes(prefabs, data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Setup] TestedField project setup done (overwrite: {overwrite})");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[]
                     {
                         MatDir, DataDir, DataDir + "/Items", DataDir + "/Slots", DataDir + "/Skills", DataDir + "/Loot",
                         DataDir + "/Mobs", DataDir + "/Bosses", DataDir + "/Bosses/Attacks", DataDir + "/Zones",
                         DataDir + "/Zones/Layouts", DataDir + "/Zones/FillRules",
                         PrefabDir, PrefabDir + "/Network", PrefabDir + "/Zones", PrefabDir + "/Environment",
                         SceneDir, ResourcesDir,
                     })
                EnsureFolder(dir);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ================= Ассеты =================

        static T Asset<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && !force)
                return asset;
            bool isNew = asset == null;
            if (isNew)
                asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            if (isNew)
                AssetDatabase.CreateAsset(asset, path);
            else
                EditorUtility.SetDirty(asset);
            return asset;
        }

        // ================= Материалы =================

        static void CreateMaterials()
        {
            mats.Clear();
            // Окружение
            Lit("MI_Bark", C(0.36f, 0.25f, 0.16f));
            Lit("MI_Dirt", C(0.47f, 0.37f, 0.25f));
            Lit("MI_GrassDry", C(0.66f, 0.62f, 0.36f));
            Lit("MI_Grass", C(0.34f, 0.55f, 0.24f));
            Lit("MI_Hay", C(0.86f, 0.74f, 0.4f));
            Lit("MI_LeavesAutumn", C(0.82f, 0.45f, 0.15f));
            Lit("MI_Leaves", C(0.25f, 0.5f, 0.2f));
            Lit("MI_PineNeedles", C(0.15f, 0.36f, 0.2f));
            Lit("MI_Plaster", C(0.88f, 0.85f, 0.76f));
            Lit("MI_RoofThatch", C(0.6f, 0.5f, 0.3f));
            Lit("MI_RoofTile", C(0.62f, 0.27f, 0.2f));
            Lit("MI_StoneDark", C(0.33f, 0.33f, 0.36f));
            Lit("MI_StoneMossy", C(0.42f, 0.48f, 0.38f));
            Lit("MI_Stone", C(0.58f, 0.57f, 0.55f));
            Lit("MI_Timber", C(0.3f, 0.2f, 0.12f));
            Lit("MI_Water", C(0.2f, 0.42f, 0.6f), 0.9f);
            Lit("MI_Window", C(0.25f, 0.35f, 0.45f), 0.8f);
            Lit("MI_Wood", C(0.55f, 0.38f, 0.22f));
            // Герой
            Lit("MI_Hero_Accent", C(0.9f, 0.7f, 0.2f), 0.4f);
            Lit("MI_Hero_Body", C(0.2f, 0.35f, 0.7f));
            Lit("MI_Hero_Head", C(0.95f, 0.78f, 0.65f));
            Lit("MI_Hero_Steel", C(0.75f, 0.77f, 0.8f), 0.65f, 0.8f);
            // Мобы и боссы
            Lit("MI_Mob_Accent", C(0.25f, 0.18f, 0.1f));
            Lit("MI_Mob_Body", C(0.45f, 0.55f, 0.25f));
            Lit("MI_Boss_Brute", C(0.6f, 0.32f, 0.2f));
            Lit("MI_Boss_Caster", C(0.38f, 0.26f, 0.62f));
            Lit("MI_Boss_Warden", C(0.3f, 0.46f, 0.52f), 0.4f, 0.3f);
            Lit("MI_Boss_Final", C(0.22f, 0.08f, 0.3f), 0.5f, 0f, C(0.35f, 0.05f, 0.5f));
            Lit("MI_Boss_Enraged", C(0.8f, 0.15f, 0.1f), 0.3f, 0f, C(0.6f, 0.05f, 0f));
            // Карта и пропсы
            Lit("MI_Dummy", C(0.8f, 0.7f, 0.45f));
            Lit("MI_Ground", C(0.38f, 0.5f, 0.28f));
            Lit("MI_Prop", C(0.6f, 0.6f, 0.6f));
            // Эффекты (overlay / прозрачные)
            Transparent("M_HitFlash", new Color(1f, 1f, 1f, 0.65f), false);
            Transparent("MI_Telegraph", new Color(1f, 0.1f, 0.05f, 0.35f), false);
            Lit("MI_LootGlow", C(1f, 0.8f, 0.3f), 0.8f, 0f, C(1.6f, 1.1f, 0.35f));
            Transparent("MI_LootBeam", new Color(1f, 0.8f, 0.35f, 0.35f), true);
            Transparent("MI_Skill_Dash", new Color(0.6f, 0.85f, 1f, 0.6f), false);
            Transparent("MI_Skill_Heal", new Color(0.4f, 1f, 0.5f, 0.6f), false);
            Transparent("MI_Skill_Fireball", new Color(1f, 0.5f, 0.12f, 0.9f), true);
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static Material Lit(string name, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !force)
                return mats[name] = m;

            bool isNew = m == null;
            if (isNew)
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (isNew)
                AssetDatabase.CreateAsset(m, path);
            else
                EditorUtility.SetDirty(m);
            return mats[name] = m;
        }

        /// <summary>URP Unlit, прозрачный (alpha или additive) — вспышки, телеграф, лучи.</summary>
        static Material Transparent(string name, Color color, bool additive)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !force)
                return mats[name] = m;

            bool isNew = m == null;
            if (isNew)
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (isNew)
                AssetDatabase.CreateAsset(m, path);
            else
                EditorUtility.SetDirty(m);
            return mats[name] = m;
        }

        static GameAssets CreateGameAssets()
        {
            var ga = Asset<GameAssets>($"{ResourcesDir}/GameAssets.asset", a =>
            {
                a.Materials = new List<Material>();
            });
            ga.Materials = new List<Material>();
            foreach (var kv in mats)
                if (kv.Key.StartsWith("MI_"))
                    ga.Materials.Add(kv.Value);
            ga.HitFlashMaterial = mats["M_HitFlash"];
            ga.TelegraphMaterial = mats["MI_Telegraph"];
            EditorUtility.SetDirty(ga);
            AssetDatabase.SaveAssets();
            GameAssets.ClearCache();
            return ga;
        }

        // ================= Данные =================

        public class DataSet
        {
            public ItemDatabase ItemDatabase;
            public InventoryConfig InventoryConfig;
            public SkillConfig SkillConfig;
            public LootTable LootMob, LootBoss, LootChest;
            public MobData MobGrunt;
            public BossData BossBrute, BossCaster, BossWarden, BossFinal;
            public ZoneData ZoneStart, ZoneA, ZoneB, ZoneFinal;
            public SlotFillRule FillPoi, FillNpcCamp, FillDungeon, FillBossArena, FillQuest, FillFinalArena;
            public RunMapData RunMap;
        }

        static DataSet CreateData()
        {
            var d = new DataSet();

            // ---- Слоты и предметы (P0-11) ----
            var slotWeapon = Asset<EquipSlotData>($"{DataDir}/Slots/DA_Slot_Weapon.asset", a => { a.SlotId = "Weapon"; a.DisplayName = "Оружие"; });
            var slotArmor = Asset<EquipSlotData>($"{DataDir}/Slots/DA_Slot_Armor.asset", a => { a.SlotId = "Armor"; a.DisplayName = "Броня"; });
            var slotAccessory = Asset<EquipSlotData>($"{DataDir}/Slots/DA_Slot_Accessory.asset", a => { a.SlotId = "Accessory"; a.DisplayName = "Аксессуар"; });

            var sword = Asset<ItemData>($"{DataDir}/Items/DA_Item_Sword.asset", a =>
            {
                a.ItemId = "Sword"; a.DisplayName = "Меч"; a.Description = "Простой стальной меч."; a.Category = "Weapon"; a.Slot = slotWeapon;
                a.FlatModifiers = new List<StatModifier> { new StatModifier("AttackDamage", 10f), new StatModifier("MaxStamina", 10f) };
                a.PercentModifiers = new List<StatModifier>();
                a.ActiveSkill = "DashStrike";
            });
            var staff = Asset<ItemData>($"{DataDir}/Items/DA_Item_Staff.asset", a =>
            {
                a.ItemId = "Staff"; a.DisplayName = "Посох"; a.Description = "Посох ученика мага."; a.Category = "Weapon"; a.Slot = slotWeapon;
                a.FlatModifiers = new List<StatModifier> { new StatModifier("AttackDamage", 3f), new StatModifier("MaxMana", 40f), new StatModifier("ManaRegenRate", 2f) };
                a.PercentModifiers = new List<StatModifier>();
                a.ActiveSkill = "Fireball";
            });
            var armor = Asset<ItemData>($"{DataDir}/Items/DA_Item_LeatherArmor.asset", a =>
            {
                a.ItemId = "LeatherArmor"; a.DisplayName = "Кожаная броня"; a.Description = "Лёгкая броня из выделанной кожи."; a.Category = "Armor"; a.Slot = slotArmor;
                a.FlatModifiers = new List<StatModifier> { new StatModifier("MaxHealth", 30f) };
                a.PercentModifiers = new List<StatModifier> { new StatModifier("StaminaRegenRate", 10f) };
                a.ActiveSkill = "Heal";
            });

            d.ItemDatabase = Asset<ItemDatabase>($"{DataDir}/Items/DA_ItemDatabase.asset", a =>
            {
                a.Items = new List<ItemData> { sword, staff, armor };
                a.Slots = new List<EquipSlotData> { slotWeapon, slotArmor, slotAccessory };
            });
            d.InventoryConfig = Asset<InventoryConfig>($"{DataDir}/Items/DA_InventoryConfig.asset", a =>
            {
                a.ItemDatabase = d.ItemDatabase;
                a.Capacity = 12;
                a.StartingItems = new List<string> { "Sword", "Staff" };
                a.StartingEquipment = new List<StartingEquipmentEntry> { new StartingEquipmentEntry { SlotId = "Armor", ItemId = "LeatherArmor" } };
            });

            // ---- Навыки (P0-17) ----
            var dash = Asset<SkillData>($"{DataDir}/Skills/DA_Skill_DashStrike.asset", a =>
            {
                a.SkillId = "DashStrike"; a.DisplayName = "Рывок"; a.Description = "Рывок вперёд, урон всем на пути.";
                a.EffectType = SkillEffectType.Dash; a.ManaCost = 15f; a.Cooldown = 5f;
                a.Damage = 30f; a.Distance = 5f; a.Radius = 0.8f;
                a.CastFlashMaterial = mats["MI_Skill_Dash"]; a.CastFlashDuration = 0.25f;
            });
            var heal = Asset<SkillData>($"{DataDir}/Skills/DA_Skill_Heal.asset", a =>
            {
                a.SkillId = "Heal"; a.DisplayName = "Лечение"; a.Description = "Лечит себя и союзников рядом.";
                a.EffectType = SkillEffectType.Heal; a.ManaCost = 25f; a.Cooldown = 10f;
                a.HealAmount = 35f; a.Radius = 4f;
                a.CastFlashMaterial = mats["MI_Skill_Heal"]; a.CastFlashDuration = 0.35f;
            });
            var fireball = Asset<SkillData>($"{DataDir}/Skills/DA_Skill_Fireball.asset", a =>
            {
                a.SkillId = "Fireball"; a.DisplayName = "Огненный шар"; a.Description = "Снаряд летит вперёд и бьёт первую цель.";
                a.EffectType = SkillEffectType.Projectile; a.ManaCost = 10f; a.Cooldown = 1.5f;
                a.Damage = 25f; a.Distance = 15f; a.Radius = 0.3f; a.Speed = 16f;
                a.CastFlashMaterial = mats["MI_Skill_Fireball"]; a.CastFlashDuration = 0.2f;
                a.ProjectileMaterial = mats["MI_Skill_Fireball"];
            });
            d.SkillConfig = Asset<SkillConfig>($"{DataDir}/Skills/DA_SkillConfig.asset", a =>
            {
                a.Skills = new List<SkillData> { dash, heal, fireball };
                a.SlotCount = 4;
                a.SlotKeys = new List<Key> { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            });

            // ---- Лут (P0-12) ----
            d.LootMob = Asset<LootTable>($"{DataDir}/Loot/DA_Loot_Mob.asset", a =>
            {
                a.Rolls = 1; a.DropChance = 0.5f; a.ScatterRadius = 0.6f;
                a.Entries = new List<LootEntry> { new LootEntry("Sword", 1f), new LootEntry("Staff", 1f), new LootEntry("LeatherArmor", 1f) };
            });
            d.LootBoss = Asset<LootTable>($"{DataDir}/Loot/DA_Loot_Boss.asset", a =>
            {
                a.Rolls = 3; a.DropChance = 1f; a.ScatterRadius = 1.5f;
                a.Entries = new List<LootEntry> { new LootEntry("Sword", 1f), new LootEntry("Staff", 1f), new LootEntry("LeatherArmor", 1f) };
            });
            d.LootChest = Asset<LootTable>($"{DataDir}/Loot/DA_Loot_Chest.asset", a =>
            {
                a.Rolls = 1; a.DropChance = 1f; a.ScatterRadius = 0.5f;
                a.Entries = new List<LootEntry> { new LootEntry("Sword", 1f), new LootEntry("Staff", 1f), new LootEntry("LeatherArmor", 2f, 1, 2) };
            });

            // ---- Моб (P0-03) ----
            d.MobGrunt = Asset<MobData>($"{DataDir}/Mobs/DA_Mob_Grunt.asset", a =>
            {
                a.MaxHealth = 60f; a.KnockbackSpeed = 3f;
                a.PatrolSpeed = 1.8f; a.ChaseSpeed = 3.8f; a.PatrolRadius = 6f; a.PatrolWaitTime = 2f;
                a.DetectionRadius = 9f; a.LoseTargetRadius = 15f; a.LeashRadius = 25f; a.ThinkInterval = 0.2f;
                a.AttackDamage = 15f; a.AttackRange = 1.7f; a.AttackHitDistance = 1.1f; a.AttackHitRadius = 1.1f;
                a.AttackWindup = 0.8f; a.AttackRecovery = 0.6f; a.AttackCooldown = 1f;
                a.CorpseLifetime = 3f;
                a.BodyMaterial = mats["MI_Mob_Body"];
                a.LootTable = d.LootMob;
            });

            // ---- Боссы (P0-09, P0-10) ----
            BossAttack Atk(string file, string id, BossAttackShape shape, float useRange, float hitDistance, float hitRadius, float damage, float windup, float recovery, float weight, int minPhase) =>
                Asset<BossAttack>($"{DataDir}/Bosses/Attacks/DA_BossAttack_{file}.asset", a =>
                {
                    a.AttackId = id; a.Shape = shape; a.UseRange = useRange; a.HitDistance = hitDistance; a.HitRadius = hitRadius;
                    a.Damage = damage; a.Windup = windup; a.Recovery = recovery; a.Weight = weight; a.MinPhase = minPhase;
                });

            var bruteAttacks = new List<BossAttack>
            {
                Atk("Brute_Swipe", "Swipe", BossAttackShape.Front, 3f, 1.6f, 1.6f, 20f, 0.8f, 0.7f, 2f, 1),
                Atk("Brute_Slam", "Slam", BossAttackShape.Front, 3.5f, 2.2f, 1.8f, 35f, 1.2f, 1f, 1f, 1),
                Atk("Brute_Stomp", "Stomp", BossAttackShape.Self, 3f, 0f, 3.5f, 25f, 1f, 1f, 1f, 2),
            };
            var casterAttacks = new List<BossAttack>
            {
                Atk("Caster_Swipe", "Swipe", BossAttackShape.Front, 3f, 1.5f, 1.5f, 18f, 0.7f, 0.6f, 1f, 1),
                Atk("Caster_Blast", "Blast", BossAttackShape.Target, 9f, 0f, 2f, 30f, 1.3f, 0.8f, 2f, 1),
                Atk("Caster_Nova", "Nova", BossAttackShape.Self, 4f, 0f, 4f, 22f, 1.2f, 1f, 1f, 2),
            };
            var wardenAttacks = new List<BossAttack>
            {
                Atk("Warden_Swipe", "Swipe", BossAttackShape.Front, 3f, 1.7f, 1.7f, 22f, 0.8f, 0.7f, 2f, 1),
                Atk("Warden_Smite", "Smite", BossAttackShape.Target, 8f, 0f, 2.2f, 28f, 1.2f, 0.9f, 1f, 2),
                Atk("Warden_Nova", "Nova", BossAttackShape.Self, 4.5f, 0f, 4.5f, 30f, 1.4f, 1.1f, 1f, 3),
            };
            var finalAttacks = new List<BossAttack>
            {
                Atk("Final_Swipe", "Swipe", BossAttackShape.Front, 3.5f, 2f, 2f, 25f, 0.8f, 0.7f, 2f, 1),
                Atk("Final_Slam", "Slam", BossAttackShape.Front, 4f, 2.6f, 2.2f, 40f, 1.2f, 1f, 1f, 1),
                Atk("Final_Blast", "Blast", BossAttackShape.Target, 10f, 0f, 2.2f, 30f, 1.2f, 0.8f, 1f, 2),
                Atk("Final_Nova", "Nova", BossAttackShape.Self, 5f, 0f, 4.8f, 35f, 1.4f, 1.1f, 1f, 3),
                Atk("Final_Meteor", "Meteor", BossAttackShape.Target, 11f, 0f, 3.2f, 45f, 1.8f, 1.2f, 1f, 4),
            };

            BossData Boss(string file, string displayName, float hp, float chaseSpeed, float attackRange, float scale, string body, string enraged,
                List<BossAttack> attacks, List<float> thresholds, List<float> speed, List<float> damage) =>
                Asset<BossData>($"{DataDir}/Bosses/DA_Boss_{file}.asset", a =>
                {
                    a.DisplayName = displayName; a.MaxHealth = hp; a.KnockbackSpeed = 0f;
                    a.PatrolSpeed = 2f; a.ChaseSpeed = chaseSpeed; a.PatrolRadius = 0f; a.PatrolWaitTime = 1f;
                    a.DetectionRadius = 0f; a.LoseTargetRadius = 30f; a.LeashRadius = 100f; a.ThinkInterval = 0.2f;
                    a.AttackDamage = 0f; a.AttackRange = attackRange; a.AttackHitDistance = 0f; a.AttackHitRadius = 0f;
                    a.AttackWindup = 1f; a.AttackRecovery = 1f; a.AttackCooldown = 1.2f;
                    a.CorpseLifetime = 5f;
                    a.BodyMaterial = mats[body];
                    a.EnragedMaterial = enraged != null ? mats[enraged] : null;
                    a.VisualScale = scale;
                    a.Attacks = attacks;
                    a.PhaseHealthThresholds = thresholds;
                    a.PhaseSpeedMultipliers = speed;
                    a.PhaseDamageMultipliers = damage;
                    a.PlayerCountHealthMultipliers = new List<float> { 1f, 1f, 1f, 1f };
                    a.PlayerCountDamageMultipliers = new List<float> { 1f, 1f, 1f, 1f };
                    a.LootTable = d.LootBoss;
                });

            d.BossBrute = Boss("Brute", "Громила", 600f, 3.3f, 2f, 1.6f, "MI_Boss_Brute", "MI_Boss_Enraged", bruteAttacks,
                new List<float> { 0.5f }, new List<float> { 1f, 1.3f }, new List<float> { 1f, 1.2f });
            d.BossCaster = Boss("Caster", "Заклинатель", 500f, 3f, 5f, 1.4f, "MI_Boss_Caster", "MI_Boss_Enraged", casterAttacks,
                new List<float> { 0.5f }, new List<float> { 1f, 1.2f }, new List<float> { 1f, 1.25f });
            d.BossWarden = Boss("Warden", "Страж", 800f, 3.1f, 2.2f, 1.8f, "MI_Boss_Warden", "MI_Boss_Enraged", wardenAttacks,
                new List<float> { 0.66f, 0.33f }, new List<float> { 1f, 1.15f, 1.3f }, new List<float> { 1f, 1.15f, 1.3f });
            d.BossFinal = Boss("Final", "Финальный босс", 1500f, 3.2f, 2.6f, 2.2f, "MI_Boss_Final", null, finalAttacks,
                new List<float> { 0.75f, 0.5f, 0.25f }, new List<float> { 1f, 1.1f, 1.2f, 1.35f }, new List<float> { 1f, 1.1f, 1.2f, 1.35f });

            // ---- Зоны (P0-05, P0-06) ----
            ZoneLayout Layout(string name) => Asset<ZoneLayout>($"{DataDir}/Zones/Layouts/{name}.asset", a => DefaultZoneLayouts.Fill(a, name));
            var start01 = Layout("Layout_Start_01");
            var start02 = Layout("Layout_Start_02");
            var a01 = Layout("Layout_A_01");
            var a02 = Layout("Layout_A_02");
            var b01 = Layout("Layout_B_01");
            var b02 = Layout("Layout_B_02");
            var final01 = Layout("Layout_Final_01");

            SlotFillRule Rule(string file, string slotType, int min, int max) =>
                Asset<SlotFillRule>($"{DataDir}/Zones/FillRules/DA_Fill_{file}.asset", a =>
                {
                    a.SlotType = slotType; a.MinCount = min; a.MaxCount = max;
                    a.Contents = new List<SlotContentOption>();
                });
            d.FillPoi = Rule("POI", "POI", 1, 2);
            d.FillNpcCamp = Rule("NpcCamp", "NpcCamp", 0, 1);
            d.FillDungeon = Rule("DungeonEntrance", "DungeonEntrance", 0, 1);
            d.FillBossArena = Rule("BossArena", "BossArena", 1, 1);
            d.FillQuest = Rule("QuestObject", "QuestObject", 1, 1);
            d.FillFinalArena = Rule("FinalBossArena", "BossArena", 1, 1);

            ZoneData Zone(string file, string id, string displayName, List<ZoneLayout> layouts, List<SlotFillRule> rules, bool final) =>
                Asset<ZoneData>($"{DataDir}/Zones/DA_Zone_{file}.asset", a =>
                {
                    a.ZoneId = id; a.DisplayName = displayName; a.Layouts = layouts; a.FillRules = rules; a.IsFinalZone = final;
                    a.BossPrefabs = new List<BossBase>();
                });
            d.ZoneStart = Zone("Start", "Start", "Стартовая", new List<ZoneLayout> { start01, start02 },
                new List<SlotFillRule> { d.FillPoi, d.FillNpcCamp, d.FillBossArena, d.FillQuest }, false);
            d.ZoneA = Zone("A", "ZoneA", "Зона A", new List<ZoneLayout> { a01, a02 },
                new List<SlotFillRule> { d.FillPoi, d.FillNpcCamp, d.FillDungeon, d.FillBossArena, d.FillQuest }, false);
            d.ZoneB = Zone("B", "ZoneB", "Зона B", new List<ZoneLayout> { b01, b02 },
                new List<SlotFillRule> { d.FillPoi, d.FillNpcCamp, d.FillDungeon, d.FillBossArena, d.FillQuest }, false);
            d.ZoneFinal = Zone("Final", "Final", "Финал", new List<ZoneLayout> { final01 },
                new List<SlotFillRule> { d.FillFinalArena }, true);

            d.RunMap = Asset<RunMapData>($"{DataDir}/Zones/DA_RunMap_Default.asset", a =>
            {
                a.StartZone = d.ZoneStart;
                a.Links = new List<ZoneLink>
                {
                    new ZoneLink { FromZone = d.ZoneStart, FromPassage = "Exit_East", ToZone = d.ZoneA, ToPassage = "Entrance", UnlockCondition = GateUnlockCondition.ZoneQuest },
                    new ZoneLink { FromZone = d.ZoneStart, FromPassage = "Exit_North", ToZone = d.ZoneB, ToPassage = "Entrance", UnlockCondition = GateUnlockCondition.ZoneQuest },
                    new ZoneLink { FromZone = d.ZoneStart, FromPassage = "Exit_West", ToZone = d.ZoneFinal, ToPassage = "Entrance", UnlockCondition = GateUnlockCondition.AllZoneBosses },
                };
            });

            AssetDatabase.SaveAssets();
            return d;
        }
    }
}
