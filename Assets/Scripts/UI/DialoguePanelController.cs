using System;
using System.Collections;
using System.Text;
using DungeonExporer.Gameplay;
using DungeonExporer.Player;
using TMPro;
using UnityEngine;
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
        [SerializeField] private float _typewriterCharsPerSecond = 56f;

        private GameObject _rootPanel;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _staticBodyText;
        private TextMeshProUGUI _llmBodyText;
        private TextMeshProUGUI _statusText;
        private Button _hearButton;
        private Button _acceptButton;
        private Button _closeButton;

        private string _displayName = string.Empty;
        private string _questId = string.Empty;
        private bool _busy;
        private int _dialogueGeneration;
        private readonly StringBuilder _streamBuffer = new StringBuilder(2048);
        private Coroutine _streamUiCoroutine;

        private void Awake()
        {
            Instance = this;
            if (_ollama == null)
                _ollama = FindFirstObjectByType<OllamaHandler>();
            BuildUi();
            SetOpen(false, restoreCursor: false);
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

        public void BeginSession(string displayName, string questId)
        {
            StopStreamUiInternal();

            _displayName = displayName ?? "Stranger";
            _questId = questId ?? string.Empty;
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

            SetHearInteractable(true);
        }

        private void SetOpen(bool open, bool restoreCursor)
        {
            IsOpen = open;
            if (_rootPanel != null)
                _rootPanel.SetActive(open);

            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (restoreCursor)
            {
                if (!PauseMenuController.IsPaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (_statusText != null)
                _statusText.text = string.Empty;
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
                _acceptButton.gameObject.SetActive(canOffer);

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
            if (_ollama == null || _busy)
                return;

            if (!QuestManager.Instance.TryGetDefinition(_questId, out QuestDefinition def))
                return;

            StopStreamUiInternal();
            int gen = _dialogueGeneration;
            _busy = true;
            SetHearInteractable(false);
            if (_statusText != null)
                _statusText.text = "Listening… (Ollama, streaming)";
            _streamBuffer.Clear();
            if (_llmBodyText != null)
                _llmBodyText.text = string.Empty;

            string model = string.IsNullOrWhiteSpace(_ollama.defaultModel) ? "qwen3:4b" : _ollama.defaultModel;
            string prompt = BuildNpcPrompt(def);

            bool streamFinished = false;
            string streamError = null;

            _ollama.RequestGenerationStreaming(model, prompt,
                onDelta: delta =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    _streamBuffer.Append(delta);
                },
                onComplete: _ =>
                {
                    if (gen != _dialogueGeneration)
                        return;
                    streamFinished = true;
                    if (_statusText != null)
                        _statusText.text = string.Empty;
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
                saveToDialogueJson: true);

            _streamUiCoroutine = StartCoroutine(StreamRevealRoutine(gen, () => streamFinished, () => streamError));
        }

        private IEnumerator StreamRevealRoutine(int gen, Func<bool> isStreamFinished, Func<string> getError)
        {
            float revealed = 0f;

            while (gen == _dialogueGeneration)
            {
                if (!string.IsNullOrEmpty(getError()))
                    break;

                if (isStreamFinished() && revealed >= _streamBuffer.Length)
                    break;

                revealed += _typewriterCharsPerSecond * Time.unscaledDeltaTime;
                int cap = _streamBuffer.Length;
                int shown = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, cap);

                if (_llmBodyText != null && gen == _dialogueGeneration)
                {
                    if (isStreamFinished() && shown >= cap)
                        _llmBodyText.text = OllamaHandler.SanitizeModelOutput(_streamBuffer.ToString());
                    else if (cap > 0)
                        _llmBodyText.text = _streamBuffer.ToString(0, shown);
                }

                yield return null;
            }

            if (gen == _dialogueGeneration)
            {
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

            string situation = completed
                ? "The adventurer has returned after finishing this quest (or its objectives). React in warm, cosy fantasy banter — thanks, jokes, or loose ends. Do not assign new formal objectives or numbered tasks."
                : active
                    ? "The adventurer accepted your quest and is working on it. Offer a short tip or color commentary; stay consistent with the briefing."
                    : "The adventurer is considering your quest. Speak in character and hook them into the fantasy; do not repeat the briefing verbatim.";

            return
                "You are " + _displayName + ", an NPC in a first-person dungeon crawler videogame.\n" +
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
                return;
            if (QuestManager.Instance.TryStartQuest(_questId))
                RefreshStaticCopy();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("DialogueCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = MakeUiObject("Dim", _canvas.transform);
            StretchToParent(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.03f, 0.07f, 0.55f);
            dimImg.raycastTarget = true;

            _rootPanel = MakeUiObject("DialoguePanel", dim.transform);
            var panelRt = _rootPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(800, 560);
            panelRt.anchoredPosition = Vector2.zero;

            var panelBg = _rootPanel.AddComponent<Image>();
            panelBg.color = MenuTheme.Panel;
            var outline = _rootPanel.AddComponent<Outline>();
            outline.effectColor = MenuTheme.PanelBorder;
            outline.effectDistance = new Vector2(3, -3);

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
            var titleLe = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 44f;
            titleLe.preferredHeight = 44f;

            var staticHint = MakeText("StaticHint", _rootPanel.transform, "Quest (game rules)",
                16f, MenuTheme.SubtitleText, TextAlignmentOptions.Left);
            var staticHintLe = staticHint.gameObject.AddComponent<LayoutElement>();
            staticHintLe.minHeight = 22f;
            staticHintLe.preferredHeight = 22f;

            _staticBodyText = MakeText("StaticBody", _rootPanel.transform, string.Empty,
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.TopLeft);
            var staticBodyLe = _staticBodyText.gameObject.AddComponent<LayoutElement>();
            staticBodyLe.minHeight = 120f;
            staticBodyLe.flexibleHeight = 1f;
            _staticBodyText.enableWordWrapping = true;

            var llmHint = MakeText("LlmHint", _rootPanel.transform, "Cap’s voice (Ollama, streams in)",
                16f, MenuTheme.SubtitleText, TextAlignmentOptions.Left);
            var llmHintLe = llmHint.gameObject.AddComponent<LayoutElement>();
            llmHintLe.minHeight = 22f;
            llmHintLe.preferredHeight = 22f;

            _llmBodyText = MakeText("LlmBody", _rootPanel.transform, string.Empty,
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.TopLeft);
            var llmBodyLe = _llmBodyText.gameObject.AddComponent<LayoutElement>();
            llmBodyLe.minHeight = 100f;
            llmBodyLe.flexibleHeight = 1f;
            _llmBodyText.enableWordWrapping = true;

            _statusText = MakeText("Status", _rootPanel.transform, string.Empty,
                18f, new Color(0.55f, 0.12f, 0.1f, 1f), TextAlignmentOptions.Center);
            var statusLe = _statusText.gameObject.AddComponent<LayoutElement>();
            statusLe.minHeight = 28f;
            statusLe.preferredHeight = 28f;

            _hearButton = MakeButton("Hear", _rootPanel.transform, "Hear them out (Ollama, streams)",
                MenuTheme.ButtonSecondary, MenuTheme.ButtonSecondaryHover, OnHearClicked);
            _acceptButton = MakeButton("Accept", _rootPanel.transform, "Accept quest",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, OnAcceptClicked);
            _closeButton = MakeButton("Close", _rootPanel.transform, "Close",
                MenuTheme.ButtonDanger, MenuTheme.ButtonDangerHover, Close);

            _rootPanel.SetActive(false);
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

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = MakeUiObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Button MakeButton(string name, Transform parent, string label,
            Color baseColor, Color hoverColor, Action onClick)
        {
            var go = MakeUiObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = baseColor;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = MenuTheme.ButtonHeight;
            le.preferredHeight = MenuTheme.ButtonHeight;
            le.minWidth = MenuTheme.ButtonMinWidth;
            le.flexibleWidth = 1;

            var btn = go.AddComponent<Button>();
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
            return btn;
        }
    }
}
