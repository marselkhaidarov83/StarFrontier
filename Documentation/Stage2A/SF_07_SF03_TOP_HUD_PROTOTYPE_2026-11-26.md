# SF_07 — SF 03 top HUD prototype (2026-11-26)
Base: 5fa479f147c974cff99e69f463e8ba349a25e35e; stage-2a-prereliz. Related to 2A-S03-03-T03.
- SF_03_Pixel_3_XL.device: 1440x2960, safe rect 0,0,1440,2780; top inset 180 px.
- Assets/Art/UI/HUD/hud_top_panel_02_sf03_tall.png: 1080x208 RGBA, derived from existing 1080x128 sprite by adding 80 rows in its center; corners and lower edge preserved.
- SystemHudSafeAreaAdapter2A: exact SF03 geometry gate, dedicated non-raycast background reaching full screen top; original safe content rect and text transforms preserved. Existing system caps restored on other profiles.
- SystemScene: existing top adapter sprite binding.
- GalaxyScene: PF_HudTopRoot moved into existing SafeAreaRoot_TopHud; full screen fallback for every profile except SF03. GalaxyMapViewFitter detects SF02/SF03 transitions at equal resolution.
- No bottom HUD, button layout, Config, State, Save or game logic changes. Unity compile, PlayMode, regression SF01/SF02, Android and UA pending. No main integration or status changes.
