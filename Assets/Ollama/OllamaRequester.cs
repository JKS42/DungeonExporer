using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;

public class OllamaRequester : MonoBehaviour
{
    // Default Ollama local URL
    private string url = "http://localhost:11434/api/generate";

    void Start()
    {
        StartCoroutine(SendOllamaRequest("llama3", "Why is the sky blue?"));
    }

    IEnumerator SendOllamaRequest(string model, string prompt)
    {
        // Create request data object
        var requestData = new {
            model = model,
            prompt = prompt,
            stream = false // Set to false for a single response
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + request.error);
            }
            else
            {
                Debug.Log("Response: " + request.downloadHandler.text);
                // Parse the response here using JsonConvert.DeserializeObject
            }
        }
    }
}
