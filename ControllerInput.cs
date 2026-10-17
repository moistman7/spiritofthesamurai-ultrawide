using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpiritOfTheSamuraiUltrawide
{
    internal enum PadButton { None, Select, Start, LeftShoulder, RightShoulder, North, South, East, West, LeftStick, RightStick, LeftTrigger, RightTrigger }

    internal static class ControllerInput
    {
        internal static ConfigEntry<PadButton> Modifier, Menu, History, Auto, Light;
        private static ConfigEntry<float> repeat_delay, repeat_interval;
        private static ChordGate menu_gate, history_gate, auto_gate, light_gate;
        internal static void Init(ConfigFile file)
        {
            Modifier = file.Bind("Controller", "Modifier", PadButton.Select, "Hold this button for mod shortcuts. None allows single-button shortcuts.");
            Menu = file.Bind("Controller", "MenuButton", PadButton.LeftShoulder, "Button combined with Modifier to open mod settings. None disables this shortcut.");
            History = file.Bind("Controller", "HistoryButton", PadButton.RightShoulder, "Button combined with Modifier to open history. None disables this shortcut.");
            Auto = file.Bind("Controller", "AutoButton", PadButton.North, "Button combined with Modifier to toggle automatic/manual dialogue. None disables this shortcut.");
            Light = file.Bind("Controller", "LightButton", PadButton.West, "Button combined with Modifier to toggle existing player lights. None disables this shortcut.");
            repeat_delay = file.Bind("Controller", "RepeatDelay", 0.35f, new ConfigDescription("Seconds before held D-pad/arrow navigation repeats.", new AcceptableValueRange<float>(0.15f, 1f)));
            repeat_interval = file.Bind("Controller", "RepeatInterval", 0.09f, new ConfigDescription("Seconds between held navigation steps.", new AcceptableValueRange<float>(0.04f, 0.3f)));
        }

        internal static ButtonControl Control(Gamepad pad, PadButton button)
        {
            if (pad == null) return null;
            switch (button)
            {
                case PadButton.Select: return pad.selectButton;
                case PadButton.Start: return pad.startButton;
                case PadButton.LeftShoulder: return pad.leftShoulder;
                case PadButton.RightShoulder: return pad.rightShoulder;
                case PadButton.North: return pad.buttonNorth;
                case PadButton.South: return pad.buttonSouth;
                case PadButton.East: return pad.buttonEast;
                case PadButton.West: return pad.buttonWest;
                case PadButton.LeftStick: return pad.leftStickButton;
                case PadButton.RightStick: return pad.rightStickButton;
                case PadButton.LeftTrigger: return pad.leftTrigger;
                case PadButton.RightTrigger: return pad.rightTrigger;
                default: return null;
            }
        }

        private static bool Held(Gamepad pad, PadButton button) => Control(pad, button)?.isPressed == true;
        internal static bool ModifierHeld(Gamepad pad) => Modifier.Value == PadButton.None || Held(pad, Modifier.Value);

        // A chord works in either press order, fires once while held, and is never auto-repeated.
        internal static int Shortcut(Gamepad pad, bool enabled)
        {
            bool modifier = enabled && pad != null && ModifierHeld(pad);
            bool menu = menu_gate.Step(modifier && Menu.Value != Modifier.Value && Held(pad, Menu.Value));
            bool history = history_gate.Step(modifier && History.Value != Modifier.Value && Held(pad, History.Value));
            bool auto = auto_gate.Step(modifier && Auto.Value != Modifier.Value && Held(pad, Auto.Value));
            bool light = light_gate.Step(modifier && Light.Value != Modifier.Value && Held(pad, Light.Value));
            return menu ? 1 : history ? 2 : auto ? 3 : light ? 4 : 0;
        }

        internal static int Repeat(ref RepeatGate gate, KeyCode negative, KeyCode positive, ButtonControl minus, ButtonControl plus)
        {
            bool left = Input.GetKey(negative) || minus?.isPressed == true;
            bool right = Input.GetKey(positive) || plus?.isPressed == true;
            return gate.Step(left == right ? 0 : left ? -1 : 1, Time.unscaledTime, repeat_delay.Value, repeat_interval.Value);
        }

        internal static string Name(PadButton button)
        {
            switch (button)
            {
                case PadButton.LeftShoulder: return "LB";
                case PadButton.RightShoulder: return "RB";
                case PadButton.North: return "Y / North";
                case PadButton.South: return "A / South";
                case PadButton.East: return "B / East";
                case PadButton.West: return "X / West";
                case PadButton.Select: return "Select / Back";
                default: return button.ToString();
            }
        }

        internal static void Cycle(ConfigEntry<PadButton> entry, int direction)
        {
            int value = (int)entry.Value;
            for (int attempt = 0; attempt < 13; attempt++)
            {
                value = (value + direction + 13) % 13;
                var candidate = (PadButton)value;
                bool conflict = candidate != PadButton.None && (entry == Modifier ?
                    candidate == Menu.Value || candidate == History.Value || candidate == Auto.Value || candidate == Light.Value :
                    candidate == Modifier.Value || (entry != Menu && candidate == Menu.Value) ||
                    (entry != History && candidate == History.Value) || (entry != Auto && candidate == Auto.Value) ||
                    (entry != Light && candidate == Light.Value));
                if (conflict) continue;
                entry.Value = candidate;
                break;
            }
        }
    }
}
