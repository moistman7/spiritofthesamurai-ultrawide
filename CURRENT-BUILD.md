# Current working build — 2026-10-18

`BepInEx/plugins/SpiritOfTheSamuraiUltrawide.dll` is the v1.15.0 release build, with the same functionality as the installed development build confirmed working by the user. The release preparation updates version metadata; it does not replace the installed DLL. See `BepInEx/SHA256SUMS.txt` for its SHA-256.

This snapshot adds a screen vignette toggle (moved from the HDR companion), keyboard/controller binding pickers, separate resets for each device, clearer binding labels and conflict handling, full-width loading fades, and further frame-work cleanup. The user confirmed the complete installed build works well. It retains game-themed context windows for feature toggles. It also includes session-only history, configurable controller shortcuts and fading feedback, controller-aware cursor visibility, Takeshi/playable Chisai/companion Chisai light suppression, conservative allocation cleanup, manual-dialogue fallback, centered cutscene skip-bar anchoring, and right-edge arrow/bag HUD positioning.

240 regression assertions passed, with zero build warnings/errors. The user confirmed the current installed build works. No measured frame-time improvement is claimed.

## Install

Close the game, back up your existing plugin DLL, then copy the included `BepInEx` folder into the game directory. BepInEx 5 must already be installed. Keep the separate RenoDX HUD companion installed; it is not bundled here.

Only the current installed plugin binary is included. No older builds, game assets, personal config/history, or failed preloader experiments are included.

## Unity splash removal

The confirmed splash removal is a separate local game-data change, not a capability of the plugin DLL alone. `tools/SplashSettings` reproduces the settings in the user's working installation without distributing game assets. See its README for preparation, verification, backup, installation and rollback. Steam verification or game updates can undo that local change.
