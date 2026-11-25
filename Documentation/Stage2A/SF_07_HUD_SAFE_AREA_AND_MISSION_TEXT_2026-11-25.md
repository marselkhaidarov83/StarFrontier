# SF_07 — HUD safe area and mission card layout — 2026-11-25

Base: stage-2a-prereliz @ d29cf897dc62c3010f6f5f0c79357bc4d19aad26. Defect correction, related to 2A-S03-03-T03 (safe area adapter source tag); formal task/status mapping pending Navigator.

- SystemScene.unity: Canvas/SafeAreaRoot_TopHud and SafeAreaRoot_BottomHud already enable fillTopUnsafeArea/fillBottomUnsafeArea respectively. No scene changes in this revision.
- Assets/_Project/Scripts/Presentation/HUD/SystemHudSafeAreaAdapter2A.cs: UpdateHudDecoration creates separate non-raycast Compact/Medium/Extended caps based on inset/screen height (3.5%, 7.5%). Original HUD artwork and safe-area geometry remain intact. Side fills limited to HUD band.
- Assets/_Project/Prefabs/Hud/PF_HudTopRoot.prefab: GroupLeft/Center/Right shifted upward 8 canvas units; HUD text autosizing and margins.
- Assets/_Project/Prefabs/Hud/PF_SystemHudBottomRoot.prefab: StatusText/TargetText/DayText/ModeText autosizing, margins, taller rects.
- Assets/_Project/Scripts/Presentation/Mission/MissionCardView.cs: LayoutMissionBody measures title and body, reserves a separate action row and bottom padding.
- PlanetController and service panel navigation unchanged. No Config/State/Save changes.
- Static source change only. Unity compile, EditMode, PlayMode, Android and UA pending. No main integration.
