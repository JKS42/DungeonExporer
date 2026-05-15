using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DungeonExporer.Gameplay;
using DungeonExporer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// In-world dialogue: authored quest text plus streamed Ollama lines (NDJSON) with a typewriter-style reveal.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class DialoguePanelController : MonoBehaviour
    {
        public static DialoguePanelController Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [SerializeField] private OllamaHandler _ollama;
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private float _typewriterCharsPerSecond = 56f;

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
        private Button _acceptButton;
        private Button _closeButton;

        private string _displayName = string.Empty;
        private string _questId = string.Empty;
        private string _npcConversationId = "npc";
        private bool _busy;
        private int _dialogueGeneration;
        private readonly StringBuilder _streamBuffer = new StringBuilder(2048);
        private Coroutine _streamUiCoroutine;
        private Coroutine _acceptanceConfirmationCoroutine;

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
            StopStreamUiInternal(false);
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
            StopStreamUiInternal();

            _displayName = displayName ?? "Stranger";
            _questId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();
            _npcConversationId = string.IsNullOrWhiteSpace(npcConversationId) ? "npc_default" : npcConversationId.Trim();
            _busy = false;

            if (_llmBodyText != null)
                _llmBodyText.text = string.Empty;
            _streamBuffer.Clear();

            RefreshStaticCopy();
            SetOpen(true, restoreCursor: true);
        }

        public void Close()
        {
            StopStreamUiInternal();
            _busy = false;
            SetOpen(false, restoreCursor: true);
        }

        private void StopStreamUiInternal(bool bumpGeneration = true)
        {
            if (bumpGeneration)
                _dialogueGeneration++;

            _ollama?.AbortActiveRequest();
            if (_streamUiCoroutine != null)
            {
                StopCoroutine(_streamUiCoroutine);
                _streamUiCoroutine = null;
            }
            
            if (_acceptanceConfirmationCoroutine != null)
            {
                StopCoroutine(_acceptanceConfirmationCoroutine);
                _acceptanceConfirmationCoroutine = null;
            }

            SetHearInteractable(true);
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
                    sb.AppendLine("Use “Hear them out” for Cap’s voice — text streams in from Ollama, then tidies up at the end.");
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
                _hearButton.gameObject.SetActive(hasQuest && (canOffer || active || done));
        }

        private void SetHearInteractable(bool interactable)
        {
            if (_hearButton != null)
                _hearButton.interactable = interactable;
        }

        private void OnHearClicked()
        {
            if (_ollama == null)
            {
                Debug.LogWarning("DialoguePanelController.OnHearClicked: OllamaHandler is null. Ensure an OllamaHandler exists in the scene and is assigned.");
                return;
            }

            if (_busy)
            {
                Debug.Log("DialoguePanelController.OnHearClicked: Busy; ignoring click.");
                return;
            }

            if (!QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
            {
                Debug.LogWarning($"DialoguePanelController.OnHearClicked: No quest definition for id '{_questId}'.");
                return;
            }

            StartCoroutine(OnHearClickedCoroutine(def));
        }

        private IEnumerator OnHearClickedCoroutine(QuestDefinition def)
        {
            StopStreamUiInternal();
            int gen = _dialogueGeneration;
            _busy = true;
            SetHearInteractable(false);
            if (_statusText != null)
                _statusText.text = "Checking Ollama model…";
            _streamBuffer.Clear();
            if (_llmBodyText != null)
                _llmBodyText.text = string.Empty;

            string model = null;
            string resolveError = null;
            yield return _ollama.ResolveModelTagCoroutine(_ollama.GetPreferredModelName(),
                resolved => model = resolved,
                err => resolveError = err);

            if (gen != _dialogueGeneration)
                yield break;

            if (string.IsNullOrEmpty(model))
            {
                _busy = false;
                SetHearInteractable(true);
                if (_statusText != null)
                    _statusText.text = resolveError ?? "Ollama model not available.";
                yield break;
            }

            if (_statusText != null)
                _statusText.text = "Listening… (Ollama, streaming)";

            string prompt = BuildNpcPrompt(def);
            Debug.Log($"DialoguePanelController: Requesting Ollama stream for NPC '{_npcConversationId}' (quest '{_questId}'), model={model}");

            bool streamFinished = false;
            string streamError = null;

            _ollama.RequestGenerationStreaming(model, prompt,
                onDelta: delta =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    _streamBuffer.Append(delta);
                },
                onComplete: full =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    streamFinished = true;
                    if (_statusText != null)
                        _statusText.text = string.Empty;
                    NpcConversationMemory.AppendAssistantReply(_npcConversationId, full);
                },
                onError: err =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    streamError = err;
                    streamFinished = true;
                    if (_statusText != null)
                        _statusText.text = err;
                },
                saveToDialogueJson: true,
                maxPredictTokens: _ollama.defaultStreamMaxTokens);

            _streamUiCoroutine = StartCoroutine(StreamRevealRoutine(gen, () => streamFinished, () => streamError));
        }

        private IEnumerator StreamRevealRoutine(int gen, Func<bool> isStreamFinished, Func<string> getError)
        {
            float revealed = 0f;
            int lastCap = 0;
            float idleSeconds = 0f;
            const float stallSeconds = 3.25f;

            while (gen == _dialogueGeneration)
            {
                string err = getError();
                if (!string.IsNullOrEmpty(err))
                    break;

                int cap = _streamBuffer.Length;
                if (cap > lastCap)
                {
                    lastCap = cap;
                    idleSeconds = 0f;
                }
                else if (!isStreamFinished())
                {
                    idleSeconds += Time.unscaledDeltaTime;
                    if (idleSeconds > stallSeconds && _statusText != null && string.IsNullOrEmpty(err))
                    {
                        _statusText.text =
                            "Still thinking… (no tokens yet — cold models can take a few seconds; see docs/setup.md if it never starts)";
                    }
                }

                if (isStreamFinished() && revealed >= _streamBuffer.Length)
                    break;

                revealed += _typewriterCharsPerSecond * Time.unscaledDeltaTime;
                cap = _streamBuffer.Length;
                int shown = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, cap);

                if (gen == _dialogueGeneration)
                {
                    if (cap > 0)
                    {
                        string slice = _streamBuffer.ToString(0, shown);
                        UpdateLlmBodyText(OllamaHandler.SanitizeForDisplay(slice));
                    }
                    else if (isStreamFinished() && _statusText != null)
                        _statusText.text = "Cap had nothing to say (empty reply from Ollama).";
                }

                yield return null;
            }

            if (gen == _dialogueGeneration)
            {
                if (_streamBuffer.Length > 0)
                    UpdateLlmBodyText(OllamaHandler.SanitizeModelOutput(_streamBuffer.ToString()));

                _busy = false;
                SetHearInteractable(true);
            }

            _streamUiCoroutine = null;
        }

        private string BuildNpcPrompt(QuestDefinition def)
        {
            string world = QuestManager.Instance != null ? QuestManager.Instance.BuildPromptContext() : string.Empty;
            string inv = PlayerInventory.Instance != null ? PlayerInventory.Instance.BuildSummaryForPrompt() : "Inventory: unknown.";
            bool completed = QuestManager.Instance != null && QuestManager.Instance.IsQuestCompleted(_questId);
            bool active = QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(_questId);

            string memory = NpcConversationMemory.BuildPromptBlock(_npcConversationId);
            string memoryBlock = string.IsNullOrEmpty(memory) ? string.Empty : memory + "\n";

            string situation = completed
                ? "The adventurer has returned after finishing this quest (or its objectives). React in warm, cosy fantasy banter — thanks, jokes, or loose ends. Do not assign new formal objectives or numbered tasks."
                : active
                    ? "The adventurer accepted your quest and is working on it. Offer a short tip or color commentary; stay consistent with the briefing."
                    : "The adventurer is considering your quest. Speak in character and hook them into the fantasy; do not repeat the briefing verbatim.";

            return
                "You are " + _displayName + ", an NPC in a first-person dungeon crawler videogame.\n" +
                memoryBlock +
                "Authoritative quest title: " + def.title + ".\n" +
                "Authoritative briefing (facts): " + def.briefing + "\n" +
                "Quest state summary from the game: " + world + "\n" +
                inv + "\n" +
                situation + "\n" +
                "Write 2–6 short sentences of spoken dialogue only. No markdown, no bullet lists, no JSON, no stage directions.";
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
                    _staticBodyText.text = $"✓ {questTitle} accepted!\n\nCheck your objectives for details.";
                
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

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
                36f, MenuTheme.TitleText, TextAlignmentOptions.Center);
            _titleText.raycastTarget = false;
            var titleLe = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 44f;
            titleLe.preferredHeight = 44f;

            var staticHint = MakeText("StaticHint", _rootPanel.transform, "Quest (game rules)",
                16f, MenuTheme.SubtitleText, TextAlignmentOptions.Left);
            staticHint.raycastTarget = false;
            var staticHintLe = staticHint.gameObject.AddComponent<LayoutElement>();
            staticHintLe.minHeight = 22f;
            staticHintLe.preferredHeight = 22f;

            _staticBodyText = MakeText("StaticBody", _rootPanel.transform, string.Empty,
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.TopLeft);
            _staticBodyText.raycastTarget = false;
            var staticBodyLe = _staticBodyText.gameObject.AddComponent<LayoutElement>();
            staticBodyLe.minHeight = 88f;
            staticBodyLe.preferredHeight = 88f;
            staticBodyLe.flexibleHeight = 0f;
            _staticBodyText.textWrappingMode = TextWrappingModes.Normal;
            _staticBodyText.overflowMode = TextOverflowModes.Ellipsis;
            _staticBodyText.maxVisibleLines = 5;

            var llmHint = MakeText("LlmHint", _rootPanel.transform, "Cap’s voice (Ollama — appears below)",
                16f, MenuTheme.SubtitleText, TextAlignmentOptions.Left);
            llmHint.raycastTarget = false;
            var llmHintLe = llmHint.gameObject.AddComponent<LayoutElement>();
            llmHintLe.minHeight = 22f;
            llmHintLe.preferredHeight = 22f;

            _llmScrollRect = BuildLlmScrollArea(_rootPanel.transform);

            _statusText = MakeText("Status", _rootPanel.transform, string.Empty,
                18f, new Color(0.55f, 0.12f, 0.1f, 1f), TextAlignmentOptions.Center);
            _statusText.raycastTarget = false;
            var statusLe = _statusText.gameObject.AddComponent<LayoutElement>();
            statusLe.minHeight = 28f;
            statusLe.preferredHeight = 28f;

            _hearButton = MakeButton("Hear", _rootPanel.transform, "Hear them out (Ollama, streams)",
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
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.TopLeft);
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

            _llmBodyText.text = text ?? string.Empty;
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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
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
                MenuTheme.ButtonFontSize, MenuTheme.ButtonText, TextAlignmentOptions.Center);
            StretchToParent(labelTmp.rectTransform);
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.raycastTarget = false;
            return btn;
        }
    }
}
