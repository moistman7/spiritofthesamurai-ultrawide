using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SpiritOfTheSamuraiUltrawide
{
    // A log of the dialogue lines shown this session, opened with a hotkey (like the history window in Castlevania:
    // Belmont's Curse). Lines are recorded when the Dialogue System starts to show them.
    internal static class DialogueHistory
    {
        internal sealed class Entry
        {
            public string Speaker;
            public string Text;
            public AudioClip Voice;
            public string VoicePath;
            public bool CanReplay => Voice != null || !string.IsNullOrEmpty(VoicePath);
            public UnityEngine.Audio.AudioMixerGroup Mixer;
            public float Volume = 1f;
            public float Pitch = 1f;
            internal GUIContent Content;
        }

        internal static readonly List<Entry> Entries = new List<Entry>();

        private static ConfigEntry<KeyCode> key;
        private static ConfigEntry<int> max_lines;
        private static ConfigEntry<int> text_size;
        internal static int Revision { get; private set; }
        internal static int TextSize { get => text_size.Value; set => text_size.Value = Mathf.Clamp(value, 18, 40); }

        private static void Changed() => ++Revision;
        private static HistoryWindow window;
        private static readonly Regex audio_command = new Regex(@"\bAudio(?:Wait|WaitOnce)?\s*\(\s*([^,)]+)", RegexOptions.IgnoreCase);

        internal static bool IsOpen => window != null && window.enabled;
        internal static int MaxLines
        {
            get => max_lines.Value;
            set
            {
                max_lines.Value = Mathf.Clamp(value, 10, 2000);
                int excess = Entries.Count - max_lines.Value;
                if (excess > 0) Entries.RemoveRange(0, excess);
                Changed();
            }
        }

        internal static void Init(ConfigFile config)
        {
            key = config.Bind("History", "Key", KeyCode.F10,
                "Key that opens and closes the dialogue history window. Set to None to disable.");
            max_lines = config.Bind("History", "MaxLines", 300,
                new ConfigDescription("How many dialogue lines to keep. Older lines are dropped.", new AcceptableValueRange<int>(10, 2000)));
            text_size = config.Bind("History", "TextSize", 24, new ConfigDescription("History dialogue font size at 1080p; scales with display height.", new AcceptableValueRange<int>(18, 40)));
        }

        internal static Entry Record(Subtitle subtitle)
        {
            if (subtitle == null || subtitle.formattedText == null || subtitle.formattedText.noSubtitle) return null;
            string text = subtitle.formattedText.text;
            if (string.IsNullOrWhiteSpace(text)) return null;
            string speaker = subtitle.speakerInfo != null ? subtitle.speakerInfo.Name : string.Empty;
            text = text.Trim();
            // The Dialogue System can show a line again (e.g. after a response menu); don't list it twice in a row.
            if (Entries.Count > 0)
            {
                Entry last = Entries[Entries.Count - 1];
                if (last.Text == text && last.Speaker == speaker) return last;
            }
            var entry = new Entry { Speaker = speaker ?? string.Empty, Text = text };
            Match audio = audio_command.Match(subtitle.sequence ?? string.Empty);
            if (audio.Success) entry.VoicePath = audio.Groups[1].Value.Trim().Trim('"', '\'');
            Entries.Add(entry);
            int excess = Entries.Count - Mathf.Max(10, max_lines.Value);
            if (excess > 0) Entries.RemoveRange(0, excess);
            Changed();
            return entry;
        }

        internal static void CaptureVoice(Entry entry, AudioSource source)
        {
            if (entry == null || source == null || source.clip == null) return;
            bool was_replayable = entry.CanReplay;
            entry.Voice = source.clip;
            entry.Mixer = source.outputAudioMixerGroup;
            entry.Volume = source.volume;
            entry.Pitch = source.pitch;
            if (!was_replayable) ++Revision;
        }

        internal static void StopReplay()
        {
            if (window != null) window.StopReplay();
        }

        internal static void Tick()
        {
            if (!ModMenu.IsOpen && key.Value != KeyCode.None && Input.GetKeyDown(key.Value)) Toggle();
            else if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) ||
                (ModMenu.Pad != null && ModMenu.Pad.buttonEast.wasPressedThisFrame))) Close();
        }

        internal static void Clear()
        {
            StopReplay();
            Entries.Clear();
            Changed();
        }

        internal static void Close()
        {
            if (window != null) window.enabled = false;
        }

        internal static void Toggle()
        {
            ModMenu.Close();
            if (window == null)
            {
                var host = new GameObject("SamuraiDialogueHistory");
                window = host.AddComponent<HistoryWindow>();
                window.enabled = false;
            }
            window.enabled = !window.enabled;
            Plugin.Log.LogInfo($"[history] {(window.enabled ? "opened" : "closed")}.");
            if (window.enabled) window.SelectLast();
            ModMenu.SyncInput();
        }
    }

    // Disabled while closed, so Unity does not call OnGUI at all until the history is opened.
    internal class HistoryWindow : MonoBehaviour
    {
        private const float DesignHeight = 1080f;

        private Vector2 scroll;
        private GUIStyle speaker_style;
        private GUIStyle text_style;
        private GUIStyle title_style;
        private GUIStyle help_style;
        private GUIStyle button_style;
        private int theme_revision = -1;
        private AudioSource replay;
        private DialogueHistory.Entry playing;
        private int selected;
        private bool reveal_selection;
        private int replay_request;
        private bool loading;
        private AudioClip loaded_voice;
        private DialogueSystemController loaded_by;
        private string replay_status;
        private RepeatGate line_repeat, page_repeat;
        private readonly List<float> row_tops = new List<float> { 0 };
        private readonly List<float> text_heights = new List<float>();
        private EntryLayoutKey layout_key;
        private float layout_width = -1;
        private int layout_revision = -1, layout_frame = -1, style_text_size = -1;
        private string summary;
        private sealed class EntryLayoutKey { internal DialogueHistory.Entry First; }

        internal void SelectLast()
        {
            selected = Mathf.Max(0, DialogueHistory.Entries.Count - 1);
            ScrollToEnd();
            reveal_selection = true;
            line_repeat = default;
            page_repeat = default;
        }

        internal void StopReplay()
        {
            ++replay_request;
            loading = false;
            if (replay != null)
            {
                replay.Stop();
                replay.clip = null;
            }
            playing = null;
            if (loaded_voice != null && loaded_by != null) loaded_by.UnloadAsset(loaded_voice);
            loaded_voice = null;
            loaded_by = null;
            replay_status = null;
        }

        private void OnDisable() => StopReplay();

        private void Replay(DialogueHistory.Entry entry)
        {
            StopReplay();
            if (!entry.CanReplay) return;
            playing = entry;
            int request = replay_request;
            if (!string.IsNullOrEmpty(entry.VoicePath) && DialogueManager.hasInstance)
            {
                loading = true;
                var owner = DialogueManager.instance;
                try
                {
                    owner.LoadAsset(entry.VoicePath, typeof(AudioClip), asset =>
                    {
                        var clip = asset as AudioClip;
                        if (this == null || !enabled || request != replay_request)
                        {
                            if (clip != null && owner != null) owner.UnloadAsset(clip);
                            return;
                        }
                        loading = false;
                        if (clip == null)
                        {
                            replay_status = "Voice unavailable for this line.";
                            Plugin.Log.LogWarning($"[history] voice asset not found: '{entry.VoicePath}'.");
                            playing = null;
                            return;
                        }
                        loaded_voice = clip;
                        loaded_by = owner;
                        PlayClip(entry, clip);
                    });
                }
                catch (System.Exception e)
                {
                    loading = false;
                    playing = null;
                    replay_status = "Could not load this voice line.";
                    Plugin.Log.LogWarning($"[history] replay load failed: {e.Message}");
                }
            }
            else PlayClip(entry, entry.Voice);
        }

        private void PlayClip(DialogueHistory.Entry entry, AudioClip clip)
        {
            if (clip == null) { playing = null; return; }
            if (replay == null)
            {
                replay = gameObject.AddComponent<AudioSource>();
                replay.playOnAwake = false;
                replay.loop = false;
                replay.spatialBlend = 0f;
            }
            replay.outputAudioMixerGroup = entry.Mixer;
            replay.volume = entry.Volume;
            replay.pitch = entry.Pitch;
            replay.clip = clip;
            playing = entry;
            replay.Play();
            Plugin.Log.LogInfo($"[history] replay '{clip.name}' for '{entry.Speaker}'.");
        }

        internal void ScrollToEnd() => scroll = new Vector2(0f, 1e6f);

        private void Update()
        {
            var pad = ModMenu.Pad;
            if (!loading && playing != null && replay != null && !replay.isPlaying) StopReplay();
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, DialogueHistory.Entries.Count - 1));
            int direction = ControllerInput.Repeat(ref line_repeat, KeyCode.UpArrow, KeyCode.DownArrow, pad?.dpad.up, pad?.dpad.down);
            if (direction != 0)
            {
                selected = Mathf.Clamp(selected + direction, 0, Mathf.Max(0, DialogueHistory.Entries.Count - 1));
                reveal_selection = true;
            }
            if (Input.GetKeyDown(KeyCode.Return) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
            {
                if (selected < DialogueHistory.Entries.Count)
                {
                    var entry = DialogueHistory.Entries[selected];
                    if (playing == entry && (loading || (replay != null && replay.isPlaying))) StopReplay();
                    else Replay(entry);
                }
            }
            float step = 0f;
            bool modifier = pad != null && ControllerInput.Modifier.Value != PadButton.None && ControllerInput.ModifierHeld(pad);
            int page_step = ControllerInput.Repeat(ref page_repeat, KeyCode.PageUp, KeyCode.PageDown,
                modifier ? null : pad?.leftShoulder, modifier ? null : pad?.rightShoulder);
            step = page_step * 400;
            if (Input.GetKeyDown(KeyCode.Home)) scroll.y = 0f;
            else if (Input.GetKeyDown(KeyCode.End)) ScrollToEnd();
            scroll.y = Mathf.Max(0f, scroll.y + step);
        }

        private void CreateStyles()
        {
            title_style = GameUiTheme.TextStyle(36, GameUiTheme.Gold, true);
            speaker_style = GameUiTheme.TextStyle(22, GameUiTheme.Gold, true);
            text_style = GameUiTheme.TextStyle(DialogueHistory.TextSize, GameUiTheme.Ink, false, true);
            help_style = GameUiTheme.TextStyle(19, GameUiTheme.Ink);
            button_style = GameUiTheme.ButtonStyle(21);
            theme_revision = GameUiTheme.Revision;
            style_text_size = DialogueHistory.TextSize;
            layout_width = -1;
        }

        private void BuildLayout(float width)
        {
            var entries = DialogueHistory.Entries;
            bool changed = layout_revision != DialogueHistory.Revision;
            if (layout_width != width || (changed && (entries.Count < text_heights.Count || layout_key == null ||
                entries.Count == 0 || layout_key.First != entries[0])))
            {
                row_tops.Clear(); row_tops.Add(0);
                text_heights.Clear();
                layout_key = new EntryLayoutKey { First = entries.Count > 0 ? entries[0] : null };
                layout_width = width;
                layout_frame = -1;
            }
            if (changed)
            {
                layout_revision = DialogueHistory.Revision;
                int voiced = 0;
                foreach (var entry in entries) if (entry.CanReplay) voiced++;
                summary = $"{entries.Count} lines   /   {voiced} voiced lines";
            }
            if (layout_frame == Time.frameCount) return;
            layout_frame = Time.frameCount;
            // Amortize large restored histories instead of measuring 2,000 strings in the opening frame.
            int end = Mathf.Min(entries.Count, text_heights.Count + 32);
            while (text_heights.Count < end)
            {
                var entry = entries[text_heights.Count];
                if (entry.Content == null) entry.Content = new GUIContent(entry.Text);
                float height = text_style.CalcHeight(entry.Content, width);
                text_heights.Add(height);
                row_tops.Add(row_tops[row_tops.Count - 1] + height + 76);
            }
        }

        private void OnGUI()
        {
            ModMenu.ConsumeNavigationEvent();
            GameUiTheme.Discover();
            if (title_style == null || theme_revision != GameUiTheme.Revision || style_text_size != DialogueHistory.TextSize) CreateStyles();
            Matrix4x4 previous_matrix = GUI.matrix;
            int previous_depth = GUI.depth;
            float scale = Mathf.Max(0.1f, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.depth = -100;
            float width = Screen.width / scale;
            float height = DesignHeight;
            float panel_width = Mathf.Min(width * 0.9f, 1200f);
            var panel = new Rect((width - panel_width) * 0.5f, height * 0.08f, panel_width, height * 0.84f);

            GameUiTheme.Panel(panel);
            GUI.Label(new Rect(panel.x + 50f, panel.y + 28f, panel.width - 240f, 50f), "Dialogue history", title_style);
            if (GameUiTheme.Button(new Rect(panel.xMax - 160f, panel.y + 35f, 110f, 40f), "Close", button_style)) DialogueHistory.Close();
            GameUiTheme.Rule(new Rect(panel.x + 50, panel.y + 115, panel.width - 100, 1));
            var view = new Rect(panel.x + 50f, panel.y + 130f, panel.width - 100f, panel.height - 220f);
            float content_width = view.width - 24f;
            float text_width = content_width - 40f;
            BuildLayout(text_width);
            GUI.Label(new Rect(panel.x + 50f, panel.y + 80f, panel.width - 100f, 28f), summary ?? "", help_style);
            float total_height = row_tops[row_tops.Count - 1];
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, content_width, Mathf.Max(view.height, total_height)));
            Rect selection = new Rect();
            if (selected < text_heights.Count) selection = new Rect(0, row_tops[selected], content_width, text_heights[selected] + 66);
            int first = ModLogic.FirstVisible(row_tops, scroll.y);
            for (int index = first; index < text_heights.Count && row_tops[index] < scroll.y + view.height; index++)
            {
                DialogueHistory.Entry entry = DialogueHistory.Entries[index];
                float y = row_tops[index];
                float text_height = text_heights[index];
                var card = new Rect(0, y, content_width, text_height + 66f);
                if (index == selected)
                {
                    selection = card;
                    GameUiTheme.Fill(card, new Color(0.43f, 0.24f, 0.10f, 0.22f));
                    GameUiTheme.Fill(new Rect(0, y, 3, card.height), GameUiTheme.Gold);
                }
                GUI.Label(new Rect(20, y + 8, content_width - 170, 32), string.IsNullOrEmpty(entry.Speaker) ? "Narration" : entry.Speaker, speaker_style);
                if (entry.CanReplay)
                {
                    bool is_playing = playing == entry && (loading || (replay != null && replay.isPlaying));
                    string action = is_playing ? (loading ? "Cancel" : "Stop") : "Replay";
                    if (GameUiTheme.Button(new Rect(content_width - 130, y + 6, 110, 36), action, button_style, is_playing))
                    {
                        selected = index;
                        if (is_playing) StopReplay();
                        else Replay(entry);
                    }
                }
                GUI.Label(new Rect(20, y + 45, text_width, text_height), entry.Text, text_style);
                GameUiTheme.Rule(new Rect(20, card.yMax + 4, content_width - 40, 1));
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && card.Contains(Event.current.mousePosition)) selected = index;
            }
            if (DialogueHistory.Entries.Count == 0) GUI.Label(new Rect(20, 24, text_width, 80), "Your conversations will appear here.", text_style);
            GUI.EndScrollView();
            if (reveal_selection && selected < text_heights.Count && Event.current.type == EventType.Repaint)
            {
                if (selection.yMin < scroll.y) scroll.y = selection.yMin;
                else if (selection.yMax > scroll.y + view.height) scroll.y = selection.yMax - view.height;
                reveal_selection = false;
            }
            float footer_y = panel.yMax - 62;
            GameUiTheme.Hint(new Rect(panel.x + 50, footer_y, 310, 34), "XB1_DPadUp", "↑", "Select line  /  arrows", help_style);
            GameUiTheme.Hint(new Rect(panel.x + 380, footer_y, 340, 34), "XB1_A", "A", "Replay or stop  /  Enter", help_style);
            GameUiTheme.Hint(new Rect(panel.xMax - 270, footer_y, 220, 34), "XB1_B", "B", "Back  /  Esc", help_style);
            if (!string.IsNullOrEmpty(replay_status)) GUI.Label(new Rect(panel.x + 50, panel.yMax - 95, panel.width - 100, 30), replay_status, help_style);
            else if (text_heights.Count < DialogueHistory.Entries.Count) GUI.Label(new Rect(panel.x + 50, panel.yMax - 95, panel.width - 100, 30), "Preparing history layout...", help_style);
            GUI.matrix = previous_matrix;
            GUI.depth = previous_depth;
        }
    }
}
