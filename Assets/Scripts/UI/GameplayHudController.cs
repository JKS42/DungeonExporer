using System;
using System.Collections;
using System.Text;
using DungeonExporer.Dungeon;
using DungeonExporer.Gameplay;
using DungeonExporer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// HUD: health, active quest one-liner, inventory (I), NPC interact hint, transient flavor toasts.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class GameplayHudController : MonoBehaviour
    {
        public static GameplayHudController Instance { get; private set; }

        [SerializeField] private int _canvasSortOrder = 35;

        private Canvas _canvas;
        private RectTransform _healthFillAnchor;
        private TextMeshProUGUI _healthNumbers;
        private TextMeshProUGUI _questObjectiveLine;
        private GameObject _inventoryRoot;
        private TextMeshProUGUI _inventoryBody;
        private bool _inventoryVisible;
        private TextMeshProUGUI _interactPrompt;
        private TextMeshProUGUI _flavorToast;
        private Coroutine _flavorCoroutine;
        private Coroutine _questCompleteLineRoutine;
        private Image _crosshair;
        private Coroutine _hitConfirmRoutine;

        private void Awake()
        {
            Instance = this;
            DungeonFlavorHudBridge.PublishFlavorToast = (msg, sec) => ShowFlavorToast(msg, sec);
        }

        private void Start()
        {
            BuildHud();
            Bind();
            RefreshHealth(PlayerHealth.Instance);
            RefreshInventory();
            RefreshQuestObjectiveLine();
        }

        private void OnDestroy()
        {
            Unbind();
            if (Instance == this)
            {
                Instance = null;
                DungeonFlavorHudBridge.PublishFlavorToast = null;
            }
        }

        private void LateUpdate()
        {
            RefreshInteractPromptFromRegistry();
        }

        public static void ShowFlavorToast(string message, float secondsVisible)
        {
            if (Instance == null)
                return;
            Instance.ShowFlavorToastInternal(message, secondsVisible);
        }

        public static void FlashMeleeHitConfirm()
        {
            if (Instance == null)
                return;
            Instance.FlashMeleeHitConfirmInternal();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            {
                if (PauseMenuController.IsPaused || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
                    return;
                ToggleInventory();
            }
        }

        private void Bind()
        {
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.HealthChanged += OnHealthChanged;
                PlayerHealth.Instance.Respawned += OnRespawned;
            }

            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnChanged += RefreshInventory;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.QuestStateChanged += OnQuestStateChanged;
                QuestManager.Instance.QuestCompleted += OnQuestCompleted;
            }
        }

        private void Unbind()
        {
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.HealthChanged -= OnHealthChanged;
                PlayerHealth.Instance.Respawned -= OnRespawned;
            }

            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnChanged -= RefreshInventory;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.QuestStateChanged -= OnQuestStateChanged;
                QuestManager.Instance.QuestCompleted -= OnQuestCompleted;
            }
        }

        private void OnQuestStateChanged()
        {
            if (_questCompleteLineRoutine != null)
                return;
            RefreshQuestObjectiveLine();
        }

        private void OnQuestCompleted(QuestDefinition def)
        {
            if (def == null)
                return;

            string title = string.IsNullOrWhiteSpace(def.title) ? "Quest" : def.title.Trim();
            ShowFlavorToastInternal("Quest complete: " + title, 6.5f);

            if (_questObjectiveLine == null)
                return;

            if (_questCompleteLineRoutine != null)
                StopCoroutine(_questCompleteLineRoutine);
            _questCompleteLineRoutine = StartCoroutine(QuestCompleteLineRoutine(title));
        }

        private IEnumerator QuestCompleteLineRoutine(string title)
        {
            _questObjectiveLine.text = "Complete: " + title + " — visit Cap when you can.";
            yield return new WaitForSecondsRealtime(8f);
            _questCompleteLineRoutine = null;
            RefreshQuestObjectiveLine();
        }

        private void OnHealthChanged(float current, float max) => RefreshHealthBar(current, max);

        private void OnRespawned() => RefreshHealth(PlayerHealth.Instance);

        private void RefreshHealth(PlayerHealth health)
        {
            if (health == null)
                return;
            RefreshHealthBar(health.CurrentHealth, health.MaxHealth);
        }

        private void RefreshHealthBar(float current, float max)
        {
            max = Mathf.Max(1f, max);
            if (_healthFillAnchor != null)
            {
                _healthFillAnchor.anchorMin = Vector2.zero;
                _healthFillAnchor.anchorMax = new Vector2(Mathf.Clamp01(current / max), 1f);
            }

            if (_healthNumbers != null)
                _healthNumbers.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void RefreshInventory()
        {
            if (_inventoryBody == null || PlayerInventory.Instance == null)
                return;

            var sb = new StringBuilder();
            foreach (PlayerInventory.StackEntry e in PlayerInventory.Instance.EnumerateStacks())
                sb.AppendLine($"{e.displayName}  ×{e.count}");

            _inventoryBody.text = sb.Length > 0 ? sb.ToString().TrimEnd() : "(empty)";
        }

        private void ToggleInventory()
        {
            _inventoryVisible = !_inventoryVisible;
            if (_inventoryRoot != null)
                _inventoryRoot.SetActive(_inventoryVisible);
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("GameplayHudCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _canvasSortOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            TmpTextUtility.ConfigureCanvasScaler(scaler);

            var topLeft = new GameObject("TopLeft", typeof(RectTransform));
            topLeft.transform.SetParent(canvasGo.transform, false);
            var topRt = topLeft.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(0f, 1f);
            topRt.pivot = new Vector2(0f, 1f);
            topRt.anchoredPosition = new Vector2(28f, -24f);
            topRt.sizeDelta = new Vector2(560f, 168f);

            var vlg = topLeft.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var hpLabel = MakeTmp("HpLabel", topLeft.transform, "Health", MenuTheme.GameHudSmallFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.Left);
            var hpLabelLe = hpLabel.gameObject.AddComponent<LayoutElement>();
            hpLabelLe.minHeight = 30f;
            hpLabelLe.preferredHeight = 30f;

            var barBg = new GameObject("HealthBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(topLeft.transform, false);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.sizeDelta = new Vector2(0f, 18f);
            var barBgLe = barBg.AddComponent<LayoutElement>();
            barBgLe.minHeight = 18f;
            barBgLe.preferredHeight = 18f;
            barBgLe.minWidth = 260f;
            barBgLe.flexibleWidth = 1f;
            barBg.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.16f, 0.92f);

            var barFill = new GameObject("HealthBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            _healthFillAnchor = barFill.GetComponent<RectTransform>();
            _healthFillAnchor.anchorMin = new Vector2(0f, 0f);
            _healthFillAnchor.anchorMax = new Vector2(1f, 1f);
            _healthFillAnchor.offsetMin = new Vector2(2f, 2f);
            _healthFillAnchor.offsetMax = new Vector2(-2f, -2f);
            barFill.GetComponent<Image>().color = new Color(0.45f, 0.78f, 0.42f, 1f);

            _healthNumbers = MakeTmp("HealthNumbers", topLeft.transform, "—", MenuTheme.GameHudFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.Left);
            var hpNumLe = _healthNumbers.gameObject.AddComponent<LayoutElement>();
            hpNumLe.minHeight = 34f;
            hpNumLe.preferredHeight = 34f;

            _questObjectiveLine = MakeTmp("QuestObjective", topLeft.transform, " ", MenuTheme.GameHudSmallFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.TopLeft);
            var questLe = _questObjectiveLine.gameObject.AddComponent<LayoutElement>();
            questLe.minHeight = 52f;
            questLe.flexibleHeight = 1f;
            _questObjectiveLine.textWrappingMode = TextWrappingModes.Normal;

            _inventoryRoot = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _inventoryRoot.transform.SetParent(canvasGo.transform, false);
            var invRt = _inventoryRoot.GetComponent<RectTransform>();
            invRt.anchorMin = new Vector2(1f, 1f);
            invRt.anchorMax = new Vector2(1f, 1f);
            invRt.pivot = new Vector2(1f, 1f);
            invRt.anchoredPosition = new Vector2(-28f, -24f);
            invRt.sizeDelta = new Vector2(380f, 400f);
            var invBg = _inventoryRoot.GetComponent<Image>();
            invBg.color = Color.clear;
            invBg.raycastTarget = false;
            var invV = _inventoryRoot.GetComponent<VerticalLayoutGroup>();
            invV.padding = new RectOffset(14, 14, 12, 12);
            invV.spacing = 6f;
            invV.childAlignment = TextAnchor.UpperLeft;
            invV.childControlWidth = true;
            invV.childControlHeight = true;
            invV.childForceExpandWidth = true;

            var invTitle = MakeTmp("InvTitle", _inventoryRoot.transform, "Inventory (I)", MenuTheme.GameHudFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.Left);
            invTitle.fontStyle = FontStyles.Bold;
            var invTitleLe = invTitle.gameObject.AddComponent<LayoutElement>();
            invTitleLe.minHeight = 34f;

            _inventoryBody = MakeTmp("InvBody", _inventoryRoot.transform, "(empty)", MenuTheme.GameHudSmallFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.TopLeft);
            var invBodyLe = _inventoryBody.gameObject.AddComponent<LayoutElement>();
            invBodyLe.flexibleHeight = 1f;
            invBodyLe.minHeight = 140f;
            _inventoryBody.textWrappingMode = TextWrappingModes.Normal;

            var invHint = MakeTmp("InvHint", _inventoryRoot.transform, "Walk over glowing orbs to pick them up.",
                MenuTheme.GameCaptionFontSize, MenuTheme.GameplayMutedText, TextAlignmentOptions.Left);
            var invHintLe = invHint.gameObject.AddComponent<LayoutElement>();
            invHintLe.minHeight = 48f;

            _inventoryRoot.SetActive(false);
            _inventoryVisible = false;

            var bottom = new GameObject("BottomHud", typeof(RectTransform));
            bottom.transform.SetParent(canvasGo.transform, false);
            var bottomRt = bottom.GetComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0.5f, 0f);
            bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.anchoredPosition = new Vector2(0f, 22f);
            bottomRt.sizeDelta = new Vector2(960f, 148f);

            var bottomV = bottom.AddComponent<VerticalLayoutGroup>();
            bottomV.spacing = 8f;
            bottomV.childAlignment = TextAnchor.MiddleCenter;
            bottomV.childControlHeight = true;
            bottomV.childControlWidth = true;
            bottomV.childForceExpandWidth = true;

            _interactPrompt = MakeTmp("InteractPrompt", bottom.transform, string.Empty, MenuTheme.GameHudFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.Center);
            var interactLe = _interactPrompt.gameObject.AddComponent<LayoutElement>();
            interactLe.minHeight = 38f;
            _interactPrompt.fontStyle = FontStyles.Bold;

            _flavorToast = MakeTmp("FlavorToast", bottom.transform, string.Empty, MenuTheme.GameHudSmallFontSize,
                MenuTheme.GameplayText, TextAlignmentOptions.Center);
            var flavorLe = _flavorToast.gameObject.AddComponent<LayoutElement>();
            flavorLe.minHeight = 52f;
            _flavorToast.alpha = 1f;

            var crosshairGo = new GameObject("MeleeCrosshair", typeof(RectTransform), typeof(Image));
            crosshairGo.transform.SetParent(canvasGo.transform, false);
            var crosshairRt = crosshairGo.GetComponent<RectTransform>();
            crosshairRt.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRt.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRt.pivot = new Vector2(0.5f, 0.5f);
            crosshairRt.sizeDelta = new Vector2(10f, 10f);
            _crosshair = crosshairGo.GetComponent<Image>();
            _crosshair.color = new Color(0.95f, 0.95f, 0.98f, 0.72f);
            _crosshair.raycastTarget = false;
        }

        private static TextMeshProUGUI MakeTmp(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            TmpTextUtility.ApplyReadableDefaults(tmp, gameplayBlackText: true);
            return tmp;
        }

        private void RefreshQuestObjectiveLine()
        {
            if (_questObjectiveLine == null)
                return;

            if (QuestManager.Instance != null && QuestManager.Instance.TryGetPrimaryObjectiveHudLine(out string line))
            {
                _questObjectiveLine.text = line;
                return;
            }

            _questObjectiveLine.text = " ";
        }

        private void RefreshInteractPromptFromRegistry()
        {
            if (_interactPrompt == null)
                return;

            if (PauseMenuController.IsPaused || DialoguePanelController.IsOpen ||
                (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
            {
                _interactPrompt.text = string.Empty;
                return;
            }

            string label = NpcPromptRegistry.CurrentLabel;
            _interactPrompt.text = string.IsNullOrEmpty(label) ? string.Empty : label;
        }

        private void ShowFlavorToastInternal(string message, float secondsVisible)
        {
            if (_flavorToast == null)
                return;

            if (_flavorCoroutine != null)
                StopCoroutine(_flavorCoroutine);

            _flavorCoroutine = StartCoroutine(FlavorToastRoutine(message ?? string.Empty, secondsVisible));
        }

        private IEnumerator FlavorToastRoutine(string message, float secondsVisible)
        {
            _flavorToast.text = message;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.35f, secondsVisible));
            _flavorToast.text = string.Empty;
            _flavorCoroutine = null;
        }

        private void FlashMeleeHitConfirmInternal()
        {
            if (_crosshair == null)
                return;

            if (_hitConfirmRoutine != null)
                StopCoroutine(_hitConfirmRoutine);
            _hitConfirmRoutine = StartCoroutine(HitConfirmRoutine());
        }

        private IEnumerator HitConfirmRoutine()
        {
            Color baseColor = new Color(0.95f, 0.95f, 0.98f, 0.72f);
            Color hitColor = new Color(1f, 0.82f, 0.35f, 1f);
            Vector2 baseSize = new Vector2(10f, 10f);
            Vector2 hitSize = new Vector2(18f, 18f);

            RectTransform rt = _crosshair.rectTransform;
            float elapsed = 0f;
            const float duration = 0.14f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f - t;
                _crosshair.color = Color.Lerp(baseColor, hitColor, pulse);
                rt.sizeDelta = Vector2.Lerp(baseSize, hitSize, pulse);
                yield return null;
            }

            _crosshair.color = baseColor;
            rt.sizeDelta = baseSize;
            _hitConfirmRoutine = null;
        }
    }
}
