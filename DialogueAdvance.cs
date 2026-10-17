using System.Collections.Generic;
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

        internal static bool AutoAdvance
        {
            get => auto_advance.Value;
            set
            {
                if (auto_advance.Value == value) return;
                auto_advance.Value = value;
                RefreshSettings();
                ShortcutFeedback.Show(value ? "Auto dialogue enabled" : "Manual dialogue enabled",
                    value ? "Dialogue advances automatically using your speed and voice settings." : "Dialogue waits for your configured advance key or controller A.");
            }
        }
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
        private static readonly List<AudioSource> source_buffer = new List<AudioSource>();
        private static readonly Transform[] voice_roots = new Transform[3];
        private static ConversationView current_view;
        private static IDialogueUI last_ui;
        private static Subtitle last_shown;
        private static Subtitle pending_advance;
        private static int pending_frame = -1;
        private static int subtitle_frame = -1;

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
                "Key that continues in manual mode. A same-subtitle fallback handles unavailable native Continue input. Set to None to disable the keyboard shortcut.");
        }

        // The Dialogue System object is created per scene; find it after a load and once in a while after that.
        internal static void SceneLoaded()
        {
            line = null;
            voice = null;
            current_view = null;
            last_ui = null;
            last_shown = null;
            pending_advance = null;
            pending_frame = -1;
            Hook();
        }

        internal static void Tick()
        {
            DialogueHistory.Tick();
            if (Time.frameCount % 120 == 0) Hook();
            if (!ModMenu.InputBlocked && toggle_key.Value != KeyCode.None && Input.GetKeyDown(toggle_key.Value)) Toggle();
            ProcessPendingAdvance();
            var pad = ModMenu.Pad;
            bool controller_advance = pad != null && pad.buttonSouth.wasPressedThisFrame &&
                !(ControllerInput.Modifier.Value != PadButton.None && ControllerInput.ModifierHeld(pad));
            if ((advance_key.Value != KeyCode.None && Input.GetKeyDown(advance_key.Value)) || controller_advance) Advance();
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
            AutoAdvance = !AutoAdvance;
            Hook();
            string message = auto_advance.Value ? "Dialogue: auto-advance on" : "Dialogue: manual advance";
            Plugin.Log.LogInfo(message);
        }

        private static void Continue()
        {
            if (current_view == null) return;
            last_advance = Time.unscaledTime;
            // Use the actual view, including conversation-specific UI overrides.
            // FinishSubtitle may synchronously start the next line; never clear that new state afterward.
            line = null;
            current_view.HandleContinueButtonClick();
        }

        private static void Advance()
        {
            if (ModMenu.InputBlocked || auto_advance.Value || !DialogueManager.hasInstance) return;
            if (Time.unscaledTime - last_advance < 0.25f || last_shown == null || subtitle_frame >= Time.frameCount) return;
            // Let the native button handle this press first. The next frame checks subtitle identity,
            // so a working native Continue can never make this fallback skip the following line.
            pending_advance = last_shown;
            pending_frame = Time.frameCount;
        }

        private static void ProcessPendingAdvance()
        {
            if (pending_frame < 0 || Time.frameCount <= pending_frame) return;
            Subtitle requested = pending_advance;
            int requested_frame = pending_frame;
            pending_frame = -1;
            pending_advance = null;
            if (ModMenu.InputBlocked || auto_advance.Value || !DialogueManager.hasInstance ||
                !ReferenceEquals(requested, last_shown)) return;
            if (DialogueManager.isConversationActive)
            {
                if (current_view == null || !ModLogic.ShouldFallbackContinue(requested, line,
                    subtitle_frame, requested_frame, current_view.isWaitingForContinue)) return;
                Plugin.Log.LogInfo("[dialogue] native Continue did not advance; continuing the same waiting subtitle.");
                Continue();
            }
            else if (last_ui != null)
            {
                // Only dismiss the subtitle recorded by this mod; never stop a conversation or close all UI.
                last_ui.HideSubtitle(requested);
                last_shown = null;
                last_advance = Time.unscaledTime;
                Plugin.Log.LogInfo("[dialogue] dismissed the tracked subtitle after its conversation ended.");
            }
        }
        // The voice clip is an AudioSource that starts playing just after the line does, on the speaker, the listener
        // or the Dialogue Manager. Prefer one whose clip name appears in the line's sequence.
        private static AudioSource FindVoice()
        {
            AudioSource best = null;
            bool best_named = false;
            string sequence = line.sequence ?? string.Empty;
            voice_roots[0] = line.speakerInfo != null ? line.speakerInfo.transform : null;
            voice_roots[1] = line.listenerInfo != null ? line.listenerInfo.transform : null;
            voice_roots[2] = DialogueManager.instance.transform;
            foreach (Transform root in voice_roots)
            {
                if (root == null) continue;
                source_buffer.Clear();
                root.GetComponentsInChildren(false, source_buffer);
                foreach (AudioSource source in source_buffer)
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
            source_buffer.Clear();
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
                        if (Plugin.EnableSceneInventory.Value) Plugin.Log.LogInfo($"[dialogue] no voice clip found for the line (sequence '{line.sequence}').");
                    }
                    return;
                }
                if (now < next_scan) return;
                next_scan = now + 0.1f;
                voice = FindVoice();
                if (voice != null)
                {
                    DialogueHistory.CaptureVoice(history_entry, voice);
                    if (Plugin.EnableSceneInventory.Value) Plugin.Log.LogInfo($"[dialogue] voice '{voice.clip.name}' ({voice.clip.length:0.0}s) on '{Plugin.GetPath(voice.transform)}', sequence '{line.sequence}'.");
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
                if (Plugin.EnableSceneInventory.Value) Plugin.Log.LogInfo("[dialogue] voice finished, advancing.");
                Continue();
            }
        }

        // Called for every line the Dialogue System shows. Lines with a voice keep the game's timing.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConversationView), nameof(ConversationView.StartSubtitle))]
        private static void StartSubtitlePrefix(ConversationView __instance, Subtitle subtitle)
        {
            DialogueHistory.StopReplay();
            history_entry = DialogueHistory.Record(subtitle);
            current_view = __instance;
            last_ui = __instance.dialogueUI;
            last_shown = subtitle;
            subtitle_frame = Time.frameCount;
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
