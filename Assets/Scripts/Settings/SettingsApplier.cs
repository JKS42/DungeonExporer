using UnityEngine;

namespace DungeonExporer.Settings
{
    /// <summary>
    /// Bridges <see cref="GameSettings"/> to engine-level state:
    /// AudioListener volume, screen resolution / fullscreen, etc.
    /// Place one of these in the first scene (e.g. Main Menu) and mark DontDestroyOnLoad,
    /// or call <see cref="ApplyAll"/> from any boot script.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SettingsApplier : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            GameSettings.OnChanged += ApplyAll;
            ApplyAll();
        }

        private void OnDestroy()
        {
            GameSettings.OnChanged -= ApplyAll;
        }

        /// <summary>Apply every setting to the engine. Safe to call repeatedly.</summary>
        public static void ApplyAll()
        {
            // Audio: master volume is enforced via AudioListener; per-bus volumes
            // (Music / SFX) should be wired into an AudioMixer in a future pass.
            AudioListener.volume = GameSettings.MasterVolume;

            ApplyDisplay();
        }

        private static void ApplyDisplay()
        {
            Resolution[] resolutions = Screen.resolutions;
            int idx = GameSettings.ResolutionIndex;

            if (resolutions != null && resolutions.Length > 0 && idx >= 0 && idx < resolutions.Length)
            {
                Resolution r = resolutions[idx];
                Screen.SetResolution(
                    r.width,
                    r.height,
                    GameSettings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
                    r.refreshRateRatio);
            }
            else
            {
                // No stored resolution yet — only toggle fullscreen state.
                Screen.fullScreenMode = GameSettings.Fullscreen
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed;
            }
        }
    }
}
