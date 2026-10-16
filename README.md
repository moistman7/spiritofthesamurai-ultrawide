# The Spirit of the Samurai — Ultrawide

A BepInEx 5 plugin that removes the 16:9 lock in *The Spirit of the Samurai* (Windows, Unity/Mono)
and fixes the things that break once the view is wider. Includes experimental Steam Deck
(SteamOS/Proton, 16:10) support. Tested on Windows at 3440x1440 (21:9); Deck hardware is untested.

> Unofficial community mod. It contains no game files. Use at your own risk and keep a backup of
> your saves.

## What it does

- Removes the centered 16:9 viewport and fits the game's resolution presets to your display,
  including 16:10. Caps presets at the display resolution by default to avoid supersampling.
- Rescales the HUD for wide screens and clips parked menu pages so neighboring pages don't peek in.
- Widens sky backdrops and the damage-border sprite so they reach the screen edges.
- Adds black bars outside full-screen cutscene video.

## Requirements

- The Spirit of the Samurai, Windows x64 (tested on Unity 2021.3.45).
- [BepInEx 5.4.23.5 x64 (Mono)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
  Not x86, not BepInEx 6 / IL2CPP.

## Install

1. Close the game and install BepInEx 5 x64 into the game folder (where `samurai.exe` is).
2. Copy `SpiritOfTheSamuraiUltrawide.dll` to `BepInEx/plugins/` (replace any older version).
3. Launch the game. `BepInEx/LogOutput.log` should contain
   `Loading [Spirit of the Samurai Ultrawide 1.9.0]`.

To uninstall, delete the DLL.

### Steam Deck / SteamOS (experimental)

1. Switch to Desktop Mode. In Steam, open the game's **Properties > Installed Files > Browse**
   to find the folder containing `samurai.exe` (works for internal storage and microSD installs).
2. Extract **Windows x64 BepInEx 5.4.23.5** into that folder. Ensure `winhttp.dll`,
   `doorstop_config.ini`, and `BepInEx/` sit beside `samurai.exe`.
3. Copy this mod's DLL into `BepInEx/plugins/`. The release ZIP contains the plugin and docs;
   install BepInEx separately. The same plugin DLL works on Windows and under Proton.
4. Add this in **Properties > General > Launch Options**:

   ```sh
   WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
   ```

   If you already use launch options, merge the override into them and keep one `%command%`.
   This follows the [BepInEx Proton instructions](https://docs.bepinex.dev/master/articles/advanced/steam_interop.html#adding-the-dll-override).
5. Return to Gaming Mode and launch through Steam using Proton. Select a high resolution/quality
   preset in the game: presets above the exposed display size are capped to it, so a 1280x800
   display receives 1280x800. Lower presets remain available for performance.
6. Check `BepInEx/LogOutput.log` for the plugin loading message. If no log is created, check the
   DLL override and BepInEx file placement. If Steam exposes only 1280x720, check the game's
   resolution setting in Steam's properties; the plugin uses the display size reported by Unity.

For diagnostics without a keyboard, map a rear button to **F9** in Steam Input, or enable
`Diagnostics.EnableSceneInventory` in the plugin config while in Desktop Mode. Remove the
plugin DLL to uninstall; remove the override only if no other BepInEx plugins need it.

The viewport fills 16:10, but gameplay framing, menus, cutscenes, suspend/resume, and performance
still need verification on a Deck. This mod does not change controller support or graphics quality.

## Settings

`BepInEx/config/renodx.spiritofthesamurai.ultrawide.cfg` is created on first run. The ultrawide
fixes are always on. Available options:

| Setting | Default | Meaning |
|---|---|---|
| `Display.LimitResolutionToDisplay` | `true` | Cap fitted presets at the display size. Set to `false` to allow supersampling. |
| `Diagnostics.EnableSceneInventory` | `false` | Write a scene dump to `BepInEx/diagnostics` automatically (for bug reports). F9 writes one on demand. |

## Known limitations

- Verified visually on one display (3440x1440) and mainly in the village level; other levels,
  Steam Deck/Proton, 16:10 and 32:9 are untested.
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
