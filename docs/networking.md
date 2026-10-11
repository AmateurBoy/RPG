# Сетевая модель (P0-01)

## Решение

**Netcode for GameObjects (NGO 2.13) + Unity Transport в режиме listen server (хост), подключение по IP.**

- Хост — один из игроков: «Создать игру» вызывает `NetSession.HostGame()` → `NetworkManager.StartHost()`; его клиент одновременно является сервером. Хост слушает все интерфейсы (`0.0.0.0`) и сразу загружает сцену `Lobby` через сетевой `SceneManager` — клиенты переходят за ним автоматически.
- Остальные игроки (до 3) подключаются по IP: кнопка «Подключиться» → `NetSession.JoinGame("IP[:порт]")` → `StartClient()`. Порт по умолчанию 7777 (`NetSession.DefaultPort`), `localhost` заменяется на `127.0.0.1`.
- Лимит игроков — 4 (`NetSession.MaxPlayers`). Проверяется в Connection Approval (`NetSession.ApproveConnection`): пятый игрок получает отказ «Сервер заполнен (максимум 4 игрока)».

## Почему так

- Это стандартный сетевой стек Unity: `NetworkVariable` / `NetworkList` (аналог реплицируемых переменных и RepNotify), RPC, `NetworkTransform`, синхронизация сцен — без сторонних плагинов и серверной инфраструктуры.
- Listen server — ровно модель «хост — один из игроков» из задачи; отдельный выделенный сервер не нужен.
- Подключение по IP не требует онлайн-сервисов (Unity Relay / Lobby, Steam), аккаунтов и регистрации проекта — проще всего для прототипа и тестов в локальной сети или через VPN (Hamachi, ZeroTier, Radmin VPN).
- Если позже понадобятся приглашения, поверх этой же модели добавляются Unity Relay + Lobby (или Steam-транспорт); игровой код и синхронизация при этом не меняются.

## Авторитет

Хост — авторитет:

- урон, смерть, стамина, мана и здоровье считаются только на хосте: `HeroStats` хранит их в `NetworkVariable`, пишет только сервер, регенерация в `Update` идёт только при `IsServer`;
- клиент шлёт запросы через Server RPC (`[Rpc(SendTo.Server)]`: `AttackServerRpc`, `RollServerRpc`, `SetSprintingServerRpc`, …), хост проверяет стамину и меняет сетевые счётчики (`attackCount`, `rollCount`), по которым у всех проигрывается визуал;
- **движение героя — у владельца:** `CharacterController` двигается на машине владельца, `NetworkTransform` в режиме `AuthorityMode = Owner` рассылает позицию остальным (аналог клиентского предсказания `CharacterMovementComponent`). Когда хосту нужно сдвинуть героя (телепорт на старт, рывок, отбрасывание), он шлёт владельцу Owner RPC (`TeleportRpc`, `KnockbackRpc`), и двигает героя сам владелец;
- мобы и боссы — серверный `NetworkTransform` (`AuthorityMode = Server`), `NavMeshAgent` включён только на хосте;
- все серверные RPC героя, инвентаря и навыков проверяют, что запрос прислал владелец героя (`RpcParams.Receive.SenderClientId == OwnerClientId`) — управлять чужим героем нельзя.

## Поток игры

1. Сцена `MainMenu` — «Создать игру» (хост: `StartHost` → сетевая загрузка `Lobby`) или «Подключиться» по IP.
2. Сцена `Lobby` — список игроков (`LobbyState`), кнопка «Начать игру» только у хоста (`NetSession.StartRun` → `NetworkManager.SceneManager.LoadScene("Run")`), «Выйти в меню».
3. Сцена `Run` — у каждого свой герой (его спавнит `GameMode` как PlayerObject) и своя камера.
4. Хост вышел → у клиентов срабатывает `OnClientStopped` в `NetSession` → загружается `MainMenu` с сообщением «Хост покинул игру».
5. Клиент вышел → NGO удаляет его героя (PlayerObject), игра у остальных продолжается.
6. Подключение к уже идущему забегу: клиент синхронизирует сцену `Run`, собирает карту по seed (см. [generation.md](generation.md)), `GameMode` спавнит ему героя на точке появления (`OnSynchronizeComplete`).

## Где что лежит

| Ассет / класс | Роль |
|---|---|
| `Scenes/MainMenu` + `UI/MainMenuUI` | Главное меню (IMGUI): поле «Имя» (сохраняется в `PlayerPrefs`, по умолчанию — имя пользователя ОС), «Создать игру», адрес + «Подключиться», «Настройки» (режим окна, разрешение, качество, лимит FPS 30/60/120/144/без лимита, V-Sync — `Core/GameSettings`, сохраняются в `PlayerPrefs` между запусками), «Выход». Пока идёт подключение — «Подключение к …» и «Отмена». |
| `Scenes/Lobby` + `UI/LobbyUI`, `UI/LobbyState` | Лобби: у хоста — его локальные IPv4-адреса и порт для подключения; список игроков из `LobbyState.Players` (`NetworkList`, имя приходит от клиента RPC `SetNameRpc`), «Начать игру» только у хоста, «Ожидание хоста…» у клиентов, «Выйти в меню» (и Esc). |
| `Core/NetSession` (static) | Аналог `BP_GameInstance`: создаёт `NetworkManager` из `Resources/NetworkManager.prefab` до загрузки первой сцены, `HostGame` / `JoinGame` / `StartRun` / `LeaveToMenu`, Connection Approval, обработка обрыва связи (`OnClientStopped`). |
| `Resources/NetworkManager.prefab` | `NetworkManager` + `UnityTransport`: Connection Approval включён, Scene Management включён, `TickRate` 30, `PlayerPrefab` пустой (героев спавнит `GameMode`), список сетевых префабов — `Assets/DefaultNetworkPrefabs.asset`. |
| `Core/CommandLineLaunch` | Запуск из командной строки: `-host`, `-connect IP[:порт]`, `-autostart N`, `-name Имя` (см. «Как проверить»). |
| `Hero/HeroController` (префаб `Prefabs/Network/Hero`) | Ввод только у владельца (`IsOwner`); Server RPC `AttackServerRpc`, `RollServerRpc`, `SetSprintingServerRpc`, `InteractPressedServerRpc`, `InteractReleasedServerRpc`, `SetPlayerNameServerRpc`; Owner RPC `TeleportRpc`, `KnockbackRpc`; визуал удара/кувырка у всех — через сетевые счётчики `attackCount` / `rollCount`; `isSprinting` и имя игрока синхронизируются; состояние «упал» — `isDowned` (см. [combat.md](combat.md)); поднятие союзника — `reviveProgress` / `currentReviverId`, считает хост (см. [party-death.md](party-death.md)). Камера — `Hero/HeroCamera` (изометрия, вешается на `Camera.main` у владельца). |
| `Hero/HeroStats` | HP / мана / стамина / максимумы / смерть / неуязвимость — `NetworkVariable`, меняет только хост; вспышка и звук попадания у всех — по сетевому счётчику `hitReactCount`. |
| `Mobs/MobBase` (префаб `Mob_Grunt`) | ИИ только на хосте (`NavMeshAgent` включён только у сервера); `aiState` и параметры телеграфа — `NetworkVariable` (телеграф, замах, смерть у всех), движение — серверный `NetworkTransform`. См. [mobs.md](mobs.md). |
| `Game/RunTimer`, `Game/RunGameState`, `Game/GameMode` (объект `Game` в сценах `Run` и `TestField`) | День забега, лог списаний, победа — меняет только хост, синхронизируется (см. [run-timer.md](run-timer.md)); спавн героев, смерть группы, дроп лута. |
| `Zones/RunMap` (объект `RunMap` в сцене `Run`) | Хост выбирает seed; в сеть уходят только seed, номер попытки и списки состояний (ворота, квесты, боссы, арены); геометрию каждый строит сам (см. [zones.md](zones.md), [generation.md](generation.md)). Сетевые объекты карты — только боссы. |
| `Game/TrainingDummy` | Сетевой объект; смерть/возрождение — `NetworkVariable` `isActive`. |
| `Loot/LootPickup`, `Loot/Chest` | Лут: дроп создаёт только хост (`GameMode.DropLoot`); подбор/открытие по `InteractPressedServerRpc` → `TryInteract` на хосте; синхронизируются `itemId`, `pickedUp`, `reservedFor`, `opened` (см. [loot.md](loot.md)). |
| `Items/Inventory` | Инвентарь и экипировка героя: `cells` / `equipped` (`NetworkList`) меняет только хост; клиент шлёт `ServerEquipFromCellRpc` / `ServerUnequipSlotRpc` / `ServerMoveItemRpc` (см. [items.md](items.md)). |
| `Skills/Skills`, `Skills/SkillProjectile` | Активные навыки: слоты, перезарядка, мана и эффекты считает хост; клиент шлёт `ServerUseSkillRpc`; синхронизируются слоты, перезарядка, `castCount` (визуал у всех); у снаряда — только `skillId` (см. [skills.md](skills.md)). |
| `Core/DevConsole`, `Core/DebugCommands` | Отладочная консоль (` или F1) вместо `ke * …` из UE; большинство команд работает только у хоста. |

Сообщения при возврате в меню: при обрыве соединения `NetSession` пишет текст в `PendingMessage` («Хост покинул игру», «Не удалось подключиться» или причину отказа сервера), загружает `MainMenu`, и `MainMenuUI` показывает его под кнопками. Если порт занят, «Создать игру» пишет «Не удалось запустить хост (порт 7777 занят?)».

**Ручных шагов для RPC нет.** В UE флаг `Replicates` у новых Server/Multicast событий приходилось ставить вручную в Details. В Unity сетевые методы объявляются атрибутами прямо в коде (`[Rpc(SendTo.Server)]`, `[Rpc(SendTo.Owner)]`), ничего помечать не нужно — всё работает у клиентов сразу.

## Как проверить

- **В редакторе, соло:** открыть `Scenes/TestField` (или `Run`) и нажать Play — компонент `DirectPlayHost` сам запускает хост (как PIE в UE, минуя меню).
- **Полный поток с меню (2 экземпляра: билд + редактор):** собрать билд (File → Build Profiles; сцены `MainMenu`, `Lobby`, `Run`, `TestField` уже в списке). В одном экземпляре «Создать игру», в другом (в редакторе — сцена `MainMenu`) ввести `127.0.0.1` (или IP хоста в сети) и «Подключиться»; хост в лобби — «Начать игру».
- **Быстро:** в редакторе Play на `TestField` (хост стартует сам), в билде — «Подключиться» на `127.0.0.1`: клиент попадает прямо на тестовую карту. Хост на этой машине должен быть один — иначе прямой запуск не сможет занять порт 7777 (в консоли Unity — `[Net] Failed to start host for direct play`).
- **Из командной строки** (каждая строка — отдельное окно):
  ```
  TestedField.x86_64 -host -autostart 2 -name Хост -screen-fullscreen 0 -screen-width 960 -screen-height 540
  TestedField.x86_64 -connect 127.0.0.1 -name Клиент -screen-fullscreen 0 -screen-width 960 -screen-height 540
  ```
  `-host` — создать игру, `-autostart N` — хост сам начинает забег, когда в лобби N игроков, `-connect` — подключиться, `-name` — имя игрока (сохраняется в `PlayerPrefs`).
- **Esc** — выйти в главное меню (в редакторе тоже работает, в отличие от PIE). Если открыт инвентарь, Esc сначала закрывает его.
- Пятый игрок → в меню сообщение «Сервер заполнен (максимум 4 игрока)». Закрыть окно хоста → у клиента «Хост покинул игру».

## Отличия от UE-версии

- Встроенная репликация UE → Netcode for GameObjects: реплицируемые переменные / RepNotify → `NetworkVariable` / `NetworkList` с `OnValueChanged`; Multicast-визуал → сетевые счётчики (`attackCount`, `rollCount`, `hitReactCount`, `castCount`).
- Движение: `CharacterMovementComponent` с предсказанием → `CharacterController` у владельца + `NetworkTransform` (Owner). Телепорт и отбрасывание хост делает через Owner RPC, а не `TeleportTo` / `LaunchCharacter` на сервере.
- `open IP` / `servertravel` → `StartClient` / `NetworkManager.SceneManager.LoadScene`; `Map_MainMenu` / `Map_Lobby` / `Map_Run` / `EmptyMap` → сцены `MainMenu` / `Lobby` / `Run` / `TestField`.
- `MaxPlayers=4` в `DefaultGame.ini` → проверка в Connection Approval.
- `BP_GameInstance` (`NetworkError` / `TravelError`, опции `?closed` / `?failed`) → статический `NetSession` + `PendingMessage`.
- `GameUserSettings` → `GameSettings` (`PlayerPrefs`); в редакторе разрешение не меняется.
- Имя игрока — поле в главном меню (в UE — `PlayerState.PlayerName`, в PIE имя компьютера).
- Флаг `Replicates` для RPC вручную ставить не нужно (см. выше).
- Новое: запуск из командной строки (`-host`, `-connect`, `-autostart`, `-name`), подключение к уже идущему забегу.

## Открытые вопросы (TODO)

- **Хост и клиенты должны быть одной сборки:** версии и данные не сверяются при подключении (важно для генерации карты, см. [generation.md](generation.md)).
