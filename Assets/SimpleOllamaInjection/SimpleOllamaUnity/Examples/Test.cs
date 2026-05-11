using UnityEngine;
using HardCodeDev.SimpleOllamaUnity;

namespace HardCodeDev.Examples
{
    public class Test : MonoBehaviour
    {
        private async void Start()
        {
            var ollama = new Ollama(new OllamaConfig(
                modelName: "qwen3:4b",
                systemPrompt: "Your answer mustn't be more than 10 words"
                ));

            var response = await ollama.SendMessage(new OllamaRequest(
                userPrompt: "When Github was created?"
                ));

            Debug.Log(response);
        }
    }
}
