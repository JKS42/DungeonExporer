using System.Collections.Generic;
using UnityEngine;

namespace DungeonExporer.AI
{
    /// <summary>
    /// Renders <c>Resources/Prompts/cap_personality.jinja2</c> for Cap's Ollama dialogue.
    /// </summary>
    public static class CapPersonalityPromptBuilder
    {
        private const string ResourcePath = "Prompts/cap_personality";
        private static string _cachedTemplate;

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
            return Render(new Dictionary<string, string>
            {
                ["display_name"] = displayName ?? "Cap",
                ["mode"] = "voice",
                ["quest_state"] = ResolveQuestState(questActive, questCompleted),
                ["quest_title"] = questTitle ?? string.Empty,
                ["quest_briefing"] = questBriefing ?? string.Empty,
                ["world_context"] = worldContext ?? string.Empty,
                ["inventory_summary"] = inventorySummary ?? string.Empty,
                ["memory_block"] = memoryBlock ?? string.Empty,
                ["max_sentences"] = maxSentences.ToString()
            });
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
            bool questCompleted,
            int maxSentences = 3)
        {
            return Render(new Dictionary<string, string>
            {
                ["display_name"] = displayName ?? "Cap",
                ["mode"] = "reactive",
                ["quest_state"] = ResolveQuestState(questActive, questCompleted),
                ["quest_title"] = questTitle ?? string.Empty,
                ["quest_briefing"] = questBriefing ?? string.Empty,
                ["world_context"] = worldContext ?? string.Empty,
                ["inventory_summary"] = inventorySummary ?? string.Empty,
                ["memory_block"] = memoryBlock ?? string.Empty,
                ["player_question"] = playerQuestion ?? string.Empty,
                ["max_sentences"] = maxSentences.ToString()
            });
        }

        private static string ResolveQuestState(bool questActive, bool questCompleted)
        {
            if (questCompleted)
                return "completed";
            if (questActive)
                return "active";
            return "considering";
        }

        private static string Render(IReadOnlyDictionary<string, string> context)
        {
            string template = LoadTemplate();
            return CapJinja2PromptRenderer.Render(template, context);
        }

        private static string LoadTemplate()
        {
            if (!string.IsNullOrEmpty(_cachedTemplate))
                return _cachedTemplate;

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                _cachedTemplate = asset.text;
                return _cachedTemplate;
            }

            Debug.LogWarning(
                "CapPersonalityPromptBuilder: missing Resources/Prompts/cap_personality.jinja2 — using embedded fallback.");
            _cachedTemplate = GetEmbeddedFallbackTemplate();
            return _cachedTemplate;
        }

        private static string GetEmbeddedFallbackTemplate()
        {
            return
                "You are {{ display_name | default('Cap') }}, a cosy dungeon guide.\n\n" +
                "Personality — {{ display_name | default('Cap') }}:\n" +
                "- Veteran dungeon guide; warm, lightly humorous, cosy fantasy tone.\n\n" +
                "Quest context: {{ quest_title }}. {{ quest_briefing }}\n" +
                "{{ world_context }}\n" +
                "{{ inventory_summary }}\n\n" +
                "{% if mode == 'reactive' %}" +
                "Player question: \"{{ player_question | default('') | trim }}\"\n" +
                "Speak only as {{ display_name | default('Cap') }} in {{ max_sentences | default(3) }} short sentences.\n" +
                "{% else %}" +
                "Speak only as {{ display_name | default('Cap') }} in {{ max_sentences | default(4) }} short sentences.\n" +
                "{% endif %}" +
                "Dialogue only — no analysis or recap.\n" +
                "{{ display_name | default('Cap') }}: \"";
        }
    }
}
