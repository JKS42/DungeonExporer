using UnityEngine;

namespace HardCodeDev.Examples
{
    /// <summary>
    /// Legacy sample for the SimpleOllamaUnity package. <see cref="HardCodeDev.SimpleOllamaUnity.Ollama"/>
    /// inherits <see cref="MonoBehaviour"/> but also exposes a config constructor; instantiating it with
    /// <c>new</c> is invalid in Unity and logs a runtime warning. DungeonExporer uses <c>OllamaHandler</c>
    /// instead; keep this script only as a placeholder if scenes still reference it.
    /// </summary>
    public class Test : MonoBehaviour
    {
        private void Start()
        {
            Debug.LogWarning(
                "[HardCodeDev.Examples.Test] Sample disabled. Use the scene's OllamaHandler (or attach " +
                "HardCodeDev.SimpleOllamaUnity.Ollama via AddComponent and initialize without `new`).");
        }
    }
}
