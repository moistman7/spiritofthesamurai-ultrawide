# The Spirit of the Samurai — Ultrawide

A BepInEx 5 plugin that removes the 16:9 lock in *The Spirit of the Samurai* (Windows, Unity/Mono)
and fixes the things that break once the view is wider. Tested at 3440x1440 (21:9).

> Unofficial community mod. It contains no game files. Use at your own risk and keep a backup of
> your saves.

## What it does

- Removes the centered 16:9 viewport and fits the game's resolution presets to your display.
- Rescales the HUD for wide screens and clips parked menu pages so neighboring pages don't peek in.
- Widens sky backdrops and the damage-border sprite so they reach the screen edges.
- Adds black bars outside full-screen cutscene video.

HDR-related extras (separate HUD brightness, vignette removal) live in the companion plugin
**The Spirit of the Samurai — RenoDX HUD**, so you only install them if you want them.

## Requirements

- The Spirit of the Samurai, Windows x64 (tested on Unity 2021.3.45).
- [BepInEx 5.4.23.5 x64 (Mono)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
  Not x86, not BepInEx 6 / IL2CPP.

## Install

1. Close the game and install BepInEx 5 x64 into the game folder (where `samurai.exe` is).
2. Copy `SpiritOfTheSamuraiUltrawide.dll` to `BepInEx/plugins/` (replace any older version).
3. Launch the game. `BepInEx/LogOutput.log` should contain
   `Loading [Spirit of the Samurai Ultrawide 1.8.1]`.

To uninstall, delete the DLL.

## Settings

`BepInEx/config/renodx.spiritofthesamurai.ultrawide.cfg` is created on first run. The ultrawide
fixes have no settings and are always on. The only option is:

| Setting | Default | Meaning |
|---|---|---|
| `Diagnostics.EnableSceneInventory` | `false` | Write a scene dump to `BepInEx/diagnostics` automatically (for bug reports). F9 writes one on demand. |

Entries named `SeparateHud` and `DisableVignette` from 1.7.x are no longer used and can be ignored.

## Known limitations

- Verified visually on one display (3440x1440) and mainly in the village level; other levels,
  16:10 and 32:9 are untested.
- Menu page transitions, cutscenes and the damage border have had limited checking.
- Screen-sized sprites may look slightly stretched horizontally.
- A game update can change the patched methods.

If something looks wrong, press F9, then attach `BepInEx/LogOutput.log` and the files in
`BepInEx/diagnostics` to a bug report. These files stay local; nothing is transmitted.

## Build

Needs the .NET SDK, the game, and BepInEx 5 installed (the project references their DLLs).

    dotnet build -c Release -p:GameDir="<path to the game folder>"

or set the `SPIRIT_OF_THE_SAMURAI_DIR` environment variable. Add `-p:SkipDeploy=true` to skip copying
the DLL into `BepInEx/plugins`. The output is `bin/Release/SpiritOfTheSamuraiUltrawide.dll`.

## How it works

The 16:9 lock is in the game's C# scripts, so the plugin uses Harmony patches instead of shaders:
the resolution presets, `LetterBoxer` and `UIResolutionManager` viewport resets, and `CanvasScaler`
matching. Scene-authored 16:9 assumptions (parked menu pages, sky and overlay sprites, video) are
handled by small per-camera components. See the comments in `Plugin.cs`.

## Credits and license

Built on [BepInEx](https://github.com/BepInEx/BepInEx) and [Harmony](https://github.com/pardeike/Harmony).
MIT licensed, see `LICENSE`. *The Spirit of the Samurai* belongs to its developers.
