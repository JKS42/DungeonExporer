using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DungeonExporer.AI;
using DungeonExporer.Gameplay;
using DungeonExporer.Player;
using TMPro;
using UnityEngine;
using DungeonExporer.Settings;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// In-world dialogue: authored quest text plus Ollama lines with a typewriter-style reveal on Ask Cap.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class DialoguePanelController : MonoBehaviour
    {
        public static DialoguePanelController Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [SerializeField] private OllamaHandler _ollama;
        [SerializeField] private InputActionAsset _inputActions;
        [Tooltip("How long to wait for a proximity prefetch before starting a new Ollama call.")]
        [SerializeField] private float _prefetchWaitSeconds = 6f;
        [Tooltip("Ask Cap: characters revealed per second after the stream finishes (filtered line).")]
        [SerializeField] private float _askCapTypewriterCharsPerSecond = 52f;

        private const int DialogueCanvasSortOrder = 300;

        private GameObject _canvasRoot;
        private GraphicRaycaster _dialogueRaycaster;
        private GraphicRaycaster _sceneTesterRaycaster;
        private GameObject _rootPanel;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _staticBodyText;
        private TextMeshProUGUI _llmBodyText;
        private ScrollRect _llmScrollRect;
        private RectTransform _llmContentRect;
        private TextMeshProUGUI _statusText;
        private Button _hearButton;
        private Button _askButton;
        private TMP_InputField _playerInput;
        private Button _acceptButton;
        private Button _closeButton;

        private string _displayName = string.Empty;
        private string _questId = string.Empty;
        private string _npcConversationId = "npc";
        private bool _voiceBusy;
        private bool _askBusy;
        private int _dialogueGeneration;
        private Coroutine _voiceCoroutine;
        private Coroutine _prefetchCoroutine;
        private Coroutine _askCoroutine;
        private Coroutine _acceptanceConfirmationCoroutine;

        private const int MaxPlayerQuestionChars = 200;

        private readonly List<RaycastResult> _raycastHits = new List<RaycastResult>(8);
        private static int _uiLayer = -1;

        private void Awake()
        {
            Instance = this;
            if (_ollama == null)
                _ollama = FindFirstObjectByType<OllamaHandler>();
            UiEventSystemBootstrap.EnsureEventSystem(_inputActions);
            CacheSceneTesterRaycaster();
            BuildUi();
            SetOpen(false, restoreCursor: false);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            TryHandleDirectButtonClick();
        }

        private void OnDestroy()
        {
            StopVoicePresentation(bumpGeneration: false);
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        public void BeginSession(string displayName, string questId) =>
            BeginSession(displayName, questId, displayName);

        public void BeginSession(string displayName, string questId, string npcConversationId)
        {
            StopVoicePresentation(abortOllama: false);

            _displayName = displayName ?? "Stranger";
            _questId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();
            _npcConversationId = string.IsNullOrWhiteSpace(npcConversationId) ? "npc_default" : npcConversationId.Trim();
            ReleaseDialogueInputLock();

            if (_llmBodyText != null)
                _llmBodyText.text = string.Empty;

            RefreshStaticCopy();
            SetOpen(true, restoreCursor: true);

            if (QuestManager.Instance != null &&
                QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
            {
                ShowInstantFallbackLine(BuildCannedNpcLine(def));
                if (_voiceCoroutine != null)
                    StopCoroutine(_voiceCoroutine);
                _voiceCoroutine = StartCoroutine(AutoPresentNpcVoiceRoutine(def));
            }

            FocusAskInput();
        }

        private void FocusAskInput()
        {
            if (!GameSettings.LlmEnabled || _playerInput == null)
                return;

            _playerInput.Select();
            _playerInput.ActivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_playerInput.gameObject);
        }

        /// <summary>Starts Ollama in the background while the player walks toward the NPC.</summary>
        public void PrefetchNpcLine(string displayName, string questId, string npcConversationId)
        {
            if (!GameSettings.LlmEnabled || _ollama == null || QuestManager.Instance == null)
                return;
            if (!QuestManager.Instance.TryGetDefinition(questId, out QuestDefinition def))
                return;

            string key = BuildVoiceCacheKey(npcConversationId, questId, def);
            if (NpcDialogueCache.TryGet(key, out _))
                return;

            if (_prefetchCoroutine != null)
                StopCoroutine(_prefetchCoroutine);
            _prefetchCoroutine = StartCoroutine(PrefetchNpcLineCoroutine(def, key));
        }

        public void Close()
        {
            StopVoicePresentation();
            ReleaseDialogueInputLock();
            SetOpen(false, restoreCursor: true);
        }

        private void StopVoicePresentation(bool bumpGeneration = true, bool abortOllama = true)
        {
            if (bumpGeneration)
                _dialogueGeneration++;

            if (abortOllama)
                _ollama?.AbortActiveRequest();
            if (_voiceCoroutine != null)
            {
                StopCoroutine(_voiceCoroutine);
                _voiceCoroutine = null;
            }

            if (abortOllama && _prefetchCoroutine != null)
            {
                StopCoroutine(_prefetchCoroutine);
                _prefetchCoroutine = null;
            }

            if (_acceptanceConfirmationCoroutine != null)
            {
                StopCoroutine(_acceptanceConfirmationCoroutine);
                _acceptanceConfirmationCoroutine = null;
            }

            if (_askCoroutine != null)
            {
                StopCoroutine(_askCoroutine);
                _askCoroutine = null;
            }

            ReleaseDialogueInputLock();
        }

        private void LockVoiceInputForLlm()
        {
            _voiceBusy = true;
            SetHearInteractable(false);
        }

        private void ReleaseVoiceInputLock()
        {
            _voiceBusy = false;
            SetHearInteractable(true);
        }

        private void LockAskInputForLlm()
        {
            _askBusy = true;
            SetAskInteractable(false);
        }

        private void ReleaseAskInputLock()
        {
            _askBusy = false;
            if (GameSettings.LlmEnabled)
                SetAskInteractable(true);
            FocusAskInput();
        }

        private void ReleaseDialogueInputLock()
        {
            _voiceBusy = false;
            _askBusy = false;
            SetHearInteractable(true);
            if (GameSettings.LlmEnabled)
                SetAskInteractable(true);
            if (_statusText != null)
                _statusText.text = string.Empty;
        }

        private void SetOpen(bool open, bool restoreCursor)
        {
            IsOpen = open;
            NarrationUiGate.DialogueOpen = open;
            if (_canvasRoot != null)
                _canvasRoot.SetActive(open);

            SetSceneTesterRaycastsEnabled(!open);

            if (open)
            {
                UiEventSystemBootstrap.SetPlayerMapEnabled(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                UiEventSystemBootstrap.SetPlayerMapEnabled(true);
                if (restoreCursor && !PauseMenuController.IsPaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (_statusText != null)
                _statusText.text = string.Empty;
        }

        private void CacheSceneTesterRaycaster()
        {
            var testerCanvas = GameObject.Find("Canvas");
            if (testerCanvas != null)
                _sceneTesterRaycaster = testerCanvas.GetComponent<GraphicRaycaster>();
        }

        private void SetSceneTesterRaycastsEnabled(bool enabled)
        {
            if (_sceneTesterRaycaster == null)
                CacheSceneTesterRaycaster();
            if (_sceneTesterRaycaster != null)
                _sceneTesterRaycaster.enabled = enabled;
        }

        /// <summary>
        /// Routes mouse clicks to dialogue buttons on this canvas only (bypasses global EventSystem ordering).
        /// </summary>
        private void TryHandleDirectButtonClick()
        {
            if (_dialogueRaycaster == null || EventSystem.current == null)
                return;

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            Vector2 screenPos = Mouse.current.position.ReadValue();

            var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
            _raycastHits.Clear();
            _dialogueRaycaster.Raycast(pointerData, _raycastHits);

            for (int i = 0; i < _raycastHits.Count; i++)
            {
                Button button = _raycastHits[i].gameObject.GetComponentInParent<Button>();
                if (button == null || !button.isActiveAndEnabled || !button.interactable)
                    continue;

                button.onClick.Invoke();
                return;
            }
        }

        private void RefreshStaticCopy()
        {
            if (_titleText != null)
                _titleText.text = _displayName;

            var sb = new StringBuilder();
            if (QuestManager.Instance != null &&
                QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
            {
                if (QuestManager.Instance.IsQuestCompleted(_questId))
                {
                    if (!string.IsNullOrWhiteSpace(def.completionSummary))
                        sb.AppendLine(def.completionSummary);
                    else
                        sb.AppendLine("You have already finished this task.");
                    sb.AppendLine();
                    sb.AppendLine("Cap’s spoken line appears below (pre-generated when you got close).");
                }
                else if (QuestManager.Instance.IsQuestActive(_questId))
                {
                    sb.AppendLine("Quest in progress: ").AppendLine(def.title).AppendLine(def.briefing);
                }
                else if (QuestManager.Instance.CanOfferQuest(_questId))
                {
                    sb.AppendLine(def.title).AppendLine().AppendLine(def.briefing);
                }
                else
                    sb.AppendLine("Nothing more from this notice.");
            }
            else
                sb.AppendLine("No quest data.");

            if (_staticBodyText != null)
                _staticBodyText.text = sb.ToString().TrimEnd();

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool hasQuest = QuestManager.Instance != null &&
                            QuestManager.Instance.TryGetDefinition(_questId, out _);
            bool canOffer = hasQuest && QuestManager.Instance.CanOfferQuest(_questId);
            bool active = hasQuest && QuestManager.Instance.IsQuestActive(_questId);
            bool done = hasQuest && QuestManager.Instance.IsQuestCompleted(_questId);

            if (_acceptButton != null)
            {
                _acceptButton.gameObject.SetActive(canOffer);
                _acceptButton.interactable = canOffer;
            }

            if (_hearButton != null)
            {
                bool showHear = hasQuest && (canOffer || active || done) && GameSettings.LlmEnabled;
                _hearButton.gameObject.SetActive(showHear);
            }

            if (_askButton != null)
                _askButton.gameObject.SetActive(GameSettings.LlmEnabled);
            if (_playerInput != null)
                _playerInput.gameObject.transform.parent.gameObject.SetActive(GameSettings.LlmEnabled);
        }

        private void SetHearInteractable(bool interactable)
        {
            if (_hearButton != null)
                _hearButton.interactable = interactable;
        }

        private void SetAskInteractable(bool interactable)
        {
            if (_askButton != null)
                _askButton.interactable = interactable;
            if (_playerInput != null)
                _playerInput.interactable = interactable;
        }

        private void OnHearClicked()
        {
            if (_ollama == null || _voiceBusy)
                return;
            if (!QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
                return;

            string key = BuildVoiceCacheKey(_npcConversationId, _questId, def);
            NpcDialogueCache.Invalidate(key);
            if (_voiceCoroutine != null)
                StopCoroutine(_voiceCoroutine);
            _voiceCoroutine = StartCoroutine(RefreshNpcVoiceRoutine(def, key));
        }

        private void OnAskClicked()
        {
            if (!GameSettings.LlmEnabled)
                return;
            if (_ollama == null)
            {
                if (_statusText != null)
                    _statusText.text = "Ollama is not available in this scene.";
                return;
            }
            if (_askBusy)
                return;
            if (_playerInput == null)
                return;

            string question = (_playerInput.text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(question))
            {
                if (_statusText != null)
                    _statusText.text = "Type a question for Cap first.";
                FocusAskInput();
                return;
            }
            if (question.Length > MaxPlayerQuestionChars)
                question = question.Substring(0, MaxPlayerQuestionChars);

            _playerInput.text = string.Empty;
            if (_askCoroutine != null)
                StopCoroutine(_askCoroutine);
            _askCoroutine = StartCoroutine(AskCapRoutine(question));
        }

        private IEnumerator AskCapRoutine(string question)
        {
            int gen = _dialogueGeneration;
            if (!QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
                yield break;

            LockAskInputForLlm();
            _ollama?.AbortActiveRequest();
            NpcConversationMemory.AppendUserMessage(_npcConversationId, question);

            if (_statusText != null)
                _statusText.text = "Cap is thinking…";

            try
            {
                string spoken = string.Empty;
                bool displayedAnswer = false;
                yield return FetchReactiveLineCoroutine(def, question, gen,
                    reply => spoken = reply,
                    uiShown => displayedAnswer = uiShown);

                if (gen != _dialogueGeneration)
                    yield break;

                string answer = !string.IsNullOrWhiteSpace(spoken)
                    ? spoken
                    : "Hmm. My whiskers are tangled — try asking that again in a simpler way.";

                Debug.Log($"[Ask Cap] Player: \"{question}\"\n{_displayName}: \"{answer}\"");

                NpcConversationMemory.ReplaceAssistantReply(_npcConversationId, answer);

                if (!displayedAnswer)
                    yield return TypewriterLlmExchangeCoroutine(question, answer, gen);
            }
            finally
            {
                if (gen == _dialogueGeneration && IsOpen)
                    ReleaseAskInputLock();
                _askCoroutine = null;
            }
        }

        private IEnumerator FetchReactiveLineCoroutine(QuestDefinition def, string question, int gen,
            Action<string> onSpoken, Action<bool> onUiShown = null)
        {
            string spoken = string.Empty;
            onUiShown?.Invoke(false);

            if (_ollama == null)
            {
                onSpoken?.Invoke(spoken);
                yield break;
            }

            string model = null;
            string resolveError = null;
            yield return _ollama.ResolveModelTagCoroutine(_ollama.GetPreferredModelName(),
                resolved => model = resolved,
                err => resolveError = err);

            if (gen != _dialogueGeneration)
            {
                onSpoken?.Invoke(spoken);
                yield break;
            }

            if (string.IsNullOrEmpty(model))
            {
                if (_statusText != null && !string.IsNullOrEmpty(resolveError))
                    _statusText.text = resolveError;
                onSpoken?.Invoke(spoken);
                yield break;
            }

            if (!TryBuildReactiveChatMessages(def, question, out List<(string role, string content)> chatMessages))
            {
                if (_statusText != null && IsOpen)
                    _statusText.text = "Could not build Cap prompt.";
                onSpoken?.Invoke(string.Empty);
                yield break;
            }

            UpdateLlmBodyText("You: " + (question ?? string.Empty).Trim());
            if (_statusText != null && IsOpen)
                _statusText.text = "Cap is thinking…";

            bool done = false;
            string raw = null;
            string err = null;

            _ollama.RequestChat(model, chatMessages,
                onSuccess: text => { raw = text; done = true; },
                onError: e => { err = e; done = true; },
                saveToDialogueJson: true,
                updateResponseUiField: false,
                maxPredictTokens: _ollama.GetEffectiveNpcChatMaxTokens(),
                disableThinking: true,
                extractNpcDialogue: true,
                npcDialogueName: _displayName);

            while (!done && gen == _dialogueGeneration)
                yield return null;

            if (gen != _dialogueGeneration)
            {
                onSpoken?.Invoke(string.Empty);
                yield break;
            }

            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogWarning($"[Ask Cap] Ollama error for \"{question}\": {err}");
                if (_statusText != null && IsOpen)
                    _statusText.text = err;
            }
            else if (_statusText != null && IsOpen)
                _statusText.text = string.Empty;

            spoken = ResolveNpcSpokenLine(raw, question);
            if (string.IsNullOrWhiteSpace(spoken) && !string.IsNullOrWhiteSpace(raw))
                Debug.LogWarning($"[Ask Cap] Ollama returned text but no usable Cap line for \"{question}\". Raw: {raw}");

            if (!string.IsNullOrWhiteSpace(spoken))
            {
                yield return TypewriterLlmExchangeCoroutine(question, spoken, gen);
                onUiShown?.Invoke(true);
            }

            onSpoken?.Invoke(spoken);
        }

        private bool TryBuildReactiveChatMessages(QuestDefinition def, string question,
            out List<(string role, string content)> messages)
        {
            messages = null;
            string trimmedQuestion = (question ?? string.Empty).Trim();
            if (trimmedQuestion.Length == 0)
                return false;

            string systemPrompt = BuildReactiveNpcPrompt(def, trimmedQuestion);
            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = BuildReactiveFallbackSystemPrompt(def);
            else
                systemPrompt = StripGenerateCompletionSuffix(systemPrompt);

            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = BuildReactiveFallbackSystemPrompt(def);

            systemPrompt += "\n\nNever repeat or quote the player's question back. Answer in Cap's own words with a helpful in-character reply.";

            messages = new List<(string role, string content)>
            {
                ("system", systemPrompt),
                ("user", trimmedQuestion)
            };
            return true;
        }

        private static string StripGenerateCompletionSuffix(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return string.Empty;

            string trimmed = prompt.TrimEnd();
            if (trimmed.EndsWith(": \"", StringComparison.Ordinal))
                return trimmed.Substring(0, trimmed.Length - 3).TrimEnd();
            if (trimmed.EndsWith(":\u201c", StringComparison.Ordinal))
                return trimmed.Substring(0, trimmed.Length - 2).TrimEnd();
            return trimmed;
        }

        private string BuildReactiveFallbackSystemPrompt(QuestDefinition def)
        {
            var sb = new StringBuilder();
            sb.Append("You are ").Append(_displayName)
                .Append(", a cosy dungeon guide NPC in a lighthearted fantasy game.\n");
            sb.Append("Quest: ").Append(def.title).Append(". ").Append(def.briefing).Append('\n');

            if (QuestManager.Instance != null)
            {
                string world = QuestManager.Instance.BuildPromptContext();
                if (!string.IsNullOrWhiteSpace(world))
                    sb.AppendLine(world.Trim());
            }

            if (PlayerInventory.Instance != null)
                sb.AppendLine(PlayerInventory.Instance.BuildSummaryForPrompt());

            string memory = NpcConversationMemory.BuildPromptBlock(_npcConversationId);
            if (!string.IsNullOrWhiteSpace(memory))
                sb.AppendLine(memory.Trim());

            sb.AppendLine("Reply with ONLY what you say out loud in 1-3 short cosy sentences. No planning or analysis.");
            return sb.ToString().TrimEnd();
        }

        private IEnumerator AutoPresentNpcVoiceRoutine(QuestDefinition def)
        {
            int gen = _dialogueGeneration;
            if (!GameSettings.LlmEnabled || _ollama == null)
                yield break;

            string key = BuildVoiceCacheKey(_npcConversationId, _questId, def);
            if (NpcDialogueCache.TryGet(key, out string cached))
            {
                if (gen == _dialogueGeneration)
                    ApplyVoiceLine(cached);
                yield break;
            }

            float waited = 0f;
            while (NpcDialogueCache.IsFetching(key) && waited < _prefetchWaitSeconds && gen == _dialogueGeneration)
            {
                if (_statusText != null)
                    _statusText.text = "Cap is warming up…";
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (gen != _dialogueGeneration)
                yield break;

            if (NpcDialogueCache.TryGet(key, out cached))
            {
                ApplyVoiceLine(cached);
                yield break;
            }

            LockVoiceInputForLlm();
            try
            {
                yield return FetchNpcLineCoroutine(def, key, gen, spoken =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    if (!string.IsNullOrWhiteSpace(spoken))
                        ApplyVoiceLine(spoken);
                });
            }
            finally
            {
                if (gen == _dialogueGeneration && IsOpen)
                    ReleaseVoiceInputLock();
            }
        }

        private IEnumerator RefreshNpcVoiceRoutine(QuestDefinition def, string cacheKey)
        {
            int gen = _dialogueGeneration;
            LockVoiceInputForLlm();
            NpcDialogueCache.EndFetch(cacheKey);
            NpcDialogueCache.Invalidate(cacheKey);
            if (_statusText != null)
                _statusText.text = "Cap is thinking up another line…";

            try
            {
                yield return FetchNpcLineCoroutine(def, cacheKey, gen, spoken =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    if (!string.IsNullOrWhiteSpace(spoken))
                        ApplyVoiceLine(spoken);
                    else
                        ShowInstantFallbackLine(BuildCannedNpcLine(def));
                });
            }
            finally
            {
                if (gen == _dialogueGeneration && IsOpen)
                    ReleaseVoiceInputLock();
            }
        }

        private IEnumerator PrefetchNpcLineCoroutine(QuestDefinition def, string cacheKey)
        {
            int gen = _dialogueGeneration;
            yield return FetchNpcLineCoroutine(def, cacheKey, gen, _ => { });
            _prefetchCoroutine = null;
        }

        private IEnumerator FetchNpcLineCoroutine(QuestDefinition def, string cacheKey, int gen, Action<string> onSpoken)
        {
            if (!NpcDialogueCache.TryBeginFetch(cacheKey))
            {
                float waited = 0f;
                while (NpcDialogueCache.IsFetching(cacheKey) && waited < _prefetchWaitSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (NpcDialogueCache.TryGet(cacheKey, out string existing))
                {
                    onSpoken?.Invoke(existing);
                    yield break;
                }

                if (!NpcDialogueCache.TryBeginFetch(cacheKey))
                {
                    onSpoken?.Invoke(string.Empty);
                    yield break;
                }
            }

            string spoken = string.Empty;
            if (_ollama == null)
            {
                NpcDialogueCache.EndFetch(cacheKey);
                onSpoken?.Invoke(spoken);
                yield break;
            }

            string model = null;
            string resolveError = null;
            yield return _ollama.ResolveModelTagCoroutine(_ollama.GetPreferredModelName(),
                resolved => model = resolved,
                err => resolveError = err);

            if (gen != _dialogueGeneration)
            {
                NpcDialogueCache.EndFetch(cacheKey);
                onSpoken?.Invoke(spoken);
                yield break;
            }

            if (string.IsNullOrEmpty(model))
            {
                if (_statusText != null && !string.IsNullOrEmpty(resolveError))
                    _statusText.text = resolveError;
                NpcDialogueCache.EndFetch(cacheKey);
                onSpoken?.Invoke(spoken);
                yield break;
            }

            bool done = false;
            string raw = null;
            string err = null;
            string prompt = BuildNpcPrompt(def);
            _ollama.RequestGeneration(model, prompt,
                onSuccess: text => { raw = text; done = true; },
                onError: e => { err = e; done = true; },
                saveToDialogueJson: true,
                updateResponseUiField: false,
                maxPredictTokens: _ollama.GetEffectiveNpcMaxTokens(),
                disableThinking: true,
                extractNpcDialogue: true,
                npcDialogueName: _displayName);

            while (!done)
                yield return null;

            if (gen != _dialogueGeneration)
            {
                NpcDialogueCache.EndFetch(cacheKey);
                onSpoken?.Invoke(string.Empty);
                yield break;
            }

            if (!string.IsNullOrEmpty(err) && _statusText != null && IsOpen)
                _statusText.text = err;

            spoken = string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : OllamaHandler.ExtractNpcSpokenDialogue(raw, _displayName);

            if (!string.IsNullOrWhiteSpace(spoken))
                NpcDialogueCache.Put(cacheKey, spoken);

            NpcDialogueCache.EndFetch(cacheKey);
            onSpoken?.Invoke(spoken);
        }

        private static string BuildVoiceCacheKey(string npcConversationId, string questId, QuestDefinition def)
        {
            string state = BuildStateSignature(questId);
            return NpcDialogueCache.BuildKey(npcConversationId, questId, state);
        }

        private static string BuildStateSignature(string questId)
        {
            if (QuestManager.Instance == null)
                return "unknown";

            bool completed = QuestManager.Instance.IsQuestCompleted(questId);
            bool active = QuestManager.Instance.IsQuestActive(questId);
            bool canOffer = QuestManager.Instance.CanOfferQuest(questId);
            string inv = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.BuildSummaryForPrompt()
                : "inv:unknown";
            return (completed ? "done" : active ? "active" : canOffer ? "offer" : "idle") + "|" + inv;
        }

        private static string BuildCannedNpcLine(QuestDefinition def)
        {
            bool completed = QuestManager.Instance != null && QuestManager.Instance.IsQuestCompleted(def.id);
            bool active = QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(def.id);
            if (completed)
                return "You came back! I knew you'd handle it — I was absolutely planning that victory speech the whole time.";
            if (active)
                return "Keep your boots light on the crimson tiles. Wallop a squatter, then strut back like it was my idea.";
            return "Take the drill, friend. Clear the pits, then let me pretend I orchestrated every heroic swing.";
        }

        private void ShowInstantFallbackLine(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                UpdateLlmBodyText(line);
        }

        private void ApplyVoiceLine(string line)
        {
            line = OllamaHandler.ExtractNpcSpokenDialogue(line, _displayName);
            if (string.IsNullOrWhiteSpace(line) || OllamaHandler.IsNpcMetaPlanningLine(line))
                return;
            UpdateLlmBodyText(line);
            NpcConversationMemory.ReplaceAssistantReply(_npcConversationId, line);
        }

        private string BuildNpcPrompt(QuestDefinition def)
        {
            string world = QuestManager.Instance != null ? QuestManager.Instance.BuildPromptContext() : string.Empty;
            string inv = PlayerInventory.Instance != null ? PlayerInventory.Instance.BuildSummaryForPrompt() : "Inventory: unknown.";
            bool completed = QuestManager.Instance != null && QuestManager.Instance.IsQuestCompleted(_questId);
            bool active = QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(_questId);
            string memory = NpcConversationMemory.BuildPromptBlock(_npcConversationId);

            return CapPersonalityPromptBuilder.BuildVoicePrompt(
                _displayName,
                def.title,
                def.briefing,
                world,
                inv,
                memory,
                active,
                completed);
        }

        private string BuildReactiveNpcPrompt(QuestDefinition def, string question)
        {
            string world = QuestManager.Instance != null ? QuestManager.Instance.BuildPromptContext() : string.Empty;
            string inv = PlayerInventory.Instance != null ? PlayerInventory.Instance.BuildSummaryForPrompt() : "Inventory: unknown.";
            string memory = NpcConversationMemory.BuildPromptBlock(_npcConversationId);
            bool completed = QuestManager.Instance != null && QuestManager.Instance.IsQuestCompleted(_questId);
            bool active = QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(_questId);

            return CapPersonalityPromptBuilder.BuildReactivePrompt(
                _displayName,
                def.title,
                def.briefing,
                world,
                inv,
                memory,
                question,
                active,
                completed);
        }

        private string BuildAskCapExchangePrefix(string question) =>
            "You: " + (question ?? string.Empty).Trim() + "\n" + _displayName + ": ";

        private IEnumerator TypewriterLlmExchangeCoroutine(string question, string answer, int gen)
        {
            yield return TypewriterRevealCoroutine(BuildAskCapExchangePrefix(question), answer ?? string.Empty, gen);
        }

        private IEnumerator TypewriterRevealCoroutine(string prefix, string body, int gen)
        {
            if (_llmBodyText == null)
                yield break;

            body = body ?? string.Empty;
            if (body.Length == 0)
            {
                UpdateLlmBodyText(prefix.TrimEnd());
                yield break;
            }

            float rate = Mathf.Max(12f, _askCapTypewriterCharsPerSecond);
            UpdateLlmBodyText(prefix);
            int revealed = 0;
            float carry = 0f;

            while (revealed < body.Length && gen == _dialogueGeneration && IsOpen)
            {
                carry += Time.unscaledDeltaTime * rate;
                int add = Mathf.FloorToInt(carry);
                if (add > 0)
                {
                    carry -= add;
                    revealed = Mathf.Min(body.Length, revealed + add);
                    UpdateLlmBodyText(prefix + body.Substring(0, revealed));
                }

                yield return null;
            }

            if (gen == _dialogueGeneration && IsOpen)
                UpdateLlmBodyText(prefix + body);
        }

        private string ResolveNpcSpokenLine(string raw, string playerQuestion = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string spoken = OllamaHandler.ExtractNpcSpokenDialogue(raw, _displayName, playerQuestion);
            if (!string.IsNullOrWhiteSpace(spoken))
                return spoken.Trim();

            // Never show raw model output when extraction failed — whiskers fallback handles empty.
            return string.Empty;
        }

        private void OnAcceptClicked()
        {
            if (QuestManager.Instance == null)
            {
                if (_statusText != null)
                    _statusText.text = "Quest manager is not available.";
                return;
            }

            bool startSuccess = QuestManager.Instance.TryStartQuest(_questId);

            if (startSuccess)
            {
                // Show a clear acceptance confirmation in the main body text area
                string questTitle = "Quest";
                if (QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
                    questTitle = def.title;
                
                if (_staticBodyText != null)
                    _staticBodyText.text = $"Quest accepted: {questTitle}\n\nCheck your objectives for details.";
                
                if (_statusText != null)
                    _statusText.text = string.Empty;
                
                // Refresh buttons to hide Accept button
                RefreshButtons();
                
                // Auto-dismiss confirmation after 2 seconds and show updated quest state
                if (_acceptanceConfirmationCoroutine != null)
                    StopCoroutine(_acceptanceConfirmationCoroutine);
                _acceptanceConfirmationCoroutine = StartCoroutine(AcceptanceConfirmationRoutine());
                return;
            }

            if (_statusText != null)
            {
                if (QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
                {
                    if (QuestManager.Instance.IsQuestCompleted(_questId))
                        _statusText.text = $"{def.title} is already complete.";
                    else if (QuestManager.Instance.IsQuestActive(_questId))
                        _statusText.text = $"{def.title} is already active.";
                    else if (!string.IsNullOrWhiteSpace(def.prerequisiteQuestIdCompleted) &&
                             !QuestManager.Instance.IsQuestCompleted(def.prerequisiteQuestIdCompleted))
                        _statusText.text = $"{def.title} is locked until its prerequisite is complete.";
                    else
                        _statusText.text = $"{def.title} could not be started.";
                }
                else
                    _statusText.text = "Quest data is missing.";
            }
        }

        private IEnumerator AcceptanceConfirmationRoutine()
        {
            yield return new WaitForSecondsRealtime(2f);
            RefreshStaticCopy();
            _acceptanceConfirmationCoroutine = null;
        }

        private void BuildUi()
        {
            _canvasRoot = new GameObject("DialogueCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRoot.transform.SetParent(transform, false);
            var canvasGo = _canvasRoot;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = DialogueCanvasSortOrder;

            _dialogueRaycaster = canvasGo.GetComponent<GraphicRaycaster>();
            ApplyUiLayer(canvasGo);

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            TmpTextUtility.ConfigureCanvasScaler(scaler);

            var dim = MakeUiObject("Dim", canvasGo.transform);
            StretchToParent(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.03f, 0.07f, 0.55f);
            dimImg.raycastTarget = false;

            _rootPanel = MakeUiObject("DialoguePanel", dim.transform);
            var panelRt = _rootPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(820, 680);
            panelRt.anchoredPosition = Vector2.zero;

            var panelBg = _rootPanel.AddComponent<Image>();
            panelBg.color = MenuTheme.Panel;
            panelBg.raycastTarget = false;
            var outline = _rootPanel.AddComponent<Outline>();
            outline.effectColor = MenuTheme.PanelBorder;
            outline.effectDistance = new Vector2(3, -3);

            var panelGroup = _rootPanel.AddComponent<CanvasGroup>();
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;

            var vlg = _rootPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            _titleText = MakeText("Title", _rootPanel.transform, "NPC",
                MenuTheme.GameTitleFontSize, MenuTheme.GameplayText, TextAlignmentOptions.Center);
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.raycastTarget = false;
            var titleLe = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 52f;
            titleLe.preferredHeight = 52f;

            var staticHint = MakeText("StaticHint", _rootPanel.transform, "Quest (game rules)",
                MenuTheme.GameCaptionFontSize, MenuTheme.GameplayMutedText, TextAlignmentOptions.Left);
            staticHint.raycastTarget = false;
            var staticHintLe = staticHint.gameObject.AddComponent<LayoutElement>();
            staticHintLe.minHeight = 28f;
            staticHintLe.preferredHeight = 28f;

            _staticBodyText = MakeText("StaticBody", _rootPanel.transform, string.Empty,
                MenuTheme.GameBodyFontSize, MenuTheme.GameplayText, TextAlignmentOptions.TopLeft);
            _staticBodyText.raycastTarget = false;
            var staticBodyLe = _staticBodyText.gameObject.AddComponent<LayoutElement>();
            staticBodyLe.minHeight = 100f;
            staticBodyLe.preferredHeight = 100f;
            staticBodyLe.flexibleHeight = 0f;
            _staticBodyText.textWrappingMode = TextWrappingModes.Normal;
            _staticBodyText.overflowMode = TextOverflowModes.Ellipsis;
            _staticBodyText.maxVisibleLines = 5;

            var llmHint = MakeText("LlmHint", _rootPanel.transform, "Cap’s voice (Ollama — appears below)",
                MenuTheme.GameCaptionFontSize, MenuTheme.GameplayMutedText, TextAlignmentOptions.Left);
            llmHint.raycastTarget = false;
            var llmHintLe = llmHint.gameObject.AddComponent<LayoutElement>();
            llmHintLe.minHeight = 28f;
            llmHintLe.preferredHeight = 28f;

            _llmScrollRect = BuildLlmScrollArea(_rootPanel.transform);
            BuildAskRow(_rootPanel.transform);

            _statusText = MakeText("Status", _rootPanel.transform, string.Empty,
                MenuTheme.GameHudSmallFontSize, MenuTheme.GameplayText, TextAlignmentOptions.Center);
            _statusText.raycastTarget = false;
            var statusLe = _statusText.gameObject.AddComponent<LayoutElement>();
            statusLe.minHeight = 34f;
            statusLe.preferredHeight = 34f;

            _hearButton = MakeButton("Hear", _rootPanel.transform, "Another line",
                MenuTheme.ButtonSecondary, MenuTheme.ButtonSecondaryHover, OnHearClicked);
            _acceptButton = MakeButton("Accept", _rootPanel.transform, "Accept quest",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, OnAcceptClicked);
            _closeButton = MakeButton("Close", _rootPanel.transform, "Close",
                MenuTheme.ButtonDanger, MenuTheme.ButtonDangerHover, Close);

            _canvasRoot.SetActive(false);
        }

        private static void ApplyUiLayer(GameObject root)
        {
            if (_uiLayer < 0)
                _uiLayer = LayerMask.NameToLayer("UI");

            if (_uiLayer < 0)
                return;

            ApplyUiLayerRecursive(root.transform);
        }

        private static void ApplyUiLayerRecursive(Transform t)
        {
            t.gameObject.layer = _uiLayer;
            for (int i = 0; i < t.childCount; i++)
                ApplyUiLayerRecursive(t.GetChild(i));
        }

        private static GameObject MakeUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void BuildAskRow(Transform parent)
        {
            var row = MakeUiObject("AskRow", parent);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 52f;
            rowLe.preferredHeight = 52f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            _playerInput = BuildPlayerInput(row.transform);
            _playerInput.onSubmit.AddListener(_ => OnAskClicked());
            var inputLe = _playerInput.gameObject.AddComponent<LayoutElement>();
            inputLe.flexibleWidth = 1f;
            inputLe.minWidth = 200f;

            _askButton = MakeButton("Ask", row.transform, "Ask Cap",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, OnAskClicked);
            var askLe = _askButton.gameObject.GetComponent<LayoutElement>();
            if (askLe != null)
                askLe.flexibleWidth = 0f;
        }

        private static TMP_InputField BuildPlayerInput(Transform parent)
        {
            var fieldGo = MakeUiObject("PlayerQuestion", parent);
            var fieldBg = fieldGo.AddComponent<Image>();
            fieldBg.color = new Color(0.96f, 0.93f, 0.86f, 1f);
            fieldBg.raycastTarget = true;

            var viewport = MakeUiObject("TextArea", fieldGo.transform);
            StretchToParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();

            var placeholderGo = MakeUiObject("Placeholder", viewport.transform);
            StretchToParent(placeholderGo.GetComponent<RectTransform>());
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Ask Cap something…";
            placeholder.fontSize = MenuTheme.GameBodyFontSize;
            placeholder.color = MenuTheme.GameplayMutedText;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.raycastTarget = false;
            TmpTextUtility.ApplyReadableDefaults(placeholder, gameplayBlackText: true);

            var textGo = MakeUiObject("Text", viewport.transform);
            StretchToParent(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = MenuTheme.GameBodyFontSize;
            text.color = MenuTheme.GameplayText;
            TmpTextUtility.ApplyReadableDefaults(text, gameplayBlackText: true);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = MaxPlayerQuestionChars;
            return input;
        }

        private ScrollRect BuildLlmScrollArea(Transform parent)
        {
            var scrollGo = MakeUiObject("LlmScroll", parent);
            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.minHeight = 200f;
            scrollLe.preferredHeight = 200f;
            scrollLe.flexibleHeight = 1f;

            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0.22f, 0.14f, 0.08f, 0.08f);
            scrollBg.raycastTarget = false;

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = MakeUiObject("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            StretchToParent(viewportRt);
            viewport.AddComponent<RectMask2D>();
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = Color.clear;
            viewportImg.raycastTarget = false;

            var content = MakeUiObject("Content", viewport.transform);
            _llmContentRect = content.GetComponent<RectTransform>();
            _llmContentRect.anchorMin = new Vector2(0f, 1f);
            _llmContentRect.anchorMax = new Vector2(1f, 1f);
            _llmContentRect.pivot = new Vector2(0.5f, 1f);
            _llmContentRect.anchoredPosition = Vector2.zero;
            _llmContentRect.sizeDelta = Vector2.zero;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _llmBodyText = MakeText("LlmBody", content.transform, string.Empty,
                MenuTheme.GameBodyFontSize, MenuTheme.GameplayText, TextAlignmentOptions.TopLeft);
            var llmRt = _llmBodyText.rectTransform;
            llmRt.anchorMin = new Vector2(0f, 1f);
            llmRt.anchorMax = new Vector2(1f, 1f);
            llmRt.pivot = new Vector2(0.5f, 1f);
            llmRt.anchoredPosition = Vector2.zero;
            llmRt.sizeDelta = new Vector2(-12f, 0f);
            _llmBodyText.textWrappingMode = TextWrappingModes.Normal;
            _llmBodyText.overflowMode = TextOverflowModes.Overflow;
            _llmBodyText.raycastTarget = false;

            scroll.content = _llmContentRect;
            scroll.viewport = viewportRt;
            return scroll;
        }

        private void UpdateLlmBodyText(string text)
        {
            if (_llmBodyText == null)
                return;

            string safe = BreakLongWords(text ?? string.Empty, 30);
            _llmBodyText.text = safe;
            _llmBodyText.ForceMeshUpdate();

            if (_llmContentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_llmContentRect);

            if (_rootPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rootPanel.GetComponent<RectTransform>());

            if (_llmScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _llmScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = MakeUiObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            TmpTextUtility.ApplyReadableDefaults(tmp, gameplayBlackText: true);
            return tmp;
        }

        // Insert zero-width spaces into very long unbroken words so TMP can wrap them.
        private static string BreakLongWords(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
                return text;

            var sb = new StringBuilder(text.Length + text.Length / maxLength + 8);
            int run = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
                {
                    sb.Append(c);
                    run = 0;
                    continue;
                }

                sb.Append(c);
                run++;
                if (run >= maxLength)
                {
                    sb.Append('\u200B'); // zero-width space
                    run = 0;
                }
            }

            return sb.ToString();
        }

        private static Button MakeButton(string name, Transform parent, string label,
            Color baseColor, Color hoverColor, Action onClick)
        {
            var go = MakeUiObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = baseColor;
            img.raycastTarget = true;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = MenuTheme.ButtonHeight;
            le.preferredHeight = MenuTheme.ButtonHeight;
            le.minWidth = MenuTheme.ButtonMinWidth;
            le.flexibleWidth = 1;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;  // Explicitly set the target graphic
            btn.navigation = new Navigation { mode = Navigation.Mode.None };  // Disable auto-navigation
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = hoverColor;
            colors.pressedColor = baseColor * 0.85f;
            colors.selectedColor = hoverColor;
            colors.disabledColor = baseColor * 0.5f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            var labelTmp = MakeText("Label", go.transform, label,
                MenuTheme.ButtonFontSize, MenuTheme.GameplayText, TextAlignmentOptions.Center);
            StretchToParent(labelTmp.rectTransform);
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.raycastTarget = false;
            return btn;
        }
    }
}
