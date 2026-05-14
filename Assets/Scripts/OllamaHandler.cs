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

    private UnityWebRequest _abortableRequest;

    [Serializable]
    private class OllamaGenerateResponse
    {
        public string model;
        public string response;
    }

    [Serializable]
    private sealed class OllamaStreamChunk
    {
        public string response;
        public bool done;
    }

    /// <summary>Buffers NDJSON lines from <c>/api/generate</c> with <c>stream: true</c> and emits decoded <c>response</c> fragments.</summary>
    private sealed class OllamaNdjsonStreamHandler : DownloadHandlerScript
    {
        private readonly Action<string> _onDelta;
        private readonly StringBuilder _lineBuffer = new StringBuilder(4096);
        private readonly StringBuilder _fullResponse = new StringBuilder(4096);

        public OllamaNdjsonStreamHandler(Action<string> onDelta)
            : base()
        {
            _onDelta = onDelta;
        }

        public string GetFullResponse() => _fullResponse.ToString();

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
                return true;

            _lineBuffer.Append(Encoding.UTF8.GetString(data, 0, dataLength));
            ExtractCompleteLines();
            return true;
        }

        public void FlushPartialLine()
        {
            if (_lineBuffer.Length == 0)
                return;

            string tail = _lineBuffer.ToString().Trim();
            _lineBuffer.Clear();
            if (tail.Length > 0)
                TryEmitLine(tail);
        }

        private void ExtractCompleteLines()
        {
            while (true)
            {
                string s = _lineBuffer.ToString();
                int nl = s.IndexOf('\n');
                if (nl < 0)
                    return;

                string line = s.Substring(0, nl).Trim();
                _lineBuffer.Remove(0, nl + 1);
                if (line.Length > 0)
                    TryEmitLine(line);
            }
        }

        private void TryEmitLine(string line)
        {
            try
            {
                OllamaStreamChunk chunk = JsonUtility.FromJson<OllamaStreamChunk>(line);
                if (chunk == null || string.IsNullOrEmpty(chunk.response))
                    return;
                _fullResponse.Append(chunk.response);
                _onDelta?.Invoke(chunk.response);
            }
            catch (Exception)
            {
                // Malformed NDJSON line; skip.
            }
        }
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

        StartCoroutine(GenerateCoroutine(model, prompt, saveToDialogueJson: true, updateResponseUiField: true,
            onSuccess: _ => { },
            onError: err =>
            {
                SetOutputText(err);
                Debug.LogError(err);
            }));
    }

    /// <summary>
    /// Sends a prompt to local Ollama for gameplay (NPC dialogue, etc.). Does not require TMP input fields.
    /// </summary>
    /// <param name="saveToDialogueJson">When true, appends to the same JSON log used by the test UI.</param>
    /// <param name="updateResponseUiField">When true, writes the model reply to <see cref="responseOutputField"/>.</param>
    public void RequestGeneration(string model, string prompt, Action<string> onSuccess, Action<string> onError,
        bool saveToDialogueJson = true, bool updateResponseUiField = false)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        StartCoroutine(GenerateCoroutine(model, prompt, saveToDialogueJson, updateResponseUiField, onSuccess, onError));
    }

    /// <summary>
    /// Streams tokens from Ollama (<c>stream: true</c>). <paramref name="onDelta"/> receives each decoded <c>response</c> fragment;
    /// <paramref name="onComplete"/> receives the full sanitized text (same as saved to JSON when enabled).
    /// </summary>
    public void RequestGenerationStreaming(string model, string prompt, Action<string> onDelta, Action<string> onComplete,
        Action<string> onError, bool saveToDialogueJson = true)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        StartCoroutine(GenerateStreamingCoroutine(model, prompt, onDelta, onComplete, onError, saveToDialogueJson));
    }

    /// <summary>Aborts the in-flight HTTP request (streaming or not). The owning coroutine still runs and disposes the request in its <c>finally</c> block.</summary>
    public void AbortActiveRequest()
    {
        if (_abortableRequest == null || _abortableRequest.isDone)
            return;

        try
        {
            _abortableRequest.Abort();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Ollama abort: {exception.Message}");
        }
    }

    private IEnumerator GenerateStreamingCoroutine(string model, string prompt, Action<string> onDelta, Action<string> onComplete,
        Action<string> onError, bool saveToDialogueJson)
    {
        AbortActiveRequest();

        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = $"{{\"model\":\"{EscapeJson(model)}\",\"prompt\":\"{EscapeJson(prompt)}\",\"stream\":true}}";

        var streamHandler = new OllamaNdjsonStreamHandler(onDelta);
        var request = new UnityWebRequest(url, "POST");
        _abortableRequest = request;
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = streamHandler;
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.Max(5, requestTimeoutSeconds);

        yield return request.SendWebRequest();

        try
        {
            streamHandler.FlushPartialLine();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = $"Ollama stream failed: {request.error} (HTTP {request.responseCode})";
                Debug.LogError(errorMessage);
                onError?.Invoke(errorMessage);
                yield break;
            }

            string responseText = SanitizeModelOutput(streamHandler.GetFullResponse());

            if (saveToDialogueJson)
            {
                if (SaveDialogueJson(model, prompt, responseText))
                    Debug.Log($"Ollama stream complete; saved to {GetDialogueJsonPath()}.");
                else
                    Debug.LogWarning("Ollama stream complete, but dialogue JSON could not be written.");
            }

            onComplete?.Invoke(responseText);
        }
        finally
        {
            request.Dispose();
            if (_abortableRequest == request)
                _abortableRequest = null;
        }
    }

    private IEnumerator GenerateCoroutine(string model, string prompt, bool saveToDialogueJson, bool updateResponseUiField,
        Action<string> onSuccess, Action<string> onError)
    {
        AbortActiveRequest();

        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = $"{{\"model\":\"{EscapeJson(model)}\",\"prompt\":\"{EscapeJson(prompt)}\",\"stream\":false}}";

        var request = new UnityWebRequest(url, "POST");
        _abortableRequest = request;
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.Max(5, requestTimeoutSeconds);

        yield return request.SendWebRequest();

        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = SanitizeModelOutput(ExtractResponseText(request.downloadHandler.text));

                if (updateResponseUiField)
                    SetOutputText(responseText);

                if (saveToDialogueJson)
                {
                    if (SaveDialogueJson(model, prompt, responseText))
                        Debug.Log($"Ollama response received and saved to {GetDialogueJsonPath()}.");
                    else
                        Debug.LogWarning("Ollama response received, but the dialogue JSON file could not be written.");
                }

                onSuccess?.Invoke(responseText);
            }
            else
            {
                string errorMessage = $"Ollama request failed: {request.error} (HTTP {request.responseCode})";
                if (updateResponseUiField)
                    SetOutputText(errorMessage);
                Debug.LogError(errorMessage);
                onError?.Invoke(errorMessage);
            }
        }
        finally
        {
            request.Dispose();
            if (_abortableRequest == request)
                _abortableRequest = null;
        }
    }

    public static string SanitizeModelOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        const string openTag = "<think>";
        const string closeTag = "</think>";
        text = StripTaggedBlocks(text, openTag, closeTag);
        return text.Trim();
    }

    private static string StripTaggedBlocks(string text, string openTag, string closeTag)
    {
        while (true)
        {
            int start = text.IndexOf(openTag, StringComparison.Ordinal);
            if (start < 0)
                return text;
            int end = text.IndexOf(closeTag, start + openTag.Length, StringComparison.Ordinal);
            if (end < 0)
                return text.Remove(start);
            end += closeTag.Length;
            text = text.Remove(start, end - start);
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
