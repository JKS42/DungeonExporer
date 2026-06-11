using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Keeps recent player questions and assistant replies per NPC id for reactive dialogue prompts.
    /// </summary>
    public static class NpcConversationMemory
    {
        private const int MaxTurnsPerNpc = 8;
        private const int MaxSnippetChars = 220;

        private sealed class Turn
        {
            public bool IsPlayer;
            public string Text;
        }

        private static readonly Dictionary<string, List<Turn>> _history = new();

        public static string BuildPromptBlock(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || !_history.TryGetValue(npcId, out List<Turn> list) || list.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Prior chat:");
            for (int i = 0; i < list.Count; i++)
            {
                Turn turn = list[i];
                sb.Append(turn.IsPlayer ? "Player: " : "You: ");
                sb.AppendLine(turn.Text);
            }

            return sb.ToString().TrimEnd();
        }

        public static void AppendUserMessage(string npcId, string rawMessage)
        {
            AppendTurn(npcId, true, rawMessage);
        }

        public static void AppendAssistantReply(string npcId, string rawReply)
        {
            AppendTurn(npcId, false, rawReply);
        }

        /// <summary>Overwrites the latest assistant turn, or appends if none exists (voice re-rolls).</summary>
        public static void ReplaceAssistantReply(string npcId, string rawReply)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return;

            string cleaned = OllamaHandler.SanitizeModelOutput(rawReply ?? string.Empty).Trim();
            cleaned = OllamaHandler.ExtractNpcSpokenDialogue(cleaned);
            if (cleaned.Length == 0 || OllamaHandler.IsNpcMetaPlanningLine(cleaned))
                return;

            if (cleaned.Length > MaxSnippetChars)
                cleaned = cleaned.Substring(0, MaxSnippetChars) + "…";

            if (!_history.TryGetValue(npcId, out List<Turn> list) || list.Count == 0)
            {
                AppendAssistantReply(npcId, cleaned);
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].IsPlayer)
                {
                    list[i].Text = cleaned;
                    return;
                }
            }

            list.Add(new Turn { IsPlayer = false, Text = cleaned });
            while (list.Count > MaxTurnsPerNpc)
                list.RemoveAt(0);
        }

        private static void AppendTurn(string npcId, bool isPlayer, string raw)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return;

            string cleaned = OllamaHandler.SanitizeModelOutput(raw ?? string.Empty).Trim();
            if (!isPlayer)
                cleaned = OllamaHandler.ExtractNpcSpokenDialogue(cleaned);
            if (cleaned.Length == 0 || (!isPlayer && OllamaHandler.IsNpcMetaPlanningLine(cleaned)))
                return;

            if (cleaned.Length > MaxSnippetChars)
                cleaned = cleaned.Substring(0, MaxSnippetChars) + "…";

            if (!_history.TryGetValue(npcId, out List<Turn> list))
            {
                list = new List<Turn>(MaxTurnsPerNpc);
                _history[npcId] = list;
            }

            list.Add(new Turn { IsPlayer = isPlayer, Text = cleaned });
            while (list.Count > MaxTurnsPerNpc)
                list.RemoveAt(0);
        }
    }
}
