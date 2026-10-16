# Changelog

## 1.8.1
- Sky backdrops and overlay sprites now overscan the screen edges by 6% and are re-widened if the
  camera later shrinks them. Fixes thin strips at the top left/right corners.
- The damage border is widened when the RenoDX HUD plugin renders the HUD with its own camera.

## 1.8.0
- Removed separate HUD rendering (F7) and vignette removal (F8). They are now the standalone
  *RenoDX HUD* plugin. The unused `SeparateHud` / `DisableVignette` config entries can be ignored.

## 1.7.2
- HDR HUD composition and vignette control (now split out) were opt-in and off by default.

## 1.7.0 – 1.7.1
- Experimental separate HUD camera for RenoDX UI brightness, and a vignette toggle (both since
  moved out), plus manual scene inventory diagnostics (F9).

## 1.6.x
- Menu pages are clipped as whole groups; cutscene video bars follow the active player.

## 1.0 – 1.5
- Resolution presets fit to the display, full viewport instead of letterboxed 16:9, HUD scaling
  via `CanvasScaler`, sky and damage-border widening, parked menu page clipping.
