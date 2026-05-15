using System;
using UnityEngine;

namespace DungeonExporer.Settings
{
    /// <summary>
    /// Central, serialization-free settings store backed by <see cref="PlayerPrefs"/>.
    /// Read/write via the static properties; subscribe to <see cref="OnChanged"/> to react.
    /// </summary>
    public static class GameSettings
    {
        // PlayerPrefs keys are namespaced to avoid collisions with other Unity projects.
        private const string KMaster = "dx.audio.master";
        private const string KMusic = "dx.audio.music";
        private const string KSfx = "dx.audio.sfx";
        private const string KFullscreen = "dx.display.fullscreen";
        private const string KResolutionIndex = "dx.display.resolutionIndex";
        private const string KMouseSensitivity = "dx.input.mouseSensitivity";
        private const string KLlmEnabled = "dx.llm.enabled";
        private const string KLlmModel = "dx.llm.model";

        public static event Action OnChanged;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KMaster, 0.8f);
            set => SetFloat(KMaster, Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KMusic, 0.7f);
            set => SetFloat(KMusic, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KSfx, 0.9f);
            set => SetFloat(KSfx, Mathf.Clamp01(value));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KFullscreen, 1) == 1;
            set => SetInt(KFullscreen, value ? 1 : 0);
        }

        public static int ResolutionIndex
        {
            get => PlayerPrefs.GetInt(KResolutionIndex, -1);
            set => SetInt(KResolutionIndex, value);
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(KMouseSensitivity, 1.0f);
            set => SetFloat(KMouseSensitivity, Mathf.Clamp(value, 0.1f, 3f));
        }

        public static bool LlmEnabled
        {
            get => PlayerPrefs.GetInt(KLlmEnabled, 1) == 1;
            set => SetInt(KLlmEnabled, value ? 1 : 0);
        }

        public static string LlmModel
        {
            get => PlayerPrefs.GetString(KLlmModel, "gemma3:4b");
            set
            {
                PlayerPrefs.SetString(KLlmModel, string.IsNullOrWhiteSpace(value) ? "gemma3:4b" : value.Trim());
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        /// <summary>Reset every setting to its default. Useful from an Options "Reset" button.</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(KMaster);
            PlayerPrefs.DeleteKey(KMusic);
            PlayerPrefs.DeleteKey(KSfx);
            PlayerPrefs.DeleteKey(KFullscreen);
            PlayerPrefs.DeleteKey(KResolutionIndex);
            PlayerPrefs.DeleteKey(KMouseSensitivity);
            PlayerPrefs.DeleteKey(KLlmEnabled);
            PlayerPrefs.DeleteKey(KLlmModel);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
