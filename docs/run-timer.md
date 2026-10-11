# Таймер забега, 30 дней (P0-08)

## Где лежит

| Ассет / класс | Роль |
|---|---|
| `Scripts/Game/RunTimer.cs` (`RunTimer`) | Компонент таймера: день, списание, события, лог. `NetworkBehaviour` на объекте `Game` в сценах `Run` и `TestField`. |
| `Scripts/Game/RunGameState.cs` (`RunGameState`) | Состояние забега (победа, P0-10); на том же объекте `Game`. |
| `Scripts/Game/GameMode.cs` (`GameMode`) | Тоже на объекте `Game`; списывает дни за смерть группы ([party-death.md](party-death.md)). |
| `Scripts/UI/HeroHUD.cs` (`HeroHUD`) | Показывает «День X / 30», «Время вышло» и всплывающее «−N дн.: причина». |

## Как пользоваться из других систем

Только на хосте:

```csharp
RunTimer.Instance.SpendDaysForReason("Quest");   // так делает заглушка квеста зоны QuestObject
```

- `SpendDaysForReason(reason)` — списать столько дней, сколько указано для причины в `DayCosts`. Возвращает, сколько дней реально списано. Неизвестная причина → 0 и предупреждение в лог `[RunTimer] Unknown day cost reason: …`.
- `SpendDays(days, reason)` — списать явное число дней (для особых случаев).
- `GetDaysLeft()` — сколько дней осталось.
- На клиентах списания ничего не делают (возвращают 0): день меняет только хост. Читать `CurrentDay`, `TimeUp`, `GetDaysLeft()` можно везде. День не уходит дальше `MaxDays`; после «Время вышло» списания игнорируются.

## События

| Событие (C#) | Где | Когда |
|---|---|---|
| `OnNewDay(day)` | хост | Для каждого наступившего дня (списание 2 дней → два вызова). Для P1-05, P1-06. |
| `OnDaysSpent(days, reason, newDay)` | хост | После каждого списания. |
| `OnTimeUp()` | хост | День дошёл до `MaxDays` → поражение (обрабатывает P0-13; сейчас никто не подписан, HUD пишет «Время вышло»). |
| `OnSpendNotified(SpendEvent)` | у всех | Пришло новое списание — по нему HUD показывает всплывающее сообщение. |

## Синхронизируемое состояние

`currentDay`, `timeUp` (`NetworkVariable`), лог `Log` (`NetworkList<RunLogEntry>`: `Day` — день после списания, `Reason`, `Days` — сколько списано) — для экрана итогов. `lastSpend` (`NetworkVariable<SpendEvent>` со счётчиком `Count`) — клиенты по изменению счётчика показывают всплывающее сообщение.

## Конфиг (инспектор `RunTimer` на объекте `Game`)

| Поле | Значение | Примечание |
|---|---|---|
| `MaxDays` | 30 | |
| `StartDay` | 1 | |
| `DayCosts` | `PartyWipe` 1, `Quest` 1, `Dungeon` 1 | **TODO:** реальные значения не определены (открытый вопрос задачи). Новая причина = новая строка списка (`Reason`, `Days`). |
| `DaysPassAutomatically` | false | Решение не принято; при включении каждые `SecondsPerDay` списывается 1 день с причиной `Time`. |
| `SecondsPerDay` | 600 | Используется только при включённой опции выше. |
| `HeroHUD.ToastDuration` | 4 | Сколько секунд висит сообщение о списании (объект `HUD` на сцене). |

Конфиг хранится в сцене: у `Run` и `TestField` свои копии `RunTimer` — меняя `DayCosts`, правьте обе (или сделайте объект `Game` префабом).

## Отладка

Консоль хоста (` или F1): `spend_days <причина>` — списать дни по причине из `DayCosts` (без аргумента — `Quest`).

## Как проверить

1. Play на `TestField` или `Run` — вверху по центру «День 1 / 30».
2. `spend_days Quest` → «День 2 / 30», рамка увеличивается и мигает красным, под ней «−1 дн.: Quest» затухает за 4 с.
3. `spend_days Unknown` → ничего не списано, в консоли Unity предупреждение.
4. Повторять `spend_days PartyWipe` до 30-го дня → «Время вышло», дальнейшие списания игнорируются.
5. Два экземпляра: у клиента тот же день и то же всплывающее сообщение; на клиенте `spend_days` отвечает «Command is available on the host only».

## Отличия от UE-версии

- Компонент `BPC_RunTimer` на `BP_RunGameState` → `NetworkBehaviour` `RunTimer` на объекте `Game` (рядом `RunGameState` и `GameMode`); доступ — `RunTimer.Instance` вместо `GetGameState → GetComponentByClass`.
- `DayCosts` (Map) → список `DayCost` (`Reason`, `Days`).
- Параллельные массивы `LogDay[]` / `LogReason[]` / `LogDays[]` → `NetworkList<RunLogEntry>`; `SpendCount` (RepNotify) → `lastSpend.Count`.
- Причина по сети — `FixedString32Bytes` (до 29 байт UTF-8, т.е. ~14 кириллических символов) — причины лучше писать латиницей, как сейчас.
- Отладка через консоль `spend_days` (в UE отдельной команды не было).
