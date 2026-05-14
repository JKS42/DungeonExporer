using System.Text;
using DungeonExporer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// Minimal HUD: health readout + optional inventory list (toggle with <b>I</b>).
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class GameplayHudController : MonoBehaviour
    {
        [SerializeField] private int _canvasSortOrder = 35;

        private Canvas _canvas;
        private RectTransform _healthFillAnchor;
        private TextMeshProUGUI _healthNumbers;
        private GameObject _inventoryRoot;
        private TextMeshProUGUI _inventoryBody;
        private bool _inventoryVisible;

        private void Start()
        {
            BuildHud();
            Bind();
            RefreshHealth(PlayerHealth.Instance);
            RefreshInventory();
        }

        private void OnDestroy()
        {
            Unbind();
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var topLeft = new GameObject("TopLeft", typeof(RectTransform));
            topLeft.transform.SetParent(canvasGo.transform, false);
            var topRt = topLeft.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(0f, 1f);
            topRt.pivot = new Vector2(0f, 1f);
            topRt.anchoredPosition = new Vector2(28f, -24f);
            topRt.sizeDelta = new Vector2(360f, 72f);

            var vlg = topLeft.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var hpLabel = MakeTmp("HpLabel", topLeft.transform, "Health", 20f, MenuTheme.SubtitleText, TextAlignmentOptions.Left);
            var hpLabelLe = hpLabel.gameObject.AddComponent<LayoutElement>();
            hpLabelLe.minHeight = 22f;
            hpLabelLe.preferredHeight = 22f;

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

            _healthNumbers = MakeTmp("HealthNumbers", topLeft.transform, "—", 22f, MenuTheme.BodyText, TextAlignmentOptions.Left);
            var hpNumLe = _healthNumbers.gameObject.AddComponent<LayoutElement>();
            hpNumLe.minHeight = 26f;
            hpNumLe.preferredHeight = 26f;

            _inventoryRoot = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _inventoryRoot.transform.SetParent(canvasGo.transform, false);
            var invRt = _inventoryRoot.GetComponent<RectTransform>();
            invRt.anchorMin = new Vector2(1f, 1f);
            invRt.anchorMax = new Vector2(1f, 1f);
            invRt.pivot = new Vector2(1f, 1f);
            invRt.anchoredPosition = new Vector2(-28f, -24f);
            invRt.sizeDelta = new Vector2(340f, 360f);
            _inventoryRoot.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.78f);
            var invV = _inventoryRoot.GetComponent<VerticalLayoutGroup>();
            invV.padding = new RectOffset(14, 14, 12, 12);
            invV.spacing = 6f;
            invV.childAlignment = TextAnchor.UpperLeft;
            invV.childControlWidth = true;
            invV.childControlHeight = true;
            invV.childForceExpandWidth = true;

            var invTitle = MakeTmp("InvTitle", _inventoryRoot.transform, "Inventory (I)", 22f, MenuTheme.TitleText, TextAlignmentOptions.Left);
            var invTitleLe = invTitle.gameObject.AddComponent<LayoutElement>();
            invTitleLe.minHeight = 28f;

            _inventoryBody = MakeTmp("InvBody", _inventoryRoot.transform, "(empty)", 20f, MenuTheme.SubtitleText, TextAlignmentOptions.TopLeft);
            var invBodyLe = _inventoryBody.gameObject.AddComponent<LayoutElement>();
            invBodyLe.flexibleHeight = 1f;
            invBodyLe.minHeight = 120f;
            _inventoryBody.enableWordWrapping = true;

            var invHint = MakeTmp("InvHint", _inventoryRoot.transform, "Walk over glowing orbs to pick them up.", 16f, MenuTheme.BodyText, TextAlignmentOptions.Left);
            var invHintLe = invHint.gameObject.AddComponent<LayoutElement>();
            invHintLe.minHeight = 40f;

            _inventoryRoot.SetActive(false);
            _inventoryVisible = false;
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
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
