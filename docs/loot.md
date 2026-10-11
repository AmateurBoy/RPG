# Лут (P0-12)

## Ассеты

| Ассет / класс | Роль |
|---|---|
| `Scripts/Loot/LootTable.cs` (`LootTable`) | Класс таблицы дропа (`ScriptableObject`): число бросков, шанс, записи (предмет, вес, количество), разброс. Функции `PickEntry` (выбор записи по весам), `RollCount` (сколько штук), `Roll` (весь бросок). Create → TestedField → Loot → Loot Table. |
| `Data/Loot/DA_Loot_Mob`, `DA_Loot_Boss`, `DA_Loot_Chest` | Таблицы для мобов, боссов и сундуков. |
| `Loot/Interactable` | Абстрактный сетевой компонент «с чем можно взаимодействовать по E»: `InteractRadius`, `CanInteract(hero)`, `Interact(hero)` (хост), `GetPromptText(hero)`; список всех — `Interactable.All`. |
| `Loot/LootPickup` (префаб `Prefabs/Network/LootPickup`) | Предмет, лежащий в мире (наследник `Interactable`). Хранит только `ItemId`. |
| `Loot/Chest` (префаб `Prefabs/Network/Chest`) | Сундук (наследник `Interactable`): открывается один раз, выдаёт лут по своей `LootTable`. |
| `Materials/MI_LootGlow`, `MI_LootBeam` | Светящийся кристалл (эмиссия) и полупрозрачный луч над предметом. |

Где ещё:

- `MobData.LootTable` (группа Loot) — таблица моба; боссы берут её из своих `DA_Boss_*` (`BossData` — наследник `MobData`).
- `GameMode`: `DropLoot`, `SpawnLootPickup`, `ChooseLootOwner`, конфиг `LootRule`, отладка `DebugSpawnChest`.
- `HeroController`: `FindInteractTarget`, `TryInteract`; вызов — в `OnServerInteractPressed`.
- `HeroHUD.DrawLootLabels` / `DrawLootPrompts` — подписи над предметами и подсказка «E — …».
- `Resources/GameAssets`: `LootPickupPrefab`, `ChestPrefab` — какие префабы спавнит `GameMode`.

**Новая таблица без кода:** ПКМ → Create → TestedField → Loot → Loot Table, заполнить поля, выбрать её в `LootTable` нужного `DA_Mob_*` / `DA_Boss_*` или у конкретного сундука (на сцене или в префабе).

## Таблица дропа (`LootTable`)

| Поле | Смысл |
|---|---|
| `Rolls` | Сколько раз бросать таблицу |
| `DropChance` | Шанс (0–1), что бросок вообще что-то даст |
| `Entries` → `ItemId` | Что может выпасть — `ItemId` из `DA_ItemDatabase` (как в стартовом наборе P0-11) |
| `Entries` → `Weight` | Вес записи; запись с весом ≤ 0 не выпадает |
| `Entries` → `MinCount` / `MaxCount` | Сколько штук предмета выпадает (случайно в диапазоне). `MinCount` < 1 считается 1, `MaxCount` < `MinCount` — равным `MinCount` |
| `ScatterRadius` | Разброс предметов вокруг точки выпадения, м |

Алгоритм `GameMode.DropLoot(table, origin)` (только хост): `Rolls` раз — если случайное число < `DropChance`, выбрать запись по весам, выпадает `RollCount` копий этого `ItemId`; каждая копия — отдельный `LootPickup` в случайной точке квадрата `±ScatterRadius` вокруг `origin` (на высоте `origin`).

### Текущие данные (заглушки, баланса нет)

| Таблица | Rolls | DropChance | Предметы (вес, кол-во) | ScatterRadius |
|---|---|---|---|---|
| `DA_Loot_Mob` → `DA_Mob_Grunt` | 1 | 0.5 | Sword (1, 1), Staff (1, 1), LeatherArmor (1, 1) | 0.6 м |
| `DA_Loot_Boss` → все `DA_Boss_*`, включая финального | 3 | 1.0 | Sword (1, 1), Staff (1, 1), LeatherArmor (1, 1) | 1.5 м |
| `DA_Loot_Chest` → префаб `Chest` | 1 | 1.0 | Sword (1, 1), Staff (1, 1), LeatherArmor (2, 1–2) | 0.5 м |

Других предметов в базе пока нет (P0-11: `Sword`, `Staff`, `LeatherArmor`).

## Откуда выпадает

- **Моб / босс.** `MobBase.HandleDeath` (хост) → `SpawnDrop`: шлёт `OnDropRequested(mob, point)`, затем `GameMode.DropLoot(MobData.LootTable, позиция моба)` (корень моба — на земле). Боссы наследуют это без изменений.
- **Сундук** `Chest`. E рядом → `Interact` на хосте: `opened = true` (`NetworkVariable` — крышка открывается у всех, `OpenLidPitch`), `DropLoot(LootTable, DropDistance м перед сундуком)`. Повторно не открывается. `LootTable` можно поменять у конкретного сундука. На `TestField` стоит тестовый сундук `Chest_Test` в нескольких метрах от точек появления (x = 9, z = −19). На карте забега сундуки стоят в точках интереса (заглушка P1-02, `PointOfInterest`).

## Подбор

- В мире: вращающийся и покачивающийся светящийся кристалл (`Gem`, `SpinSpeed` 90 °/с, `BobHeight` 0.12 м), луч света вверх (`Beam`) и точечный свет (`Glow`); над предметами в радиусе `HeroHUD.LootLabelDistance` (12 м) — название предмета из базы.
- E (`GameInput.Interact`) → `InteractPressedServerRpc` → `OnServerInteractPressed`. Если рядом есть упавший союзник — работает поднятие (P0-04, приоритет). Иначе `TryInteract`: ближайшая цель `IInteractTarget` в её радиусе, у которой `CanInteract` = true, → `Interact`. Цели — сетевые `Interactable` (лут, сундуки) и объекты карты из `InteractTargets.Local` (квестовый объект зоны, [passages.md](passages.md)).
- `LootPickup.Interact` (хост): `Inventory.AddItem(ItemId)` (P0-11). Удалось — `pickedUp = true`, объект удаляется (`Despawn`). Инвентарь полон — предмет остаётся лежать, владельцу на экран приходит предупреждение «Inventory is full…».
- HUD: подсказка по центру снизу — «E — подобрать: Меч» / «Инвентарь полон: Меч» / «E — открыть сундук» (`DrawLootPrompts`; не рисуется, если рядом упавший союзник или герой сам упал).

## Мультиплеер

- Дроп создаёт только хост (`DropLoot` проверяет `IsServer`). `LootPickup` и `Chest` — сетевые объекты (префабы в `DefaultNetworkPrefabs`); у пикапа синхронизируются `itemId`, `pickedUp`, `reservedFor`, у сундука — `opened`.
- Один предмет — один игрок: подбор только на хосте, Server RPC обрабатываются по очереди; первый `Interact` ставит `pickedUp` и удаляет объект, все следующие получают `false`.
- По сети ходит только `ItemId` (строка), а не ссылка на ассет.

## Правило дележа

`GameMode.LootRule` (строка, инспектор объекта `Game`), сейчас `FirstPickup` — забирает тот, кто первым подобрал.

Точка расширения — `ChooseLootOwner()`: вызывается для каждого выпавшего предмета, результат пишется в `LootPickup.ReservedFor` (ClientId игрока). `LootPickup.NoOwner` — подобрать может любой; задано — только этот игрок (`CanInteract`). Новое правило = новое значение `LootRule` + ветка в `ChooseLootOwner` (например, «по очереди» — вернуть следующего игрока). Неизвестное значение — предупреждение на экране хоста и поведение `FirstPickup`.

## Конфиг

| Где | Поле | Значение |
|---|---|---|
| `LootPickup` | `InteractRadius` | 1.5 м |
| `Chest` | `InteractRadius` / `DropDistance` / `OpenLidPitch` | 1.8 м / 0.9 м / 105° |
| `Chest` | `LootTable` | `DA_Loot_Chest` |
| `GameMode` | `LootRule` | `FirstPickup` |
| `HeroHUD` | `LootLabelDistance` | 12 м |

Радиус берётся по горизонтали от центра героя до корня объекта (у пикапа и сундука корень на земле). Значения задаются в префабах `LootPickup` и `Chest` (поле `InteractRadius`).

## Отладка

- Консоль хоста: `spawn_chest` — сундук в 2.5 м перед героем хоста, лицом к нему (алиас `DebugSpawnChest`).
- `give <ItemId>` — положить предмет в инвентарь героя хоста.

## Как проверить

1. Play на `TestField`. Подойти к `Chest_Test` (в нескольких метрах от точки появления, x = 9, z = −19) → «E — открыть сундук» → E: крышка открылась, выпало 1–2 предмета. Повторно не открывается.
2. Предметы светятся, над ними названия; «E — подобрать: …» → E → предмет в инвентаре (I).
3. Убить мобов → с каждого с шансом 50% выпадает предмет; босс в `Run` — 3 предмета.
4. Заполнить инвентарь (`give Sword` несколько раз) → «Инвентарь полон: …», предмет остаётся лежать.
5. Два экземпляра: у клиента сундук открыт, предметы видны; подобрал один — предмет исчез у обоих, нажатие второго ничего не даёт. E с клавиатуры клиента работает так же.

## Отличия от UE-версии

- Параллельные массивы `ItemIds` / `Weights` / `MinCounts` / `MaxCounts` → список `Entries` (`LootEntry`).
- `BP_Interactable` → абстрактный `NetworkBehaviour` `Interactable`; пикап и сундук — сетевые префабы.
- `ReservedFor` (PlayerState) → ClientId (`ulong`, `NoOwner` = любой).
- Случайные числа дропа — `UnityEngine.Random` на хосте.
- На `TestField` есть тестовый сундук `Chest_Test` (на `EmptyMap` в UE сундука не было — только `DebugSpawnChest`).
- `ke * DebugSpawnChest` → `spawn_chest`.
- При полном инвентаре владелец видит предупреждение на экране (в UE — только подсказка HUD).
- Единицы — метры (`InteractRadius` 150 / 180 см, `DropDistance` 90 см, `ScatterRadius` 60 / 150 / 50 см, `LootLabelDistance` 1200 см).

## Открытые вопросы (TODO)

- **Правило дележа в группе** не утверждено (общий / личный / первый подобрал). Сейчас `FirstPickup`; для «личного» лута, скорее всего, нужен отдельный пикап на каждого игрока — решить вместе с правилом.
- **Числа таблиц** (шансы, веса, количество, броски) — заглушки. Предметов в базе всего три.
- **Сундуки в мире** — пока заглушка P1-02: в каждой точке интереса (слот `POI`, 1–2 на зону) хост ставит сундук `Chest` с `DA_Loot_Chest` (`Zones/PointOfInterest`, см. [generation.md](generation.md)). На `TestField` — `Chest_Test`; ещё есть `spawn_chest`. Настоящие точки интереса — P1-02.
- **Земля под лутом.** Точка выпадения — корень моба / точка перед сундуком, без трассировки к земле; на склонах предметы могут висеть или утонуть.
- **Предмет не помещается** (инвентарь полон) — остаётся лежать; фидбек — подсказка HUD и предупреждение владельцу. Нужен ли отдельный — не определено.
- **Время жизни предметов** на земле не ограничено (в ТЗ не сказано).
- **Цвет/редкость** предметов в мире одинаковые — у `ItemData` нет редкости, не придумывал.
