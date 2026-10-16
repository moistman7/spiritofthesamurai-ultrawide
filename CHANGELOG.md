# Changelog

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
