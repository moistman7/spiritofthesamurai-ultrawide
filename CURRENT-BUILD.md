# Current working build — 2026-10-17

`BepInEx/plugins/SpiritOfTheSamuraiUltrawide.dll` is an exact copy of the installed DLL confirmed working by the user. Its assembly version remains 1.11.0; it contains the newer changes committed after the published v1.11.0 release. See `BepInEx/SHA256SUMS.txt` for its SHA-256.

This snapshot includes session-only history, configurable controller shortcuts and fading feedback, controller-aware cursor visibility, Takeshi/playable Chisai/companion Chisai light suppression, conservative allocation cleanup, manual-dialogue fallback, centered cutscene skip-bar anchoring, and right-edge arrow/bag HUD positioning.

229 regression assertions passed, with zero build warnings/errors. The user confirmed the current installed build works. No measured frame-time improvement is claimed.

## Install

Close the game, back up your existing plugin DLL, then copy the included `BepInEx` folder into the game directory. BepInEx 5 must already be installed. Keep the separate RenoDX HUD companion installed; it is not bundled here.

Only the current installed plugin binary is included. No older builds, game assets, personal config/history, or failed preloader experiments are included.

## Unity splash removal

The confirmed splash removal is a separate local game-data change, not a capability of the plugin DLL alone. `tools/SplashSettings` reproduces the settings in the user's working installation without distributing game assets. See its README for preparation, verification, backup, installation and rollback. Steam verification or game updates can undo that local change.
