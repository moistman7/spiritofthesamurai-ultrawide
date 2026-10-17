using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SpiritOfTheSamuraiUltrawide
{
    internal static class PlayerLight
    {
        private struct Original { internal Light2D Light; internal bool Enabled; }
        private static readonly List<Original> lights = new List<Original>();
        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<KeyCode> key;
        private static float next_scan;
        private static bool suppressed;
        internal static bool Enabled { get => enabled.Value; set => enabled.Value = value; }
        internal static void Init(ConfigFile file)
        {
            enabled = file.Bind("PlayerLight", "Enabled", true, "Allow the game's existing player lights. False suppresses direct child Light2D components on player objects; does not add a light or alter scenery/VFX.");
            key = file.Bind("PlayerLight", "ToggleKey", KeyCode.F4, "Toggle the existing player lights. F4 avoids the separate RenoDX HUD companion's F7/F8 keys. None disables.");
        }

        internal static void SceneLoaded()
        {
            Restore();
            next_scan = 0;
        }

        internal static void Tick()
        {
            if (!ModMenu.InputBlocked && key.Value != KeyCode.None && Input.GetKeyDown(key.Value)) Enabled = !Enabled;
            if (Enabled)
            {
                if (suppressed) Restore();
                return;
            }
            if (Time.unscaledTime >= next_scan)
            {
                next_scan = Time.unscaledTime + 2f;
                // PlayerInput.all is maintained by Unity; no global Resources scan or per-frame hierarchy search.
                foreach (var player in PlayerInput.all)
                {
                    if (player.GetComponent<SamuraiController>() == null) continue;
                    foreach (Transform child in player.transform)
                    {
                        Light2D light = child.GetComponent<Light2D>();
                        if (light == null) continue;
                        bool known = false;
                        for (int i = 0; i < lights.Count; i++) if (lights[i].Light == light) { known = true; break; }
                        if (!known) lights.Add(new Original { Light = light, Enabled = light.enabled });
                    }
                }
            }
            suppressed = true;
            for (int i = 0; i < lights.Count; i++)
                if (lights[i].Light != null && lights[i].Light.enabled) lights[i].Light.enabled = false;
        }

        internal static void Restore()
        {
            for (int i = 0; i < lights.Count; i++) if (lights[i].Light != null) lights[i].Light.enabled = lights[i].Enabled;
            lights.Clear();
            suppressed = false;
        }
    }
}
