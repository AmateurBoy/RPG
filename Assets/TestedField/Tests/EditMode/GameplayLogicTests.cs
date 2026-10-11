using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TestedField.Tests
{
    /// <summary>Логика без сети: генератор случайных чисел, инвентарь (P0-11), лут (P0-12), формула статов.</summary>
    public class GameplayLogicTests
    {
        ItemDatabase db;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/TestedField/Data/Items/DA_ItemDatabase.asset");
            Assert.IsNotNull(db, "Run menu TestedField → Setup Project first");
        }

        [Test]
        public void RandomStreamIsDeterministicAndInRange()
        {
            var a = new RandomStream(42);
            var b = new RandomStream(42);
            for (int i = 0; i < 1000; i++)
            {
                float fa = a.GetFraction();
                Assert.AreEqual(fa, b.GetFraction());
                Assert.GreaterOrEqual(fa, 0f);
                Assert.Less(fa, 1f);
                int r = a.RandRange(3, 7);
                b.RandRange(3, 7);
                Assert.GreaterOrEqual(r, 3);
                Assert.LessOrEqual(r, 7);
            }
        }

        [Test]
        public void StatFormula()
        {
            var flat = new Dictionary<string, float> { { "MaxHealth", 30f } };
            var percent = new Dictionary<string, float> { { "MaxHealth", 10f } };
            Assert.AreEqual(143f, StatMath.Modified(100f, "MaxHealth", flat, percent), 0.001f);
            Assert.AreEqual(50f, StatMath.Modified(50f, "MaxMana", flat, percent), 0.001f);
        }

        [Test]
        public void StartingLoadoutModifiers()
        {
            var flat = new Dictionary<string, float>();
            var percent = new Dictionary<string, float>();
            InventoryModel.SumModifiers(db, new[] { "Sword", "LeatherArmor" }, flat, percent);
            Assert.AreEqual(130f, StatMath.Modified(100f, "MaxHealth", flat, percent), 0.001f, "HP 130 = 100 + броня");
            Assert.AreEqual(35f, StatMath.Modified(25f, "AttackDamage", flat, percent), 0.001f);
            Assert.AreEqual(22f, StatMath.Modified(20f, "StaminaRegenRate", flat, percent), 0.001f, "+10% регенерации стамины");
        }

        [Test]
        public void InventoryAddAndOverflow()
        {
            var inv = new InventoryModel(db, 2);
            Assert.IsTrue(inv.AddItem("Sword", out int c0));
            Assert.AreEqual(0, c0);
            Assert.IsTrue(inv.AddItem("Staff", out int c1));
            Assert.AreEqual(1, c1);
            Assert.IsFalse(inv.AddItem("Sword", out _));
            StringAssert.Contains("Inventory is full", inv.LastWarning);
            Assert.IsFalse(inv.AddItem("UnknownThing", out _));
        }

        [Test]
        public void EquipSwapsWithSlot()
        {
            var inv = new InventoryModel(db, 4);
            inv.AddItem("Sword", out _);
            inv.AddItem("Staff", out _);
            int weapon = db.GetSlotIndexById("Weapon");

            Assert.IsTrue(inv.EquipFromCell(0));
            Assert.AreEqual("Sword", inv.Equipped[weapon]);
            Assert.AreEqual("", inv.Cells[0]);

            Assert.IsTrue(inv.EquipFromCell(1));
            Assert.AreEqual("Staff", inv.Equipped[weapon]);
            Assert.AreEqual("Sword", inv.Cells[1], "предмет, который был в слоте, возвращается в эту же ячейку");
        }

        [Test]
        public void UnequipNeedsFreeCell()
        {
            var inv = new InventoryModel(db, 1);
            inv.AddItem("LeatherArmor", out _);
            inv.EquipFromCell(0);
            inv.AddItem("Sword", out _);
            int armor = db.GetSlotIndexById("Armor");
            Assert.IsFalse(inv.UnequipSlot(armor));
            Assert.AreEqual("LeatherArmor", inv.Equipped[armor]);
        }

        [Test]
        public void MoveItemRules()
        {
            var inv = new InventoryModel(db, 4);
            inv.AddItem("Sword", out _);
            inv.AddItem("LeatherArmor", out _);
            int weapon = db.GetSlotIndexById("Weapon");
            int armor = db.GetSlotIndexById("Armor");

            // Ячейка → ячейка: обмен
            Assert.IsTrue(inv.MoveItem(0, 3));
            Assert.AreEqual("Sword", inv.Cells[3]);

            // Ячейка → чужой слот: отказ
            Assert.IsFalse(inv.MoveItem(3, InventoryModel.SlotToMoveIndex(armor)));
            StringAssert.Contains("does not fit", inv.LastWarning);

            // Ячейка → свой слот: надеть
            Assert.IsTrue(inv.MoveItem(3, InventoryModel.SlotToMoveIndex(weapon)));
            Assert.AreEqual("Sword", inv.Equipped[weapon]);

            // Слот → пустая ячейка: снять
            Assert.IsTrue(inv.MoveItem(InventoryModel.SlotToMoveIndex(weapon), 2));
            Assert.AreEqual("Sword", inv.Cells[2]);
            Assert.AreEqual("", inv.Equipped[weapon]);

            // Слот → слот: ничего
            inv.EquipFromCell(1);
            Assert.IsFalse(inv.MoveItem(InventoryModel.SlotToMoveIndex(armor), InventoryModel.SlotToMoveIndex(weapon)));
        }

        [Test]
        public void LootTableRolls()
        {
            var chest = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/TestedField/Data/Loot/DA_Loot_Chest.asset");
            var boss = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/TestedField/Data/Loot/DA_Loot_Boss.asset");
            var rng = new RandomStream(5);
            var result = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                boss.Roll(rng.GetFraction, result);
                Assert.AreEqual(3, result.Count, "босс: 3 броска со шансом 1.0");
                chest.Roll(rng.GetFraction, result);
                Assert.GreaterOrEqual(result.Count, 1);
                Assert.LessOrEqual(result.Count, 2);
                foreach (var id in result)
                    Assert.IsNotNull(db.GetItemDefinition(id, false), id);
            }
        }

        [Test]
        public void ItemsReferenceKnownSkills()
        {
            var skills = AssetDatabase.LoadAssetAtPath<SkillConfig>("Assets/TestedField/Data/Skills/DA_SkillConfig.asset");
            foreach (var item in db.Items)
                if (!string.IsNullOrEmpty(item.ActiveSkill))
                    Assert.IsNotNull(skills.GetSkillDefinition(item.ActiveSkill, false), $"{item.ItemId} → {item.ActiveSkill}");
        }
    }
}
