using System.Collections;
using DungeonExporer.UI;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// On startup, verifies Ollama is reachable and the configured model tag exists; otherwise shows <see cref="OllamaSetupPanelController"/>.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class OllamaFirstRunHealthCheck : MonoBehaviour
    {
        [SerializeField] private OllamaHandler _ollama;
        [SerializeField] private OllamaSetupPanelController _setupPanel;
        [SerializeField] private bool _runOnStart = true;

        private void Start()
        {
            if (!_runOnStart)
                return;
            if (_ollama == null)
                _ollama = FindFirstObjectByType<OllamaHandler>();
            if (_setupPanel == null)
                _setupPanel = FindFirstObjectByType<OllamaSetupPanelController>();

            if (_ollama == null)
            {
                Debug.LogWarning("OllamaFirstRunHealthCheck: no OllamaHandler in scene.");
                return;
            }

            StartCoroutine(RunCheck(_ollama.GetPreferredModelName()));
        }

        private IEnumerator RunCheck(string model)
        {
            bool ok = false;
            string err = null;
            yield return _ollama.CheckConnectivityCoroutine(model, () => ok = true, e => err = e);

            if (ok)
            {
                _setupPanel?.Hide();
                yield break;
            }

            string msg = (err ?? "Ollama check failed.") + "\n\nSee docs/setup.md in the repository (README also links install steps).";
            if (_setupPanel != null)
                _setupPanel.Show(msg);
            else
                Debug.LogWarning(msg);
        }
    }
}
