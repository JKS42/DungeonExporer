namespace DungeonExporer.Settings
{
    /// <summary>Central fast-mode tuning for Cap dialogue prompts (token caps live on <see cref="OllamaHandler"/>).</summary>
    public static class LlmPerformanceProfile
    {
        public static bool IsFastMode => GameSettings.LlmFastMode;

        public static int VoiceMaxSentences => IsFastMode ? 2 : 4;

        public static int ReactiveMaxSentences => IsFastMode ? 2 : 3;

        public static int MemoryTurnsInPrompt => IsFastMode ? 3 : 8;
    }
}
