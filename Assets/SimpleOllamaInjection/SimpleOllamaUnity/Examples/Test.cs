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
                systemPrompt: "Your answer mustn't be more than 100 words"
                ));

            var response = await ollama.SendMessage(new OllamaRequest(
                userPrompt: "What is the weather like today? Give an explanation in 100 words."
                ));

            Debug.Log(response);
        }
    }
}
