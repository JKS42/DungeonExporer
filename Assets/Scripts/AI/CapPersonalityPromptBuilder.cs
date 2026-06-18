namespace DungeonExporer.AI
{
    /// <summary>
    /// Builds Cap voice and Ask Cap prompts via <see cref="CharacterPersonalityTemplateManager"/> (DatingSim pattern).
    /// </summary>
    public static class CapPersonalityPromptBuilder
    {
        public static string BuildVoicePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            bool questActive,
            bool questCompleted)
            => BuildVoicePrompt(displayName, questTitle, questBriefing, worldContext, inventorySummary,
                memoryBlock, questActive, questCompleted, DungeonExporer.Settings.LlmPerformanceProfile.VoiceMaxSentences);

        public static string BuildVoicePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            bool questActive,
            bool questCompleted,
            int maxSentences = 4)
        {
            var context = CharacterPersonalityTemplateManager.BuildCapContext(
                displayName,
                questTitle,
                questBriefing,
                worldContext,
                inventorySummary,
                memoryBlock,
                questActive,
                questCompleted,
                playerQuestion: null,
                maxSentences,
                reactive: false);

            return CharacterPersonalityTemplateManager.RenderCapPersonality(context);
        }

        public static string BuildReactivePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            string playerQuestion,
            bool questActive,
            bool questCompleted)
            => BuildReactivePrompt(displayName, questTitle, questBriefing, worldContext, inventorySummary,
                memoryBlock, playerQuestion, questActive, questCompleted,
                DungeonExporer.Settings.LlmPerformanceProfile.ReactiveMaxSentences);

        public static string BuildReactivePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            string playerQuestion,
            bool questActive,
            bool questCompleted,
            int maxSentences = 3)
        {
            var context = CharacterPersonalityTemplateManager.BuildCapContext(
                displayName,
                questTitle,
                questBriefing,
                worldContext,
                inventorySummary,
                memoryBlock,
                questActive,
                questCompleted,
                playerQuestion,
                maxSentences,
                reactive: true);

            return CharacterPersonalityTemplateManager.RenderCapPersonality(context);
        }
    }
}
