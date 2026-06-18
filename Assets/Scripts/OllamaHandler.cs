using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DungeonExporer.AI;
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
    public string defaultModel = "gemma3:4b";
    public TMP_InputField modelInputField;
    public TMP_InputField promptInputField;
    public TMP_Text responseOutputField;

    [Header("Dialogue Output")]
    public string dialogueJsonFileName = "ollama-dialogue.json";

        [Header("Character Personality Template (Cap — DatingSim)")]
    public string characterContextFileName = "cap_context.json";
    public string templateFileName = "cap_personality.j2";

    [Header("Request Timing")]
    public int requestTimeoutSeconds = 120;

    [Header("Token limits (Ollama options.num_predict)")]
    [Tooltip("Default max tokens for streamed gameplay dialogue.")]
    public int defaultStreamMaxTokens = 180;
    [Tooltip("Default max tokens for non-stream test UI and short checks.")]
    public int defaultNonStreamMaxTokens = 256;
    [Tooltip("Max tokens for NPC voice (prefetch + dialogue panel). Keep low for speed.")]
    public int defaultNpcMaxTokens = 80;
    [Tooltip("Max tokens for Ask Cap /api/chat (qwen3 needs headroom beyond planning text).")]
    public int defaultNpcChatMaxTokens = 160;

    [Header("Fast mode (Options → Fast AI responses)")]
    [Tooltip("Model tag used when fast mode is on (should be a small, fast local model).")]
    public string fastPreferredModel = "gemma3:4b";
    public int fastNpcMaxTokens = 48;
    public int fastNpcChatMaxTokens = 80;
    public int fastStreamMaxTokens = 100;
    public int fastNonStreamMaxTokens = 128;
    public int fastFlavorMaxTokens = 40;
    public int defaultFlavorMaxTokens = 72;

    private UnityWebRequest _abortableRequest;
    private readonly Queue<IEnumerator> _requestQueue = new Queue<IEnumerator>(8);
    private Coroutine _requestQueueRunner;

    [Serializable]
    private class OllamaGenerateResponse
    {
        public string model;
        public string response;
    }

    [Serializable]
    private class OllamaChatMessage
    {
        public string role;
        public string content;
        public string thinking;
    }

    [Serializable]
    private class OllamaChatResponse
    {
        public string model;
        public OllamaChatMessage message;
    }

    [Serializable]
    private sealed class OllamaStreamChunk
    {
        public string response;
        public bool done;
        public string thinking;
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

    /// <summary>Preferred model tag: fast-mode override, settings, tester field, then inspector default.</summary>
    public string GetPreferredModelName()
    {
        if (GameSettings.LlmFastMode && !string.IsNullOrWhiteSpace(fastPreferredModel))
            return fastPreferredModel.Trim();

        string fromSettings = GameSettings.LlmModel;
        if (!string.IsNullOrWhiteSpace(fromSettings))
            return fromSettings.Trim();

        if (modelInputField != null && !string.IsNullOrWhiteSpace(modelInputField.text))
            return modelInputField.text.Trim();

        if (!string.IsNullOrWhiteSpace(defaultModel))
            return defaultModel.Trim();

        return "gemma3:4b";
    }

    public int GetEffectiveNpcMaxTokens() =>
        GameSettings.LlmFastMode ? fastNpcMaxTokens : defaultNpcMaxTokens;

    public int GetEffectiveNpcChatMaxTokens() =>
        GameSettings.LlmFastMode ? fastNpcChatMaxTokens : defaultNpcChatMaxTokens;

    public int GetEffectiveStreamMaxTokens() =>
        GameSettings.LlmFastMode ? fastStreamMaxTokens : defaultStreamMaxTokens;

    public int GetEffectiveNonStreamMaxTokens() =>
        GameSettings.LlmFastMode ? fastNonStreamMaxTokens : defaultNonStreamMaxTokens;

    public int GetEffectiveFlavorMaxTokens() =>
        GameSettings.LlmFastMode ? fastFlavorMaxTokens : defaultFlavorMaxTokens;

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
        if (string.IsNullOrEmpty(match)
            && GameSettings.LlmFastMode
            && !string.IsNullOrWhiteSpace(fastPreferredModel)
            && string.Equals(preferred, fastPreferredModel.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            match = FindBestMatchingTag(installed, GameSettings.LlmModel);
        }

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
        private readonly object _pendingLock = new object();
        private readonly List<string> _pendingDeltas = new List<string>(8);
        private readonly List<string> _pendingRawLines = new List<string>(8);
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
            string delta = ExtractStreamDelta(line);
            // Always record the raw line so we can inspect malformed NDJSON on the main thread.
            lock (_pendingLock)
            {
                _pendingRawLines.Add(line);
            }

            if (string.IsNullOrEmpty(delta))
                return;

            _fullResponse.Append(delta);
            // Queue deltas for main-thread draining. DownloadHandlerScript.ReceiveData
            // may be invoked on a network thread, so avoid calling back into Unity API here.
            lock (_pendingLock)
            {
                _pendingDeltas.Add(delta);
            }
        }

        /// <summary>
        /// Drain pending deltas into a new list. Call from the Unity main thread.
        /// </summary>
        public List<string> DrainPendingDeltas()
        {
            lock (_pendingLock)
            {
                if (_pendingDeltas.Count == 0)
                    return null;
                var copy = new List<string>(_pendingDeltas);
                _pendingDeltas.Clear();
                return copy;
            }
        }

        public List<string> DrainPendingRawLines()
        {
            lock (_pendingLock)
            {
                if (_pendingRawLines.Count == 0)
                    return null;
                var copy = new List<string>(_pendingRawLines);
                _pendingRawLines.Clear();
                return copy;
            }
        }

        private static string ExtractStreamDelta(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            string response = ExtractJsonStringField(line, "response");
            if (!string.IsNullOrEmpty(response))
                return response;

            // qwen3 often leaves "response" empty and streams tokens in "thinking".
            return ExtractJsonStringField(line, "thinking");
        }

        private static string ExtractJsonStringField(string line, string key)
        {
            try
            {
                if (key == "response")
                {
                    OllamaStreamChunk chunk = JsonUtility.FromJson<OllamaStreamChunk>(line);
                    if (chunk != null && !string.IsNullOrEmpty(chunk.response))
                        return chunk.response;
                }
                else
                {
                    OllamaStreamChunk chunk = JsonUtility.FromJson<OllamaStreamChunk>(line);
                    if (chunk != null && !string.IsNullOrEmpty(chunk.thinking))
                        return chunk.thinking;
                }
            }
            catch (Exception)
            {
                // Fall through to manual JSON string extraction.
            }

            return TryExtractJsonStringValue(line, key);
        }
    }

    private static string TryExtractJsonStringValue(string json, string key)
    {
        string keyToken = "\"" + key + "\"";
        int keyIndex = json.IndexOf(keyToken, StringComparison.Ordinal);
        if (keyIndex < 0)
            return null;

        int colon = json.IndexOf(':', keyIndex + keyToken.Length);
        if (colon < 0)
            return null;

        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i]))
            i++;

        if (i >= json.Length || json[i] != '"')
            return null;

        int valueStart = i + 1;
        var sb = new StringBuilder();
        for (int pos = valueStart; pos < json.Length; pos++)
        {
            char c = json[pos];
            if (c == '\\' && pos + 1 < json.Length)
            {
                char next = json[pos + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    default: sb.Append(next); break;
                }

                pos++;
                continue;
            }

            if (c == '"')
                break;

            sb.Append(c);
        }

        return sb.ToString();
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
    /// <summary>
    /// DatingSim-style: reads prompt/model from TMP fields, renders Cap template, calls <c>/api/generate</c>.
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
        string systemPrompt = LoadAndRenderCharacterTemplate(prompt);
        string fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? prompt
            : systemPrompt + "\n\nPlayer: " + prompt;

        bool done = false;
        string error = null;
        RequestGeneration(model, fullPrompt,
            onSuccess: text =>
            {
                SetOutputText(text);
                done = true;
            },
            onError: err =>
            {
                error = err;
                SetOutputText(err);
                done = true;
            },
            saveToDialogueJson: true,
            updateResponseUiField: true,
            maxPredictTokens: GetEffectiveNonStreamMaxTokens(),
            disableThinking: true,
            extractNpcDialogue: true,
            npcDialogueName: "Cap");

        while (!done)
            yield return null;

        if (!string.IsNullOrEmpty(error) && !IsSupersededAbortError(error))
            Debug.LogError(error);
    }

    /// <summary>
    /// DatingSim-style: load <c>cap_personality.j2</c> + <c>cap_context.json</c> from <c>Assets/Prompts</c>.
    /// </summary>
    public string LoadAndRenderCharacterTemplate(string playerMessage = null) =>
        LoadAndRenderCapTemplate(playerMessage);

    /// <summary>Alias kept for existing gameplay / test UI callers.</summary>
    public string LoadAndRenderCapTemplate(string playerMessage = null)
    {
        try
        {
            string templatePath = Path.Combine(Application.dataPath, "Prompts", templateFileName);
            string contextPath = Path.Combine(Application.dataPath, "Prompts", characterContextFileName);

            if (!File.Exists(templatePath))
            {
                Debug.LogWarning($"Template file not found: {templatePath}");
                return RenderCapTemplateFromResolvedPaths(playerMessage);
            }

            if (!File.Exists(contextPath))
            {
                Debug.LogWarning($"Character context file not found: {contextPath}");
                return RenderCapTemplateFromResolvedPaths(playerMessage);
            }

            string templateContent = File.ReadAllText(templatePath, Encoding.UTF8);
            string jsonContent = File.ReadAllText(contextPath, Encoding.UTF8);

            CharacterPersonalityTemplateManager.CapContextData contextData =
                JsonUtility.FromJson<CharacterPersonalityTemplateManager.CapContextData>(jsonContent);
            if (contextData == null)
                contextData = new CharacterPersonalityTemplateManager.CapContextData();

            if (!string.IsNullOrWhiteSpace(playerMessage))
            {
                contextData.player_question = playerMessage.Trim();
                contextData.output_rules =
                    "Never repeat the player's question. Answer in Cap's own words in 2-3 short cosy sentences. Dialogue only.";
            }

            return CharacterPersonalityTemplateManager.RenderTemplate(templateContent, contextData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading and rendering Cap template: {ex.Message}");
            return string.Empty;
        }
    }

    private static string RenderCapTemplateFromResolvedPaths(string playerMessage)
    {
        if (!CharacterPersonalityTemplateManager.TryLoadDefaultContext(
                out CharacterPersonalityTemplateManager.CapContextData context))
        {
            context = new CharacterPersonalityTemplateManager.CapContextData();
        }

        if (!string.IsNullOrWhiteSpace(playerMessage))
        {
            context.player_question = playerMessage.Trim();
            context.output_rules =
                "Never repeat the player's question. Answer in Cap's own words in 2-3 short cosy sentences. Dialogue only.";
        }

        return CharacterPersonalityTemplateManager.RenderCapPersonality(context);
    }

    /// <summary>
    /// Sends a prompt to local Ollama for gameplay (NPC dialogue, etc.). Does not require TMP input fields.
    /// </summary>
    /// <param name="saveToDialogueJson">When true, appends to the same JSON log used by the test UI.</param>
    /// <param name="updateResponseUiField">When true, writes the model reply to <see cref="responseOutputField"/>.</param>
    public void RequestGeneration(string model, string prompt, Action<string> onSuccess, Action<string> onError,
        bool saveToDialogueJson = true, bool updateResponseUiField = true, int maxPredictTokens = 0,
        bool disableThinking = false, bool extractNpcDialogue = false, string npcDialogueName = null,
        bool jsonResponse = false)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : GetEffectiveNonStreamMaxTokens();
        EnqueueOllamaRequest(GenerateCoroutine(model, prompt, saveToDialogueJson, updateResponseUiField, onSuccess, onError,
            limit, disableThinking, extractNpcDialogue, npcDialogueName, jsonResponse));
    }

    /// <summary>
    /// DatingSim-style: builds one generate prompt from chat messages (system + user).
    /// </summary>
    public void RequestChat(string model, IReadOnlyList<(string role, string content)> messages, Action<string> onSuccess,
        Action<string> onError, bool saveToDialogueJson = true, bool updateResponseUiField = false, int maxPredictTokens = 0,
        bool disableThinking = true, bool extractNpcDialogue = false, string npcDialogueName = null)
    {
        if (messages == null || messages.Count == 0)
        {
            onError?.Invoke("Chat message list is empty.");
            return;
        }

        string systemPrompt = null;
        string userPrompt = null;
        for (int i = 0; i < messages.Count; i++)
        {
            string role = messages[i].role ?? string.Empty;
            string content = messages[i].content ?? string.Empty;
            if (role.Equals("system", StringComparison.OrdinalIgnoreCase))
                systemPrompt = content;
            else if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
                userPrompt = content;
        }

        string fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? userPrompt
            : string.IsNullOrWhiteSpace(userPrompt)
                ? systemPrompt
                : systemPrompt + "\n\nPlayer: " + userPrompt;

        if (string.IsNullOrWhiteSpace(fullPrompt))
        {
            onError?.Invoke("Chat messages had no usable content.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : GetEffectiveNpcChatMaxTokens();
        EnqueueOllamaRequest(GenerateCoroutine(model, fullPrompt, saveToDialogueJson, updateResponseUiField, onSuccess, onError,
            limit, disableThinking, extractNpcDialogue, npcDialogueName, jsonResponse: false), highPriority: true);
    }

    /// <summary>
    /// DatingSim-style chat prompt with streamed NDJSON tokens (<c>/api/generate</c>, <c>stream: true</c>).
    /// </summary>
    public void RequestChatStreaming(string model, IReadOnlyList<(string role, string content)> messages,
        Action<string> onDelta, Action<string> onComplete, Action<string> onError,
        bool saveToDialogueJson = true, int maxPredictTokens = 0, bool disableThinking = true,
        bool extractNpcDialogue = false, string npcDialogueName = null)
    {
        if (messages == null || messages.Count == 0)
        {
            onError?.Invoke("Chat message list is empty.");
            return;
        }

        string systemPrompt = null;
        string userPrompt = null;
        for (int i = 0; i < messages.Count; i++)
        {
            string role = messages[i].role ?? string.Empty;
            string content = messages[i].content ?? string.Empty;
            if (role.Equals("system", StringComparison.OrdinalIgnoreCase))
                systemPrompt = content;
            else if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
                userPrompt = content;
        }

        string fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? userPrompt
            : string.IsNullOrWhiteSpace(userPrompt)
                ? systemPrompt
                : systemPrompt + "\n\nPlayer: " + userPrompt;

        if (string.IsNullOrWhiteSpace(fullPrompt))
        {
            onError?.Invoke("Chat messages had no usable content.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : GetEffectiveNpcChatMaxTokens();
        RequestGenerationStreaming(model, fullPrompt, onDelta, onComplete, onError, saveToDialogueJson, limit,
            updateResponseUiField: false, disableThinking, extractNpcDialogue, npcDialogueName, highPriority: true);
    }

    /// <summary>
    /// Loads the preferred model into memory with a tiny completion (for Main Menu cold-start).
    /// </summary>
    public IEnumerator WarmupModelCoroutine(Action onSuccess = null, Action<string> onFail = null)
    {
        if (!GameSettings.LlmEnabled)
        {
            onSuccess?.Invoke();
            yield break;
        }

        string model = null;
        string resolveError = null;
        yield return ResolveModelTagCoroutine(GetPreferredModelName(),
            resolved => model = resolved,
            err => resolveError = err);

        if (string.IsNullOrEmpty(model))
        {
            onFail?.Invoke(resolveError ?? "Model unavailable for warm-up.");
            yield break;
        }

        bool done = false;
        string error = null;
        RequestGeneration(model, "Reply with exactly: ok",
            onSuccess: _ => done = true,
            onError: e => { error = e; done = true; },
            saveToDialogueJson: false,
            updateResponseUiField: false,
            maxPredictTokens: 8,
            disableThinking: true);

        while (!done)
            yield return null;

        if (!string.IsNullOrEmpty(error))
        {
            if (!IsSupersededAbortError(error))
                onFail?.Invoke(error);
            yield break;
        }

        onSuccess?.Invoke();
    }

    /// <summary>
    /// Streams tokens from Ollama (<c>stream: true</c>). <paramref name="onDelta"/> receives each decoded <c>response</c> fragment;
    /// <paramref name="onComplete"/> receives the full sanitized text (same as saved to JSON when enabled).
    /// </summary>
    public void RequestGenerationStreaming(string model, string prompt, Action<string> onDelta, Action<string> onComplete,
        Action<string> onError, bool saveToDialogueJson = true, int maxPredictTokens = 0,
        bool updateResponseUiField = false, bool disableThinking = true, bool extractNpcDialogue = false,
        string npcDialogueName = null, bool highPriority = false)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Prompt is empty.");
            return;
        }

        int limit = maxPredictTokens > 0 ? maxPredictTokens : GetEffectiveStreamMaxTokens();
        Action<string> wrappedComplete = (response) =>
        {
            if (updateResponseUiField)
                SetOutputText(response);
            onComplete?.Invoke(response);
        };
        Action<string> wrappedError = (error) =>
        {
            if (updateResponseUiField)
                SetOutputText(error);
            onError?.Invoke(error);
        };
        EnqueueOllamaRequest(GenerateStreamingCoroutine(model, prompt, onDelta, wrappedComplete, wrappedError,
            saveToDialogueJson, limit, disableThinking, extractNpcDialogue, npcDialogueName), highPriority);
    }

    private void EnqueueOllamaRequest(IEnumerator routine, bool highPriority = false)
    {
        if (routine == null)
            return;

        if (highPriority && _requestQueue.Count > 0)
        {
            var pending = new Queue<IEnumerator>(_requestQueue);
            _requestQueue.Clear();
            _requestQueue.Enqueue(routine);
            while (pending.Count > 0)
                _requestQueue.Enqueue(pending.Dequeue());
        }
        else
        {
            _requestQueue.Enqueue(routine);
        }

        if (_requestQueueRunner == null)
            _requestQueueRunner = StartCoroutine(RunRequestQueue());
    }

    private IEnumerator RunRequestQueue()
    {
        while (_requestQueue.Count > 0)
        {
            IEnumerator next = _requestQueue.Dequeue();
            if (next != null)
                yield return next;
        }

        _requestQueueRunner = null;
    }

    /// <summary>Aborts the in-flight HTTP request (e.g. dialogue closed or Ask Cap preempts voice).</summary>
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
        Action<string> onError, bool saveToDialogueJson, int maxPredictTokens, bool disableThinking, bool extractNpcDialogue,
        string npcDialogueName)
    {
        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = BuildGenerateJsonBody(model, prompt, stream: true, maxPredictTokens, disableThinking);

        var streamHandler = new OllamaNdjsonStreamHandler(onDelta);
        var request = new UnityWebRequest(url, "POST");
        _abortableRequest = request;
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = streamHandler;
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.Max(5, requestTimeoutSeconds);

        var operation = request.SendWebRequest();

        try
        {
            // While the request is in-flight, drain any pending deltas produced by the
            // DownloadHandlerScript on the network thread and invoke the UI callback
            // on the main thread so it's safe to update Unity objects.
            while (!operation.isDone)
            {
                var pending = streamHandler.DrainPendingDeltas();
                if (pending != null)
                {
                    for (int i = 0; i < pending.Count; i++)
                        onDelta?.Invoke(pending[i]);
                }

                yield return null;
            }

            // Flush any partial trailing line and process remaining deltas.
            streamHandler.FlushPartialLine();

            var remaining = streamHandler.DrainPendingDeltas();
            if (remaining != null)
            {
                for (int i = 0; i < remaining.Count; i++)
                    onDelta?.Invoke(remaining[i]);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage =
                    $"Ollama stream failed: {request.error} (HTTP {request.responseCode}){BuildOllamaFailureDetail(request)}";
                LogOllamaRequestFailure(errorMessage, request.responseCode);
                onError?.Invoke(errorMessage);
                yield break;
            }

            string responseText = SanitizeModelOutput(streamHandler.GetFullResponse());
            if (extractNpcDialogue)
                responseText = ExtractNpcSpokenDialogue(responseText, npcDialogueName);

            if (saveToDialogueJson && !string.IsNullOrWhiteSpace(responseText))
            {
                if (SaveDialogueJson(model, prompt, responseText))
                    Debug.Log($"Ollama stream complete; saved to {GetDialogueJsonPath()}.");
                else
                    Debug.LogWarning("Ollama stream complete, but dialogue JSON could not be written.");
            }
            else if (saveToDialogueJson && string.IsNullOrWhiteSpace(responseText))
            {
                Debug.LogWarning("Ollama stream finished with no dialogue text in the response field. " +
                                 "If using a reasoning model, ensure think=false is supported or raise num_predict.");
            }

            Debug.Log("Ollama stream complete.");
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
        Action<string> onSuccess, Action<string> onError, int maxPredictTokens, bool disableThinking = false,
        bool extractNpcDialogue = false, string npcDialogueName = null, bool jsonResponse = false)
    {
        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/generate";
        string jsonBody = BuildGenerateJsonBody(model, prompt, stream: false, maxPredictTokens, disableThinking, jsonResponse);

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
                if (extractNpcDialogue)
                    responseText = ExtractNpcSpokenDialogue(responseText, npcDialogueName);

                if (updateResponseUiField)
                    SetOutputText(responseText);

                if (saveToDialogueJson && !string.IsNullOrWhiteSpace(responseText))
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

    private IEnumerator ChatCoroutine(string model, IReadOnlyList<(string role, string content)> messages,
        bool saveToDialogueJson, bool updateResponseUiField, Action<string> onSuccess, Action<string> onError,
        int maxPredictTokens, bool disableThinking = true, bool extractNpcDialogue = false, string npcDialogueName = null)
    {
        string protocol = ShouldUseHttps() ? "https" : "http";
        string url = $"{protocol}://{ollamaHost}:{ollamaPort}/api/chat";
        string jsonBody = BuildChatJsonBody(model, messages, stream: false, maxPredictTokens, disableThinking);

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
                string responseText = SanitizeModelOutput(ExtractChatResponseText(request.downloadHandler.text));
                if (extractNpcDialogue)
                    responseText = ExtractNpcSpokenDialogue(responseText, npcDialogueName);

                if (updateResponseUiField)
                    SetOutputText(responseText);

                if (saveToDialogueJson && !string.IsNullOrWhiteSpace(responseText))
                {
                    string promptSummary = BuildChatLogPrompt(messages);
                    if (SaveDialogueJson(model, promptSummary, responseText))
                        Debug.Log($"Ollama chat response received and saved to {GetDialogueJsonPath()}.");
                    else
                        Debug.LogWarning("Ollama chat response received, but the dialogue JSON file could not be written.");
                }

                onSuccess?.Invoke(responseText);
            }
            else
            {
                string errorMessage =
                    $"Ollama chat request failed: {request.error} (HTTP {request.responseCode}){BuildOllamaFailureDetail(request)}";
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

    public static string SanitizeModelOutput(string text) => SanitizeForDisplay(text, stripIncompleteThinking: false);

    /// <summary>
    /// Drops qwen-style planning lines (quest labels, "we are writing as…") and keeps spoken NPC dialogue.
    /// </summary>
    public static string ExtractNpcSpokenDialogue(string text, string npcName = null, string excludeEchoOf = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string original = SanitizeModelOutput(text).Trim();
        if (LooksLikeBulkMetaPlanning(original))
        {
            string salvage = ExtractLabeledExampleDialogue(original, excludeEchoOf);
            if (!string.IsNullOrWhiteSpace(salvage))
                return salvage;

            salvage = ExtractQuotedDialogue(original, excludeEchoOf);
            if (!string.IsNullOrWhiteSpace(salvage))
                return salvage;

            return string.Empty;
        }

        string fromQuote = ExtractPromptContinuationSpeech(original);
        if (!string.IsNullOrWhiteSpace(fromQuote) && !IsPlayerQuestionEcho(fromQuote, excludeEchoOf))
            return fromQuote;

        int anchor = FindNpcDialogueStart(original, npcName);
        if (anchor >= 0)
        {
            string afterAnchor = CleanupSpokenLine(original.Substring(anchor));
            fromQuote = ExtractPromptContinuationSpeech(afterAnchor) ?? afterAnchor;
            if (!string.IsNullOrWhiteSpace(fromQuote) && !IsNpcMetaPlanningLine(fromQuote) &&
                !IsPlayerQuestionEcho(fromQuote, excludeEchoOf))
                return fromQuote;
        }

        string quoted = ExtractQuotedDialogue(original, excludeEchoOf);
        if (!string.IsNullOrWhiteSpace(quoted))
            return quoted;

        string labeled = ExtractLabeledExampleDialogue(original, excludeEchoOf);
        if (!string.IsNullOrWhiteSpace(labeled))
            return labeled;

        var lines = original.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = CleanupSpokenLine(lines[i]);
            if (line.Length == 0 || IsNpcMetaPlanningLine(line))
                continue;
            kept.Add(line);
        }

        if (kept.Count > 0)
        {
            string joined = string.Join(" ", kept);
            if (!IsNpcMetaPlanningLine(joined) && !LooksLikeBulkMetaPlanning(joined) &&
                !IsPlayerQuestionEcho(joined, excludeEchoOf))
                return joined;
        }

        string sentences = ExtractInCharacterSentences(original);
        if (!string.IsNullOrWhiteSpace(sentences) && !LooksLikeBulkMetaPlanning(sentences) &&
            !IsPlayerQuestionEcho(sentences, excludeEchoOf))
            return sentences;

        return string.Empty;
    }

    /// <summary>One-line dungeon flavor for HUD toasts; rejects qwen-style planning blobs.</summary>
    public static string ExtractFlavorLine(string raw, int maxWords = 32)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string text = SanitizeModelOutput(raw).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (LooksLikeNpcMetaPlanning(text) || LooksLikeBulkMetaPlanning(text))
        {
            string labeled = ExtractLabeledExampleDialogue(text);
            if (!string.IsNullOrWhiteSpace(labeled) && CountWords(labeled) <= maxWords)
                return labeled;

            string quoted = ExtractQuotedDialogue(text, null);
            if (!string.IsNullOrWhiteSpace(quoted) && CountWords(quoted) <= maxWords &&
                !LooksLikeNpcMetaPlanning(quoted))
                return quoted;

            string you = TryExtractYouSentence(text, maxWords);
            if (!string.IsNullOrWhiteSpace(you))
                return you;

            return string.Empty;
        }

        if (CountWords(text) > maxWords)
        {
            string first = TakeFirstSentenceUnderWordLimit(text, maxWords);
            if (!string.IsNullOrWhiteSpace(first) && !LooksLikeNpcMetaPlanning(first))
                return first;
            return string.Empty;
        }

        return text;
    }

    /// <summary>True when the model echoed the player's question instead of answering.</summary>
    public static bool IsPlayerQuestionEcho(string candidate, string playerQuestion)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(playerQuestion))
            return false;

        return string.Equals(NormalizeEchoText(candidate), NormalizeEchoText(playerQuestion), StringComparison.Ordinal);
    }

    private static string NormalizeEchoText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            if (c is '"' or '\'' or '?' or '!' or '.')
                continue;

            sb.Append(c);
            lastWasSpace = false;
        }

        return sb.ToString().Trim();
    }

    /// <summary>True when text looks like model planning rather than spoken NPC lines.</summary>
    public static bool IsNpcMetaPlanningLine(string line) =>
        LooksLikeNpcMetaPlanning(line) || LooksLikeBulkMetaPlanning(line);

    /// <summary>Prompts end with <c>Name: "</c>; model output should be dialogue inside the quote.</summary>
    private static string ExtractPromptContinuationSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Trim();
        if (text.StartsWith("\"", StringComparison.Ordinal))
        {
            int end = text.IndexOf('"', 1);
            if (end > 1)
            {
                string inner = CleanupSpokenLine(text.Substring(1, end - 1));
                if (!IsNpcMetaPlanningLine(inner))
                    return inner;
            }
        }

        int close = text.IndexOf('"');
        if (close > 0)
        {
            string beforeClose = CleanupSpokenLine(text.Substring(0, close));
            if (beforeClose.Length >= 12 && !IsNpcMetaPlanningLine(beforeClose))
                return beforeClose;
        }

        if (!text.Contains('"') && text.Length >= 8 && !IsNpcMetaPlanningLine(text))
            return CleanupSpokenLine(text);

        return string.Empty;
    }

    private static string CleanupSpokenLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Trim().Trim('"').Trim();
    }

    /// <summary>Pulls quoted speech segments that are not planning/meta or player echoes.</summary>
    private static string ExtractQuotedDialogue(string text, string excludeEchoOf = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string best = string.Empty;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '"')
            {
                i++;
                continue;
            }

            int start = i + 1;
            int end = start;
            while (end < text.Length && text[end] != '"')
                end++;

            if (end > start)
            {
                string segment = CleanupSpokenLine(text.Substring(start, end - start));
                if (segment.Length >= 8 && !IsNpcMetaPlanningLine(segment) &&
                    !IsPlayerQuestionEcho(segment, excludeEchoOf))
                    best = segment;
            }

            i = end + 1;
        }

        return best;
    }

    /// <summary>Picks dialogue after qwen-style labels like "Possible response:".</summary>
    private static string ExtractLabeledExampleDialogue(string text, string excludeEchoOf = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string[] labels =
        {
            "possible response:", "example response:", "example of what we want:",
            "let's think of a response that fits:", "response that fits:",
            "what to say:", "what cap might say:", "cap might say:", "narrator line:"
        };

        string lower = text.ToLowerInvariant();
        string best = string.Empty;
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = lower.IndexOf(labels[i], StringComparison.Ordinal);
            if (idx < 0)
                continue;

            string tail = text.Substring(idx + labels[i].Length);
            string quoted = ExtractQuotedDialogue(tail, excludeEchoOf);
            if (!string.IsNullOrWhiteSpace(quoted))
                best = quoted;
            else
            {
                string[] parts = tail.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string line = CleanupSpokenLine(parts[0]);
                    if (line.Length >= 8 && !IsNpcMetaPlanningLine(line) && !IsPlayerQuestionEcho(line, excludeEchoOf))
                        best = line;
                }
            }
        }

        return best;
    }

    private static string ExtractInCharacterSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var kept = new List<string>(4);
        var chunk = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            chunk.Append(c);
            if (c != '.' && c != '!' && c != '?')
                continue;

            string sentence = CleanupSpokenLine(chunk.ToString());
            chunk.Clear();
            if (sentence.Length >= 8 && !IsNpcMetaPlanningLine(sentence))
                kept.Add(sentence);
        }

        string tail = CleanupSpokenLine(chunk.ToString());
        if (tail.Length >= 8 && !IsNpcMetaPlanningLine(tail))
            kept.Add(tail);

        if (kept.Count == 0)
            return string.Empty;

        return string.Join(" ", kept);
    }

    private static int FindNpcDialogueStart(string text, string npcName)
    {
        var starters = new List<string>(4) { "Cap says:", "Cap said:" };
        if (!string.IsNullOrWhiteSpace(npcName))
        {
            string name = npcName.Trim();
            starters.Add(name + " says:");
            starters.Add(name + " said:");
            starters.Add(name + ":");
        }
        else
        {
            starters.Add("Cap:");
        }

        int best = -1;
        for (int s = 0; s < starters.Count; s++)
        {
            string starter = starters[s];
            int searchFrom = text.Length;
            while (searchFrom > 0)
            {
                int idx = text.LastIndexOf(starter, searchFrom - 1, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                int after = idx + starter.Length;
                if (starter.EndsWith(":", StringComparison.Ordinal) && after < text.Length && text[after] == '\'')
                {
                    searchFrom = idx;
                    continue;
                }

                if (idx > best)
                    best = after;
                break;
            }
        }

        return best;
    }

    private static bool LooksLikeNpcMetaPlanning(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        string lower = line.ToLowerInvariant();
        string[] markers =
        {
            "we are writing as", "we are building on", "we are cap,", "we are cap ", "i am writing as",
            "recent conversation", "the player asked", "the player just asked", "the player is asking",
            "player question:", "the user wants", "the user asked", "what to say", "i need to write",
            "let me think", "dungeon narrator", "dungeon narrator for", "they've described",
            "quest title:", "authoritative quest", "authoritative briefing", "briefing:",
            "current state:", "current situation involves", "constraints:", "key points", "approach:",
            "game facts", "cap's role", "npc in a cozy", "npc in a cosy",
            "quest state summary", "spoken dialogue only", "write 2–6", "write 2-6",
            "write 2 to 6", "do not repeat the briefing", "do not repeat these labels",
            "the adventurer is considering", "the adventurer accepted your quest",
            "inventory: empty", "inventory: unknown", "no active quests",
            "no markdown", "no bullet", "no json", "no stage directions", "no planning",
            "first-person dungeon crawler", "dungeon crawler game", "cozy first-person dungeon",
            "build on it; do not repeat", "answer their question in character",
            "reply with only what", "output 1-3 short sentences of dialogue"
        };

        for (int i = 0; i < markers.Length; i++)
        {
            if (lower.Contains(markers[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool LooksLikeBulkMetaPlanning(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 60)
            return false;

        string lower = text.ToLowerInvariant();
        string[] strongMarkers =
        {
            "the user wants me to", "the user wants", "the player is asking", "what to say:",
            "i need to write exactly", "i need to write", "let me think", "hmm, the user",
            "focusing only on sensory", "cosy fantasy first-person", "cozy fantasy first-person",
            "specific scenario", "chain of thought", "step by step"
        };

        for (int i = 0; i < strongMarkers.Length; i++)
        {
            if (lower.Contains(strongMarkers[i], StringComparison.Ordinal))
                return true;
        }

        int bulletLines = 0;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* ", StringComparison.Ordinal))
                bulletLines++;
        }

        if (bulletLines >= 2)
            return true;

        return text.Length > 140 && lines.Length >= 3;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        int count = 0;
        bool inWord = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                inWord = false;
                continue;
            }

            if (!inWord)
            {
                count++;
                inWord = true;
            }
        }

        return count;
    }

    private static string TakeFirstSentenceUnderWordLimit(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chunk = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            chunk.Append(c);
            if (c != '.' && c != '!' && c != '?')
                continue;

            string sentence = CleanupSpokenLine(chunk.ToString());
            chunk.Clear();
            if (sentence.Length >= 8 && CountWords(sentence) <= maxWords)
                return sentence;
        }

        string tail = CleanupSpokenLine(chunk.ToString());
        return CountWords(tail) <= maxWords ? tail : string.Empty;
    }

    private static string TryExtractYouSentence(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chunk = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            chunk.Append(c);
            if (c != '.' && c != '!' && c != '?')
                continue;

            string sentence = CleanupSpokenLine(chunk.ToString());
            chunk.Clear();
            if (sentence.Length < 8 || CountWords(sentence) > maxWords)
                continue;
            if (!sentence.StartsWith("You ", StringComparison.OrdinalIgnoreCase))
                continue;
            if (LooksLikeNpcMetaPlanning(sentence) || LooksLikeBulkMetaPlanning(sentence))
                continue;

            return sentence;
        }

        string tail = CleanupSpokenLine(chunk.ToString());
        if (tail.Length >= 8 && CountWords(tail) <= maxWords &&
            tail.StartsWith("You ", StringComparison.OrdinalIgnoreCase) &&
            !LooksLikeNpcMetaPlanning(tail) && !LooksLikeBulkMetaPlanning(tail))
            return tail;

        return string.Empty;
    }

    /// <summary>Strips model “thinking” wrappers for UI display (including partial streams).</summary>
    public static string SanitizeForDisplay(string text, bool stripIncompleteThinking = true)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        const string thinkOpen = "<" + "think" + ">";
        const string thinkClose = "<" + "/" + "think" + ">";
        text = StripTaggedBlocks(text, thinkOpen, thinkClose);
        text = StripTaggedBlocks(text, "<think>", "</think>");

        if (stripIncompleteThinking)
        {
            int thinkStart = text.IndexOf(thinkOpen, StringComparison.Ordinal);
            if (thinkStart < 0)
                thinkStart = text.IndexOf("<think>", StringComparison.Ordinal);
            if (thinkStart >= 0)
                text = text.Substring(0, thinkStart);
        }

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
            return false;

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
                return parsedResponse.response;
        }
        catch (Exception)
        {
            // Fall back to the raw response below.
        }

        string thinking = TryExtractJsonStringValue(rawResponse, "thinking");
        if (!string.IsNullOrWhiteSpace(thinking))
            return thinking;

        return rawResponse;
    }

    private static string ExtractChatResponseText(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return string.Empty;

        try
        {
            OllamaChatResponse parsed = JsonUtility.FromJson<OllamaChatResponse>(rawResponse);
            if (parsed?.message != null)
            {
                if (!string.IsNullOrWhiteSpace(parsed.message.content))
                    return parsed.message.content;
                if (!string.IsNullOrWhiteSpace(parsed.message.thinking))
                    return parsed.message.thinking;
            }
        }
        catch (Exception)
        {
            // Fall back to direct JSON field extraction.
        }

        string content = TryExtractNestedJsonStringValue(rawResponse, "message", "content");
        if (!string.IsNullOrWhiteSpace(content))
            return content;

        string thinking = TryExtractNestedJsonStringValue(rawResponse, "message", "thinking");
        if (!string.IsNullOrWhiteSpace(thinking))
            return thinking;

        return ExtractResponseText(rawResponse);
    }

    private static string TryExtractNestedJsonStringValue(string json, string objectKey, string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        string objectToken = "\"" + objectKey + "\"";
        int objectIndex = json.IndexOf(objectToken, StringComparison.Ordinal);
        if (objectIndex < 0)
            return null;

        string fieldToken = "\"" + fieldKey + "\"";
        int fieldIndex = json.IndexOf(fieldToken, objectIndex, StringComparison.Ordinal);
        if (fieldIndex < 0)
            return null;

        return TryExtractJsonStringValue(json, fieldKey);
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
        if (IsSupersededAbort(responseCode, message))
            return;

        if (responseCode == 404)
            Debug.LogWarning(message);
        else
            Debug.LogError(message);
    }

    /// <summary>True when a newer Ollama call intentionally replaced this request (HTTP 0 abort).</summary>
    public static bool IsSupersededAbortError(string errorMessage) =>
        IsSupersededAbort(0, errorMessage);

    private static bool IsSupersededAbort(long responseCode, string message)
    {
        if (responseCode != 0 || string.IsNullOrEmpty(message))
            return false;

        return message.IndexOf("abort", StringComparison.OrdinalIgnoreCase) >= 0;
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

    private static string BuildGenerateJsonBody(string model, string prompt, bool stream, int maxPredictTokens,
        bool disableThinking = false, bool jsonResponse = false)
    {
        int n = Mathf.Clamp(maxPredictTokens, 8, 8192);
        string think = disableThinking ? ",\"think\":false" : string.Empty;
        string format = jsonResponse ? ",\"format\":\"json\"" : string.Empty;
        return "{\"model\":\"" + EscapeJson(model) + "\",\"prompt\":\"" + EscapeJson(prompt) + "\",\"stream\":" +
               (stream ? "true" : "false") + think + format + ",\"options\":{\"num_predict\":" + n + "}}";
    }

    private static string BuildChatJsonBody(string model, IReadOnlyList<(string role, string content)> messages,
        bool stream, int maxPredictTokens, bool disableThinking = true)
    {
        int n = Mathf.Clamp(maxPredictTokens, 8, 8192);
        string think = disableThinking ? ",\"think\":false" : string.Empty;

        var sb = new StringBuilder(512);
        sb.Append("{\"model\":\"").Append(EscapeJson(model)).Append("\",\"messages\":[");
        for (int i = 0; i < messages.Count; i++)
        {
            string role = string.IsNullOrWhiteSpace(messages[i].role) ? "user" : messages[i].role.Trim().ToLowerInvariant();
            string content = messages[i].content ?? string.Empty;
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"role\":\"").Append(EscapeJson(role)).Append("\",\"content\":\"").Append(EscapeJson(content))
                .Append("\"}");
        }

        sb.Append("],\"stream\":").Append(stream ? "true" : "false")
            .Append(think)
            .Append(",\"options\":{\"num_predict\":").Append(n).Append("}}");
        return sb.ToString();
    }

    private static string BuildChatLogPrompt(IReadOnlyList<(string role, string content)> messages)
    {
        if (messages == null || messages.Count == 0)
            return string.Empty;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(messages[i].role, "user", StringComparison.OrdinalIgnoreCase))
                return messages[i].content ?? string.Empty;
        }

        return messages[messages.Count - 1].content ?? string.Empty;
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
