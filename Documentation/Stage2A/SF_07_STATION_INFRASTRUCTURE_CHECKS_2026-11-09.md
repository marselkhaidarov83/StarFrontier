# SF_07 — команды и проверки инфраструктуры, 2A-S07-05

Дата: 09.11.2026. Источник: GitHub и чистая локальная копия `stage-2a-prereliz` @ `f2b46edfd0eff404970004f300d24654b45fceb8`; main @ `5f4e19b788fcae8660b5f82702d831bb5e426f7a`. GitHub HEAD и содержимое станционных debug-команд проверены через GitHub. Изменения в рабочем дереве, commit/push/merge не выполнялись. Т04 исключена владельцем из текущей работы. Этот документ уточняет реализацию SF_07, не меняет production-статусы.

## Существующие команды сохранены

Все пункты находятся в контекстном меню `DebugController2A` на `BootstrapRoot` сцены `Assets/_Project/Scenes/BootstrapScene.unity`. В Play Mode компонент доступен также после перемещения корня в DontDestroyOnLoad.

| Файл относительно корня Unity-проекта | Класс / метод | Пункт меню STAR FRONTIER |
|---|---|---|
| Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.ArrivalAndBuild.cs | DebugBuildCurrentStationInstantly / StationArrivalBuildDebug2A.TryBuildFirstLevel | Station/Мгновенно построить станцию в текущей системе |
| Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.StationUpgrade.cs | DebugIncreaseCurrentStationLevel / StationUpgradeDebug2A.TryIncrease | Station/Мгновенно повысить уровень станции на 1 |
| Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.Stations.cs | DebugDamage100CurrentStation / StationDebugCommands2A.TryDamage100Current | Station/Урон 100 |
| Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.Stations.cs | DebugDestroyCurrentStation / StationDebugCommands2A.TryDestroyCurrent | Station/Уничтожить станцию в текущей системе |
| Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.Stations.cs | **новые** DebugRestoreCurrentStation / StationDebugCommands2A.TryRestoreCurrent | Station/Мгновенно восстановить станцию в текущей системе |

Новые Inspector-поля, ассеты и регистрации сервисов не нужны. Используются существующие `IGameSessionService`, `IStationService`, `ISystemNpcRuntimeService`, `SimpleEventBus`. Их регистрация уже находится в `Bootstrapper.RegisterServices()`.

`DebugRestoreCurrentStation` использует `DeveloperModeGate.CanRunExplicitCommand`: только Play Mode; в редакторе ручной вызов разрешён независимо от показа debug HUD. На Android контекстное меню Inspector недоступно; отдельная мобильная debug-панель не добавлена.

`TryRestoreCurrent` восстанавливает только разрушенную построенную станцию текущей системы. Захват, вторжение, живые враги/враждебные NPC, отключённая станция и отсутствующий конфиг запрещают действие. Используется обычный `StationLifecycleRules.BeginPaidOperation` → `Advance(0)`; debug-снимок явно отмечен `debug_free_recovery`. Оплата не вызывается, уровень и независимый оплаченный Development сохраняются. Legacy-восстановление с таймером завершается без повторной оплаты. Повтор на восстановленной станции отклоняется. Успех согласует состояние через `IStationService.ReconcileSystem` и публикует один `SaveNeedEvent("debug_station_restored")`.

## Дополненные существующие тесты

Все тесты остаются в существующей сборке `Sprint7Verification`, папка `Assets/_Project/Tests/EditMode/Sprint7Stations/`.

- `StationVisitEditModeTests.cs`: восстановление новой/legacy-операции, сохранение уровня и оплаченного прогресса, отсутствие новой оплаты, одна публикация изменения/запроса сохранения, повтор команды; шесть запретов без изменения мира и сохранения — 8 новых случаев.
- `StationServiceEditModeTests.cs`: сквозная цепочка строительства, улучшения, повреждения, разрушения и восстановления с JSON-загрузкой и пересозданием сервиса между этапами; используется настоящий `StationResourcePaymentGateway`. Миграция старой повреждённой/разрушенной инфраструктуры через `SaveMigrationService` и привязку сессии, повторная миграция — 3 новых случая.
- `StationMilitaryRestoreEditModeTests.cs`: научный эффект при повторной инициализации и загрузке, повреждении/разрушении/восстановлении; саморемонт при повторном входе из наблюдателя и при пересоздании сервиса после загрузки — 2 новых случая.

Повторное начисление тиков ремонта, офлайн-интервал и стоимость уже проверялись прежними тестами; эти сценарии сохранены. Всего добавлено 13 случаев.

## Границы подтверждения

09.11.2026, 14:20 МСК, до последующего указания владельца больше не запускать компиляцию и тесты: Unity 6000.3.11f1, отдельная копия проекта, EditMode, сборка `Sprint7Verification`: **482/482 PASSED**, 0 failed, 0 skipped. XML находится в пакете задачи `station_s07_05_2026_11_09/verification/Sprint7.xml`. Основа копии — точный базовый SHA выше плюс четыре изменённых исходника; новый коммит не создавался.

После указания владельца новых компиляций и прогонов не выполняется. Запуск в отдельной копии не является ручной Unity/Play Mode-проверкой, UA, Android/device или подтверждением интеграции в main. Владелец получает отдельный ручной чек-лист. Синхронизированные документы sources не изменялись.
