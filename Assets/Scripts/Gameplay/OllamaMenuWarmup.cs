using System.Collections;
using DungeonExporer.Settings;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Issues a tiny Ollama completion on the Main Menu so the model is loaded before Level1.
    /// </summary>
    public sealed class OllamaMenuWarmup : MonoBehaviour
    {
        private void Start()
        {
            if (!GameSettings.LlmEnabled)
                return;

            StartCoroutine(RunWarmup());
        }

        private IEnumerator RunWarmup()
        {
            OllamaHandler handler = GetComponent<OllamaHandler>();
            if (handler == null)
                handler = gameObject.AddComponent<OllamaHandler>();

            string modelName = GameSettings.LlmModel;
            string failMessage = null;

            yield return handler.WarmupModelCoroutine(
                onFail: msg => failMessage = msg);

            if (string.IsNullOrEmpty(failMessage))
                Debug.Log($"Ollama warm-up complete ({modelName}).");
            else
                Debug.LogWarning($"Ollama warm-up skipped: {failMessage}");
        }
    }
}
