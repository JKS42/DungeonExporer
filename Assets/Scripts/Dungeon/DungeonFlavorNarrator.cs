using DungeonExporer.Gameplay;
using UnityEngine;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// Requests a one-line narrator flavor from Ollama when entering safe / encounter areas (rate-limited).
    /// </summary>
    [DefaultExecutionOrder(15)]
    public sealed class DungeonFlavorNarrator : MonoBehaviour
    {
        public static DungeonFlavorNarrator Instance { get; private set; }

        [SerializeField] private OllamaHandler _ollama;
        [SerializeField] private float _cooldownSeconds = 42f;
        [SerializeField] private int _maxPredictTokens = 72;

        private float _nextAllowedTime;
        private bool _busy;

        private void Awake()
        {
            Instance = this;
            if (_ollama == null)
                _ollama = FindFirstObjectByType<OllamaHandler>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void NotifyFlavorEnter(DungeonFlavorKind kind)
        {
            if (Instance == null)
                return;
            Instance.HandleEnter(kind);
        }

        private void HandleEnter(DungeonFlavorKind kind)
        {
            if (!isActiveAndEnabled || _ollama == null)
                return;
            if (NarrationUiGate.PauseOpen || NarrationUiGate.DialogueOpen)
                return;
            if (Time.unscaledTime < _nextAllowedTime || _busy)
                return;

            _busy = true;
            _nextAllowedTime = Time.unscaledTime + _cooldownSeconds;

            string model = _ollama.GetPreferredModelName();
            string kindText = kind == DungeonFlavorKind.Safe
                ? "mint-tinted calm safe tiles where breath comes easier"
                : "crimson encounter tiles that hum as if something listens back";

            string prompt =
                "You are the dungeon narrator for a cosy fantasy first-person game.\n" +
                "The player just stepped onto " + kindText + ".\n" +
                "Write exactly one short sentence, second person (\"you\"), sensory mood only. " +
                "No instructions, no quest spoilers, no NPC names, no markdown. Max 26 words.";

            _ollama.RequestGeneration(model, prompt,
                onSuccess: text =>
                {
                    _busy = false;
                    string line = OllamaHandler.SanitizeModelOutput(text).Trim();
                    if (line.Length > 0)
                        DungeonFlavorHudBridge.PublishFlavorToast?.Invoke(line, 5.5f);
                },
                onError: _ => { _busy = false; },
                saveToDialogueJson: false,
                updateResponseUiField: false,
                maxPredictTokens: _maxPredictTokens);
        }
    }
}
