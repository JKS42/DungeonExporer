using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Keeps the last few assistant replies per NPC id so prompts can ask the model to vary wording.
    /// </summary>
    public static class NpcConversationMemory
    {
        private const int MaxSnippetsPerNpc = 6;
        private const int MaxSnippetChars = 220;
        private static readonly Dictionary<string, List<string>> _lastReplies = new();

        public static string BuildPromptBlock(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || !_lastReplies.TryGetValue(npcId, out List<string> list) || list.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("Lines you already spoke recently (do not repeat the same joke or phrasing; build on them): ");
            for (int i = 0; i < list.Count; i++)
                sb.Append('[').Append(i + 1).Append("] ").Append(list[i]).Append(' ');
            return sb.ToString().TrimEnd();
        }

        public static void AppendAssistantReply(string npcId, string rawReply)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return;

            string cleaned = OllamaHandler.SanitizeModelOutput(rawReply ?? string.Empty).Trim();
            if (cleaned.Length == 0)
                return;

            if (cleaned.Length > MaxSnippetChars)
                cleaned = cleaned.Substring(0, MaxSnippetChars) + "…";

            if (!_lastReplies.TryGetValue(npcId, out List<string> list))
            {
                list = new List<string>(MaxSnippetsPerNpc);
                _lastReplies[npcId] = list;
            }

            list.Add(cleaned);
            while (list.Count > MaxSnippetsPerNpc)
                list.RemoveAt(0);
        }
    }
}
