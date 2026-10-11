# Базовый моб с ИИ (P0-03)

## Ассеты

| Ассет / класс | Роль |
|---|---|
| `Scripts/Mobs/MobBase.cs` (`MobBase`) | Базовый моб (`NetworkBehaviour` + `NavMeshAgent` + `HeroStats`). От него наследуются боссы (`BossBase`). |
| `Scripts/Mobs/MobData.cs` (`MobData`) | Класс данных моба (`ScriptableObject`): все числа, внешний вид, таблица лута. |
| `Data/Mobs/DA_Mob_Grunt` | Данные первого моба; стоят в префабе `Mob_Grunt`. |
| `Prefabs/Network/Mob_Grunt` | Сетевой префаб моба: `NetworkObject`, `CapsuleCollider` (r 0.4, h 1.8 м), `NavMeshAgent` (выключен, включается на хосте), `HeroStats`, `MobBase`, `MobVisual`, `NetworkTransform` (Server). |
| `Scripts/Mobs/MobVisual.cs` | Модель из примитивов: замах дубиной по телеграфу, поза смерти, смена материала и масштаба (для боссов). |
| `Materials/MI_Mob_Body`, `MI_Mob_Accent`, `MI_Telegraph` | Материалы модели и круга-телеграфа. Телеграф — `Core/TelegraphDecal`: красный контур и заливка, которая дорастает до края к моменту удара. |

**Новый тип моба без кода:** ассет `MobData` (ПКМ → Create → TestedField → Mobs → Mob Data), заполнить числа и материал. Дальше либо поставить `Mob_Grunt` на сцену и выбрать этот ассет в `MobData` (инспектор → `MobBase`), либо сделать Prefab Variant от `Mob_Grunt`, поменяв в нём только `MobData`. Если вариант будет спавниться в рантайме — добавить его в список сетевых префабов `Assets/DefaultNetworkPrefabs.asset`.

## Состояния ИИ

ИИ работает только на хосте: корутина `ThinkLoop` с шагом `ThinkInterval`. `NavMeshAgent` включается, когда под мобом появился navmesh (поиск в радиусе 3 м). Состояние хранится в `aiState` (`NetworkVariable<MobAIState>`); клиенты по `OnRepAIState` прячут телеграф и показывают смерть, по счётчику `attackSerial` — телеграф и замах (центр, радиус, замах и восстановление удара тоже синхронизируются).

| `MobAIState` | Что делает | Переход |
|---|---|---|
| `Patrol` | Ходит к случайным достижимым точкам navmesh в `PatrolRadius` от точки появления (только полный путь), между точками ждёт `PatrolWaitTime`. | Живой герой ближе `DetectionRadius` → `Chase` |
| `Chase` | Каждый шаг выбирает **ближайшего живого** героя в `LoseTargetRadius` (упавших и мёртвых игнорирует) и идёт к нему (`NavMeshAgent.SetDestination`, обход препятствий по navmesh). | Цель в `AttackRange` и прошло `AttackCooldown` после конца прошлой атаки → `Attack`. Нет цели или моб дальше `LeashRadius` от дома → `Return` |
| `Attack` | Останавливается, разворачивается к цели. Телеграф: красный круг на земле (центр `AttackHitDistance` перед мобом, радиус `AttackHitRadius`) и замах дубиной `AttackWindup` секунд. Затем удар: урон `AttackDamage` всем живым героям, чей центр (по горизонтали) внутри круга. Потом `AttackRecovery`. | → `Chase` |
| `Return` | Идёт к точке появления, новых целей не берёт. | Дома (путь закончен или ближе 1 м) → `Patrol` |
| `Dead` | Корутины остановлены, агент и коллайдеры выключены (у всех), модель ложится. Вызываются `SpawnDrop`, `OnMobDied` и `Died`; объект удаляется (`NetworkObject.Despawn`) через `CorpseLifetime`. | — |

Место удара фиксируется в начале замаха, поэтому от атаки можно уйти: выйти из круга или кувыркнуться (i-frames из P0-02 блокируют урон).

## Точки расширения

- `SpawnDrop` — вызывается при смерти: шлёт событие `OnDropRequested(mob, point)` и выбрасывает лут по `MobData.LootTable` через `GameMode.DropLoot` (P0-12, см. [loot.md](loot.md)).
- `MobBase.OnMobDied(mob)` (статическое событие) и `Died(mob)` (событие экземпляра) — смерть для квестов, счётчиков, арен. Только на хосте.
- Методы состояний виртуальные: `TickPatrol`, `TickChase`, `TickReturn`, `EnterChase`, `EnterReturn`, `EnterPatrol`, `StartAttack`, `AttackHit`, `FindTarget`, `GetChaseSpeed`, `ApplyMobData`, `HandleDeath`, `SpawnDrop` — переопределяются в C#-наследнике. Так сделаны боссы — см. [bosses.md](bosses.md).
- Общий телеграфированный удар — `BeginAttack(center, radius, windup, recovery, damage)`: им пользуются и моб, и босс.
- Здоровье, урон, i-frames, вспышка и отбрасывание — общий компонент `HeroStats` (P0-02). `MaxHealth` и `KnockbackSpeed` моба берутся из данных (`ApplyMobData` на хосте). Торможение после отбрасывания — `MobBase.KnockbackDeceleration` (20 м/с²).

## Данные `DA_Mob_Grunt`

| Группа | Поле | Значение |
|---|---|---|
| Stats | `MaxHealth` / `KnockbackSpeed` | 60 / 3 м/с |
| Movement | `PatrolSpeed` / `ChaseSpeed` | 1.8 / 3.8 м/с |
| | `PatrolRadius` / `PatrolWaitTime` | 6 м / 2 с |
| Perception | `DetectionRadius` / `LoseTargetRadius` / `LeashRadius` | 9 / 15 / 25 м |
| | `ThinkInterval` | 0.2 с |
| Attack | `AttackDamage` | 15 |
| | `AttackRange` (дистанция начала атаки) | 1.7 м |
| | `AttackHitDistance` / `AttackHitRadius` | 1.1 / 1.1 м |
| | `AttackWindup` / `AttackRecovery` / `AttackCooldown` | 0.8 / 0.6 / 1.0 с |
| Death | `CorpseLifetime` | 3 с |
| Visual | `BodyMaterial` | `MI_Mob_Body` |
| Loot | `LootTable` | `DA_Loot_Mob` |

## Навигация

- Пакет AI Navigation, navmesh строится в рантайме (аналог `RuntimeGeneration = Dynamic`): `NavMeshSurface` собирает физические коллайдеры дочерних объектов, `NavMeshBoundsVolume` не нужен.
- `TestField`: на объекте `Environment` стоят `NavMeshSurface` и `RuntimeNavMesh` — navmesh строится при старте сцены по земле 140 × 140 м и окружению.
- `Run`: navmesh строит хост после сборки карты из заготовок (`RunMap` → `NavMeshBaker.Build`, см. [zones.md](zones.md)).
- На `TestField` три моба: в поле (`Mob_Field`, к югу от старта), в деревне между домами (`Mob_Village`) и у западной рощи (`Mob_Grove`).

## Отладка

- Выделенный моб в Scene view: жёлтая сфера — `DetectionRadius`, голубая — `PatrolRadius`, фиолетовая — `LeashRadius` (от точки появления).

## Как проверить

1. Play на `TestField`. Подойти к `Mob_Field` ближе 9 м — моб бежит к герою.
2. Рядом с героем — красный круг заполняется 0.8 с, затем удар 15 (−15 HP, отбрасывание). Кувырок в момент удара или выход из круга — урона нет.
3. Убежать дальше 15 м (или увести моба дальше 25 м от дома) — моб возвращается домой и снова патрулирует.
4. Убить моба (60 HP) — модель ложится, с шансом 50% выпадает предмет, через 3 с труп исчезает.
5. Два экземпляра: телеграф и замах в той же точке у клиента; моб выбирает ближайшего живого героя, упавших игнорирует.

## Отличия от UE-версии

- Единицы — метры (было: `PatrolRadius` 600, `DetectionRadius` / `LoseTargetRadius` / `LeashRadius` 900 / 1500 / 2500 см, скорости 180 / 380 см/с и т.д.).
- `AIController` + `MoveToActor` → `NavMeshAgent` на хосте (`SetDestination` каждый `ThinkInterval`); на клиентах агент выключен, позиция приходит через серверный `NetworkTransform`.
- Таймер `Think` → корутина `ThinkLoop`; агент включается, только когда navmesh готов.
- `AIState` (Name, RepNotify) → `enum MobAIState` в `NetworkVariable`; телеграф синхронизируется отдельными переменными и счётчиком `attackSerial`.
- Дочерний BP / переопределение событием в EventGraph → Prefab Variant / C#-наследник с `override`.
- `NavMeshBoundsVolume` + `RecastNavMesh` → `NavMeshSurface` (AI Navigation), строится в рантайме.
- Актор после смерти → `NetworkObject.Despawn` через `CorpseLifetime`.

## Открытые вопросы (TODO)

- **Агро от урона.** Моб, которого ударили из-за пределов `DetectionRadius` (или во время `Return`), не реагирует — в задаче не описано.
- **Возврат домой.** Во время `Return` моб не берёт новые цели и не восстанавливает HP — не определено.
- **Leash.** Предел преследования `LeashRadius` — допущение для «потери цели», значение подобрать.
- **Атака.** Урон по кругу без проверки линии видимости; другие мобы от удара не страдают.
- **Размер моба.** Капсула и агент в префабе общие у всех мобов (боссы масштабируют их по `VisualScale`); если понадобятся крупные обычные мобы, вынести размер в данные.
- **Препятствия вне navmesh.** Агент обходит только то, что запечено в navmesh (и ворота с `NavMeshObstacle`); коллайдеры, которых нет в `NavMeshSurface` (контент слотов, стены арен), мобы не учитывают.
- **Дроп** — сделан в P0-12, см. [loot.md](loot.md).
