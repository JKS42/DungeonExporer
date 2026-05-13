using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaHandler : MonoBehaviour
{
    [Header("Ollama Connection")]
    public string ollamaHost = "localhost";
    public int ollamaPort = 11434;
    public bool useHttps = false;

    [Header("Model and UI")]
    public string defaultModel = "qwen3:4b";
    public TMP_InputField modelInputField;
    public TMP_InputField promptInputField;
    public TMP_Text responseOutputField;

    [Header("Dialogue Output")]
    public string dialogueJsonFileName = "ollama-dialogue.json";

    [Header("Request Timing")]
    public int requestTimeoutSeconds = 120;

    [Serializable]
    private class OllamaGenerateResponse
    {
        public string model;
        public string response;
    }

    [Serializable]
    private class DialogueJsonEntry
    {
        public string model;
        public string prompt;
        public string response;
        public string createdAtUtc;
    }

    [Serializable]
    private class DialogueHistoryJson
    {
        public List<DialogueJsonEntry> entries = new List<DialogueJsonEntry>();
    }

    /// <summary>
    /// Hook this method to a UI Button OnClick event.
    /// It reads prompt/model from TMP input fields and sends the request to local Ollama.
    /// </summary>
    public void SendPromptFromInputField()
    {
        string prompt = promptInputField != null ? promptInputField.text : string.Empty;
        string model = modelInputField != null && !string.IsNullOrWhiteSpace(modelInputField.text)
            ? modelInputField.text
            : defaultModel;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            Debug.LogWarning("Prompt is empty. Enter text in the TMP input field.");
            return;
        }

        StartCoroutine(SendPromptRequest(model, prompt));
    }

    private IEnumerator SendPromptRequest(string model, string prompt)
    {
        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = $"{{\"model\":\"{EscapeJson(model)}\",\"prompt\":\"{EscapeJson(prompt)}\",\"stream\":false}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(5, requestTimeoutSeconds);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = ExtractResponseText(request.downloadHandler.text);
                SetOutputText(responseText);

                if (SaveDialogueJson(model, prompt, responseText))
                {
                    Debug.Log($"Ollama response received and saved to {GetDialogueJsonPath()}.");
                }
                else
                {
                    Debug.LogWarning("Ollama response received, but the dialogue JSON file could not be written.");
                }
            }
            else
            {
                string errorMessage = $"Ollama request failed: {request.error} (HTTP {request.responseCode})";
                SetOutputText(errorMessage);
                Debug.LogError(errorMessage);
            }
        }
    }

    private void SetOutputText(string text)
    {
        if (responseOutputField != null)
        {
            responseOutputField.text = text;
        }
    }

    private string GetDialogueJsonPath()
    {
        string assetsFolder = Application.dataPath;
        string outputDir = Path.Combine(assetsFolder, "DialogueOutput");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return Path.Combine(outputDir, dialogueJsonFileName);
    }

    private bool ShouldUseHttps()
    {
        if (IsLocalHost(ollamaHost))
        {
            return false;
        }

        return useHttps;
    }

    private static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        string normalizedHost = host.Trim().ToLowerInvariant();
        return normalizedHost == "localhost" || normalizedHost == "127.0.0.1" || normalizedHost == "::1";
    }

    private bool SaveDialogueJson(string model, string prompt, string responseText)
    {
        try
        {
            DialogueHistoryJson history = LoadDialogueHistory();
            history.entries.Add(new DialogueJsonEntry
            {
                model = model,
                prompt = prompt,
                response = responseText,
                createdAtUtc = DateTime.UtcNow.ToString("o")
            });

            string json = JsonUtility.ToJson(history, true);
            File.WriteAllText(GetDialogueJsonPath(), json, Encoding.UTF8);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to write dialogue JSON: {exception.Message}");
            return false;
        }
    }

    private static string ExtractResponseText(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        try
        {
            OllamaGenerateResponse parsedResponse = JsonUtility.FromJson<OllamaGenerateResponse>(rawResponse);
            if (parsedResponse != null && !string.IsNullOrWhiteSpace(parsedResponse.response))
            {
                return parsedResponse.response;
            }
        }
        catch (Exception)
        {
            // Fall back to the raw response below.
        }

        return rawResponse;
    }

    private DialogueHistoryJson LoadDialogueHistory()
    {
        string path = GetDialogueJsonPath();
        if (!File.Exists(path))
        {
            return new DialogueHistoryJson();
        }

        string existingJson = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return new DialogueHistoryJson();
        }

        try
        {
            DialogueHistoryJson history = JsonUtility.FromJson<DialogueHistoryJson>(existingJson);
            if (history != null && history.entries != null)
            {
                return history;
            }
        }
        catch (Exception)
        {
            // Fall through to legacy single-entry migration below.
        }

        try
        {
            DialogueJsonEntry legacyEntry = JsonUtility.FromJson<DialogueJsonEntry>(existingJson);
            if (legacyEntry != null &&
                (!string.IsNullOrWhiteSpace(legacyEntry.model) ||
                 !string.IsNullOrWhiteSpace(legacyEntry.prompt) ||
                 !string.IsNullOrWhiteSpace(legacyEntry.response) ||
                 !string.IsNullOrWhiteSpace(legacyEntry.createdAtUtc)))
            {
                return new DialogueHistoryJson
                {
                    entries = new List<DialogueJsonEntry> { legacyEntry }
                };
            }
        }
        catch (Exception)
        {
            // If the existing file is unreadable, start a fresh history below.
        }

        return new DialogueHistoryJson();
    }

    private static string EscapeJson(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
