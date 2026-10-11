# Карта из трёх зон (P0-05)

## Как грузится мир — решение

**Одна сцена; зоны собираются локально у каждого участника по seed, по сети идут только seed, номер попытки и состояния.**

- Игровая сцена `Scenes/Run`: камера, свет, объект `Game` (`GameMode`, `RunTimer`, `RunGameState`), `HUD`, `DirectPlayHost`, объект `RunMap` (`NetworkObject`) и запасная точка `PlayerStart_Fallback`. Окружения в самой сцене нет.
- На хосте `RunMap.OnNetworkSpawn → BuildMap`: генератор `MapPlanner` строит план карты (см. [generation.md](generation.md)), затем `ApplyPlan`: `ZoneBuilder` строит геометрию каждой зоны под `RunMap/Zones` — стартовую в точке, где стоит `RunMap`, остальные сдвинуты так, чтобы их проход совпал с проходом предыдущей зоны.
- Клиенты получают `MapBuildInfo` (seed, попытка, версия) и строят тот же план и ту же геометрию сами (`BuildLocal`). Заготовки — данные (`ZoneLayout`), генератор детерминирован, поэтому карта у всех одинаковая. Сетевые объекты карты — только боссы.
- Навигация: после сборки хост строит navmesh (`NavMeshSurface` на корне `Zones`, по физическим коллайдерам геометрии зон, `NavMeshBaker.Build`). Ограничивающий объём не нужен — navmesh покрывает все построенные зоны.

**Почему не additive-сцены на каждую зону:** подгружаемые сцены в кооперативе нужно синхронизировать (NGO умеет, но это лишние события и ожидание загрузки у всех), а реплицировать геометрию объектами — дорого по сети. Зоны маленькие и описываются данными, поэтому каждый собирает их сам по seed за долю секунды. Если зон станет много или они станут тяжёлыми, заготовку можно перевести на префаб или additive-сцену, не меняя данные и генератор.

Лобби переходит на карту через `NetSession.StartRun` (`NetworkManager.SceneManager.LoadScene("Run")`, `UI/LobbyUI`). `TestField` осталась тестовой площадкой.

## Модель данных

| Ассет / класс | Что хранит |
|---|---|
| `ZoneData` (`DA_Zone_*`) | `ZoneId` (строка), `DisplayName`, `Layouts` — список заготовок `ZoneLayout`, `FillRules` (P0-06), `BossPrefabs` — боссы зоны (P0-09), `IsFinalZone` (P0-10). |
| `RunMapData` (`DA_RunMap_Default`) | `StartZone` и список связей `Links`: `FromZone`.`FromPassage` → `ToZone`.`ToPassage` + `UnlockCondition` (P0-07/P0-10). Связи идут по порядку: зона «откуда» должна быть уже построена. `PassageGatePrefab` — ворота (см. [passages.md](passages.md)). |
| `ZoneLayout` (`Data/Zones/Layouts/Layout_*`) | Заготовка зоны (`ScriptableObject`): `CellSize` (4 м), `Rects` — прямоугольники в клетках, `FloorMaterial` / `WallMaterial` (имена материалов палитры `MI_*`), `WallHeight` (2.2 м), `WallThickness` (0.4 м), `Passages` (`PassageId`, `Cell`, `Side`: North/East/South/West), `Slots` (`SlotType`, `Position` — метры от угла клетки (0, 0), `Radius` — радиус свободного места), `Props` (блоки-декор). Функции: `GetLocalBounds`, `FindPassage`, `ContainsLocal`, `CountSlots`. |
| `ZoneBuilder` (static) | Строит геометрию заготовки: пол по прямоугольникам (верх на y = 0), стены по контуру с проёмами под проходы, декор. |
| `ZoneInstance` (компонент на корне построенной зоны) | `Data`, `Layout`, `Index`, `Origin`; `ZoneId`, `DisplayName`, `IsFinalZone`, `QuestCompleted` / `BossDefeated` (читаются из `RunMap`), `WorldBounds`, `ContainsLocation`, `GetSlots(type)`, `FindPassage(id)`. |
| `RunMap` (объект на сцене `Run`) | `RunMapData`; строит мир; списки `Zones`, `Gates`, `Contents`, `Arenas`, `StartZoneLayout`; поиск `GetZoneById`, `GetZoneAtLocation`, `FindZoneByData`, `GetPlayerSpawnPoints`; состояния (квесты, боссы, ворота, арены) в `NetworkList`; отладка `DebugDrawSlots` (сферы слотов, рамки проходов на `DebugDrawDuration` с). |

Типы слотов (строки): `PlayerSpawn`, `POI`, `NpcCamp`, `DungeonEntrance`, `BossArena`, `QuestObject`. Сборщик использует `PlayerSpawn`: это точки появления героев (`GameMode` спавнит туда новых игроков, `RunMap` после сборки переносит туда уже заспавненных, туда же возрождается группа). Остальные слоты заполняет генератор по правилам `FillRules` (см. [generation.md](generation.md)).

### Данные этого забега

| Зона | Data Asset (`ZoneId`) | Заготовка | Проходы | Слоты |
|---|---|---|---|---|
| Стартовая | `DA_Zone_Start` (`Start`) | `Layout_Start_01`, `_02` | `Exit_East`, `Exit_North`, `Exit_West` | PlayerSpawn×4, POI×3, NpcCamp, BossArena, QuestObject×2 |
| Зона A | `DA_Zone_A` (`ZoneA`) | `Layout_A_01`, `_02` | `Entrance` (запад) | POI×3, NpcCamp, DungeonEntrance, BossArena, QuestObject×2 |
| Зона B | `DA_Zone_B` (`ZoneB`) | `Layout_B_01`, `_02` | `Entrance` (юг) | POI×3, NpcCamp, DungeonEntrance, BossArena, QuestObject×2 |
| Финал | `DA_Zone_Final` (`Final`) | `Layout_Final_01` | `Entrance` (восток) | BossArena |

`DA_RunMap_Default`: Start.`Exit_East` → ZoneA.`Entrance`, Start.`Exit_North` → ZoneB.`Entrance`, Start.`Exit_West` → Final.`Entrance` (финальная локация, P0-10, см. [final-boss.md](final-boss.md)).

Размеры: стартовая — зал 44 × 44 м (`_01`) или два зала буквой L (`_02`); A и B — зал 44 × 44 м с коридором-входом или длинный коридор, зал 36 × 44 / 44 × 36 м и боковая комната (`_02`); финал — комната 28 × 28 м.

## Как сделать новую заготовку зоны (без кода)

1. ПКМ → Create → TestedField → Zones → Zone Layout (или дублировать существующий `Layout_*` и переименовать).
2. Заполнить `Rects` — прямоугольники в клетках `CellSize` (не должны пересекаться). Пол и стены по контуру строятся автоматически (персонажи не прыгают; стены 1.8–2.4 м не пропускают героев и отбрасывание). Коридор — прямоугольник шириной в одну клетку.
3. Добавить `Passages` в точках стыковки: `PassageId` как в `RunMapData`, `Cell` — клетка на краю зоны, `Side` — какая её сторона остаётся открытой.
4. Добавить `Slots` нужных типов (`Position` в метрах от угла клетки (0, 0), `Radius` — свободное место вокруг).
5. Добавить заготовку в `Layouts` нужного `ZoneData` (или указать в связях `RunMapData` другие зоны/проходы).
6. Проверить: Window → General → Test Runner → EditMode → `MapGenerationTests` (`SlotsAreInsideTheirZones`, `AllLayoutCombinationsDoNotOverlap`).

Ограничение: сборщик только сдвигает зоны, без поворота — проход «откуда» и проход «куда» должны смотреть друг на друга (например, восточный выход ↔ западный вход); иначе попытка генерации проваливается («Passages … do not face each other»).

Тестовые заготовки описаны в коде `Editor/DefaultZoneLayouts.cs` (сетка клеток 4 × 4 м, коридоры в одну клетку). **TestedField → Rebuild Generated Assets** перезаписывает эти 7 ассетов (и `DA_Zone_*`, `DA_RunMap_Default`) значениями из кода — ручные правки в них пропадут; новые заготовки делайте под своими именами. «Setup Project (create missing)» существующее не трогает.

## Отладка

- `RunMap.DebugDrawSlots = true` (объект `RunMap` в сцене `Run`) — сферы слотов (голубые) и рамки проходов (жёлтые) при каждой сборке на `DebugDrawDuration` с. Это `Debug.DrawLine`: видно в Scene view или в Game view с включёнными Gizmos.
- Выделенная `BossArena` в Scene view показывает радиус арены и радиус начала боя.

## Как проверить

1. Play на `Scenes/Run` (хост стартует сам) — на экране хоста «Map built. Seed N, attempt 1», героя ставит в слот `PlayerSpawn` стартовой зоны.
2. Справа вверху — карта из 4 зон, текущая подсвечена. Ворота закрыты, проходы ведут к закрытым барьерам.
3. Два экземпляра (см. [networking.md](networking.md)): у клиента те же зоны и объекты, в консоли Unity клиента — `[RunMap] Client built map: seed N, …`.

## Отличия от UE-версии

- **Реплицируемые акторы-заготовки → локальная сборка по seed.** В UE хост спавнил `BP_ZoneLayout` (`bReplicates`, `bAlwaysRelevant`); здесь по сети идут только seed, номер попытки и списки состояний, геометрию каждый строит сам. Сетевые объекты карты — только боссы.
- Blueprint на каждую заготовку (`BP_Zone_*_NN` с компонентами `BPC_ZonePassage` / `BPC_ZoneSlot`) → ассет `ZoneLayout` (клетки, проходы, слоты — данными), геометрию строит `ZoneBuilder` в рантайме.
- `BoundsCenter` / `BoundsExtent` → вычисляются из `Rects` (`GetLocalBounds`, `WorldBounds`).
- Ширина прохода (`BPC_ZonePassage.Width`) → всегда одна клетка (`CellSize` = 4 м).
- `BossClasses` → `BossPrefabs`; `ZoneId` / `SlotType` — строки вместо Name.
- `PlayerStart` не переносится: точки появления читаются из слотов `PlayerSpawn`.
- `NavMeshBoundsVolume` (12 000 × 10 800 см, расширенный до x = −6000) → `NavMeshSurface` на корне зон, ничего расширять не нужно.
- Генератор отбрасывает попытки с пересекающимися зонами (в UE непересечение проверялось вручную).
- Единицы — метры (клетка 400 см → 4 м).

## Открытые вопросы (TODO)

- **Выбор заготовки и заполнение слотов** — по seed, см. [generation.md](generation.md) (P0-06).
- **Проходы закрыты до квеста зоны** — ворота `PassageGate`, см. [passages.md](passages.md) (P0-07).
- **Возрождение группы** — в слотах `PlayerSpawn` стартовой зоны; «стартовая точка текущей зоны» (P0-04) можно сделать через `GetZoneAtLocation` + слоты `PlayerSpawn` этой зоны, когда они появятся у зон A/B.
- **Падение за край** невозможно при текущих стенах; отдельной обработки нет.
- **Визуал** — простые блоки и материалы палитры `MI_*`, без декора (кроме колонн финальной комнаты).
- **Ширина проходов** фиксирована (одна клетка); проходы шире — расширить `PassageDef` и `ZoneBuilder`.
