using System.Collections;
using DungeonExporer.Settings;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Issues a tiny Ollama completion while the Main Menu is open so Level1 hits a hot model.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class OllamaMenuWarmup : MonoBehaviour
    {
        private OllamaHandler _handler;
        private Coroutine _warmupRoutine;
        private string _lastWarmedModelTag;

        private void Awake()
        {
            _handler = GetComponent<OllamaHandler>();
            if (_handler == null)
                _handler = gameObject.AddComponent<OllamaHandler>();
        }

        private void OnEnable()
        {
            GameSettings.OnChanged += OnSettingsChanged;
            ScheduleWarmup();
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= OnSettingsChanged;
            if (_warmupRoutine != null)
            {
                StopCoroutine(_warmupRoutine);
                _warmupRoutine = null;
            }
        }

        private void OnSettingsChanged()
        {
            if (!GameSettings.LlmEnabled)
            {
                _lastWarmedModelTag = null;
                return;
            }

            ScheduleWarmup(force: true);
        }

        private void ScheduleWarmup(bool force = false)
        {
            if (!isActiveAndEnabled || !GameSettings.LlmEnabled)
                return;

            if (_warmupRoutine != null)
                StopCoroutine(_warmupRoutine);

            _warmupRoutine = StartCoroutine(RunWarmup(force));
        }

        private IEnumerator RunWarmup(bool force)
        {
            if (_handler == null)
                yield break;

            string preferred = _handler.GetPreferredModelName();
            if (!force && !string.IsNullOrEmpty(_lastWarmedModelTag)
                && string.Equals(_lastWarmedModelTag, preferred, System.StringComparison.OrdinalIgnoreCase))
            {
                _warmupRoutine = null;
                yield break;
            }

            string failMessage = null;

            yield return _handler.WarmupModelCoroutine(
                onSuccess: () => { },
                onFail: msg => failMessage = msg);

            if (string.IsNullOrEmpty(failMessage))
            {
                _lastWarmedModelTag = preferred;
                Debug.Log($"[Main Menu] Ollama warm-up complete ({preferred}).");
            }
            else if (!OllamaHandler.IsSupersededAbortError(failMessage))
            {
                Debug.LogWarning($"[Main Menu] Ollama warm-up skipped: {failMessage}");
            }

            _warmupRoutine = null;
        }
    }
}
