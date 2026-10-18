using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace SpiritOfTheSamuraiUltrawide
{
    // Hides the game's own UI/Canvas/vignette sprite, which RenoDX's Vignette slider does not reach.
    // Damage and fatigue overlays are left alone. Suppression is restored when toggled off.
    internal static class Vignette
    {
        private const string TargetPath = "UI/Canvas/vignette";
        private static ConfigEntry<bool> disabled;
        private static ConfigEntry<KeyCode> key;

        internal static bool Disabled
        {
            get => disabled.Value;
            set
            {
                if (disabled.Value == value) return;
                disabled.Value = value;
                ShortcutFeedback.Show(value ? "Vignette hidden" : "Vignette: game default",
                    value ? "Hides the game's screen-edge vignette sprite." : "Restores the game's screen-edge vignette sprite.");
            }
        }

        internal static void Init(ConfigFile file)
        {
            disabled = file.Bind("Vignette", "DisableVignette", false, "Hide the game's UI/Canvas/vignette sprite.");
            key = file.Bind("Vignette", "ToggleKey", KeyCode.F8, "Toggle the vignette sprite. None disables the shortcut.");
        }

        internal static void Tick()
        {
            if (!ModMenu.InputBlocked && key.Value != KeyCode.None && Input.GetKeyDown(key.Value)) Disabled = !Disabled;
        }

        internal static void Attach(Transform target)
        {
            if (target.name != "vignette" || !target.gameObject.scene.IsValid() || Plugin.GetPath(target) != TargetPath
                || target.GetComponent<VignetteSuppressor>() != null) return;
            target.gameObject.AddComponent<VignetteSuppressor>();
            Plugin.Log.LogInfo($"Vignette control attached to '{TargetPath}'.");
        }
    }

    public class VignetteSuppressor : MonoBehaviour
    {
        private Graphic graphic;
        private Renderer target_renderer;
        private bool graphic_suppressed;
        private bool renderer_suppressed;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            target_renderer = GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (Vignette.Disabled)
            {
                if (target_renderer != null && target_renderer.enabled)
                {
                    target_renderer.enabled = false;
                    renderer_suppressed = true;
                }
                if (graphic != null && graphic.enabled)
                {
                    graphic.enabled = false;
                    graphic_suppressed = true;
                }
            }
            else
            {
                Restore();
            }
        }

        private void Restore()
        {
            if (renderer_suppressed && target_renderer != null) target_renderer.enabled = true;
            if (graphic_suppressed && graphic != null) graphic.enabled = true;
            renderer_suppressed = graphic_suppressed = false;
        }

        private void OnDisable() => Restore();
    }
}
