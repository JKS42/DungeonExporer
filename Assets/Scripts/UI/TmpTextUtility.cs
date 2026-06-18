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
            tmp.raycastTarget = false;

            if (lightTextOnDarkBackground)
            {
                tmp.outlineWidth = 0.24f;
                tmp.outlineColor = new Color(0.04f, 0.03f, 0.06f, 0.92f);
            }
            else
            {
                tmp.outlineWidth = 0.16f;
                tmp.outlineColor = new Color(0.98f, 0.95f, 0.88f, 0.5f);
            }
        }
    }
}
