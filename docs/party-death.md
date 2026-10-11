# Смерть игрока и группы (P0-04)

## Упал

HP = 0 → `HeroStats.OnDeath` → `HeroController.HandleDeath` (хост): `isDowned = true` (поза, движение и атака выключены — P0-02), спринт сброшен, отменяется поднятие, которое этот игрок делал сам, и `GameMode` проверяет смерть группы.

HUD у всех: над каждым упавшим героем красная метка «УПАЛ» и полоса прогресса, если его поднимают; в панели союзников — «УПАЛ» / «ПОДНИМАЮТ». Упавший видит «Вы упали…» / «Вас поднимают…», стоящий рядом союзник — «Удерживайте E, чтобы поднять союзника» (см. [hud.md](hud.md)).

## Поднятие союзником

- Ввод: действие `GameInput.Interact` (E / геймпад Y). Нажатие → Server RPC `InteractPressedServerRpc`, отпускание (или открытие инвентаря/консоли) → `InteractReleasedServerRpc`.
- Хост, пока кнопка держится, каждые `ReviveTickInterval` с (`TickRevive`): ищет ближайшего упавшего союзника в `ReviveRadius` (по горизонтали), которого никто другой не поднимает, записывает себя в его `currentReviverId` и растит его `reviveProgress` на `ReviveTickInterval / ReviveDuration` (оба — `NetworkVariable`, прогресс видят все). Одного упавшего поднимает один игрок.
- `reviveProgress` ≥ 1 → `GetRevived(ReviveHealthFraction)`: HP = доля от максимума (не меньше 1), `isDowned = false`.
- Прерывание → прогресс упавшего обнуляется: отпустил кнопку, отошёл дальше `ReviveRadius`, сам получил урон (`OnDamaged`), сам упал, отключился. Если кнопка всё ещё зажата, поднятие начинается заново с нуля.
- Приоритет: если рядом упавший союзник, E поднимает его, а не подбирает лут / открывает сундук ([loot.md](loot.md)).

## Смерть группы

`GameMode.NotifyHeroDowned` (хост): если упали **все** заспавненные герои (`AreAllHeroesDowned`, в соло — единственный) →
1. сообщение «[Party] All heroes are down — party wipe» на экране хоста и `RunTimer.SpendDaysForReason(PartyWipeReason)` (P0-08);
2. через `PartyWipeRespawnDelay` с — `RespawnParty`: герои телепортируются на точки появления (по кругу) и поднимаются с `PartyWipeRespawnHealthFraction` HP (`HeroController.RespawnAt`; телепорт выполняет владелец по `TeleportRpc`).

Точки появления (`GameMode.GetSpawnPoints`): в `Run` — слоты `PlayerSpawn` стартовой зоны (4 шт., см. [zones.md](zones.md)); если карты нет — компоненты `PlayerStart` на сцене (на `TestField` — 4 шт., в `Run` — запасной `PlayerStart_Fallback`); если нет и их — точка (0, 0, 0).

Смерть одного игрока день не трогает.

Бой с боссом сбрасывается сам (P0-09, [bosses.md](bosses.md)): арена следит, остались ли внутри живые герои.

## Конфиг

| Где | Поле | Значение |
|---|---|---|
| `HeroController` → Revive (префаб `Hero`) | `ReviveDuration` | 3 с |
| | `ReviveHealthFraction` | 0.3 |
| | `ReviveRadius` | 2 м |
| | `ReviveTickInterval` | 0.05 с |
| `GameMode` → PartyDeath (объект `Game` на сцене) | `PartyWipeRespawnDelay` | 3 с |
| | `PartyWipeRespawnHealthFraction` | 1.0 |
| | `PartyWipeReason` | `PartyWipe` (сколько дней — `DayCosts` в `RunTimer`) |

## Как проверить

1. Соло: Play на `TestField`, подставиться под удары моба `Mob_Field` (к югу от старта). HP = 0 → «Вы упали…», в соло это сразу смерть группы: «День 2 / 30», «−1 дн.: PartyWipe», через 3 с герой на `PlayerStart` с полным HP.
2. Два экземпляра (см. [networking.md](networking.md)): один падает — у второго над ним «УПАЛ», рядом подсказка «Удерживайте E…». Держать E 3 с — союзник встаёт с 30% HP; отпустить раньше или отойти дальше 2 м — прогресс обнуляется.
3. Упасть обоим — party wipe: −1 день, через 3 с оба на старте.

## Отличия от UE-версии

- `ReviveRadius` 200 см → 2 м.
- `ReviveProgress` / `CurrentReviver` (реплицируемые) → `NetworkVariable` `reviveProgress` / `currentReviverId` (`NetworkObjectId` поднимающего, 0 — никто).
- Таймер `TickRevive` → накопление времени в `Update` хоста.
- `IA_Interact` в `IMC_Default` → `GameInput.Interact` (Input System, действия собираются в коде).
- `BP_TestGameMode` → компонент `GameMode` на объекте `Game` (в сценах `Run` и `TestField`).
- Актор `PlayerStart` → компонент `PlayerStart`; в `Run` точки появления берутся прямо из слотов `PlayerSpawn` (в UE `BP_RunMap` переносил туда `PlayerStart`).

## Открытые вопросы (TODO)

- **Сколько дней отнимает смерть группы** — `DayCosts` → `PartyWipe`, сейчас 1.
- **Где возрождается группа** — слоты `PlayerSpawn` стартовой зоны. «Стартовая точка текущей зоны» — заменить в `GameMode.GetSpawnPoints` (через `RunMap.GetZoneAtLocation` + слоты `PlayerSpawn` этой зоны, когда они появятся у зон A/B).
- Состояние мобов, маны и стамины при возрождении группы не сбрасывается.
- Отключившийся игрок не участвует в проверке «все упали» (`GameMode` смотрит только на заспавненных героев). Проверка запускается, когда кто-то падает и когда кто-то отключается: если ушёл последний стоящий игрок, оставшиеся упавшие получают смерть группы и возрождаются.
