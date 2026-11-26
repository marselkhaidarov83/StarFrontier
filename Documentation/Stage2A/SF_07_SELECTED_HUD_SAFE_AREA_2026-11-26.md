# SF_07 — selected HUD profiles, 2026-11-26
Base: 4e03bfd679672e53ef389470b88653ff2a5f6bcd; stage-2a-prereliz; related to 2A-S03-03-T03.
Scope based on owner screenshots: SF05,06,07,08,10,14,15 top; SF15,16 bottom. SF01–04 existing behavior retained; SF09,11,12,13 not targeted.
- SystemHudSafeAreaAdapter2A selects reviewed profiles using exact checked-in simulator resolution and safe rectangle. Top SF05/06/07/08/10/14 reuse existing 1080x170 artwork; SF15 uses 1080x208. SF03/04 bindings unchanged. Bottom SF15 reuses 1080x170, SF16 reuses 1080x208 (same base sprite as both bottom prefabs). No new artwork file required.
- SystemScene: SafeAreaRoot_BottomHud existing adapter binds both lower artwork variants; original 150-unit HUD remains in safe area; controls unchanged.
- GalaxyScene: PF_GalaxyHudBottomRoot moved into existing SafeAreaRoot_BottomHud; reviewedBottomProfilesOnly preserves full-screen geometry for other profiles. Top adapter uses expanded reviewed profile set. New decorative images do not receive raycasts.
- GalaxyMapViewFitter detects transitions between selected layout profiles, including same-resolution stress profiles.
- No changes to shared prefabs, Config, State, Save, mission or camera zoom logic. Reused sprites may need minor visual adjustment depending on exact proportions. Compile, manual, Android, regression and UA pending. No main integration or production status changes.
