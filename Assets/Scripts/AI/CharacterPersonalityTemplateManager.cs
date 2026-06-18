using System;
using System.IO;
using System.Reflection;
using System.Text;
using DungeonExporer.Settings;
using UnityEngine;

/// <summary>
/// Loads <c>Assets/Prompts/*.j2</c> and renders <c>{{ field }}</c> placeholders (DatingSim pattern).
/// Cap personality: <c>cap_personality.j2</c> + <c>cap_context.json</c>.
/// </summary>
public class CharacterPersonalityTemplateManager : MonoBehaviour
{
    public const string DefaultTemplateFileName = "cap_personality.j2";
    public const string DefaultContextFileName = "cap_context.json";

    [SerializeField] private string templateFileName = DefaultTemplateFileName;

    [Serializable]
    public class CapContextData
    {
        public string display_name = "Cap";
        public string role = "Veteran quest-giver and maze guide";
        public string background =
            "Cap has guided greenhorn adventurers through these humming stone corridors for longer than anyone remembers.";
        public string personality_traits =
            "- Grandfatherly warmth with a storyteller's timing\n" +
            "- Light, cosy humour — never cruel or grim\n" +
            "- Sensory imagery: torchlight, mossy stone, crimson tiles that hum\n" +
            "- Encouraging without talking down; hates sounding like a game manual";
        public string communication_style =
            "Warm, grandfatherly, lightly humorous. Speaks like a cosy fantasy storyteller — never like a tutorial.";
        public string current_mood = "welcoming and mildly amused";
        public string relationship_level = "a new adventurer at his safe-room hub";
        public string memory_block = string.Empty;
        public string quest_title = string.Empty;
        public string quest_briefing = string.Empty;
        public string world_context = string.Empty;
        public string inventory_summary = string.Empty;
        public string situation = string.Empty;
        public string interaction_scenario =
            "The adventurer has just found Cap in a mint-tinted safe room near the maze spawn.";
        public string additional_context =
            "Cap nudges, never commands. He may joke about his whiskers, stale tea, or spike strips.";
        public string player_question = string.Empty;
        public string output_rules =
            "Speak only as Cap in 2-4 short sentences. Dialogue only — no analysis or instruction recap.";
        public string world_setting =
            "A cosy first-person dungeon with safe green-tinted rooms and crimson encounter pits.";
        public string location = "Cap's hub safe room near the maze spawn.";
    }

    public string RenderCapPersonality(CapContextData characterData) =>
        RenderCapPersonality(characterData, ResolveTemplateFileName());

    public string GetCapSystemPrompt(CapContextData characterData) => RenderCapPersonality(characterData);

    public static string RenderCapPersonality(CapContextData context, string templateFile = DefaultTemplateFileName)
    {
        if (context == null)
            return string.Empty;

        if (!TryLoadTemplate(templateFile, out string templateContent))
            return string.Empty;

        return RenderTemplate(templateContent, context);
    }

    public static bool TryLoadDefaultContext(out CapContextData context, string contextFileName = DefaultContextFileName)
    {
        context = null;
        if (!TryResolvePromptsPath(contextFileName, out string contextPath))
            return false;

        try
        {
            string json = File.ReadAllText(contextPath, Encoding.UTF8);
            context = JsonUtility.FromJson<CapContextData>(json);
            return context != null;
        }
        catch (Exception ex)
        {
            Debug.LogError("CharacterPersonalityTemplateManager: failed to load context — " + ex.Message);
            return false;
        }
    }

    public static string RenderTemplate(string template, CapContextData data)
    {
        if (string.IsNullOrEmpty(template) || data == null)
            return string.Empty;

        string result = template;
        FieldInfo[] fields = typeof(CapContextData).GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            string placeholder = "{{ " + fields[i].Name + " }}";
            object value = fields[i].GetValue(data);
            result = result.Replace(placeholder, value?.ToString() ?? string.Empty);
        }

        return result.Trim();
    }

    public static bool TryLoadTemplate(string templateFileName, out string templateContent)
    {
        templateContent = string.Empty;
        if (!TryResolvePromptsPath(templateFileName, out string templatePath))
        {
            Debug.LogError("CharacterPersonalityTemplateManager: template not found — " + templateFileName);
            return false;
        }

        try
        {
            templateContent = File.ReadAllText(templatePath, Encoding.UTF8);
            return !string.IsNullOrWhiteSpace(templateContent);
        }
        catch (Exception ex)
        {
            Debug.LogError("CharacterPersonalityTemplateManager: failed to read template — " + ex.Message);
            return false;
        }
    }

    public static bool TryResolvePromptsPath(string fileName, out string fullPath)
    {
        string assetsPrompts = Path.Combine(Application.dataPath, "Prompts", fileName);
        if (File.Exists(assetsPrompts))
        {
            fullPath = assetsPrompts;
            return true;
        }

        string streaming = Path.Combine(Application.streamingAssetsPath, "Prompts", fileName);
        if (File.Exists(streaming))
        {
            fullPath = streaming;
            return true;
        }

        fullPath = null;
        return false;
    }

    public static CapContextData BuildCapContext(
        string displayName,
        string questTitle,
        string questBriefing,
        string worldContext,
        string inventorySummary,
        string memoryBlock,
        bool questActive,
        bool questCompleted,
        string playerQuestion,
        int maxSentences,
        bool reactive)
    {
        if (!TryLoadDefaultContext(out CapContextData defaults))
            defaults = new CapContextData();

        return new CapContextData
        {
            display_name = displayName ?? defaults.display_name,
            role = defaults.role,
            background = defaults.background,
            personality_traits = defaults.personality_traits,
            communication_style = defaults.communication_style,
            current_mood = ResolveMood(questActive, questCompleted),
            relationship_level = ResolveRelationshipLevel(questActive, questCompleted),
            quest_title = questTitle ?? string.Empty,
            quest_briefing = questBriefing ?? string.Empty,
            world_context = worldContext ?? string.Empty,
            inventory_summary = inventorySummary ?? string.Empty,
            memory_block = memoryBlock ?? string.Empty,
            player_question = reactive ? (playerQuestion ?? string.Empty).Trim() : string.Empty,
            situation = ResolveSituation(questActive, questCompleted),
            interaction_scenario = ResolveInteractionScenario(questActive, questCompleted, reactive),
            additional_context = defaults.additional_context,
            world_setting = defaults.world_setting,
            location = defaults.location,
            output_rules = reactive
                ? "Never repeat or quote the player's question. Answer in Cap's own words in " +
                  maxSentences + " short cosy sentences. Dialogue only — no planning or analysis."
                : "Speak only as " + (displayName ?? "Cap") + " in " +
                  (GameSettings.LlmFastMode ? Math.Min(maxSentences, 2) : maxSentences) +
                  " short sentences. Dialogue only — no analysis or instruction recap."
        };
    }

    private string ResolveTemplateFileName() =>
        string.IsNullOrWhiteSpace(templateFileName) ? DefaultTemplateFileName : templateFileName.Trim();

    private static string ResolveMood(bool questActive, bool questCompleted)
    {
        if (questCompleted)
            return "proud and warmly teasing";
        if (questActive)
            return "encouraging, keeping an eye on progress";
        return "welcoming and mildly amused";
    }

    private static string ResolveRelationshipLevel(bool questActive, bool questCompleted)
    {
        if (questCompleted)
            return "a returning adventurer who finished his errand";
        if (questActive)
            return "an adventurer he has already sent into the maze";
        return "a new adventurer at his safe-room hub";
    }

    private static string ResolveSituation(bool questActive, bool questCompleted)
    {
        if (questCompleted)
            return "The adventurer finished this quest. Offer warm banter — thanks or jokes. No new formal objectives.";
        if (questActive)
            return "The adventurer accepted your quest and is working on it. Offer a short tip; stay consistent with the briefing.";
        return "The adventurer is considering your quest. Hook them into the fantasy; do not repeat the briefing verbatim.";
    }

    private static string ResolveInteractionScenario(bool questActive, bool questCompleted, bool reactive)
    {
        if (reactive)
            return "The adventurer typed a question in the dialogue panel and waits for your in-character answer.";
        if (questCompleted)
            return "They have returned to your safe room after completing the task you set.";
        if (questActive)
            return "They stopped by again while the quest is still in progress — maybe for encouragement or a hint.";
        return "They have just opened dialogue with you near the maze spawn safe room.";
    }
}
