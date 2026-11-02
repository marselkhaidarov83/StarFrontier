# SF_07 — дополнение по аналитике первого игрового опыта

Проверено по коду ветки `stage-2a-prereliz` на исходном SHA
`a36994dfc4e835ecb9e7417041e739959939b38b`. Изменения ниже относятся к
рабочей копии подэтапа 2A-S08-06 и до Unity/Android-проверок остаются
`IMPLEMENTED_UNVERIFIED`. Этот файл дополняет SF_07 v6 от 21.10.2026, где
появившаяся позже аналитика ещё не описана.

| Назначение | Подтверждённая точка |
| --- | --- |
| Контракт, имена событий и безопасная схема полей | `Assets/_Project/Scripts/Data/Analytics/PlayerAnalyticsRecord.cs`; `Assets/_Project/Scripts/Core/Services/Analytics/PlayerAnalyticsService.cs` |
| Данные первой сессии, стадии, интервалы и итоговый снимок | `PlayerAnalyticsService.StartSession`, `Track`, `TryTrackFirstSessionStage`, `FinishSession` |
| Постоянный признак первой сессии и команда сброса только для Editor | `Assets/_Project/Scripts/Core/Services/Analytics/UnityPlayerAnalyticsPreferences.cs`; меню `Tools/STAR FRONTIER/Analytics/Reset First Session` |
| Существующие события перехода и боя | `Assets/_Project/Scripts/Core/Services/Analytics/PlayerAnalyticsEventBridge.cs` |
| Принятие и завершение миссии | `Assets/_Project/Scripts/Core/Services/Mission/MissionService.cs` |
| Первый вылет | `Assets/_Project/Scripts/Presentation/Planet/PlanetController.cs`, `OnOpenSystemClicked` |
| Подбор лута и причины отказа | `Assets/_Project/Scripts/Core/Services/Npc/SystemSpaceLootService.cs`, `TryCollectForPlayer` |
| Оснащение как первый шаг улучшения | `Assets/_Project/Scripts/Core/Services/Hangar/HangarService.cs`, `EquipWeapon`, `EquipModule` |
| Отказ выбора цели | `Assets/_Project/Scripts/Core/Services/Targeting/TargetService2A.cs`, `TrySelectTarget` |
| Блокировка маршрута из-за перегруза | `Assets/_Project/Scripts/Core/Services/Inventory/PlayerCargoOverload2A.cs`, `RejectRouteIfOverloaded` |
| Регистрация и завершение | `Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs`, `InitializePlayerAnalytics`, `OnDestroy`, `OnApplicationQuit` |
| Канал отправки | `Assets/_Project/Scripts/Core/Services/Analytics/AppMetricaPlayerAnalyticsTransport.cs`, `Report` |
| Приёмник аналитики и ключи | `Assets/_Project/Scripts/Data/Analytics/PlayerAnalyticsConfig.cs`; `Assets/_Project/Content/Configs/Game/playerAnalyticsConfig_01.asset` |
| Выключаемый вывод в Console | `Assets/_Project/Scripts/Data/Configs/Debug/DebugLogConfig.cs`; `Assets/_Project/Content/Configs/Game/debugLogConfig01.asset`; `PlayerAnalyticsUnityFactory.Create` |
| Назначение конфига в сцене | `Assets/_Project/Scenes/BootstrapScene.unity`, поле `playerAnalyticsConfig` у `Bootstrapper` |
| Автотесты контрактов | `Assets/_Project/Tests/EditMode/Sprint7Analytics/PlayerAnalyticsContractTests.cs` |

Отчёт передаётся событием `sf_first_session_report`: `first_session_stage_path`
содержит последовательность, `first_session_stage_gaps` — интервалы в секундах,
`last_first_session_stage` — последний этап. Каждое событие
`sf_first_session_stage` содержит текущий снимок пути, поэтому последний
наблюдаемый этап доступен и до закрытия приложения. Ошибки содержат
`first_session_last_attempt`, `first_session_last_failure_reason` и число
отказов. Отправка в AppMetrica на Android ещё не подтверждена.

Проверки Unity, автотестов, Android build/device, пользовательская приёмка и
интеграция в `main` не проводились. Компиляция не запускалась по запросу
владельца. Production-статусы SF_03 и SF_06 этим документом не меняются.
