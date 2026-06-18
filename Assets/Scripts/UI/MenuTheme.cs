using UnityEngine;

namespace DungeonExporer.UI
{
    /// <summary>
    /// Centralised palette + typography for the menu UI.
    /// Tone: lighthearted fantasy — warm parchment, sun-gold accents, friendly greens.
    /// Adjust here to re-skin the whole menu in one place.
    /// </summary>
    public static class MenuTheme
    {
        // Background: deep dusk-purple → twilight blue, evoking a cosy tavern at night.
        public static readonly Color BackgroundTop = new(0.20f, 0.16f, 0.30f, 1f);
        public static readonly Color BackgroundBottom = new(0.10f, 0.08f, 0.18f, 1f);

        // Panels: warm parchment.
        public static readonly Color Panel = new(0.97f, 0.92f, 0.79f, 0.96f);
        public static readonly Color PanelBorder = new(0.55f, 0.38f, 0.20f, 1f);

        // Buttons: sun-gold primary, mossy-green secondary, brick-red destructive.
        public static readonly Color ButtonPrimary = new(0.96f, 0.74f, 0.30f, 1f);
        public static readonly Color ButtonPrimaryHover = new(1.00f, 0.83f, 0.43f, 1f);
        public static readonly Color ButtonSecondary = new(0.50f, 0.65f, 0.38f, 1f);
        public static readonly Color ButtonSecondaryHover = new(0.62f, 0.78f, 0.48f, 1f);
        public static readonly Color ButtonDanger = new(0.78f, 0.32f, 0.28f, 1f);
        public static readonly Color ButtonDangerHover = new(0.90f, 0.42f, 0.36f, 1f);

        // Text.
        public static readonly Color TitleText = new(1.00f, 0.91f, 0.62f, 1f);   // candlelight
        public static readonly Color SubtitleText = new(0.85f, 0.78f, 0.62f, 1f);
        public static readonly Color BodyText = new(0.22f, 0.14f, 0.08f, 1f);    // dark cocoa on parchment
        public static readonly Color ButtonText = new(0.22f, 0.14f, 0.08f, 1f);

        // Typography (point sizes, used with TMP).
        public const float TitleFontSize = 96f;
        public const float SubtitleFontSize = 30f;
        public const float ButtonFontSize = 34f;
        public const float BodyFontSize = 26f;
        public const float CaptionFontSize = 20f;
        public const float HudFontSize = 26f;
        public const float HudSmallFontSize = 22f;

        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        // Layout.
        public const float PanelPadding = 32f;
        public const float ButtonSpacing = 16f;
        public const float ButtonHeight = 64f;
        public const float ButtonMinWidth = 280f;

        public const string GameTitle = "Dungeon Exporer";
        public const string GameTagline = "A cosy crawl through whimsical depths";

        public static string BuildHowToPlayText()
        {
            return
                "MOVEMENT\n" +
                "Move — WASD\n" +
                "Look — Mouse\n" +
                "Jump — Space\n" +
                "Sprint — Left Shift\n" +
                "Crouch — C\n\n" +
                "COMBAT & INTERACTION\n" +
                "Attack — Left click or Enter\n" +
                "Interact — E (talk to Cap, use prompts)\n" +
                "Inventory — I\n\n" +
                "MENUS & SAVE\n" +
                "Pause — Escape\n" +
                "Save session — F5\n" +
                "Load session — F9\n\n" +
                "TIPS\n" +
                "Find Cap in a safe room (S) near spawn to accept your first quest.\n" +
                "Red hazard plates are spike traps — jump over them when you can.\n" +
                "Install Ollama for AI dialogue and dungeon flavor (optional).";
        }
    }
}
