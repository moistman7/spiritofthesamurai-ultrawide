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
- Adds faster automatic dialogue, an automatic/manual toggle, and session dialogue history with voice replay.
- Adds game-styled mod settings (F1) and history (F10), with controller shortcuts and navigation.
- Skips start-up logo videos by default (configurable).

## Requirements

- The Spirit of the Samurai, Windows x64 (tested on Unity 2021.3.45).
- [BepInEx 5.4.23.5 x64 (Mono)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
  Not x86, not BepInEx 6 / IL2CPP.

## Install

1. Close the game and install BepInEx 5 x64 into the game folder (where `samurai.exe` is).
2. Copy `SpiritOfTheSamuraiUltrawide.dll` to `BepInEx/plugins/` (replace any older version).
3. Launch the game. `BepInEx/LogOutput.log` should contain
   `Loading [Spirit of the Samurai Ultrawide 1.13.0]`.

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
still need verification on a Deck. Mod controller shortcuts also require hardware verification.

## Settings

`BepInEx/config/renodx.spiritofthesamurai.ultrawide.cfg` is created on first run. The ultrawide
fixes are always on. Available options:

| Setting | Default | Meaning |
|---|---|---|
| `Display.LimitResolutionToDisplay` | `true` | Cap fitted presets at the display size. Set to `false` to allow supersampling. |
| `General.SkipIntros` | `true` | Skip the start-up logo videos and go straight to the main menu. Set to `false` to play them. |
| `Dialogue.AutoAdvance` | `true` | Advance dialogue lines on a timer. `false` makes each line wait for the advance key. F6 toggles it in game. |
| `Dialogue.AutoAdvanceSpeed` | `2` | How much faster than the game's default timing lines without a voice advance (1 = unchanged). |
| `Dialogue.AdvanceOnVoiceEnd` | `true` | In automatic mode, advance a voiced line shortly after its voice clip ends. |
| `Dialogue.VoiceEndDelay` | `0.4` | Seconds to wait after the voice clip ends before advancing. |
| `History.Key` | `F10` | Open and close dialogue history (Esc also closes it). Up/Down selects a line; Page Up/Down, Home/End and the mouse wheel scroll. Click Replay or press Enter to replay a voiced line; Stop ends playback. Closing history or starting a new line stops replay. |
| `History.MaxLines` | `300` | How many dialogue lines to keep (10–2000); oldest lines are dropped. |
| `History.TextSize` | `24` | History dialogue text size at 1080p (18–40); scales with display height. |
| `Dialogue.ToggleKey` | `F6` | Switch between automatic and manual advance. |
| `Dialogue.AdvanceKey` | `Space` | Continue to the next line in manual mode (ignored when the dialogue box shows its own continue button). |
| `Menu.Key` | `F1` | Open or close mod settings. Changes save automatically. |
| `Controller.EnableShortcuts` | `true` | Enable configurable mod controller shortcuts. Button names use Xbox positions. |
| `Controller.Modifier` | `Select` | Hold this button for shortcuts; `None` allows single-button shortcuts. |
| `Controller.MenuButton` | `LeftShoulder` | Mod settings shortcut, combined with Modifier. |
| `Controller.HistoryButton` | `RightShoulder` | History shortcut, combined with Modifier. |
| `Controller.AutoButton` | `North` | Automatic/manual dialogue shortcut (Y), combined with Modifier. |
| `Controller.LightButton` | `West` | Existing player-light shortcut (X), combined with Modifier. |
| `Controller.RepeatDelay` | `0.35` | Delay before held D-pad/arrow navigation repeats, in seconds. |
| `Controller.RepeatInterval` | `0.09` | Seconds between held navigation steps. |
| `PlayerLight.Enabled` | `true` | Preserve the game's existing player lights; `false` suppresses direct child 2D lights on Takeshi and playable/companion Chisai. Adds no light and does not change scenery lights. |
| `PlayerLight.ToggleKey` | `F4` | Toggle existing player lights; `None` disables the keyboard shortcut. |
| `Diagnostics.EnableSceneInventory` | `false` | Write a scene dump to `BepInEx/diagnostics` automatically (for bug reports). F9 writes one on demand. |

History and mod settings reuse loaded game panel art, menu button frames, the menu font and Xbox button icons.
Assets are found at runtime; no game assets are included with the plugin. Scenes without these assets use a fallback.
Voiced history entries store the voice asset path from their dialogue sequence and reload it on demand, allowing
older lines to replay after the original clip has unloaded. History is kept only in memory for the current
game session, survives scene changes, and starts empty on each launch. Older saved-history files are ignored.
Clear dialogue history in the menu requires a second press to confirm and clears the current session's lines.

The player-light toggle covers Takeshi, playable Chisai, and companion Chisai. F4 or Select/Back + X
suppresses their existing direct-child lights and restores the original states when enabled again.
Feature toggles from keyboard, controller, and the mod menu show a five-second fading context window using
the game's loaded font and panel artwork. Each window explains the new state, including settings that apply
on the next launch or resolution change, without taking focus, dimming the screen, or pausing gameplay.
Controller use hides the cursor in the mod overlays; mouse movement, clicks, or scrolling brings it back.
Manual dialogue accepts Space or controller A, with a fallback if the native Continue input does not respond.
The fallback checks the subtitle identity and start frame to avoid advancing twice.
The lower-right arrow/bag HUD follows the ultrawide right edge, and the cutscene skip bar uses a centered anchor.

The mod menu has Dialogue, Display and Controller pages. LB/RB or Tab changes page; mouse tabs also work.
Use Up/Down to select and Left/Right to adjust, or click a row and its +/− buttons. Hold the D-pad or arrow
keys to repeat navigation and numeric changes. Controller shortcut buttons cycle through available choices;
None disables a shortcut. The menu avoids assigning duplicate buttons when cycling bindings.
Enter or controller A activates; Escape or B closes. Intro skipping applies on the next launch; the resolution
cap applies the next time the game sets its resolution.

In history, controller D-pad Up/Down selects a line, A replays/stops its captured voice, LB/RB pages,
and B closes. Enter also replays the selected line. Manual dialogue advance uses the game's own controller
continue button. History and mod settings suspend enabled Input System actions and pause game time while open,
then restore the prior time scale and cursor state after closing and releasing buttons. Voice audio can continue
while game time is paused. The current development changes were confirmed working by the user on Windows;
other controllers and platforms still need testing.

To reduce frame-time spikes, history layout is cached, built at up to 32 lines per frame and draws only visible
rows. Config writes are debounced and performed by background workers. Input actions are suspended
on overlay entry or when newly enabled rather than enumerated each frame. Voice scans reuse buffers, and
per-line voice logging runs only with Diagnostics.EnableSceneInventory enabled. These changes have not yet
been benchmarked in game.

## Known limitations

- Verified visually on one display (3440x1440) and mainly in the village level; other levels,
  Steam Deck/Proton, 16:10 and 32:9 are untested.
- Menu page transitions, cutscenes and the damage border have had limited checking.
- Screen-sized sprites may look slightly stretched horizontally.
- A game update can change the patched methods.

If something looks wrong, press F9, then attach `BepInEx/LogOutput.log` and the files in
`BepInEx/diagnostics` to a bug report. These files stay local; nothing is transmitted.

## Build

Logic/storage checks (no game required, .NET 10 SDK): `dotnet run --project tests/Logic.Tests -c Release`.

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

## Current working snapshot

See [CURRENT-BUILD.md](CURRENT-BUILD.md) for the exact installed plugin binary and [the splash settings tool](tools/SplashSettings/README.md) for the confirmed local Unity splash fix. No previous binaries or game assets are included.
