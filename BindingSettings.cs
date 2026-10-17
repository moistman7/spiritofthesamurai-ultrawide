using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace SpiritOfTheSamuraiUltrawide
{
    // Edits the existing config entries so every input path sees changes immediately.
    internal static class BindingSettings
    {
        private static ConfigFile config;
        private static readonly ConfigEntryBase[] entries = new ConfigEntryBase[10];
        private static readonly string[] sections = { "Menu", "History", "Dialogue", "Dialogue", "PlayerLight", "Controller", "Controller", "Controller", "Controller", "Controller" };
        private static readonly string[] keys = { "Key", "Key", "ToggleKey", "AdvanceKey", "ToggleKey", "Modifier", "MenuButton", "HistoryButton", "AutoButton", "LightButton" };
        private static readonly string[] names = { "Mod menu key", "History key", "Auto/manual key", "Manual advance key", "Player light key", "Shortcut modifier", "Mod menu button", "History button", "Auto/manual button", "Player light button" };
        internal static void Init(ConfigFile file) { config = file; Array.Clear(entries, 0, entries.Length); }
        private static ConfigEntryBase Entry(int index) => entries[index] ?? (entries[index] = config[new ConfigDefinition(sections[index], keys[index])]);
        internal static string Label(int index) => names[index] + ": " + OptionName(index, Entry(index).BoxedValue) + "  \u25BC";
        internal static string OptionName(int index, object value) => index == 5 && value.Equals(PadButton.None) ? "None (single-button shortcuts)" : Name(value);
        internal static string Name(object value)
        {
            if (value is PadButton button) return button == PadButton.None ? "None (disabled)" : ControllerInput.Name(button);
            if (!(value is KeyCode key)) return value.ToString();
            int code = (int)key;
            if (code >= (int)KeyCode.Alpha0 && code <= (int)KeyCode.Alpha9)
                return (code - (int)KeyCode.Alpha0) + " (top row)";
            if (code >= (int)KeyCode.Keypad0 && code <= (int)KeyCode.Keypad9)
                return (code - (int)KeyCode.Keypad0) + " (numeric keypad)";
            switch (key)
            {
                case KeyCode.None: return "None (disabled)";
                case KeyCode.BackQuote: return "` / ~ (backtick)";
                case KeyCode.Minus: return "- (minus)";
                case KeyCode.Equals: return "= (equals)";
                default: return key.ToString();
            }
        }
        internal static bool Allowed(int index, object value)
        {
            if (Convert.ToInt32(value) == 0)
            {
                if (index == 0) return ModMenu.ControllerShortcuts && !Entry(6).BoxedValue.Equals(PadButton.None);
                if (index == 6) return !Entry(0).BoxedValue.Equals(KeyCode.None);
                return true;
            }
            for (int other = index < 5 ? 0 : 5; other < (index < 5 ? 5 : 10); other++)
                if (other != index && Entry(other).BoxedValue.Equals(value)) return false;
            return true;
        }
        internal static string UnavailableReason(int index, object value)
        {
            if (Convert.ToInt32(value) == 0) return " (keep a settings shortcut)";
            for (int other = index < 5 ? 0 : 5; other < (index < 5 ? 5 : 10); other++)
                if (other != index && Entry(other).BoxedValue.Equals(value)) return " (used by " + names[other].ToLowerInvariant() + ")";
            return " (unavailable)";
        }
        internal static List<object> Options(int index)
        {
            var result = new List<object>();
            if (index >= 5)
            {
                foreach (PadButton button in Enum.GetValues(typeof(PadButton))) result.Add(button);
            }
            else
            {
                result.Add(KeyCode.None);
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    // Keep panel navigation and companion/diagnostic keys available.
                    int code = (int)key;
                    if ((code >= (int)KeyCode.A && code <= (int)KeyCode.Z) ||
                        (code >= (int)KeyCode.Alpha0 && code <= (int)KeyCode.Alpha9) ||
                        (code >= (int)KeyCode.F1 && code <= (int)KeyCode.F12 && key != KeyCode.F7 && key != KeyCode.F8 && key != KeyCode.F9) ||
                        (index == 3 && key == KeyCode.Space) || key == KeyCode.BackQuote || key == KeyCode.Minus || key == KeyCode.Equals ||
                        (code >= (int)KeyCode.Keypad0 && code <= (int)KeyCode.Keypad9)) result.Add(key);
                }
            }
            object current = Entry(index).BoxedValue;
            if (!result.Contains(current)) result.Add(current);
            return result;
        }
        internal static object Current(int index) => Entry(index).BoxedValue;
        internal static bool Set(int index, object value)
        {
            if (!Allowed(index, value)) return false;
            Entry(index).BoxedValue = value;
            return true;
        }
        internal static void Reset(bool controller)
        {
            for (int index = controller ? 5 : 0; index < (controller ? 10 : 5); index++)
                Entry(index).BoxedValue = Entry(index).DefaultValue;
            // A controller-only reset must leave settings accessible if the keyboard shortcut is disabled.
            if (controller && Entry(0).BoxedValue.Equals(KeyCode.None)) ModMenu.ControllerShortcuts = true;
            ShortcutFeedback.Show(controller ? "Default controller bindings restored" : "Default keyboard bindings restored",
                controller ? "Keyboard bindings were kept. Controller shortcuts are available if needed to open settings." : "Controller bindings and other mod settings were kept.");
        }
    }
}
