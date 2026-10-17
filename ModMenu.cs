using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiritOfTheSamuraiUltrawide
{
    // Shared overlay ownership keeps input, cursor and pause state intact when switching panels.
    internal sealed class ModMenu : MonoBehaviour
    {
        private static ConfigEntry<KeyCode> menu_key;
        private static ConfigEntry<bool> controller_shortcuts;
        private static ConfigFile config;
        private static ModMenu window;
        private static readonly List<InputAction> suspended = new List<InputAction>();
        private static bool owns_input;
        private static float saved_time_scale;
        private static bool saved_cursor;
        private static CursorLockMode saved_lock;
        private static int closed_frame;
        private static int consumed_frame = -1;
        private int selected;
        private GUIStyle row_style;
        private GUIStyle heading_style;
        private GUIStyle help_style;
        private GUIStyle small_button_style;
        private int theme_revision = -1;

        internal static bool IsOpen => window != null && window.enabled;
        internal static bool InputBlocked => owns_input || IsOpen || DialogueHistory.IsOpen || consumed_frame == Time.frameCount;
        internal static Gamepad Pad => controller_shortcuts.Value || InputBlocked ? Gamepad.current : null;
        internal static bool ControllerShortcuts { get => controller_shortcuts.Value; set => controller_shortcuts.Value = value; }

        internal static void Init(ConfigFile file)
        {
            config = file;
            menu_key = file.Bind("Menu", "Key", KeyCode.F1, "Open or close mod settings. Set to None to disable the keyboard shortcut.");
            controller_shortcuts = file.Bind("Controller", "EnableShortcuts", true,
                "Enable mod controller shortcuts: hold Select/Back and press LB for settings, RB for history, or Y for auto/manual dialogue. D-pad navigates panels, A activates, B closes.");
        }

        internal static void Attach(GameObject host)
        {
            GameUiTheme.Reset();
            window = host.AddComponent<ModMenu>();
            window.enabled = false;
        }

        internal static void Toggle()
        {
            bool open = !IsOpen;
            DialogueHistory.Close();
            window.enabled = open;
            if (open) window.selected = 0;
            consumed_frame = Time.frameCount;
            Plugin.Log.LogInfo($"[menu] settings {(open ? "opened" : "closed")}.");
            SyncInput();
        }

        internal static void Close()
        {
            if (window != null) window.enabled = false;
        }

        internal static void Tick()
        {
            Gamepad pad = Pad;
            bool modifier = controller_shortcuts.Value && pad != null && pad.selectButton.isPressed;
            if ((menu_key.Value != KeyCode.None && Input.GetKeyDown(menu_key.Value)) ||
                (modifier && pad.leftShoulder.wasPressedThisFrame)) Toggle();
            else if (modifier && pad.rightShoulder.wasPressedThisFrame)
            {
                Close();
                DialogueHistory.Toggle();
                consumed_frame = Time.frameCount;
            }
            else if (!InputBlocked && modifier && pad.buttonNorth.wasPressedThisFrame)
            {
                DialogueAdvance.Toggle();
                consumed_frame = Time.frameCount;
            }
            else if (IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.buttonEast.wasPressedThisFrame))
                {
                    Close();
                    consumed_frame = Time.frameCount;
                }
                else
                {
                    int vertical = Direction(KeyCode.UpArrow, KeyCode.DownArrow, pad?.dpad.up, pad?.dpad.down);
                    window.selected = (window.selected + vertical + 10) % 10;
                    int horizontal = Direction(KeyCode.LeftArrow, KeyCode.RightArrow, pad?.dpad.left, pad?.dpad.right);
                    bool activate = Input.GetKeyDown(KeyCode.Return) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                    if (horizontal != 0 || activate) Change(window.selected, horizontal == 0 ? 1 : horizontal);
                }
            }
            SyncInput();
        }

        internal static int Direction(KeyCode negative, KeyCode positive,
            UnityEngine.InputSystem.Controls.ButtonControl minus, UnityEngine.InputSystem.Controls.ButtonControl plus)
        {
            if ((negative != KeyCode.None && Input.GetKeyDown(negative)) || (minus != null && minus.wasPressedThisFrame)) return -1;
            if ((positive != KeyCode.None && Input.GetKeyDown(positive)) || (plus != null && plus.wasPressedThisFrame)) return 1;
            return 0;
        }

        internal static void SyncInput()
        {
            if (IsOpen || DialogueHistory.IsOpen)
            {
                if (!owns_input)
                {
                    saved_time_scale = Time.timeScale;
                    saved_cursor = Cursor.visible;
                    saved_lock = Cursor.lockState;
                    owns_input = true;
                }
                closed_frame = Time.frameCount;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                // Snapshot individual actions: a partly enabled map must stay partly enabled on return.
                foreach (InputAction action in InputSystem.ListEnabledActions())
                {
                    if (!suspended.Contains(action)) suspended.Add(action);
                    action.Disable();
                }
            }
            else if (owns_input && Time.frameCount > closed_frame + 1 && ControlsReleased()) ReleaseInput();
        }

        private static bool ControlsReleased()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Escape) || Input.GetMouseButton(0)) return false;
            Gamepad pad = Gamepad.current;
            return pad == null || !(pad.selectButton.isPressed || pad.buttonSouth.isPressed || pad.buttonEast.isPressed ||
                pad.buttonNorth.isPressed || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed ||
                pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed || pad.dpad.right.isPressed);
        }

        private static void ReleaseInput()
        {
            if (!owns_input) return;
            owns_input = false;
            Time.timeScale = saved_time_scale;
            Cursor.lockState = saved_lock;
            Cursor.visible = saved_cursor;
            foreach (InputAction action in suspended)
            {
                try { action.Enable(); }
                catch (System.Exception e) { Plugin.Log.LogDebug($"[menu] could not restore action: {e.Message}"); }
            }
            suspended.Clear();
        }

        internal static void Shutdown()
        {
            Close();
            DialogueHistory.Close();
            ReleaseInput();
        }

        private static string Label(int row)
        {
            switch (row)
            {
                case 0: return "Auto-advance: " + (DialogueAdvance.AutoAdvance ? "On" : "Off");
                case 1: return $"Dialogue speed: {DialogueAdvance.Speed:0.00}x";
                case 2: return "Advance after voice: " + (DialogueAdvance.AdvanceOnVoiceEnd ? "On" : "Off");
                case 3: return $"Voice end delay: {DialogueAdvance.VoiceEndDelay:0.0}s";
                case 4: return $"History capacity: {DialogueHistory.MaxLines} lines";
                case 5: return "Skip intro logos (next launch): " + (Plugin.SkipIntros.Value ? "On" : "Off");
                case 6: return "Cap resolution to display (next resolution change): " + (Plugin.LimitResolutionToDisplay.Value ? "On" : "Off");
                case 7: return "Controller shortcuts: " + (ControllerShortcuts ? "On" : "Off");
                case 8: return "Open dialogue history";
                default: return "Close";
            }
        }

        private static void Change(int row, int direction)
        {
            switch (row)
            {
                case 0: DialogueAdvance.AutoAdvance = !DialogueAdvance.AutoAdvance; break;
                case 1: DialogueAdvance.Speed += direction * 0.25f; break;
                case 2: DialogueAdvance.AdvanceOnVoiceEnd = !DialogueAdvance.AdvanceOnVoiceEnd; break;
                case 3: DialogueAdvance.VoiceEndDelay = Mathf.Round((DialogueAdvance.VoiceEndDelay + direction * 0.1f) * 10f) / 10f; break;
                case 4: DialogueHistory.MaxLines += direction * 50; break;
                case 5: Plugin.SkipIntros.Value = !Plugin.SkipIntros.Value; break;
                case 6: Plugin.LimitResolutionToDisplay.Value = !Plugin.LimitResolutionToDisplay.Value; break;
                case 7: ControllerShortcuts = !ControllerShortcuts; break;
                case 8: Close(); DialogueHistory.Toggle(); break;
                case 9: Close(); break;
            }
            config.Save();
        }

        private void OnGUI()
        {
            ConsumeNavigationEvent();
            GameUiTheme.Discover();
            if (row_style == null || theme_revision != GameUiTheme.Revision)
            {
                row_style = GameUiTheme.ButtonStyle(24);
                row_style.alignment = TextAnchor.MiddleLeft;
                heading_style = GameUiTheme.TextStyle(36, GameUiTheme.Gold, true);
                help_style = GameUiTheme.TextStyle(20, GameUiTheme.Ink, false, true);
                small_button_style = GameUiTheme.ButtonStyle(24);
                theme_revision = GameUiTheme.Revision;
            }
            Matrix4x4 previous = GUI.matrix;
            int previous_depth = GUI.depth;
            float scale = Mathf.Max(0.1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.depth = -100;
            float width = Mathf.Min(1100f, Screen.width / scale - 40f);
            var panel = new Rect((Screen.width / scale - width) / 2f, 90f, width, 900f);
            GameUiTheme.Panel(panel);
            GUI.Label(new Rect(panel.x + 50, panel.y + 30, panel.width - 100, 48), "Mod settings", heading_style);
            GUI.Label(new Rect(panel.x + 50, panel.y + 80, panel.width - 100, 30), "THE SPIRIT OF THE SAMURAI", help_style);
            for (int row = 0; row < 10; row++)
            {
                float row_y = panel.y + 132 + row * 64;
                bool numeric = row == 1 || row == 3 || row == 4;
                float row_width = panel.width - 100 - (numeric ? 120 : 0);
                if (GameUiTheme.Button(new Rect(panel.x + 50, row_y, row_width, 56), Label(row), row_style, row == selected))
                {
                    selected = row;
                    Change(row, 1);
                }
                if (numeric)
                {
                    if (GameUiTheme.Button(new Rect(panel.xMax - 165, row_y, 55, 56), "-", small_button_style)) { selected = row; Change(row, -1); }
                    if (GameUiTheme.Button(new Rect(panel.xMax - 105, row_y, 55, 56), "+", small_button_style)) { selected = row; Change(row, 1); }
                }
            }
            GUI.Label(new Rect(panel.x + 50, panel.yMax - 116, panel.width - 100, 28), "Settings save automatically. Hold Select/Back + RB for dialogue history.", help_style);
            GameUiTheme.Rule(new Rect(panel.x + 50, panel.yMax - 85, panel.width - 100, 1));
            float hint_y = panel.yMax - 65;
            GameUiTheme.Hint(new Rect(panel.x + 50, hint_y, 300, 35), "XB1_DPadUp", "↑", "Navigate / adjust", help_style);
            GameUiTheme.Hint(new Rect(panel.x + 390, hint_y, 300, 35), "XB1_A", "A", "Select  /  Enter", help_style);
            GameUiTheme.Hint(new Rect(panel.xMax - 300, hint_y, 250, 35), "XB1_B", "B", "Back  /  Esc", help_style);
            GUI.matrix = previous;
            GUI.depth = previous_depth;
        }

        // Navigation is handled once in Update; prevent a focused IMGUI button from also activating.
        internal static void ConsumeNavigationEvent()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp) return;
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.Space:
                case KeyCode.Escape:
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    e.Use();
                    break;
            }
        }
    }

    // The game's BepInEx manager does not reliably receive Update; use the same scene-owned
    // pattern as the working F9 diagnostics and the separate RenoDX HUD hotkey controller.
    public sealed class ModInputController : MonoBehaviour
    {
        private bool started;

        private void Update()
        {
            if (!started)
            {
                started = true;
                Plugin.Log.LogInfo($"[menu] scene input controller running in '{gameObject.scene.name}'; F1=settings, F10=history, controller={Gamepad.current?.displayName ?? "none"}.");
            }
            ModMenu.Tick();
            DialogueAdvance.Tick();
        }

        private void OnDisable() => ModMenu.Shutdown();
    }
}
