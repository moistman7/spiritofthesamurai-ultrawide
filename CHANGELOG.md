# Changelog

## 1.11.0 — 2026-10-17
- Configurable controller shortcut modifier and buttons, with held D-pad/arrow navigation and numeric adjustment.
- Dialogue, Display and Controller settings pages, saved dialogue history with atomic replacement/recovery,
  clear-history confirmation, and adjustable history text size (18–40).
- Existing player-light toggle (F4 or Select/Back + X by default); preserves original light states on return.
- Cache/amortize history layout, draw visible rows only, reuse voice/sprite buffers, suspend input actions on
  change, and debounce config/history disk writes onto workers. No in-game frame-time benchmark yet.
- Add game-independent regression checks for input repeat, visible-row search and history persistence/recovery.

## 1.10.0 — 2026-10-17
- Fix voice tracking being cleared when finishing one subtitle synchronously starts the next. History retains
  each voiced line's asset path and reloads voices on demand so older entries remain replayable.
- Restyle history and mod settings with the game's loaded panel art, menu frames, font and Xbox icons;
  add highlighted history rows, voiced-line counts, and keyboard line selection. No game assets are bundled.
- Fix mod menu, controller shortcuts and dialogue polling not running: move per-frame work and the menu
  renderer from the BepInEx manager to a scene-owned controller, matching the working F9/HDR hotkey pattern.
- Mod settings panel (F1 or Select/Back + LB), with mouse, keyboard and controller navigation and saved settings.
- Controller shortcuts: Select/Back + RB opens history; Select/Back + Y toggles automatic/manual dialogue.
  History supports D-pad line selection, A voice replay/stop, LB/RB paging, and B to close.
- History and mod settings pause game time and suspend Input System actions while open, restoring the previous
  time scale, cursor and enabled actions on return. Controller behavior still needs in-game validation.
- Full-screen cutscene videos are no longer stretched to the wide canvas: they keep their own aspect
  ratio with black bars at the sides (or top and bottom on narrower displays).
- New option `General.SkipIntros` (on by default) skips the start-up logo videos.
- Dialogue auto-advance is twice as fast by default (`Dialogue.AutoAdvanceSpeed`) and F6 switches to manual
  advance, where each line waits for Space (`Dialogue.AdvanceKey`). In automatic mode a voiced line advances
  shortly after its voice clip ends (`Dialogue.AdvanceOnVoiceEnd`).
- Dialogue history window (F10) listing the lines shown this session.
- Voiced history lines have Replay/Stop buttons. Voice clips are captured in both automatic and manual
  modes; replay uses a separate audio source with the original voice volume and mixer routing.

## 1.9.0
- Experimental Steam Deck / SteamOS support with Proton installation and Steam Input diagnostic instructions.
- Fitted resolution presets are capped to the display size by default, avoiding supersampling
  on 1280x800 displays. Set Display.LimitResolutionToDisplay=false to restore uncapped presets.
- Ignore invalid resolution/display sizes and skip sprite-widening discovery on 16:9 or narrower displays.
- Steam Deck gameplay and Proton loading still require hardware validation.

## 1.8.1
- Sky backdrops and overlay sprites now overscan the screen edges by 6% and are re-widened if the
  camera later shrinks them. Fixes thin strips at the top left/right corners.
- The damage border is also widened when a second camera at the same pose renders the UI canvas.

## 1.8.0
- Removed the experimental separate HUD camera and the vignette toggle (F7/F8). The `SeparateHud`
  and `DisableVignette` config entries are no longer used and can be ignored.

## 1.7.0 – 1.7.2
- Experimental separate HUD camera, vignette toggle and manual scene inventory diagnostics (F9).

## 1.6.x
- Menu pages are clipped as whole groups; cutscene video bars follow the active player.

## 1.0 – 1.5
- Resolution presets fit to the display, full viewport instead of letterboxed 16:9, HUD scaling
  via `CanvasScaler`, sky and damage-border widening, parked menu page clipping.
