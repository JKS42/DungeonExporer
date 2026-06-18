using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// Shared TMP + canvas settings so runtime UI text stays sharp and legible.
    /// </summary>
    public static class TmpTextUtility
    {
        private static TMP_FontAsset _defaultFont;

        public static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = MenuTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
        }

        public static void ApplyReadableDefaults(TMP_Text tmp, bool lightTextOnDarkBackground = false)
        {
            if (tmp == null)
                return;

            _defaultFont ??= TMP_Settings.defaultFontAsset;
            if (_defaultFont != null && tmp.font == null)
                tmp.font = _defaultFont;

            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.enableKerning = true;
            tmp.richText = true;

            if (tmp is TextMeshProUGUI)
            {
                tmp.raycastTarget = false;
                ApplyUiShadow(tmp.gameObject, lightTextOnDarkBackground);
                return;
            }

            // World-space TMP: avoid outlineWidth (needs a ready font material instance).
            if (lightTextOnDarkBackground)
                tmp.color = new Color(
                    Mathf.Min(1f, tmp.color.r * 1.05f),
                    Mathf.Min(1f, tmp.color.g * 1.05f),
                    Mathf.Min(1f, tmp.color.b * 1.05f),
                    tmp.color.a);
        }

        private static void ApplyUiShadow(GameObject go, bool lightTextOnDarkBackground)
        {
            if (go == null || go.GetComponent<Shadow>() != null)
                return;

            var shadow = go.AddComponent<Shadow>();
            shadow.useGraphicAlpha = true;
            if (lightTextOnDarkBackground)
            {
                shadow.effectColor = new Color(0.04f, 0.03f, 0.06f, 0.9f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                shadow.effectColor = new Color(0.98f, 0.95f, 0.88f, 0.45f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }
        }
    }
}
