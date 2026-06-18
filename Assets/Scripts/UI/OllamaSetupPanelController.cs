using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// Blocking overlay when Ollama is missing, unreachable, or the expected model is not pulled.
    /// </summary>
    public sealed class OllamaSetupPanelController : MonoBehaviour
    {
        [SerializeField] private int _sortingOrder = 220;

        private GameObject _root;
        private TextMeshProUGUI _body;

        private void Awake() => BuildUi();

        public void Show(string message)
        {
            BuildUi();
            if (_body != null)
                _body.text = message ?? string.Empty;
            if (_root != null)
                _root.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            if (!PauseMenuController.IsPaused && !DialoguePanelController.IsOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void BuildUi()
        {
            if (_root != null)
                return;

            var canvasGo = new GameObject("OllamaSetupCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            TmpTextUtility.ConfigureCanvasScaler(scaler);

            _root = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _root.transform.SetParent(canvasGo.transform, false);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 420f);
            rt.anchoredPosition = Vector2.zero;
            _root.GetComponent<Image>().color = MenuTheme.Panel;
            var v = _root.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 24, 24);
            v.spacing = 14f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;

            var title = MakeText("Title", _root.transform, "Ollama needs attention",
                MenuTheme.GameTitleFontSize, MenuTheme.GameplayText);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.minHeight = 48f;

            _body = MakeText("Body", _root.transform, string.Empty, MenuTheme.GameBodyFontSize, MenuTheme.GameplayText);
            var bodyLe = _body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 160f;

            var hint = MakeText("Hint", _root.transform,
                "First model load can take 30–60s (cold). After Ollama is healthy, press Continue.",
                MenuTheme.GameCaptionFontSize, MenuTheme.GameplayMutedText);
            var hintLe = hint.gameObject.AddComponent<LayoutElement>();
            hintLe.minHeight = 56f;

            var row = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_root.transform, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 52f);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 52f;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childForceExpandWidth = true;

            MakePanelButton(row.transform, "Open setup guide (README)", OpenSetupDoc);
            MakePanelButton(row.transform, "Continue (retry later)", () => Hide());

            _root.SetActive(false);
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            TmpTextUtility.ApplyReadableDefaults(tmp, gameplayBlackText: true);
            return tmp;
        }

        private static void MakePanelButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label.GetHashCode().ToString("x"), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = MenuTheme.ButtonSecondary;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = MenuTheme.ButtonHeight;
            le.flexibleWidth = 1f;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            var tmp = MakeText("Lbl", go.transform, label, MenuTheme.ButtonFontSize, MenuTheme.GameplayText);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void OpenSetupDoc()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string readme = Path.Combine(repoRoot, "README.md");
            string setup = Path.Combine(repoRoot, "docs", "setup.md");
            string pick = File.Exists(setup) ? setup : readme;
            if (!File.Exists(pick))
            {
                Debug.LogWarning("OllamaSetupPanel: could not find docs/setup.md or README.md next to the project.");
                return;
            }

            string uri = "file:///" + pick.Replace("\\", "/");
            Application.OpenURL(uri);
        }
    }
}
