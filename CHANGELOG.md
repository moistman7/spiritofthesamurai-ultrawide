# Changelog

## 1.15.0 — 2026-10-18
- Add a screen vignette toggle (Display page of the mod menu, F8, `[Vignette] DisableVignette` / `ToggleKey`), moved from the RenoDX HUD companion. Hides only `UI/Canvas/vignette`; default is the game's own vignette. The scene-load scan is shared with existing discovery, so no extra per-frame or per-load cost beyond one component on one object.

## 1.14.0 — 2026-10-17
- Add game-themed Keyboard/Controller binding pickers with dropdown arrows, clear top-row/numeric-keypad labels, current-binding markers, conflict explanations, and navigation that skips unavailable choices.
- Reset only the current device's bindings after confirmation. Keep settings accessible and cancel destructive confirmations when navigating away.
- Align shared default shortcuts with Belmont's Curse: F1 / Select+LB settings, F10 / Select+Y history, F6 / Select+RB combined auto/manual dialogue. Preserve existing saved bindings; keep F4 / Select+X lights and reserve F7 for the HDR companion.
- Extend loading fades to the ultrawide edges using actual sprite corners and black UI panels in LoadingScreen, retaining native fade animation. User confirmed coverage works.
- Cache binding labels/config entries and notification measurements; draw only visible binding options. Consume controller chords inside the picker to prevent replay after closing.
- Skip inactive menu-page bounds and unnecessary 16:9 clipping; share periodic canvas discovery between cameras; reuse sprite-discovery lists; skip inactive video surfaces and unchanged video-bar layout/color writes. Retain existing mod features and UI; no measured frame-time improvement is claimed.
- Build passed with zero warnings/errors; 240 regression assertions passed. User confirmed the final installed build works well.
## 1.13.0 — 2026-10-17
- Add five-second fading context windows using the game's loaded font and panel artwork, explaining each feature toggle and when it takes effect.
- Unify keyboard, controller and menu feedback for auto/manual dialogue and player lights; explain voice completion, intro skipping, resolution caps and controller shortcut toggles.
- Keep notifications non-interactive without pausing gameplay or dimming the screen; preserve panel fade alpha and disable feedback while idle.
- Exclude packaged source under dist from plugin compilation.
## 1.12.0 — 2026-10-17
- Move the lower-right arrow/incense HUD together toward the ultrawide right edge, preserving 16:9 positions, spacing, scale, and counters. Recompute only when the parent canvas width changes.
- Center the cutscene hold-to-skip bar horizontally by converting its fixed left anchor to a center anchor; preserve its original 16:9 offset, vertical layout, and fill behavior.
- Add manual Space/controller A fallback after native input processing, guarded by subtitle identity/start frame to prevent double advancement. Use the current conversation view; allow explicit dismissal of its tracked subtitle after conversation end.
- Hide the cursor and stale mouse hover in mod overlays during controller use; mouse movement/click/scroll restores it. Restore the game's prior cursor state on close.
- Disable shortcut-feedback IMGUI while idle, share video-player discovery across video surfaces, reuse clipping component lists and fallback-camera buffers. Preserve scan cadence and clipping behavior; no in-game benchmark yet.
- Also suppress companion Chisai's direct-child light (NPCS/CHISAI-PET), which has no PlayerInput.
- Show brief fading feedback for controller auto-dialogue and player-light shortcuts without pausing or taking input focus.
- Extend the existing F4 / Select+X player-light toggle to both playable Chisai controller variants, with original light-state restoration.
- Keep dialogue history only for the current game session, across scene changes. Remove the saved-history toggle; old history files are ignored and no history is written to disk.

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
