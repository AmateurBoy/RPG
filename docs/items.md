# Предметы, инвентарь и экипировка (P0-11)

## Ассеты

| Ассет / класс | Роль |
|---|---|
| `Scripts/Items/ItemData.cs` (`ItemData`) | Класс данных предмета (`ScriptableObject`). Одна запись = один предмет. Create → TestedField → Items → Item. |
| `EquipSlotData` | Класс данных слота экипировки. Create → TestedField → Items → Equip Slot. |
| `ItemDatabase` | Класс общей базы: список всех предметов и слотов + `GetItemDefinition(ItemId)`, `GetSlotIndex`, `GetSlotIndexById`, `GetDisplayName`. |
| `InventoryConfig` | Класс конфига инвентаря: ссылка на базу, вместимость, стартовый набор. |
| `Inventory` | Компонент инвентаря и экипировки на префабе героя `Prefabs/Network/Hero` (сеть, RPC). |
| `InventoryModel` | Логика ячеек и слотов без сети (обмен, проверки, сумма модификаторов) — её вызывает `Inventory` на хосте; покрыта EditMode-тестами. |
| `Data/Items/DA_ItemDatabase` | **Общая база предметов** — единственный источник данных о предметах. Та же база стоит в `Resources/GameAssets` (подписи лута, HUD). |
| `Data/Items/DA_InventoryConfig` | Вместимость и стартовый набор; стоит в `Inventory.Config` префаба героя. |
| `Data/Items/DA_Item_*` | Предметы: `Sword`, `Staff`, `LeatherArmor`. |
| `Data/Slots/DA_Slot_*` | Слоты: `Weapon`, `Armor`, `Accessory`. |

**Новый предмет без кода:** ПКМ → Create → TestedField → Items → Item, заполнить поля и добавить ассет в `DA_ItemDatabase.Items`. Дальше предмет выдаётся по `ItemId` (строкой в стартовом наборе, в таблице лута или командой `give <ItemId>`).

**Новый слот без кода:** ассет Equip Slot + добавить его в `DA_ItemDatabase.Slots`. Порядок в `Slots` = порядок слотов в UI и в списке `equipped`.

Внимание: **TestedField → Rebuild Generated Assets** перезаписывает стандартные `DA_*` значениями из кода, в том числе списки `DA_ItemDatabase` — свои предметы после этого нужно снова добавить в базу. «Setup Project (create missing)» существующее не трогает.

## Модель данных

### Определение предмета (`ItemData`)

| Поле | Тип | Смысл |
|---|---|---|
| `ItemId` | string | Уникальный ID, по нему предмет ищут и выдают |
| `DisplayName`, `Description` | string | Название и описание (подсказка в UI) |
| `Icon` | Texture2D | Иконка; если пусто — в ячейке рисуется название |
| `Category` | string | `Weapon` / `Armor` / `Accessory` (пока только информативно) |
| `Slot` | `EquipSlotData` | Слот экипировки — ссылка на ассет слота, а не enum |
| `FlatModifiers` | список (`Stat`, `Value`) | Плоские бонусы: `AttackDamage 10` = +10 |
| `PercentModifiers` | список (`Stat`, `Value`) | Процентные бонусы: `StaminaRegenRate 10` = +10% |
| `ActiveSkill` | string | `SkillId` активного навыка (P0-17, см. [skills.md](skills.md)); пусто — без навыка |
| `UseCondition` | string | Условие использования для P2-03 (пока не проверяется) |

Требований к уровню в предметах нет.

### Экземпляр предмета

В инвентаре и по сети хранится только `ItemId` (по сети — `FixedString32Bytes`, до 29 байт UTF-8: ID лучше короткие, латиницей). Статичные данные всегда берутся из базы через `DA_ItemDatabase.GetItemDefinition(ItemId)` → `ItemData` или `null`. Неизвестный ID пишет в лог `[Items] Unknown ItemId: …` и возвращает `null` — вызывающий код его пропускает, игра не падает.

### Статы, которые понимают модификаторы

`MaxHealth`, `MaxMana`, `MaxStamina`, `HealthRegenRate`, `ManaRegenRate`, `StaminaRegenRate` (в `HeroStats`) и `AttackDamage` (в `HeroController.GetAttackDamage`). Итог: `(База + Σ плоских) × (1 + Σ процентов / 100)` (`StatMath.Modified`). Неизвестные имена статов игнорируются.

## Данные тестовых предметов

| ItemId | Название | Слот | FlatModifiers | PercentModifiers |
|---|---|---|---|---|
| `Sword` | Меч | Weapon | `AttackDamage +10`, `MaxStamina +10` | — |
| `Staff` | Посох | Weapon | `AttackDamage +3`, `MaxMana +40`, `ManaRegenRate +2` | — |
| `LeatherArmor` | Кожаная броня | Armor | `MaxHealth +30` | `StaminaRegenRate +10%` |

Навыки (P0-17): `Sword` → `DashStrike`, `Staff` → `Fireball`, `LeatherArmor` → `Heal`.

Значения — заглушки для проверки, баланс не настраивался.

## Конфиг `DA_InventoryConfig`

| Поле | Значение | Смысл |
|---|---|---|
| `ItemDatabase` | `DA_ItemDatabase` | Какая база используется |
| `Capacity` | 12 | Число ячеек инвентаря |
| `StartingItems` | `Sword`, `Staff` | Кладутся в инвентарь при старте забега |
| `StartingEquipment` | `Armor → LeatherArmor` | Пары (`SlotId`, `ItemId`), сразу надеваются |

## Как устроено (`Inventory`)

- **Состояние:** `cells` (`NetworkList`, длина = `Capacity`, пустая строка = пустая ячейка) и `equipped` (`NetworkList`, индекс = индекс слота в `DA_ItemDatabase.Slots`). На хосте рядом живёт `InventoryModel` с теми же массивами; после каждой операции изменённые элементы копируются в сетевые списки. При изменении у всех пересчитываются суммы модификаторов и вызывается событие `OnInventoryChanged`.
- **Старт забега** (`OnNetworkSpawn`, только хост): `InitStorage` → `GrantStartingLoadout`: `StartingItems` через `AddItem`; каждая пара `StartingEquipment` — `AddItem`, затем `EquipFromCell` (тот же путь, что и ручное надевание). Если слот в паре не совпадает со слотом предмета — ошибка в лог, пара пропускается. В конце — пересчёт статов и `RefillAll` (полные HP/MP/SP на старте).
- **Операции (только хост):** `AddItem(itemId, out cellIndex) → bool`, `RemoveItemAt(cellIndex) → ItemId`, `HasFreeSpace` (у клиента считается по синхронизированным ячейкам). При переполнении — предупреждение `[Inventory] Inventory is full, item not added: …` в лог и на экран владельца (`ShowWarningRpc`), `AddItem` возвращает `false`.
- **Надеть** `EquipFromCell(cellIndex)`: предмет уходит в свой слот, предмет, который был в слоте, возвращается в эту же ячейку (обмен).
- **Снять** `UnequipSlot(slotIndex)`: нужна свободная ячейка, иначе предупреждение `[Inventory] No free cell to unequip` и отказ.
- **Пересчёт** `RecalculateStats`: суммирует модификаторы надетых предметов в `TotalFlatModifiers` / `TotalPercentModifiers` и вызывает `HeroStats.ApplyItemModifiers`. Текущие HP/MP/SP обрезаются по новому максимуму (не лечатся).
- **Перемещение** `MoveItem(from, to)` — для drag & drop. Индекс `>= 0` — ячейка инвентаря, `< 0` — слот (`-1 - SlotIndex`: `-1` = первый слот, `-2` = второй…). Ячейка → ячейка: обмен. Ячейка → слот: надеть, если слот предмета совпадает с целевым (иначе предупреждение `Item does not fit this slot`). Слот → ячейка: снять в эту ячейку; если в ней лежит предмет того же слота — обмен. Слот → слот: ничего.
- **Запросы от UI:** `RequestEquipFromCell` / `RequestUnequipSlot` / `RequestMoveItem` — на хосте вызывают операцию напрямую, на клиенте шлют Server RPC `ServerEquipFromCellRpc` / `ServerUnequipSlotRpc` / `ServerMoveItemRpc` (хост выполняет их, только если запрос прислал владелец героя).

## UI

Окно рисуется в `HeroHUD.DrawInventory` (IMGUI, как остальной HUD).

- **I** (геймпад Select) — открыть/закрыть окно; Esc — закрыть. Пока окно открыто, герой не двигается, ЛКМ не атакует, навыки не нажимаются. Упавшему окно закрывается.
- Сверху «Экипировка» — слоты из `DA_ItemDatabase.Slots` (в пустом слоте подписано его название), снизу «Сумка» — сетка по `Capacity` (4 колонки, `HeroHUD.InventoryColumns`).
- Клик по предмету в сумке — надеть, клик по надетому — снять.
- Перетаскивание (зажать ЛКМ на предмете, сдвинуть, отпустить над ячейкой/слотом): в слот — надеть, из слота в ячейку — снять в неё, между ячейками — поменять местами. Отпустить вне ячеек — отмена.
- Наведение — подсказка: название, слот, модификаторы, навык (название, мана, перезарядка), описание.
- UI только читает синхронизированные `cells` / `equipped` и данные базы, менять состояние может только хост.

## Мультиплеер

Инвентарь каждого игрока — его компонент на его герое; все изменения на хосте, клиенты получают `cells` / `equipped` через `NetworkList`. Модификаторы и урон считает хост (суммы модификаторов есть и у клиентов — для отображения).

Ручных шагов нет: в UE события `ServerEquipFromCell_0` и др. нужно было один раз пометить Replicates = Run on Server; здесь RPC объявлены атрибутами в коде и работают у клиента сразу (см. [networking.md](networking.md)).

## Как проверить

1. Play на `TestField` (соло) — или два экземпляра, см. [networking.md](networking.md).
2. HP должно быть 130/130 (100 + броня), в окне **I**: «Кожаная броня» в слоте «Броня», «Меч» и «Посох» в сумке.
3. Клик по «Мечу» → он в слоте «Оружие» (макс. SP 110); клик по «Посоху» → меч вернулся в сумку (макс. MP 90). Клик по броне → снялась в сумку, макс. HP = 100.
4. Перетащить броню из сумки на слот «Оружие» → на экране «Item does not fit this slot».
5. `give Sword` несколько раз до заполнения 12 ячеек → «Inventory is full, item not added: Sword».
6. На клиенте всё то же самое — без дополнительных настроек.
7. Test Runner → EditMode → `GameplayLogicTests`: формула статов, стартовые модификаторы, переполнение, обмен при надевании, снятие без свободной ячейки, правила `MoveItem`.

## Отличия от UE-версии

- `PrimaryDataAsset` → `ScriptableObject`; `Name` → `string` (по сети `FixedString32Bytes`); Map `String → Float` → список `StatModifier`.
- Массивы `Cells` / `Equipped` (RepNotify) → `NetworkList`; логика вынесена в `InventoryModel` и покрыта тестами.
- RPC `ServerEquipFromCell_0` / `ServerUnequipSlot_0` / `ServerMoveItem_0` → `ServerEquipFromCellRpc` / `ServerUnequipSlotRpc` / `ServerMoveItemRpc`, флаг Replicates вручную ставить не нужно; хост проверяет отправителя.
- Предупреждения инвентаря показываются и на экране владельца (в UE — только в лог).
- Окно — IMGUI (`HeroHUD.DrawInventory`) вместо Canvas; ассеты лежат в `Data/Items` и `Data/Slots`.
- Подсказка показывает название и параметры навыка, а не `SkillId`.

## Открытые вопросы (TODO)

- **P0-16 не сделана.** `HeroStats.ApplyItemModifiers` — временный мост: при первом вызове запоминает базовые значения (`base*`) и применяет к ним модификаторы. Когда появится система статов, заменить вызов в `Inventory.RecalculateStats` на неё.
- **Защиты нет среди статов.** У тестовой брони нет «защиты» — такого стата в проекте нет; не придумывал.
- **Атака без оружия.** Атака мечом работает и без надетого `Sword` (базовый `AttackDamage = 25`); оружие только добавляет бонус. Нужно ли запрещать атаку без оружия и чем отличается атака посохом — не определено.
- **Категория** (`Category`) пока ни на что не влияет — слот определяется полем `Slot`.
- **Имена статов в подсказке** выводятся как есть (`AttackDamage`, `MaxHealth`) — локализация не делалась.
- **Иконок нет** — в ячейке рисуется название; поле `Icon` готово.
- **Полные HP на старте** (`RefillAll` после стартового набора) — допущение: в начале забега у героя полные полоски с учётом брони.
