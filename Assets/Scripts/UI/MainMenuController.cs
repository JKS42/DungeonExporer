using System.Collections.Generic;
using DungeonExporer.Gameplay;
using DungeonExporer.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonExporer.UI
{
    /// <summary>
    /// Procedurally builds the main menu and its options panel at runtime.
    /// Attach to a single empty GameObject in <c>Assets/Scenes/MainMenu.unity</c>.
    /// All UI styling is sourced from <see cref="MenuTheme"/>.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Scene loaded when the player presses Start. Must be added to Build Settings.")]
        public string startSceneName = "Level1";

        private Canvas _canvas;
        private GameObject _mainPanel;
        private GameObject _optionsPanel;

        private readonly List<Resolution> _uniqueResolutions = new();

        private void Start()
        {
            EnsureSettingsApplier();
            EnsureOllamaWarmup();
            BuildCanvas();
            BuildBackground();
            _mainPanel = BuildMainPanel();
            _optionsPanel = BuildOptionsPanel();
            _optionsPanel.SetActive(false);
        }

        // ---------------------------------------------------------------- helpers

        private static void EnsureSettingsApplier()
        {
            if (FindFirstObjectByType<SettingsApplier>() == null)
            {
                var go = new GameObject("SettingsApplier");
                go.AddComponent<SettingsApplier>();
            }
        }

        private static void EnsureOllamaWarmup()
        {
            if (FindFirstObjectByType<OllamaMenuWarmup>() != null)
                return;

            var go = new GameObject("OllamaMenuWarmup");
            go.AddComponent<OllamaMenuWarmup>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("MenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            TmpTextUtility.ConfigureCanvasScaler(scaler);

            // EventSystem is required for Buttons to receive clicks.
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        private void BuildBackground()
        {
            var bg = MakeUiObject("Background", _canvas.transform);
            StretchToParent(bg.GetComponent<RectTransform>());
            var img = bg.AddComponent<Image>();
            img.color = MenuTheme.BackgroundBottom;

            // Soft vertical gradient via a second, additive image.
            var top = MakeUiObject("BackgroundTop", bg.transform);
            StretchToParent(top.GetComponent<RectTransform>());
            var topImg = top.AddComponent<Image>();
            topImg.color = new Color(
                MenuTheme.BackgroundTop.r,
                MenuTheme.BackgroundTop.g,
                MenuTheme.BackgroundTop.b,
                0.6f);
        }

        // ---------------------------------------------------------------- main panel

        private GameObject BuildMainPanel()
        {
            var panel = MakeUiObject("MainPanel", _canvas.transform);
            var rt = panel.GetComponent<RectTransform>();
            StretchToParent(rt);

            // Title block.
            var title = MakeText("Title", panel.transform, MenuTheme.GameTitle,
                MenuTheme.TitleFontSize, MenuTheme.TitleText, TextAlignmentOptions.Center);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 0.78f);
            titleRt.anchorMax = new Vector2(0.5f, 0.92f);
            titleRt.sizeDelta = new Vector2(1400, 160);
            titleRt.anchoredPosition = Vector2.zero;
            title.fontStyle = FontStyles.Bold;

            var tagline = MakeText("Tagline", panel.transform, MenuTheme.GameTagline,
                MenuTheme.SubtitleFontSize, MenuTheme.SubtitleText, TextAlignmentOptions.Center);
            var taglineRt = tagline.rectTransform;
            taglineRt.anchorMin = new Vector2(0.5f, 0.72f);
            taglineRt.anchorMax = new Vector2(0.5f, 0.78f);
            taglineRt.sizeDelta = new Vector2(1400, 60);
            taglineRt.anchoredPosition = Vector2.zero;
            tagline.fontStyle = FontStyles.Italic;

            // Button column.
            var buttonsGo = MakeUiObject("Buttons", panel.transform);
            var buttonsRt = buttonsGo.GetComponent<RectTransform>();
            buttonsRt.anchorMin = new Vector2(0.5f, 0.18f);
            buttonsRt.anchorMax = new Vector2(0.5f, 0.62f);
            buttonsRt.sizeDelta = new Vector2(MenuTheme.ButtonMinWidth + 40,
                3 * MenuTheme.ButtonHeight + 2 * MenuTheme.ButtonSpacing);
            buttonsRt.anchoredPosition = Vector2.zero;

            var vlg = buttonsGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = MenuTheme.ButtonSpacing;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            MakeButton("StartButton", buttonsGo.transform, "Start Adventure",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, OnStart);
            MakeButton("OptionsButton", buttonsGo.transform, "Options",
                MenuTheme.ButtonSecondary, MenuTheme.ButtonSecondaryHover, OnOpenOptions);
            MakeButton("QuitButton", buttonsGo.transform, "Quit",
                MenuTheme.ButtonDanger, MenuTheme.ButtonDangerHover, OnQuit);

            return panel;
        }

        // ---------------------------------------------------------------- options panel

        private GameObject BuildOptionsPanel()
        {
            var panel = MakeUiObject("OptionsPanel", _canvas.transform);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(960, 880);
            rt.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = MenuTheme.Panel;

            var title = MakeText("OptionsTitle", panel.transform, "Options",
                MenuTheme.TitleFontSize * 0.55f, MenuTheme.BodyText, TextAlignmentOptions.Center);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(800, 72);
            titleRt.anchoredPosition = new Vector2(0, -24);
            title.fontStyle = FontStyles.Bold;

            // Vertical list of settings rows.
            // Content area sits between the title (top) and the footer (bottom).
            var content = MakeUiObject("Content", panel.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.offsetMin = new Vector2(MenuTheme.PanelPadding, 128); // 88 footer + 16 gap + 24 margin
            contentRt.offsetMax = new Vector2(-MenuTheme.PanelPadding, -108); // 72 title + 12 gap + 24 margin

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;   // honour each row's LayoutElement.preferredHeight
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            // Rows.
            MakeSliderRow(content.transform, "Master Volume",
                GameSettings.MasterVolume, 0f, 1f,
                v => GameSettings.MasterVolume = v);

            MakeSliderRow(content.transform, "Music Volume",
                GameSettings.MusicVolume, 0f, 1f,
                v => GameSettings.MusicVolume = v);

            MakeSliderRow(content.transform, "SFX Volume",
                GameSettings.SfxVolume, 0f, 1f,
                v => GameSettings.SfxVolume = v);

            MakeSliderRow(content.transform, "Mouse Sensitivity",
                GameSettings.MouseSensitivity, 0.1f, 3f,
                v => GameSettings.MouseSensitivity = v);

            MakeToggleRow(content.transform, "Fullscreen",
                GameSettings.Fullscreen,
                v => GameSettings.Fullscreen = v);

            MakeResolutionRow(content.transform);

            MakeToggleRow(content.transform, "AI-driven dialogue (Ollama)",
                GameSettings.LlmEnabled,
                v => GameSettings.LlmEnabled = v);

            // Footer: Reset + Back buttons.
            var footer = MakeUiObject("Footer", panel.transform);
            var footerRt = footer.GetComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0, 0);
            footerRt.anchorMax = new Vector2(1, 0);
            footerRt.pivot = new Vector2(0.5f, 0);
            footerRt.sizeDelta = new Vector2(0, 88);
            footerRt.anchoredPosition = new Vector2(0, 16);

            var hlg = footer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = MenuTheme.ButtonSpacing;
            hlg.padding = new RectOffset(
                (int)MenuTheme.PanelPadding, (int)MenuTheme.PanelPadding, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = true;

            MakeButton("ResetButton", footer.transform, "Reset to Defaults",
                MenuTheme.ButtonSecondary, MenuTheme.ButtonSecondaryHover, OnResetSettings);
            MakeButton("BackButton", footer.transform, "Back",
                MenuTheme.ButtonPrimary, MenuTheme.ButtonPrimaryHover, OnCloseOptions);

            return panel;
        }

        // ---------------------------------------------------------------- button handlers

        private void OnStart()
        {
            if (string.IsNullOrEmpty(startSceneName))
            {
                Debug.LogError("[MainMenu] startSceneName is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(startSceneName))
            {
                Debug.LogError(
                    $"[MainMenu] Scene '{startSceneName}' is not in Build Settings. " +
                    "Add it via File → Build Profiles → Scene List.");
                return;
            }

            SceneManager.LoadScene(startSceneName);
        }

        private void OnOpenOptions()
        {
            _mainPanel.SetActive(false);
            _optionsPanel.SetActive(true);
        }

        private void OnCloseOptions()
        {
            _optionsPanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        private static void OnResetSettings()
        {
            GameSettings.ResetToDefaults();
            // Easiest way to refresh the on-screen control values is a scene reload.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- UI factories

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
            TmpTextUtility.ApplyReadableDefaults(tmp);
            return tmp;
        }

        private static Button MakeButton(string name, Transform parent, string label,
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

            var label1 = MakeText("Label", go.transform, label,
                MenuTheme.ButtonFontSize, MenuTheme.ButtonText, TextAlignmentOptions.Center);
            StretchToParent(label1.rectTransform);
            label1.fontStyle = FontStyles.Bold;

            return btn;
        }

        private static void MakeSliderRow(Transform parent, string label,
            float initial, float min, float max, System.Action<float> onChanged)
        {
            var row = MakeRow(parent, label, out _);

            var sliderGo = MakeUiObject("Slider", row.transform);
            var rowLe = sliderGo.AddComponent<LayoutElement>();
            rowLe.flexibleWidth = 1;
            rowLe.minHeight = 28;
            rowLe.preferredHeight = 28;

            var bgGo = MakeUiObject("Background", sliderGo.transform);
            StretchToParent(bgGo.GetComponent<RectTransform>());
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.65f, 0.55f, 0.40f, 1f);

            var fillArea = MakeUiObject("Fill Area", sliderGo.transform);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1, 0.75f);
            fillAreaRt.offsetMin = new Vector2(8, 0);
            fillAreaRt.offsetMax = new Vector2(-8, 0);

            var fill = MakeUiObject("Fill", fillArea.transform);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = MenuTheme.ButtonPrimary;

            var handleArea = MakeUiObject("Handle Slide Area", sliderGo.transform);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0, 0);
            handleAreaRt.anchorMax = new Vector2(1, 1);
            handleAreaRt.offsetMin = new Vector2(8, 0);
            handleAreaRt.offsetMax = new Vector2(-8, 0);

            var handle = MakeUiObject("Handle", handleArea.transform);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(24, 32);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = MenuTheme.PanelBorder;

            var slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.onValueChanged.AddListener(v => onChanged(v));
        }

        private static void MakeToggleRow(Transform parent, string label,
            bool initial, System.Action<bool> onChanged)
        {
            var row = MakeRow(parent, label, out _);

            // The toggle host is sized to match the row so it picks up clicks
            // anywhere on the right side of the row, not just on the small box.
            var toggleGo = MakeUiObject("Toggle", row.transform);
            var rowLe = toggleGo.AddComponent<LayoutElement>();
            rowLe.flexibleWidth = 1;
            rowLe.minHeight = 36;
            rowLe.preferredHeight = 36;

            var bg = MakeUiObject("Background", toggleGo.transform);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(1, 0.5f);
            bgRt.anchorMax = new Vector2(1, 0.5f);
            bgRt.pivot = new Vector2(1, 0.5f);
            bgRt.sizeDelta = new Vector2(36, 36);
            bgRt.anchoredPosition = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.92f, 0.86f, 0.70f, 1f);

            // Border accent so the box stands out against the parchment panel.
            var border = MakeUiObject("Border", bg.transform);
            StretchToParent(border.GetComponent<RectTransform>());
            var borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(MenuTheme.PanelBorder.r, MenuTheme.PanelBorder.g, MenuTheme.PanelBorder.b, 0.35f);
            borderImg.raycastTarget = false;

            var checkmark = MakeUiObject("Checkmark", bg.transform);
            StretchToParent(checkmark.GetComponent<RectTransform>());
            var checkImg = checkmark.AddComponent<Image>();
            checkImg.color = MenuTheme.ButtonSecondary;
            checkImg.raycastTarget = false;

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = initial;
            toggle.onValueChanged.AddListener(v => onChanged(v));
        }

        private void MakeResolutionRow(Transform parent)
        {
            BuildUniqueResolutions();
            if (_uniqueResolutions.Count == 0) return;

            var row = MakeRow(parent, "Resolution", out _);

            var dropdownGo = MakeUiObject("Dropdown", row.transform);
            var le = dropdownGo.AddComponent<LayoutElement>();
            le.minWidth = 320;
            le.flexibleWidth = 1;
            le.minHeight = 40;
            le.preferredHeight = 40;

            var img = dropdownGo.AddComponent<Image>();
            img.color = new Color(0.92f, 0.86f, 0.70f, 1f);

            var label = MakeText("Label", dropdownGo.transform, "",
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.Center);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12, 4);
            labelRt.offsetMax = new Vector2(-40, -4);

            // Template (offscreen — TMP_Dropdown requires this to exist).
            var template = MakeUiObject("Template", dropdownGo.transform);
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.sizeDelta = new Vector2(0, 240);
            templateRt.anchoredPosition = new Vector2(0, 2);
            template.AddComponent<Image>().color = MenuTheme.Panel;
            template.SetActive(false);

            var viewport = MakeUiObject("Viewport", template.transform);
            StretchToParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = MakeUiObject("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0, 40);

            var item = MakeUiObject("Item", content.transform);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 0.5f);
            itemRt.anchorMax = new Vector2(1, 0.5f);
            itemRt.sizeDelta = new Vector2(0, 32);

            var itemToggle = item.AddComponent<Toggle>();
            var itemBg = MakeUiObject("Item Background", item.transform);
            StretchToParent(itemBg.GetComponent<RectTransform>());
            var itemBgImg = itemBg.AddComponent<Image>();
            itemBgImg.color = new Color(0.92f, 0.86f, 0.70f, 1f);
            itemToggle.targetGraphic = itemBgImg;

            var itemCheck = MakeUiObject("Item Checkmark", item.transform);
            var itemCheckRt = itemCheck.GetComponent<RectTransform>();
            itemCheckRt.anchorMin = new Vector2(0, 0.5f);
            itemCheckRt.anchorMax = new Vector2(0, 0.5f);
            itemCheckRt.pivot = new Vector2(0, 0.5f);
            itemCheckRt.sizeDelta = new Vector2(20, 20);
            itemCheckRt.anchoredPosition = new Vector2(10, 0);
            var itemCheckImg = itemCheck.AddComponent<Image>();
            itemCheckImg.color = MenuTheme.ButtonPrimary;
            itemToggle.graphic = itemCheckImg;

            var itemLabel = MakeText("Item Label", item.transform, "Option",
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.Left);
            var itemLabelRt = itemLabel.rectTransform;
            itemLabelRt.anchorMin = new Vector2(0, 0);
            itemLabelRt.anchorMax = new Vector2(1, 1);
            itemLabelRt.offsetMin = new Vector2(36, 0);
            itemLabelRt.offsetMax = new Vector2(-8, 0);

            var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = img;
            dropdown.captionText = label;
            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;

            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var r in _uniqueResolutions)
            {
                options.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height}"));
            }
            dropdown.options = options;

            int current = Mathf.Clamp(GameSettings.ResolutionIndex,
                0, _uniqueResolutions.Count - 1);
            if (GameSettings.ResolutionIndex < 0)
            {
                for (int i = 0; i < _uniqueResolutions.Count; i++)
                {
                    if (_uniqueResolutions[i].width == Screen.currentResolution.width &&
                        _uniqueResolutions[i].height == Screen.currentResolution.height)
                    {
                        current = i;
                        break;
                    }
                }
            }
            dropdown.value = current;
            dropdown.RefreshShownValue();

            dropdown.onValueChanged.AddListener(idx => GameSettings.ResolutionIndex = idx);
        }

        private void BuildUniqueResolutions()
        {
            _uniqueResolutions.Clear();
            var seen = new HashSet<(int, int)>();
            foreach (var r in Screen.resolutions)
            {
                var key = (r.width, r.height);
                if (seen.Add(key)) _uniqueResolutions.Add(r);
            }
        }

        private static GameObject MakeRow(Transform parent, string labelText, out TextMeshProUGUI labelTmp)
        {
            var row = MakeUiObject("Row_" + labelText, parent);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 48;
            le.preferredHeight = 48;
            le.flexibleWidth = 1;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;   // share remaining row width between children
            hlg.childForceExpandHeight = false; // keep children at their preferred height (sliders are 28 in a 48 row)

            labelTmp = MakeText("Label", row.transform, labelText,
                MenuTheme.BodyFontSize, MenuTheme.BodyText, TextAlignmentOptions.MidlineLeft);
            var labelLe = labelTmp.gameObject.AddComponent<LayoutElement>();
            labelLe.minWidth = 280;
            labelLe.preferredWidth = 280;
            labelLe.flexibleWidth = 0; // label width is fixed; controls take the remaining space

            return row;
        }
    }
}
