using System.Collections.Generic;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// In-memory cache for pre-generated NPC voice lines (keyed by NPC + quest state).
    /// </summary>
    public static class NpcDialogueCache
    {
        private sealed class Entry
        {
            public string Text;
        }

        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>(8);
        private static readonly HashSet<string> InFlight = new HashSet<string>();

        public static string BuildKey(string npcConversationId, string questId, string stateSignature)
        {
            return (npcConversationId ?? "npc") + "|" + (questId ?? string.Empty) + "|" + (stateSignature ?? string.Empty);
        }

        public static bool TryGet(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key) || !Cache.TryGetValue(key, out Entry entry))
                return false;
            if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                return false;
            text = entry.Text;
            return true;
        }

        public static void Put(string key, string text)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(text))
                return;
            Cache[key] = new Entry { Text = text.Trim() };
        }

        public static void Invalidate(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            Cache.Remove(key);
        }

        /// <summary>Returns false if another caller is already fetching this key.</summary>
        public static bool TryBeginFetch(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (InFlight.Contains(key))
                return false;
            InFlight.Add(key);
            return true;
        }

        public static void EndFetch(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            InFlight.Remove(key);
        }

        public static bool IsFetching(string key) =>
            !string.IsNullOrEmpty(key) && InFlight.Contains(key);
    }
}
