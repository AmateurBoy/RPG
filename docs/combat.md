# Боевая система: урон и смерть (P0-02)

## Как устроено

Всё считает хост (`IsServer`), клиенты получают результат через `NetworkVariable`. Все расстояния — в метрах.

1. **Удар.** Владелец: ЛКМ → `TryAttack` (не в замахе, не в кувырке, не упал, стамины ≥ `AttackStaminaCost`) → `AttackServerRpc` (повторная проверка и списание стамины `HeroStats.TrySpendStamina`) → сетевой счётчик `attackCount` → у всех `OnRepAttackCount` → `StartAttackLocal`: замах мечом (`HeroVisual.PlaySwing`), очистка списка `hitActorsThisSwing`.
2. **Хитбокс.** На хосте `ServerUpdate → UpdateAttack` каждый кадр считает прогресс замаха `t = 0..1`. Пока `AttackHitWindowStart <= t <= AttackHitWindowEnd`, вызывается `DoAttackHit`: `Combat.OverlapSphere` (`Physics.OverlapSphere` по всем слоям, триггеры игнорируются) радиусом `AttackRadius` с центром `HitboxCenter` = позиция + `AttackRange` вперёд + `AttackHeight` вверх. Цель — компонент `HeroStats` на объекте коллайдера или у его родителя.
3. **Фильтр целей** (`Combat.CanHitTarget`): не сам атакующий, ещё не задет в этом замахе, есть `HeroStats`, цель не мертва, другой герой — только при `FriendlyFire`. Задетые цели попадают в `hitActorsThisSwing`, поэтому одна цель получает урон максимум один раз за замах.
4. **Урон.** `target.ApplyDamage(GetAttackDamage(), атакующий)` — напрямую в `HeroStats` цели. `GetAttackDamage` = `AttackDamage` + модификаторы надетых предметов (P0-11, [items.md](items.md)).
5. **`HeroStats.ApplyDamage`** (общий компонент здоровья для героев, манекенов, мобов и боссов): не проходит, если вызван не на хосте, цель мертва, неуязвима (`IsInvulnerable`: флаг `SetInvulnerable` или окно `invulnerableUntil` по серверному времени NGO) или урон ≤ 0. Иначе: HP ↓ → `OnHealthChanged` (у всех) → `OnDamaged(amount, causer)` (хост) → отклик на попадание → при HP = 0 `isDead = true` и `OnDeath`. Возвращает `true`, если урон прошёл.
6. **Отклик на попадание** (`PlayHitReact`, хост): сетевой счётчик `hitReactCount` → у всех `OnRepHitReactCount`: overlay-вспышка (`OverlayFlash`: материал `HitFlashMaterial` добавляется вторым проходом ко всем рендерерам `FlashRoot`) на `HitFlashDuration` секунд и звук `HitSound`. Если объект умеет получать отбрасывание (`IKnockbackReceiver` — герой и моб) и `KnockbackSpeed > 0`, хост отбрасывает его от атакующего: героя — через владельца (`KnockbackRpc`, скорость гасится `KnockbackDeceleration`), моба — `NavMeshAgent.Move` на хосте.
7. **Кувырок.** Space → `TryRoll`: владелец сразу начинает кувырок (`RollDistance` за `RollDuration`, в сторону ввода или, без ввода, куда смотрит герой) и шлёт `RollServerRpc` → хост списывает стамину и вызывает `GrantInvulnerability(IFrameDuration)`: неуязвимость с начала кувырка на `IFrameDuration` секунд.
8. **Смерть героя.** `HeroStats.OnDeath` → `HeroController.HandleDeath` (хост) → `isDowned = true`, спринт выключен, своё поднятие союзника отменено, `GameMode.NotifyHeroDowned` (см. [party-death.md](party-death.md)). У всех `OnRepIsDowned`: сброс атаки, кувырка и отбрасывания, модель ложится (`HeroVisual.SetDowned`: `DownedVisualRoll`, `DownedVisualOffsetY`); ввод владельца заблокирован. Объект не уничтожается. Поднятие: `GetRevived` на хосте (`HeroStats.Revive` + `isDowned = false`) — модель встаёт.
9. **Стамина.** Удар и кувырок не стартуют, если стамины меньше стоимости (проверка и у владельца `HasStamina`, и на хосте `TrySpendStamina`); восстановление начинается через `StaminaRegenDelay` после последней траты. Спринт (Shift) тратит `SprintStaminaPerSecond` в секунду; после полного истощения снова доступен, когда стамина восстановится до `SprintResumeStamina`.

## Конфиг (инспектор префаба `Prefabs/Network/Hero`)

| Где | Поле | Значение | Смысл |
|---|---|---|---|
| `HeroController` → Combat | `AttackDamage` | 25 | Базовый урон (до P0-16) |
| | `AttackRange` / `AttackRadius` | 0.9 / 0.8 м | Центр и радиус сферы хитбокса |
| | `AttackHeight` | 1 м | Высота центра сферы над землёй |
| | `AttackDuration` | 0.4 с | Длительность замаха |
| | `AttackHitWindowStart` / `End` | 0.2 / 0.55 | Окно активного хитбокса, доля замаха |
| | `AttackStaminaCost` | 10 | Стоимость удара |
| | `FriendlyFire` | false | Урон по другим героям |
| | `DebugDrawHitbox` | false | Рисовать сферу хитбокса |
| `HeroController` → Roll | `RollStaminaCost` | 25 | Стоимость кувырка |
| | `RollDuration` / `RollDistance` | 0.45 с / 4 м | Длительность и длина кувырка |
| | `IFrameDuration` | 0.3 с | Неуязвимость с начала кувырка |
| `HeroController` → Movement | `WalkSpeed` / `SprintSpeed` | 4.5 / 7 м/с | Скорость шага и бега |
| | `SprintStaminaPerSecond` / `SprintResumeStamina` | 10 / 20 | Расход стамины на бег; с какого запаса бег снова доступен после истощения |
| | `AttackMoveSpeedMultiplier` | 0.5 | Множитель скорости во время замаха |
| | `KnockbackDeceleration` | 20 м/с² | Торможение после отбрасывания |
| | `TurnSpeed` / `Gravity` | 720 °/с / −20 м/с² | Поворот и гравитация |
| `HeroController` → State | `DownedVisualRoll` / `DownedVisualOffsetY` | 90 / −0.6 | Поза «упал» |
| `HeroStats` → HitReact | `KnockbackSpeed` | 4.5 м/с | Скорость отбрасывания (0 — без него) |
| | `HitFlashDuration` | 0.12 с | Длительность вспышки |
| | `HitFlashMaterial` | `Materials/M_HitFlash` | Overlay-материал вспышки (пусто — `GameAssets.HitFlashMaterial`) |
| | `HitSound` | пусто | Звук попадания; пусто — заглушка, сгенерированная в коде (`ProceduralAudio`) |
| `HeroStats` → Stats | `MaxHealth` / `MaxMana` / `MaxStamina` | 100 / 50 / 100 | Базовые максимумы |
| | `HealthRegenRate` / `ManaRegenRate` / `StaminaRegenRate` | 0 / 2 / 25 в с | Регенерация |
| | `StaminaRegenDelay` | 1 с | Пауза перед восстановлением стамины |
| `TrainingDummy` (префаб `TrainingDummy`) | `RespawnDelay` | 3 с | Возрождение манекена (100 HP, без отбрасывания) |

## Отладка

- `HeroController.DebugDrawHitbox = true` — сфера хитбокса рисуется на время активного окна (`Debug.DrawLine`: видно в Scene view или в Game view с включёнными Gizmos, на хосте).
- Выделенный герой в Scene view показывает гизмо хитбокса (красная сфера) и радиус поднятия (зелёная).

## Как проверить

1. Play на `Scenes/TestField` (хост стартует сам). Перед стартом три манекена (`Dummy_1…3`).
2. ЛКМ по манекену — вспышка, звук, HP манекена падает (25 без меча, 35 с надетым мечом); после смерти манекен исчезает и через 3 с возрождается. SP −10 за удар; при SP < 10 удар не стартует.
3. Space — кувырок на 4 м, SP −25. Моб `Mob_Field` (к югу от старта): кувырок в момент удара (круг на земле заполнился) — урона нет; выход из круга — тоже.
4. Удар моба — героя отбрасывает, вспышка.
5. Два экземпляра (см. [networking.md](networking.md)): удар и кувырок клиента видны у хоста и наоборот; при `FriendlyFire = false` герои друг друга не бьют.

## Отличия от UE-версии

- Единицы — метры (было: `AttackRange` / `AttackRadius` 90 / 80 см, `KnockbackSpeed` 450 см/с, `DownedVisualOffsetZ` −60 см).
- Урон вызывается напрямую `HeroStats.ApplyDamage` (в UE — движковый `ApplyDamage` → `EventAnyDamage`).
- Хитбокс — `Physics.OverlapSphere` по всем слоям с центром на высоте `AttackHeight` (в UE — `SphereOverlapActors` по WorldStatic/WorldDynamic/Pawn/PhysicsBody).
- Отбрасывание героя — через владельца (Owner RPC), т.к. движение у владельца; в UE — `LaunchCharacter` на хосте с репликацией `CharacterMovementComponent`.
- Кувырок стартует у владельца сразу, хост только списывает стамину и даёт i-frames.
- Вспышка — `OverlayFlash` (материал вторым проходом, аналог overlay-материала), звук-заглушка генерируется в коде вместо `S_Hit_Placeholder`.
- Окно неуязвимости считается по серверному времени NGO (`NetworkManager.ServerTime`).
- RepNotify-счётчики `AttackCount` / `RollCount` / `HitReactCount` / `bIsDowned` → `NetworkVariable` с `OnValueChanged`.

## Открытые вопросы (TODO)

- **Урон из статов (P0-16).** `HeroController.GetAttackDamage` возвращает `AttackDamage` из конфига + бонусы предметов — заменить на чтение статов атакующего.
- **Friendly fire.** В задаче не определён; по умолчанию выключен (`FriendlyFire = false`).
- **Отбрасывание задаётся на цели** (`KnockbackSpeed` в `HeroStats`), а не на оружии/атакующем — когда появятся разные оружия/навыки, вероятно, понадобится сила отбрасывания от источника урона.
- **Форма хитбокса** — сфера перед героем, а не меш меча: с изометрической камерой и текущей анимацией рубящего удара так надёжнее. Если нужен точный хитбокс по клинку — заменить в `DoAttackHit`.
- **Поза «упал»** — заглушка (поворот модели), анимаций нет: замах и кувырок — процедурные (`HeroVisual`).
- **Кувырок без подтверждения.** Если хост отклонит кувырок (по его данным стамины не хватило), владелец всё равно кувыркнётся — без i-frames и без списания стамины. Сейчас владелец проверяет стамину заранее, поэтому на практике это редкость.
- **Смерть всей группы** — сделана в P0-04, см. [party-death.md](party-death.md).
