using System.Collections.Generic;
using System.Threading.Tasks;
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
        private static readonly List<InputAction> enabled_actions = new List<InputAction>();
        private static readonly HashSet<InputAction> suspended_set = new HashSet<InputAction>();
        private static bool input_dirty;
        private static int input_sync_frame = -1;
        private static int config_revision, config_saved;
        private static float config_save_after;
        private static Task config_save_task;
        private static bool owns_input;
        private static bool controller_cursor;
        private static float saved_time_scale;
        private static bool saved_cursor;
        private static CursorLockMode saved_lock;
        private static int closed_frame;
        private static int consumed_frame = -1;
        private int selected;
        private int page;
        private RepeatGate vertical_repeat, horizontal_repeat;
        private float confirm_clear_until;
        private float confirm_reset_until;
        private int binding_index = -1, binding_selected;
        private List<object> binding_options;
        private string[] binding_labels;
        private bool[] binding_allowed;
        private string binding_title;
        private int binding_revision;
        private Vector2 binding_scroll;
        private static readonly string[] page_names = { "Dialogue", "Display", "Controller", "Keyboard" };
        private static readonly int[][] page_rows = {
            new[] { 0, 1, 2, 3, 4, 10, 12, 8, 9 },
            new[] { 5, 6, 17, 9 },
            new[] { 7, 13, 14, 15, 16, 18, 24, 9 },
            new[] { 19, 20, 21, 22, 23, 24, 9 }
        };
        private readonly string[] labels = new string[25];
        private bool labels_dirty = true;
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
            BindingSettings.Init(file);
            menu_key = file.Bind("Menu", "Key", KeyCode.F1, "Open or close mod settings. Set to None to disable the keyboard shortcut.");
            controller_shortcuts = file.Bind("Controller", "EnableShortcuts", true,
                "Enable mod controller shortcuts: hold Select/Back and press LB for settings, Y for history, or RB for auto/manual dialogue. D-pad navigates panels, A activates, B closes.");
            ControllerInput.Init(file);
            file.SaveOnConfigSet = false;
            file.SettingChanged += OnSettingChanged;
            InputSystem.onActionChange += OnActionChange;
            config_revision = 1;
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            ++config_revision;
            config_save_after = Time.unscaledTime + 1f;
            if (window != null) window.labels_dirty = true;
        }

        private static void OnActionChange(object subject, InputActionChange change)
        {
            if (owns_input && (change == InputActionChange.ActionEnabled || change == InputActionChange.ActionMapEnabled)) input_dirty = true;
        }

        private static void SaveConfigTick()
        {
            if (config_save_task != null && config_save_task.IsCompleted)
            {
                if (config_save_task.IsFaulted)
                {
                    Plugin.Log.LogWarning($"[menu] settings save failed: {config_save_task.Exception.GetBaseException().Message}");
                    config_saved = -1;
                    config_save_after = Time.unscaledTime + 10;
                }
                config_save_task = null;
            }
            if (config_save_task == null && config_saved != config_revision && Time.unscaledTime >= config_save_after)
            {
                config_saved = config_revision;
                config_save_task = Task.Run(() => config.Save());
            }
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
            if (open) { window.binding_index = -1; window.confirm_reset_until = 0; window.selected = 0; window.vertical_repeat = default; window.horizontal_repeat = default; window.labels_dirty = true; }
            consumed_frame = Time.frameCount;
            Plugin.Log.LogInfo($"[menu] settings {(open ? "opened" : "closed")}.");
            SyncInput();
        }

        internal static void Close()
        {
            if (window != null) { window.binding_index = -1; window.enabled = false; }
        }

        internal static void Tick()
        {
            SaveConfigTick();
            Gamepad pad = Pad;
            UpdateCursorMode(pad);
            if (IsOpen && window.binding_index >= 0)
            {
                // Track held chords while the picker consumes them, so closing cannot replay a press.
                ControllerInput.Shortcut(pad, ControllerShortcuts);
                window.BindingTick(pad);
                SyncInput();
                return;
            }
            int shortcut = ControllerInput.Shortcut(pad, ControllerShortcuts);
            if ((menu_key.Value != KeyCode.None && Input.GetKeyDown(menu_key.Value)) ||
                shortcut == 1) Toggle();
            else if (shortcut == 2)
            {
                Close();
                DialogueHistory.Toggle();
                consumed_frame = Time.frameCount;
            }
            else if (!InputBlocked && shortcut == 3)
            {
                DialogueAdvance.Toggle();
                consumed_frame = Time.frameCount;
            }
            else if (!InputBlocked && shortcut == 4)
            {
                PlayerLight.Enabled = !PlayerLight.Enabled;
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
                    bool modifier = pad != null && ControllerInput.Modifier.Value != PadButton.None && ControllerInput.ModifierHeld(pad);
                    if (Input.GetKeyDown(KeyCode.Tab) || (!modifier && pad != null && pad.rightShoulder.wasPressedThisFrame)) window.ChangePage(1);
                    else if (!modifier && pad != null && pad.leftShoulder.wasPressedThisFrame) window.ChangePage(-1);
                    int count = page_rows[window.page].Length;
                    int vertical = ControllerInput.Repeat(ref window.vertical_repeat, KeyCode.UpArrow, KeyCode.DownArrow, pad?.dpad.up, pad?.dpad.down);
                    if (vertical != 0) window.CancelConfirmations();
                    window.selected = (window.selected + vertical + count) % count;
                    int horizontal = ControllerInput.Repeat(ref window.horizontal_repeat, KeyCode.LeftArrow, KeyCode.RightArrow, pad?.dpad.left, pad?.dpad.right);
                    bool activate = Input.GetKeyDown(KeyCode.Return) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                    int row = page_rows[window.page][window.selected];
                    // Only numeric/cycling rows repeat. A held direction must not flip toggles repeatedly.
                    bool toggle = row == 0 || row == 2 || row == 5 || row == 6 || row == 7 || row == 17;
                    bool tapped = toggle && Direction(KeyCode.LeftArrow, KeyCode.RightArrow, pad?.dpad.left, pad?.dpad.right) != 0;
                    if (activate || tapped || (horizontal != 0 && IsNumeric(row))) Change(row, horizontal == 0 ? 1 : horizontal);
                }
            }
            SyncInput();
        }

        private void ChangePage(int direction)
        {
            page = (page + direction + page_names.Length) % page_names.Length;
            selected = 0;
            vertical_repeat = default;
            horizontal_repeat = default;
            CancelConfirmations();
            labels_dirty = true;
        }

        private static bool IsNumeric(int row) => row == 1 || row == 3 || row == 4 || row == 10;

        private void CancelConfirmations()
        {
            if (confirm_reset_until == 0 && confirm_clear_until == 0) return;
            confirm_reset_until = confirm_clear_until = 0;
            labels_dirty = true;
        }

        private static int BindingIndex(int row) => row >= 19 && row <= 23 ? row - 19 :
            row >= 13 && row <= 16 ? row - 8 : row == 18 ? 9 : -1;

        private void OpenBinding(int index)
        {
            binding_index = index;
            binding_options = BindingSettings.Options(index);
            RefreshBindingOptions();
            binding_selected = binding_options.IndexOf(BindingSettings.Current(index));
            binding_scroll = new Vector2(0, Mathf.Max(0, binding_selected - 5) * 48f);
            vertical_repeat = default;
            confirm_reset_until = 0;
            confirm_clear_until = 0;
        }

        private void RefreshBindingOptions()
        {
            if (binding_labels == null || binding_labels.Length != binding_options.Count)
            {
                binding_labels = new string[binding_options.Count];
                binding_allowed = new bool[binding_options.Count];
            }
            for (int index = 0; index < binding_options.Count; index++)
            {
                object option = binding_options[index];
                binding_allowed[index] = BindingSettings.Allowed(binding_index, option);
                binding_labels[index] = BindingSettings.OptionName(binding_index, option) + (binding_allowed[index] ? (option.Equals(BindingSettings.Current(binding_index)) ? " (current)" : "") : BindingSettings.UnavailableReason(binding_index, option));
            }
            binding_title = BindingSettings.Label(binding_index);
            binding_revision = config_revision;
        }

        private void SelectBinding(int index)
        {
            if (!BindingSettings.Set(binding_index, binding_options[index])) return;
            binding_index = -1;
            labels_dirty = true;
            consumed_frame = Time.frameCount;
        }

        private void BindingTick(Gamepad pad)
        {
            if (binding_revision != config_revision) RefreshBindingOptions();
            consumed_frame = Time.frameCount;
            if (Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.buttonEast.wasPressedThisFrame)) { binding_index = -1; return; }
            int delta = ControllerInput.Repeat(ref vertical_repeat, KeyCode.UpArrow, KeyCode.DownArrow, pad?.dpad.up, pad?.dpad.down);
            if (delta != 0)
            {
                binding_selected = ModLogic.NextAvailable(binding_allowed, binding_selected, delta);
                binding_scroll.y = Mathf.Clamp(binding_scroll.y, (binding_selected + 1) * 48f - 480f, binding_selected * 48f);
            }
            if (Input.GetKeyDown(KeyCode.Return) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) SelectBinding(binding_selected);
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
                    input_dirty = true;
                }
                closed_frame = Time.frameCount;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = !controller_cursor;
                // Snapshot individual actions: a partly enabled map must stay partly enabled on return.
                if (!input_dirty || input_sync_frame == Time.frameCount) return;
                input_sync_frame = Time.frameCount;
                input_dirty = false;
                enabled_actions.Clear();
                InputSystem.ListEnabledActions(enabled_actions);
                foreach (InputAction action in enabled_actions)
                {
                    if (suspended_set.Add(action)) suspended.Add(action);
                    action.Disable();
                }
            }
            else if (owns_input && Time.frameCount > closed_frame + 1 && ControlsReleased()) ReleaseInput();
        }

        private static void UpdateCursorMode(Gamepad pad)
        {
            if (pad != null && (pad.selectButton.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
                pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame ||
                pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame ||
                pad.dpad.ReadValue().sqrMagnitude > 0f ||
                pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f))
                controller_cursor = true;
            Mouse mouse = Mouse.current;
            // Real mouse input wins when both devices are used in the same frame.
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude >= 1f ||
                mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0f))
                controller_cursor = false;
        }

        private static bool ControlsReleased()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Escape) || Input.GetMouseButton(0)) return false;
            Gamepad pad = Gamepad.current;
            return pad == null || !(ControllerInput.Control(pad, ControllerInput.Modifier.Value)?.isPressed == true ||
                ControllerInput.Control(pad, ControllerInput.Menu.Value)?.isPressed == true ||
                ControllerInput.Control(pad, ControllerInput.History.Value)?.isPressed == true ||
                ControllerInput.Control(pad, ControllerInput.Auto.Value)?.isPressed == true || pad.selectButton.isPressed || pad.buttonSouth.isPressed || pad.buttonEast.isPressed ||
                ControllerInput.Control(pad, ControllerInput.Light.Value)?.isPressed == true ||
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
            suspended_set.Clear();
        }

        internal static void Shutdown()
        {
            Close();
            DialogueHistory.Close();
            ReleaseInput();
        }

        internal static void FlushConfig()
        {
            try
            {
                config_save_task?.Wait(2000);
                if (config_saved != config_revision) config.Save();
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[menu] final settings save failed: {e.GetBaseException().Message}"); }
        }

        private static string Label(int row)
        {
            int binding = BindingIndex(row);
            if (binding >= 0) return BindingSettings.Label(binding);
            if (row == 24)
            {
                string device = window.page == 2 ? "controller" : "keyboard";
                return window.confirm_reset_until > Time.unscaledTime ? "Reset " + device + " bindings? Press again to confirm" : "Reset " + device + " bindings to defaults";
            }
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
                case 10: return $"History text size: {DialogueHistory.TextSize}";
                case 12: return window.confirm_clear_until > Time.unscaledTime ? "Clear history? Press again to confirm" : "Clear dialogue history";
                case 13: return "Shortcut modifier: " + ControllerInput.Name(ControllerInput.Modifier.Value);
                case 14: return "Mod menu button: " + ControllerInput.Name(ControllerInput.Menu.Value);
                case 15: return "History button: " + ControllerInput.Name(ControllerInput.History.Value);
                case 16: return "Auto/manual button: " + ControllerInput.Name(ControllerInput.Auto.Value);
                case 17: return "Player lights (Takeshi / Chisai): " + (PlayerLight.Enabled ? "Game default" : "Off");
                case 18: return "Player light button: " + ControllerInput.Name(ControllerInput.Light.Value);
                default: return "Close";
            }
        }

        private static void Change(int row, int direction)
        {
            if (row != 24) window.confirm_reset_until = 0;
            if (row != 12) window.confirm_clear_until = 0;
            int binding = BindingIndex(row);
            if (binding >= 0) { window.OpenBinding(binding); return; }
            switch (row)
            {
                case 0: DialogueAdvance.AutoAdvance = !DialogueAdvance.AutoAdvance; break;
                case 1: DialogueAdvance.Speed += direction * 0.25f; break;
                case 2:
                    DialogueAdvance.AdvanceOnVoiceEnd = !DialogueAdvance.AdvanceOnVoiceEnd;
                    ShortcutFeedback.Show(DialogueAdvance.AdvanceOnVoiceEnd ? "Advance after voice enabled" : "Advance after voice disabled",
                        DialogueAdvance.AdvanceOnVoiceEnd ? "In auto mode, voiced lines advance after playback and your voice end delay." : "In auto mode, dialogue uses the text timer instead of voice completion.");
                    break;
                case 3: DialogueAdvance.VoiceEndDelay = Mathf.Round((DialogueAdvance.VoiceEndDelay + direction * 0.1f) * 10f) / 10f; break;
                case 4: DialogueHistory.MaxLines += direction * 50; break;
                case 5:
                    Plugin.SkipIntros.Value = !Plugin.SkipIntros.Value;
                    ShortcutFeedback.Show(Plugin.SkipIntros.Value ? "Intro logo skipping enabled" : "Intro logo skipping disabled",
                        "Applies to startup logo videos on the next game launch.");
                    break;
                case 6:
                    Plugin.LimitResolutionToDisplay.Value = !Plugin.LimitResolutionToDisplay.Value;
                    ShortcutFeedback.Show(Plugin.LimitResolutionToDisplay.Value ? "Display resolution cap enabled" : "Display resolution cap disabled",
                        Plugin.LimitResolutionToDisplay.Value ? "Limits resolution to your display size at the next resolution change." : "Allows resolutions above your display size at the next resolution change.");
                    break;
                case 7:
                    if (ControllerShortcuts && menu_key.Value == KeyCode.None)
                    {
                        ShortcutFeedback.Show("Keep a settings shortcut", "Assign a keyboard key for the mod menu before disabling controller shortcuts.");
                        break;
                    }
                    ControllerShortcuts = !ControllerShortcuts;
                    ShortcutFeedback.Show(ControllerShortcuts ? "Controller shortcuts enabled" : "Controller shortcuts disabled",
                        ControllerShortcuts ? "Your configured controller shortcuts are active." : "Keyboard shortcuts still work. Controller navigation remains available in open mod panels.");
                    break;
                case 8: Close(); DialogueHistory.Toggle(); break;
                case 9: Close(); break;
                case 10: DialogueHistory.TextSize += direction * 2; break;
                case 12:
                    if (window.confirm_clear_until > Time.unscaledTime) { DialogueHistory.Clear(); window.confirm_clear_until = 0; }
                    else window.confirm_clear_until = Time.unscaledTime + 4;
                    break;
                case 13: ControllerInput.Cycle(ControllerInput.Modifier, direction); break;
                case 14: ControllerInput.Cycle(ControllerInput.Menu, direction); break;
                case 15: ControllerInput.Cycle(ControllerInput.History, direction); break;
                case 16: ControllerInput.Cycle(ControllerInput.Auto, direction); break;
                case 17: PlayerLight.Enabled = !PlayerLight.Enabled; break;
                case 18: ControllerInput.Cycle(ControllerInput.Light, direction); break;
                case 24:
                    if (window.confirm_reset_until > Time.unscaledTime) { BindingSettings.Reset(window.page == 2); window.confirm_reset_until = 0; }
                    else window.confirm_reset_until = Time.unscaledTime + 4f;
                    break;
            }
            window.labels_dirty = true;
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
            float tab_width = (panel.width - 100) / page_names.Length;
            for (int tab = 0; tab < page_names.Length; tab++)
            {
                if (binding_index < 0 && GameUiTheme.Button(new Rect(panel.x + 50 + tab * tab_width, panel.y + 122, tab_width - 8, 42), page_names[tab], small_button_style, page == tab))
                    ChangePage(tab - page);
            }
            if (confirm_clear_until != 0 && Time.unscaledTime >= confirm_clear_until) { confirm_clear_until = 0; labels_dirty = true; }
            if (confirm_reset_until != 0 && Time.unscaledTime >= confirm_reset_until) { confirm_reset_until = 0; labels_dirty = true; }
            if (labels_dirty)
            {
                for (int index = 0; index < labels.Length; index++) labels[index] = Label(index);
                labels_dirty = false;
            }
            int[] rows = page_rows[page];
            for (int index = 0; binding_index < 0 && index < rows.Length; index++)
            {
                int row = rows[index];
                float row_y = panel.y + 180 + index * 56;
                bool numeric = IsNumeric(row);
                float row_width = panel.width - 100 - (numeric ? 120 : 0);
                if (GameUiTheme.Button(new Rect(panel.x + 50, row_y, row_width, 50), labels[row], row_style, index == selected))
                {
                    selected = index;
                    Change(row, 1);
                }
                if (numeric)
                {
                    if (GameUiTheme.Button(new Rect(panel.xMax - 165, row_y, 55, 50), "-", small_button_style)) { selected = index; Change(row, -1); }
                    if (GameUiTheme.Button(new Rect(panel.xMax - 105, row_y, 55, 50), "+", small_button_style)) { selected = index; Change(row, 1); }
                }
            }
            if (binding_index >= 0) DrawBindingDropdown(panel);
            GUI.Label(new Rect(panel.x + 50, panel.yMax - 116, panel.width - 100, 28), binding_index >= 0 ? "Up / Down: choose. Enter / A: apply. Esc / B: cancel." : "LB / RB or Tab: change page. Hold D-pad / arrows to repeat. Settings save automatically.", help_style);
            GameUiTheme.Rule(new Rect(panel.x + 50, panel.yMax - 85, panel.width - 100, 1));
            float hint_y = panel.yMax - 65;
            GameUiTheme.Hint(new Rect(panel.x + 50, hint_y, 300, 35), "XB1_DPadUp", "↑", "Navigate / adjust", help_style);
            GameUiTheme.Hint(new Rect(panel.x + 390, hint_y, 300, 35), "XB1_A", "A", "Select  /  Enter", help_style);
            GameUiTheme.Hint(new Rect(panel.xMax - 300, hint_y, 250, 35), "XB1_B", "B", "Back  /  Esc", help_style);
            GUI.matrix = previous;
            GUI.depth = previous_depth;
        }

        private void DrawBindingDropdown(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 50, panel.y + 175, panel.width - 100, 40), binding_title, row_style);
            GUI.Label(new Rect(panel.x + 50, panel.y + 220, panel.width - 100, 55), binding_index < 5 ? "Choose a key. Top row means the numbers above the letters. Numeric keypad means the separate number pad." : "Choose a controller button. Used buttons are unavailable. None disables the shortcut; for the modifier it allows single-button shortcuts.", help_style);
            var view = new Rect(panel.x + 50, panel.y + 285, panel.width - 100, 480);
            binding_scroll = GUI.BeginScrollView(view, binding_scroll, new Rect(0, 0, view.width - 24, binding_options.Count * 48f));
            int first = Mathf.Max(0, Mathf.FloorToInt(binding_scroll.y / 48f));
            int end = Mathf.Min(binding_options.Count, Mathf.CeilToInt((binding_scroll.y + view.height) / 48f));
            for (int index = first; index < end; index++)
            {
                bool previous = GUI.enabled;
                GUI.enabled = previous && binding_allowed[index];
                bool clicked = GameUiTheme.Button(new Rect(0, index * 48f, view.width - 28, 44), binding_labels[index], row_style, index == binding_selected);
                GUI.enabled = previous;
                if (clicked) { SelectBinding(index); break; }
            }
            GUI.EndScrollView();
            if (GameUiTheme.Button(new Rect(panel.x + 50, panel.y + 780, panel.width - 100, 42), "Cancel", small_button_style)) binding_index = -1;
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
            PlayerLight.Tick();
        }

        private void Awake() => ShortcutFeedback.Init(gameObject);

        private void OnDisable()
        {
            ShortcutFeedback.Clear();
            ModMenu.Shutdown();
        }
        private void OnApplicationQuit()
        {
            ModMenu.FlushConfig();
            PlayerLight.Restore();
        }
    }
}
