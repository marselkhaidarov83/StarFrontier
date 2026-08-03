# Sprint 4 - Full Combat System

Updated: 2026-08-03
Status: READY_TO_START
Base: main @ b57d223341446b6a79da1ee486ff6508174b3a73

Goal: the player can enter a combat situation, select a target, attack, receive damage, destroy enemies, complete or fail an encounter, claim reward, save/load combat state, and pass QA/Android gates.

Important scope update: Sprint 1-3 are CANONICAL_COMPLETE. Sprint 4 no longer starts with a Sprint 1-3 integration gate.

## Substages and Tasks

### 2A-S04-01 - Канонизация боевой архитектуры и сцены

- `2A-S04-01-T01` Зафиксировать канонический runtime боя: SystemScene или отдельная CombatScene.
- `2A-S04-01-T02` Провести аудит существующих боевых сервисов: encounter, enemy, ally, NPC combat, player attack.
- `2A-S04-01-T03` Убрать или явно пометить debug-only боевые пути, которые не должны попасть в production.
- `2A-S04-01-T04` Зафиксировать ownership боевых root-объектов: enemies, allies, projectiles, VFX.
- `2A-S04-01-T05` Проверить регистрацию всех боевых сервисов в Bootstrap/ServiceRegistry.
- `2A-S04-01-T06` Зафиксировать contract событий боя: start, damage, projectile, destroyed, victory, defeat, reward.
- `2A-S04-01-T07` Проверить, что бой не ломает Sprint 3 управление, камеру, HUD, fuel и targeting.
- `2A-S04-01-T08` Добавить smoke-test: сцена открывается, сервисы боя доступны, ошибок нет.

### 2A-S04-02 - Враги, союзники и encounter lifecycle

- `2A-S04-02-T01` Привязать enemy spawn к реальной системе/маршрутам, а не только к debug system id.
- `2A-S04-02-T02` Проверить и стабилизировать EnemyConfig: hull, shield, energy, speed, weapon, rewards.
- `2A-S04-02-T03` Проверить и стабилизировать EnemyGroupSpawnRuleConfig.
- `2A-S04-02-T04` Реализовать production-условия старта encounter.
- `2A-S04-02-T05` Реализовать корректное появление врагов на System Map.
- `2A-S04-02-T06` Реализовать/проверить союзников в encounter, если они входят в Sprint 4.
- `2A-S04-02-T07` Закрыть победу: все враги уничтожены, encounter переходит в reward pending.
- `2A-S04-02-T08` Закрыть поражение: игрок уничтожен, улетел, или оставил союзников без поддержки.

### 2A-S04-03 - Оружие, снаряды, урон

- `2A-S04-03-T01` Проверить WeaponConfig: damage, range, cooldown, fireRate, projectile speed/lifetime.
- `2A-S04-03-T02` Зафиксировать единый путь атаки игрока: target selected -> weapon fires -> projectile/hit -> damage.
- `2A-S04-03-T03` Зафиксировать единый путь атаки врага/NPC.
- `2A-S04-03-T04` Проверить shield-before-hull damage для игрока, врагов и союзников.
- `2A-S04-03-T05` Проверить уничтожение врага и публикацию события destroyed.
- `2A-S04-03-T06` Проверить уничтожение игрока и defeat event.
- `2A-S04-03-T07` Стабилизировать projectile visuals: spawn, movement, hit, cleanup.
- `2A-S04-03-T08` Добавить tests на cooldown, range, invalid target, dead target, no weapon.

### 2A-S04-04 - Боевой UI, targeting и награды

- `2A-S04-04-T01` Интегрировать боевое targeting-поведение с Sprint 3 target marker.
- `2A-S04-04-T02` Показывать выбранную цель, hull/shield врага и состояние боя.
- `2A-S04-04-T03` Показывать hull/shield игрока в HUD без конфликта с Sprint 3 HUD.
- `2A-S04-04-T04` Добавить понятный CTA для атаки/выбора цели/сброса цели.
- `2A-S04-04-T05` Показывать victory pending reward.
- `2A-S04-04-T06` Реализовать claim reward через существующий GovernmentRewardService.
- `2A-S04-04-T07` Показать defeat state и безопасный выход из него.
- `2A-S04-04-T08` Проверить touch targets и Safe Area на Android.

### 2A-S04-05 - Save/Load, QA gate и Android

- `2A-S04-05-T01` Проверить capture/restore encounter state.
- `2A-S04-05-T02` Проверить сохранение enemies/allies hull, shield, position, alive/dead.
- `2A-S04-05-T03` Проверить восстановление pending reward после reload.
- `2A-S04-05-T04` Проверить, что resolved encounter не воскресает после reload.
- `2A-S04-05-T05` Добавить Sprint 4 verification suite.
- `2A-S04-05-T06` Добавить validator для scene bindings боя.
- `2A-S04-05-T07` Провести regression Sprint 1-3 после включения боя.
- `2A-S04-05-T08` Собрать Android build и пройти player-facing UA.

## Acceptance Gates

- Sprint 4 tasks remain NOT_STARTED until implemented and verified.
- UA_PASSED is assigned only after observable player-facing checks.
- CANONICAL_COMPLETE requires evidence on main: exact SHA, regression, relevant validation tests, and Android evidence for the final gate.

## Current Source Anchors

- Assets/_Project/Scenes/SystemScene.unity
- Assets/_Project/Scenes/CombatScene.unity
- Assets/_Project/Scripts/Core/Services/Combat
- Assets/_Project/Scripts/Core/Services/Enemy
- Assets/_Project/Scripts/Core/Services/Npc
- Assets/_Project/Scripts/Presentation/Combat
- Assets/_Project/Scripts/Presentation/Targeting
- Assets/_Project/Content/Configs/Enemies
- Assets/_Project/Content/Configs/Weapons
