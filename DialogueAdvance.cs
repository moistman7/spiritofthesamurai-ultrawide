using BepInEx.Configuration;
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SpiritOfTheSamuraiUltrawide
{
    // Subtitles advance on a timer taken from the Dialogue System's display settings (characters per second, with a
    // minimum), and the game gives no way to change that. This scales the timer, advances voiced lines when their
    // voice clip ends, and lets a hotkey switch to manual mode, where each line waits for a key press (the Dialogue
    // System's "continue").
    internal static class DialogueAdvance
    {
        private static ConfigEntry<bool> auto_advance;
        private static ConfigEntry<float> speed;
        private static ConfigEntry<bool> advance_on_voice_end;
        private static ConfigEntry<float> voice_end_delay;
        private static ConfigEntry<KeyCode> toggle_key;
        private static ConfigEntry<KeyCode> advance_key;

        internal static bool AutoAdvance { get => auto_advance.Value; set { auto_advance.Value = value; RefreshSettings(); } }
        internal static float Speed { get => speed.Value; set { speed.Value = Mathf.Clamp(value, 0.25f, 5f); RefreshSettings(); } }
        internal static bool AdvanceOnVoiceEnd { get => advance_on_voice_end.Value; set => advance_on_voice_end.Value = value; }
        internal static float VoiceEndDelay { get => voice_end_delay.Value; set => voice_end_delay.Value = Mathf.Clamp(value, 0f, 3f); }

        private static void RefreshSettings()
        {
            if (DialogueManager.hasInstance) Apply(LineHasVoice() ? 0f : 1f);
        }

        private static bool LineHasVoice() => line != null && line.sequence != null && line.sequence.IndexOf("Audio", System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static DialogueSystemController hooked;
        private static DisplaySettings.SubtitleSettings settings;
        private static float original_chars_per_second;
        private static float original_min_seconds;
        private static DisplaySettings.SubtitleSettings.ContinueButtonMode original_mode;
        private static float last_advance;

        // State of the line being shown.
        private static Subtitle line;
        private static float line_start;
        private static float next_scan;
        private static bool scan_logged;
        private static AudioSource voice;
        private static float voice_ended_at;
        private static DialogueHistory.Entry history_entry;

        internal static void Init(ConfigFile config)
        {
            auto_advance = config.Bind("Dialogue", "AutoAdvance", true,
                "Advance dialogue lines automatically. When false, each line waits for the advance key. Toggled in game with the toggle key.");
            speed = config.Bind("Dialogue", "AutoAdvanceSpeed", 2f,
                "How much faster than the game's default timing lines without a voice advance automatically (1 = unchanged, 2 = twice as fast).");
            advance_on_voice_end = config.Bind("Dialogue", "AdvanceOnVoiceEnd", true,
                "In automatic mode, advance a voiced line when its voice clip has finished instead of waiting for the timer.");
            voice_end_delay = config.Bind("Dialogue", "VoiceEndDelay", 0.4f,
                "Seconds to wait after the voice clip ends before advancing.");
            toggle_key = config.Bind("Dialogue", "ToggleKey", KeyCode.F6,
                "Key that switches between automatic and manual dialogue advance.");
            advance_key = config.Bind("Dialogue", "AdvanceKey", KeyCode.Space,
                "Key that continues to the next line in manual mode, unless the dialogue box already has its own continue button. Set to None to disable.");
        }

        // The Dialogue System object is created per scene; find it after a load and once in a while after that.
        internal static void SceneLoaded() => Hook();

        internal static void Tick()
        {
            DialogueHistory.Tick();
            if (Time.frameCount % 120 == 0) Hook();
            if (!ModMenu.InputBlocked && toggle_key.Value != KeyCode.None && Input.GetKeyDown(toggle_key.Value)) Toggle();
            if (advance_key.Value != KeyCode.None && Input.GetKeyDown(advance_key.Value)) Advance();
            if (line != null) WatchVoice();
        }

        private static void Hook()
        {
            if (!DialogueManager.hasInstance) return;
            DialogueSystemController controller = DialogueManager.instance;
            if (controller != hooked)
            {
                hooked = controller;
                controller.conversationStarted += OnConversationStarted;
            }
            RefreshSettings();
        }

        private static void OnConversationStarted(Transform actor)
        {
            Apply(1f);
            DisplaySettings.SubtitleSettings s = settings;
            if (s != null)
            {
                Plugin.Log.LogInfo($"[dialogue] conversation started: game timing {original_chars_per_second:0.#} chars/s, min {original_min_seconds:0.#}s, continue={original_mode}; "
                    + $"auto={auto_advance.Value}, speed x{speed.Value:0.##}.");
            }
        }

        // The settings object can be replaced by per-conversation overrides, so remember the game's values per object.
        // `voice_factor` is 1 for lines with a voice, so a sped-up timer cannot cut the voice off.
        private static void Apply(float voice_factor)
        {
            DisplaySettings display = DialogueManager.displaySettings;
            DisplaySettings.SubtitleSettings s = display != null ? display.subtitleSettings : null;
            if (s == null) return;
            if (s != settings)
            {
                settings = s;
                original_chars_per_second = s.subtitleCharsPerSecond;
                original_min_seconds = s.minSubtitleSeconds;
                original_mode = s.continueButton;
            }
            float factor = Mathf.Max(0.1f, speed.Value) * voice_factor;
            if (voice_factor < 1f) factor = 1f;
            s.subtitleCharsPerSecond = original_chars_per_second * factor;
            s.minSubtitleSeconds = original_min_seconds / factor;
            s.continueButton = auto_advance.Value ? original_mode : DisplaySettings.SubtitleSettings.ContinueButtonMode.Always;
        }

        internal static void Toggle()
        {
            auto_advance.Value = !auto_advance.Value;
            Hook();
            string message = auto_advance.Value ? "Dialogue: auto-advance on" : "Dialogue: manual advance";
            Plugin.Log.LogInfo(message);
            try
            {
                if (DialogueManager.hasInstance) DialogueManager.ShowAlert(message);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[dialogue] could not show the toggle message: {e.Message}");
            }
        }

        private static void Continue()
        {
            StandardDialogueUI ui = DialogueManager.hasInstance ? DialogueManager.instance.standardDialogueUI : null;
            if (ui == null) return;
            last_advance = Time.unscaledTime;
            line = null;
            ui.OnContinue();
        }

        private static void Advance()
        {
            if (ModMenu.InputBlocked || auto_advance.Value || !DialogueManager.hasInstance || !DialogueManager.isConversationActive) return;
            if (Time.unscaledTime - last_advance < 0.25f) return;
            // A visible continue button takes the game's own accept input; calling Continue as well would skip two lines.
            foreach (StandardUISubtitlePanel panel in Resources.FindObjectsOfTypeAll<StandardUISubtitlePanel>())
            {
                if (panel.continueButton != null && panel.continueButton.gameObject.activeInHierarchy) return;
            }
            Continue();
        }

        // The voice clip is an AudioSource that starts playing just after the line does, on the speaker, the listener
        // or the Dialogue Manager. Prefer one whose clip name appears in the line's sequence.
        private static AudioSource FindVoice()
        {
            AudioSource best = null;
            bool best_named = false;
            string sequence = line.sequence ?? string.Empty;
            var roots = new[]
            {
                line.speakerInfo != null ? line.speakerInfo.transform : null,
                line.listenerInfo != null ? line.listenerInfo.transform : null,
                DialogueManager.instance.transform,
            };
            foreach (Transform root in roots)
            {
                if (root == null) continue;
                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>())
                {
                    if (!source.isPlaying || source.clip == null || source.loop) continue;
                    bool named = sequence.Contains(source.clip.name);
                    if (best == null || (named && !best_named))
                    {
                        best = source;
                        best_named = named;
                    }
                }
            }
            return best;
        }

        private static void WatchVoice()
        {
            if (!DialogueManager.hasInstance || !DialogueManager.isConversationActive)
            {
                line = null;
                return;
            }
            float now = Time.unscaledTime;
            if (voice == null)
            {
                if (now - line_start > 2f)
                {
                    if (!scan_logged)
                    {
                        scan_logged = true;
                        Plugin.Log.LogInfo($"[dialogue] no voice clip found for the line (sequence '{line.sequence}').");
                    }
                    return;
                }
                if (now < next_scan) return;
                next_scan = now + 0.1f;
                voice = FindVoice();
                if (voice != null)
                {
                    DialogueHistory.CaptureVoice(history_entry, voice);
                    Plugin.Log.LogInfo($"[dialogue] voice '{voice.clip.name}' ({voice.clip.length:0.0}s) on '{Plugin.GetPath(voice.transform)}', sequence '{line.sequence}'.");
                }
                return;
            }
            if (ModMenu.InputBlocked || !advance_on_voice_end.Value || !auto_advance.Value) return;
            if (voice.isPlaying)
            {
                voice_ended_at = -1f;
            }
            else if (voice_ended_at < 0f)
            {
                voice_ended_at = now;
            }
            else if (now - voice_ended_at >= voice_end_delay.Value)
            {
                Plugin.Log.LogInfo("[dialogue] voice finished, advancing.");
                Continue();
            }
        }

        // Called for every line the Dialogue System shows. Lines with a voice keep the game's timing.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConversationView), nameof(ConversationView.StartSubtitle))]
        private static void StartSubtitlePrefix(Subtitle subtitle)
        {
            DialogueHistory.StopReplay();
            history_entry = DialogueHistory.Record(subtitle);
            line = subtitle;
            line_start = Time.unscaledTime;
            next_scan = 0f;
            scan_logged = false;
            voice = null;
            voice_ended_at = -1f;
            bool has_voice = subtitle != null && subtitle.sequence != null && subtitle.sequence.IndexOf("Audio", System.StringComparison.OrdinalIgnoreCase) >= 0;
            Apply(has_voice ? 0f : 1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConversationView), "FinishSubtitle")]
        private static void FinishSubtitlePrefix(out Subtitle __state) => __state = line;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConversationView), "FinishSubtitle")]
        private static void FinishSubtitlePostfix(Subtitle __state, bool ___waitForContinue)
        {
            // FinishedSubtitleHandler can synchronously start the NEXT subtitle before this postfix.
            // Also leave the current line alone when FinishSubtitle returned while waiting for Continue.
            if (!___waitForContinue && ReferenceEquals(line, __state)) line = null;
        }
    }
}
