using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DungeonExporer.Settings;
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

    [Header("Token limits (Ollama options.num_predict)")]
    [Tooltip("Default max tokens for streamed gameplay dialogue.")]
    public int defaultStreamMaxTokens = 96;
    [Tooltip("Default max tokens for non-stream test UI and short checks.")]
    public int defaultNonStreamMaxTokens = 256;

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

    [Serializable]
    private class OllamaApiErrorBody
    {
        public string error;
    }

    [Serializable]
    private class OllamaTagsResponse
    {
        public OllamaTagEntry[] models;
    }

    [Serializable]
    private class OllamaTagEntry
    {
        public string name;
        public string model;
    }

    /// <summary>Preferred model tag: settings, tester field, then inspector default.</summary>
    public string GetPreferredModelName()
    {
        string fromSettings = GameSettings.LlmModel;
        if (!string.IsNullOrWhiteSpace(fromSettings))
            return fromSettings.Trim();

        if (modelInputField != null && !string.IsNullOrWhiteSpace(modelInputField.text))
            return modelInputField.text.Trim();

        if (!string.IsNullOrWhiteSpace(defaultModel))
            return defaultModel.Trim();

        return "qwen3:4b";
    }

    /// <summary>
    /// Resolves <paramref name="preferredModel"/> to an exact tag from <c>GET /api/tags</c> (e.g. <c>qwen3:4b</c>).
    /// </summary>
    public IEnumerator ResolveModelTagCoroutine(string preferredModel, Action<string> onResolved, Action<string> onFail)
    {
        string preferred = string.IsNullOrWhiteSpace(preferredModel) ? GetPreferredModelName() : preferredModel.Trim();

        string tagsBody = null;
        string fetchError = null;
        yield return FetchTagsBodyCoroutine(body => tagsBody = body, err => fetchError = err);

        if (tagsBody == null)
        {
            onFail?.Invoke(fetchError ?? "Could not read models from Ollama.");
            yield break;
        }

        List<string> installed = ParseInstalledModelNames(tagsBody);
        string match = FindBestMatchingTag(installed, preferred);
        if (!string.IsNullOrEmpty(match))
        {
            onResolved?.Invoke(match);
            yield break;
        }

        onFail?.Invoke(BuildModelNotFoundMessage(preferred, installed));
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
            },
            defaultNonStreamMaxTokens));
    }

    /// <summary>
    /// Sends a prompt to local Ollama for gameplay (NPC dialogue, etc.). Does not require TMP input fields.
    /// </summary>
    /// <param name="saveToDialogueJson">When true, appends to the same JSON log used by the test UI.</param>
    /// <param name="updateResponseUiField">When true, writes the model reply to <see cref="responseOutputField"/>.</param>
    public void RequestGeneration(string model, string prompt, Action<string> onSuccess, Action<string> onError,
        bool saveToDialogueJson = true, bool updateResponseUiField = false, int maxPredictTokens = 0)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : defaultNonStreamMaxTokens;
        StartCoroutine(GenerateCoroutine(model, prompt, saveToDialogueJson, updateResponseUiField, onSuccess, onError, limit));
    }

    /// <summary>
    /// Streams tokens from Ollama (<c>stream: true</c>). <paramref name="onDelta"/> receives each decoded <c>response</c> fragment;
    /// <paramref name="onComplete"/> receives the full sanitized text (same as saved to JSON when enabled).
    /// </summary>
    public void RequestGenerationStreaming(string model, string prompt, Action<string> onDelta, Action<string> onComplete,
        Action<string> onError, bool saveToDialogueJson = true, int maxPredictTokens = 0)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : defaultStreamMaxTokens;
        StartCoroutine(GenerateStreamingCoroutine(model, prompt, onDelta, onComplete, onError, saveToDialogueJson, limit));
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
        Action<string> onError, bool saveToDialogueJson, int maxPredictTokens)
    {
        AbortActiveRequest();

        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = BuildGenerateJsonBody(model, prompt, stream: true, maxPredictTokens);

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
                string errorMessage =
                    $"Ollama stream failed: {request.error} (HTTP {request.responseCode}){BuildOllamaFailureDetail(request)}";
                LogOllamaRequestFailure(errorMessage, request.responseCode);
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
        Action<string> onSuccess, Action<string> onError, int maxPredictTokens)
    {
        AbortActiveRequest();

        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = BuildGenerateJsonBody(model, prompt, stream: false, maxPredictTokens);

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
                string errorMessage =
                    $"Ollama request failed: {request.error} (HTTP {request.responseCode}){BuildOllamaFailureDetail(request)}";
                if (updateResponseUiField)
                    SetOutputText(errorMessage);
                LogOllamaRequestFailure(errorMessage, request.responseCode);
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

    public IEnumerator CheckConnectivityCoroutine(string modelToVerify, Action onOk, Action<string> onFail)
    {
        string preferred = string.IsNullOrWhiteSpace(modelToVerify) ? GetPreferredModelName() : modelToVerify.Trim();
        yield return ResolveModelTagCoroutine(preferred, _ => onOk?.Invoke(), onFail);
    }

    private IEnumerator FetchTagsBodyCoroutine(Action<string> onSuccess, Action<string> onFail)
    {
        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/tags";

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onFail?.Invoke(
                    $"Cannot reach Ollama at {ollamaHost}:{ollamaPort} ({request.error}). Install from https://ollama.com and see docs/setup.md.");
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text ?? string.Empty);
        }
    }

    private static List<string> ParseInstalledModelNames(string tagsJson)
    {
        var names = new List<string>(8);
        if (string.IsNullOrWhiteSpace(tagsJson))
            return names;

        try
        {
            OllamaTagsResponse parsed = JsonUtility.FromJson<OllamaTagsResponse>(tagsJson);
            if (parsed?.models != null)
            {
                for (int i = 0; i < parsed.models.Length; i++)
                {
                    OllamaTagEntry entry = parsed.models[i];
                    string tag = !string.IsNullOrWhiteSpace(entry.name) ? entry.name : entry.model;
                    if (!string.IsNullOrWhiteSpace(tag) && !names.Contains(tag))
                        names.Add(tag.Trim());
                }
            }
        }
        catch (Exception)
        {
            // Fall through to regex scrape.
        }

        if (names.Count == 0)
        {
            const string marker = "\"name\":\"";
            int idx = 0;
            while (idx < tagsJson.Length)
            {
                int start = tagsJson.IndexOf(marker, idx, StringComparison.Ordinal);
                if (start < 0)
                    break;
                start += marker.Length;
                int end = tagsJson.IndexOf('"', start);
                if (end < 0)
                    break;
                string tag = tagsJson.Substring(start, end - start);
                if (!string.IsNullOrWhiteSpace(tag) && !names.Contains(tag))
                    names.Add(tag);
                idx = end + 1;
            }
        }

        return names;
    }

    private static string FindBestMatchingTag(IReadOnlyList<string> installed, string preferred)
    {
        if (installed == null || installed.Count == 0 || string.IsNullOrWhiteSpace(preferred))
            return null;

        preferred = preferred.Trim();

        for (int i = 0; i < installed.Count; i++)
        {
            if (string.Equals(installed[i], preferred, StringComparison.OrdinalIgnoreCase))
                return installed[i];
        }

        string prefStem = preferred;
        int colon = preferred.IndexOf(':');
        if (colon > 0)
            prefStem = preferred.Substring(0, colon);

        string stemMatch = null;
        for (int i = 0; i < installed.Count; i++)
        {
            string tag = installed[i];
            int c2 = tag.IndexOf(':');
            string instStem = c2 > 0 ? tag.Substring(0, c2) : tag;
            if (!string.Equals(instStem, prefStem, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(tag, preferred, StringComparison.OrdinalIgnoreCase))
                return tag;

            stemMatch = tag;
        }

        return stemMatch;
    }

    private static string BuildModelNotFoundMessage(string preferred, IReadOnlyList<string> installed)
    {
        var sb = new StringBuilder(256);
        sb.Append("Model '").Append(preferred).Append("' is not installed.\nRun: ollama pull ").Append(preferred);
        if (installed != null && installed.Count > 0)
        {
            sb.Append("\n\nInstalled models: ");
            for (int i = 0; i < installed.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(installed[i]);
            }
            sb.Append("\n\nUse an installed name on OllamaHandler → Default Model, or pull the model above.");
        }
        else
            sb.Append("\n\n(ollama list is empty — pull a model first.)");

        sb.Append("\nSee docs/setup.md.");
        return sb.ToString();
    }

    private static void LogOllamaRequestFailure(string message, long responseCode)
    {
        if (responseCode == 404)
            Debug.LogWarning(message);
        else
            Debug.LogError(message);
    }

    /// <summary>
    /// Appends Ollama's JSON <c>error</c> field (when present) and short hints for common HTTP codes.
    /// Ollama often returns <b>404</b> when the model name does not exist locally (not pulled or wrong tag).
    /// </summary>
    private static string BuildOllamaFailureDetail(UnityWebRequest request)
    {
        if (request == null)
            return string.Empty;

        string body = request.downloadHandler != null ? request.downloadHandler.text : null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                OllamaApiErrorBody parsed = JsonUtility.FromJson<OllamaApiErrorBody>(body);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.error))
                    return " Ollama: " + parsed.error.Trim();
            }
            catch (Exception)
            {
                // Ignore malformed JSON.
            }
        }

        long code = request.responseCode;
        if (code == 404)
        {
            return " (HTTP 404 usually means the model is missing or the name does not match `ollama list` — run `ollama pull <tag>` and align the Model field / OllamaHandler.defaultModel. See docs/setup.md troubleshooting.)";
        }

        if (code == 401 || code == 403)
            return " (Check Ollama auth / proxy settings if you use a non-default host.)";

        return string.Empty;
    }

    private static string BuildGenerateJsonBody(string model, string prompt, bool stream, int maxPredictTokens)
    {
        int n = Mathf.Clamp(maxPredictTokens, 8, 8192);
        return "{\"model\":\"" + EscapeJson(model) + "\",\"prompt\":\"" + EscapeJson(prompt) + "\",\"stream\":" +
               (stream ? "true" : "false") + ",\"options\":{\"num_predict\":" + n + "}}";
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
