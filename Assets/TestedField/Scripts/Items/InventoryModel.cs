using System.Collections.Generic;

namespace TestedField
{
    /// <summary>
    /// Логика инвентаря и экипировки без сети (ячейки, слоты, обмен, модификаторы) — её вызывает
    /// компонент Inventory на хосте; вынесена отдельно, чтобы покрыть тестами.
    /// Пустая ячейка/слот — пустая строка (None в UE).
    /// </summary>
    public class InventoryModel
    {
        public readonly ItemDatabase Database;
        public readonly string[] Cells;
        public readonly string[] Equipped;

        /// <summary>Последнее предупреждение (переполнение, не тот слот…) — для лога и экрана.</summary>
        public string LastWarning { get; private set; }

        public InventoryModel(ItemDatabase database, int capacity)
        {
            Database = database;
            Cells = new string[System.Math.Max(0, capacity)];
            Equipped = new string[database != null ? database.Slots.Count : 0];
            for (int i = 0; i < Cells.Length; i++) Cells[i] = "";
            for (int i = 0; i < Equipped.Length; i++) Equipped[i] = "";
        }

        public static bool IsEmpty(string id) => string.IsNullOrEmpty(id);

        public int FindFreeCell()
        {
            for (int i = 0; i < Cells.Length; i++)
                if (IsEmpty(Cells[i]))
                    return i;
            return -1;
        }

        public bool HasFreeSpace => FindFreeCell() >= 0;

        public bool AddItem(string itemId, out int cellIndex)
        {
            cellIndex = -1;
            LastWarning = null;
            if (Database == null || Database.GetItemDefinition(itemId) == null)
                return false;
            cellIndex = FindFreeCell();
            if (cellIndex < 0)
            {
                LastWarning = $"[Inventory] Inventory is full, item not added: {itemId}";
                return false;
            }
            Cells[cellIndex] = itemId;
            return true;
        }

        public string RemoveItemAt(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Cells.Length)
                return "";
            string id = Cells[cellIndex];
            Cells[cellIndex] = "";
            return id;
        }

        int SlotOf(string itemId)
        {
            var def = Database != null ? Database.GetItemDefinition(itemId) : null;
            return def != null ? Database.GetSlotIndex(def.Slot) : -1;
        }

        /// <summary>Надеть из ячейки: предмет уходит в свой слот, бывший в слоте — в эту же ячейку.</summary>
        public bool EquipFromCell(int cellIndex)
        {
            LastWarning = null;
            if (cellIndex < 0 || cellIndex >= Cells.Length || IsEmpty(Cells[cellIndex]))
                return false;
            string itemId = Cells[cellIndex];
            int slot = SlotOf(itemId);
            if (slot < 0)
            {
                LastWarning = $"[Inventory] Item has no equip slot: {itemId}";
                return false;
            }
            Cells[cellIndex] = Equipped[slot];
            Equipped[slot] = itemId;
            return true;
        }

        /// <summary>Снять: нужна свободная ячейка.</summary>
        public bool UnequipSlot(int slotIndex)
        {
            LastWarning = null;
            if (slotIndex < 0 || slotIndex >= Equipped.Length || IsEmpty(Equipped[slotIndex]))
                return false;
            int cell = FindFreeCell();
            if (cell < 0)
            {
                LastWarning = "[Inventory] No free cell to unequip";
                return false;
            }
            Cells[cell] = Equipped[slotIndex];
            Equipped[slotIndex] = "";
            return true;
        }

        public static int SlotToMoveIndex(int slotIndex) => -1 - slotIndex;
        public static int MoveIndexToSlot(int index) => -1 - index;

        /// <summary>
        /// Перемещение для drag &amp; drop. Индекс ≥ 0 — ячейка, &lt; 0 — слот (-1 - SlotIndex).
        /// Ячейка→ячейка: обмен. Ячейка→слот: надеть, если слот совпадает. Слот→ячейка: снять
        /// (если в ячейке предмет того же слота — обмен). Слот→слот: ничего.
        /// </summary>
        public bool MoveItem(int from, int to)
        {
            LastWarning = null;
            if (from == to)
                return false;

            if (from >= 0 && to >= 0)
            {
                if (from >= Cells.Length || to >= Cells.Length || IsEmpty(Cells[from]))
                    return false;
                (Cells[from], Cells[to]) = (Cells[to], Cells[from]);
                return true;
            }

            if (from >= 0 && to < 0)
            {
                int slot = MoveIndexToSlot(to);
                if (from >= Cells.Length || slot >= Equipped.Length || IsEmpty(Cells[from]))
                    return false;
                if (SlotOf(Cells[from]) != slot)
                {
                    LastWarning = "[Inventory] Item does not fit this slot";
                    return false;
                }
                (Cells[from], Equipped[slot]) = (Equipped[slot], Cells[from]);
                return true;
            }

            if (from < 0 && to >= 0)
            {
                int slot = MoveIndexToSlot(from);
                if (slot >= Equipped.Length || to >= Cells.Length || IsEmpty(Equipped[slot]))
                    return false;
                if (IsEmpty(Cells[to]))
                {
                    Cells[to] = Equipped[slot];
                    Equipped[slot] = "";
                    return true;
                }
                if (SlotOf(Cells[to]) == slot)
                {
                    (Cells[to], Equipped[slot]) = (Equipped[slot], Cells[to]);
                    return true;
                }
                LastWarning = "[Inventory] Item does not fit this slot";
                return false;
            }

            return false;
        }

        /// <summary>Сумма модификаторов надетых предметов.</summary>
        public static void SumModifiers(ItemDatabase database, IEnumerable<string> equipped, Dictionary<string, float> flat, Dictionary<string, float> percent)
        {
            flat.Clear();
            percent.Clear();
            if (database == null)
                return;
            foreach (var id in equipped)
            {
                if (IsEmpty(id))
                    continue;
                var def = database.GetItemDefinition(id);
                if (def == null)
                    continue;
                foreach (var m in def.FlatModifiers)
                    flat[m.Stat] = (flat.TryGetValue(m.Stat, out var v) ? v : 0f) + m.Value;
                foreach (var m in def.PercentModifiers)
                    percent[m.Stat] = (percent.TryGetValue(m.Stat, out var v) ? v : 0f) + m.Value;
            }
        }
    }
}
