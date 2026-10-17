# Changelog

## Unreleased
- Full-screen cutscene videos are no longer stretched to the wide canvas: they keep their own aspect
  ratio with black bars at the sides (or top and bottom on narrower displays).
- New option `General.SkipIntros` (on by default) skips the start-up logo videos.

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
