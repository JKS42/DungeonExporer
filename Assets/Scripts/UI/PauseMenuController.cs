using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonExporer.UI
{
    /// <summary>
    /// Simple pause overlay: Escape toggles pause, freezes time, Resume / Main Menu / Quit.
    /// Uses <see cref="MenuTheme"/> for consistent styling with the main menu.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class PauseMenuController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        [Header("Scenes")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        private Canvas _canvas;
        private GameObject _rootPanel;
        private float _savedTimeScale = 1f;

        private void Start()
        {
            BuildUi();
            SetPaused(false, restoreTimeScale: false);
        }

        private void OnDestroy()
        {
            if (IsPaused)
                Time.timeScale = Mathf.Approximately(_savedTimeScale, 0f) ? 1f : _savedTimeScale;
            IsPaused = false;
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (DialoguePanelController.IsOpen)
                {
                    var dialogue = FindFirstObjectByType<DialoguePanelController>();
                    dialogue?.Close();
                    return;
                }

                TogglePause();
            }
        }

        private void TogglePause()
        {
            SetPaused(!IsPaused, restoreTimeScale: true);
        }

        private void SetPaused(bool paused, bool restoreTimeScale)
        {
            IsPaused = paused;

            if (paused)
            {
                if (restoreTimeScale)
                    _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = Mathf.Approximately(_savedTimeScale, 0f) ? 1f : _savedTimeScale;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (_rootPanel != null)
                _rootPanel.SetActive(paused);
        }

        private void Resume()
        {
            SetPaused(false, restoreTimeScale: true);
        }

        private void LoadMainMenu()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("PauseCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = MakeUiObject("Dim", _canvas.transform);
            StretchToParent(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.05f, 0.04f, 0.08f, 0.72f);
            dimImg.raycastTarget = true;

            _rootPanel = MakeUiObject("PausePanel", dim.transform);
            var panelRt = _rootPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520, 420);
            panelRt.anchoredPosition = Vector2.zero;

            var panelBg = _rootPanel.AddComponent<Image>();
            panelBg.color = MenuTheme.Panel;
            var outline = _rootPanel.AddComponent<Outline>();
            outline.effectColor = MenuTheme.PanelBorder;
            outline.effectDistance = new Vector2(3, -3);

            var vlg = _rootPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var title = MakeText("Title", _rootPanel.transform, "Paused",
                44f, MenuTheme.TitleText, TextAlignmentOptions.Center);
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 56f;
            titleLe.preferredHeight = 56f;

            var hint = MakeText("Hint", _rootPanel.transform, "Press Escape to resume",
                MenuTheme.BodyFontSize, MenuTheme.SubtitleText, TextAlignmentOptions.Center);
            var hintLe = hint.gameObject.AddComponent<LayoutElement>();
            hintLe.minHeight = 28f;
            hintLe.preferredHeight = 28f;

            var spacer = MakeUiObject("Spacer", _rootPanel.transform);
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.minHeight = 12f;
            spacerLe.preferredHeight = 12f;

            MakeButton("Resume", _rootPanel.transform, "Resume",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, Resume);
            MakeButton("MainMenu", _rootPanel.transform, "Main Menu",
                MenuTheme.ButtonSecondary, MenuTheme.ButtonSecondaryHover, LoadMainMenu);
            MakeButton("Quit", _rootPanel.transform, "Quit",
                MenuTheme.ButtonDanger, MenuTheme.ButtonDangerHover, QuitGame);

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

        private static void MakeButton(string name, Transform parent, string label,
            Color baseColor, Color hoverColor, UnityEngine.Events.UnityAction onClick)
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
            btn.onClick.AddListener(onClick);

            var labelTmp = MakeText("Label", go.transform, label,
                MenuTheme.ButtonFontSize, MenuTheme.ButtonText, TextAlignmentOptions.Center);
            StretchToParent(labelTmp.rectTransform);
            labelTmp.fontStyle = FontStyles.Bold;
        }
    }
}
