# 2A-S03-03-T01 — HUD Legacy Audit

- Generated UTC: `2026-07-28 13:48:47`
- Unity: `6000.3.11f1`
- Branch: `task/2A-S03-03-T01-hud-audit`
- Exact SHA: `NOT_RECORDED`

> Generated file. Do not store final manual decisions here. Fill HUD_Mapping.md instead.

## Enabled Build Settings scenes

- `Assets/_Project/Scenes/BootstrapScene.unity`
- `Assets/_Project/Scenes/MainMenuScene.unity`
- `Assets/_Project/Scenes/SystemScene.unity`
- `Assets/_Project/Scenes/GalaxyScene.unity`
- `Assets/_Project/Scenes/CombatScene.unity`
- `Assets/_Project/Scenes/SettingsScene.unity`
- `Assets/_Project/Scenes/LoadingScene.unity`

# Scene: `Assets/_Project/Scenes/BootstrapScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| True | `Canvas/BackgroundImage` | Direct Canvas child |
| True | `Canvas/BackgroundImage/MainPanel` | HUD token in name |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/MainMenuScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| True | `Canvas/bg_main_menu_01` | Direct Canvas child |
| True | `Canvas/bg_main_menu_01/MainPanel` | HUD token in name |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/SystemScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| False | `Canvas/ErrorImage` | Direct Canvas child |
| True | `Canvas/PF_HudTopRoot` | Direct Canvas child |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel` | HUD token in name |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel/FuelImage` | HUD token in name |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel/FuelText` | HUD token in name |
| True | `Canvas/PF_SystemHudBottomRoot` | Direct Canvas child |
| True | `Canvas/PF_SystemHudBottomRoot/CenterPanel` | HUD token in name |
| True | `Canvas/PF_SystemHudBottomRoot/LeftPanel` | HUD token in name |
| True | `Canvas/PF_SystemHudBottomRoot/LeftPanel/StatusText` | HUD token in name |
| True | `Canvas/PF_SystemHudBottomRoot/LeftPanel/TargetText` | HUD token in name |
| True | `Canvas/PF_SystemHudBottomRoot/RightPanel` | HUD token in name |
| False | `Canvas/PlanetRoot` | Direct Canvas child |
| False | `Canvas/PlanetRoot/BackgroundImage/ActionButtonsBlock/RefuelButton` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/FuelItemsScrollView` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/FuelCountSlider` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/FuelIcon` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/FuelText` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/RefuelButton` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/RefuelCostText` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/RefuelWarningText` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/GovernmentBlock/ClaimRewardPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/GovernmentBlock/MissionItemsScrollView/Viewport/MissionContent/MissionCardView/StatusText` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/GovernmentBlock/MissionPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/EquipmentPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/ShipListPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/ShipPreviewPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/ShipPreviewPanel/ShipDescription` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/ShipPreviewPanel/ShipImage` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/ShipPreviewPanel/ShipName` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HangarBlock/StatsPanel` | HUD token in name |
| False | `Canvas/PlanetRoot/BackgroundImage/HeaderBlock/PlanetStatusText` | HUD token in name |
| True | `Canvas/SystemMapHUD` | Direct Canvas child |
| True | `Canvas/SystemMapHUD/ActionButtonsBlock/ReturnToShipButton` | HUD token in name |
| False | `Canvas/SystemMapHUD/CurrentPositionMarker` | HUD token in name |
| False | `Canvas/SystemMapHUD/InvalidTargetMarker` | HUD token in name |
| False | `Canvas/SystemMapHUD/MapPointDestinationMarker` | HUD token in name |
| False | `Canvas/SystemMapHUD/PlanetDestinationMarker` | HUD token in name |
| False | `Canvas/SystemMapHUD/SelectedTargetFrame` | HUD token in name |
| False | `Canvas/SystemMapHUD/ShipEngineGlow` | HUD token in name |
| False | `Canvas/SystemMapHUD/SystemEntryMarker` | HUD token in name |
| False | `Canvas/SystemMapHUD/SystemExitMarker` | HUD token in name |

### External scene-object references from UI

| Classification | UI component | Serialized field | Referenced object | Type |
|---|---|---|---|---|
| REVIEW_EXTERNAL_REFERENCE | `SystemCameraReturnButton2A @ Canvas/SystemMapHUD/ActionButtonsBlock/ReturnToShipButton` | `cameraController` | `SystemMapRoot` | `SystemCameraController2A` |

## Duplicate HUD-like names

### `fueltext`
- Active `False` — `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/FuelText`
- Active `True` — `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel/FuelText`

### `refuelbutton`
- Active `False` — `Canvas/PlanetRoot/BackgroundImage/ActionButtonsBlock/RefuelButton`
- Active `False` — `Canvas/PlanetRoot/BackgroundImage/FuelBlock/HeaderPanel/RefuelButton`

### `statustext`
- Active `False` — `Canvas/PlanetRoot/BackgroundImage/GovernmentBlock/MissionItemsScrollView/Viewport/MissionContent/MissionCardView/StatusText`
- Active `True` — `Canvas/PF_SystemHudBottomRoot/LeftPanel/StatusText`

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/GalaxyScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| False | `Canvas/ErrorImage` | Direct Canvas child |
| True | `Canvas/GalaxyHudBottomRoot` | Direct Canvas child |
| True | `Canvas/GalaxyHudBottomRoot/CenterPanel` | HUD token in name |
| True | `Canvas/GalaxyHudBottomRoot/CenterPanel/StatusText` | HUD token in name |
| True | `Canvas/GalaxyHudBottomRoot/LeftPanel` | HUD token in name |
| True | `Canvas/GalaxyHudBottomRoot/LeftPanel/TargetSystemText` | HUD token in name |
| True | `Canvas/GalaxyHudBottomRoot/RightPanel` | HUD token in name |
| True | `Canvas/PF_HudTopRoot` | Direct Canvas child |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel` | HUD token in name |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel/FuelImage` | HUD token in name |
| True | `Canvas/PF_HudTopRoot/BackgroundImage/GroupCenter/Fuel/FuelText` | HUD token in name |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/CombatScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/SettingsScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| True | `Canvas/bg_main_menu_01` | Direct Canvas child |
| True | `Canvas/bg_main_menu_01/MainMenuPanel` | HUD token in name |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


# Scene: `Assets/_Project/Scenes/LoadingScene.unity`

- All Canvas components: **1**
- Root Canvas components: **1**

## Root Canvas: `Canvas`

- Active in hierarchy: `True`
- Active self: `True`
- Render Mode: `ScreenSpaceOverlay`
- Sorting Order: `0`
- Canvas Scaler: `Mode=ScaleWithScreenSize; Ref=(1080.00, 1920.00); Match=0.5`
- Graphic Raycaster: `PRESENT`
- Initial classification: `SCREEN_SPACE_PRODUCTION_REVIEW`

### HUD/panel candidates

| Active | Object path | Reason |
|---|---|---|
| True | `Canvas` | Canvas root |
| True | `Canvas/BackgroundImage` | Direct Canvas child |
| True | `Canvas/BackgroundImage/MainPanel` | HUD token in name |

### External scene-object references from UI

_No external scene-object references found._

## Duplicate HUD-like names

_No duplicate HUD-like names found._

## Missing Script findings

_No Missing Script components found._


