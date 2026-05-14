using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    [Serializable]
    public sealed class QuestDefinition
    {
        public string id;
        public string title;
        public string briefing;
        [Tooltip("Optional: shown when this quest is completed (return to NPC / journal).")]
        public string completionSummary;
        [Tooltip("If set, this quest is only offered after the named quest id is completed.")]
        public string prerequisiteQuestIdCompleted;
        public string[] objectiveEvents;
    }

    /// <summary>
    /// Tracks active and completed quests. Objectives advance when <see cref="NotifyWorldEvent"/> matches the next required event id.
    /// </summary>
    public sealed class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        private readonly Dictionary<string, QuestDefinition> _definitions = new();
        private readonly Dictionary<string, int> _activeObjectiveIndex = new();
        private readonly HashSet<string> _completed = new();

        private void Awake()
        {
            Instance = this;
            RegisterDefaultContent();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void RegisterDefaultContent()
        {
            Register(new QuestDefinition
            {
                id = "cap_training",
                title = "Cap's corridor drill",
                briefing = "Cap wants you to wallop the training dummy down the hall, then come back so they can pretend they planned it all along.",
                completionSummary = "You flattened the training dummy. Cap is grinning like a cat in a creamery — go on, let them talk your ear off.",
                objectiveEvents = new[] { "defeated_training_dummy" }
            });

            Register(new QuestDefinition
            {
                id = "echoes_in_the_dark",
                title = "Echoes in the dark",
                briefing = "Cap swears the marked encounter tiles hum when something listens back. Step onto one of those crimson floors and see if the dungeon answers.",
                completionSummary = "You stood where the floor felt wrong and lived to tell. Cap will want every detail.",
                prerequisiteQuestIdCompleted = "cap_training",
                objectiveEvents = new[] { "entered_encounter_zone" }
            });
        }

        private void Register(QuestDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                return;
            _definitions[def.id] = def;
        }

        public bool TryStartQuest(string questId)
        {
            if (!_definitions.TryGetValue(questId, out QuestDefinition def))
            {
                Debug.LogWarning($"QuestManager: unknown quest '{questId}'.");
                return false;
            }

            if (_completed.Contains(questId))
                return false;

            if (_activeObjectiveIndex.ContainsKey(questId))
                return false;

            if (!string.IsNullOrWhiteSpace(def.prerequisiteQuestIdCompleted) &&
                !_completed.Contains(def.prerequisiteQuestIdCompleted))
            {
                Debug.LogWarning($"QuestManager: prerequisite not met for '{questId}'.");
                return false;
            }

            _activeObjectiveIndex[questId] = 0;
            Debug.Log($"Quest started: {def.title}");
            return true;
        }

        public void NotifyWorldEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            var toComplete = new List<string>();
            foreach (var kv in _activeObjectiveIndex)
            {
                string questId = kv.Key;
                int idx = kv.Value;
                if (!_definitions.TryGetValue(questId, out QuestDefinition def))
                    continue;
                if (def.objectiveEvents == null || idx >= def.objectiveEvents.Length)
                    continue;
                if (def.objectiveEvents[idx] != eventId)
                    continue;

                int next = idx + 1;
                if (next >= def.objectiveEvents.Length)
                    toComplete.Add(questId);
                else
                    _activeObjectiveIndex[questId] = next;
            }

            foreach (string q in toComplete)
            {
                _activeObjectiveIndex.Remove(q);
                _completed.Add(q);
                if (_definitions.TryGetValue(q, out QuestDefinition d))
                    Debug.Log($"Quest complete: {d.title}");
            }
        }

        public bool IsQuestActive(string questId) => _activeObjectiveIndex.ContainsKey(questId);

        public bool IsQuestCompleted(string questId) => _completed.Contains(questId);

        public bool CanOfferQuest(string questId)
        {
            if (!_definitions.TryGetValue(questId, out QuestDefinition def))
                return false;
            if (IsQuestActive(questId) || IsQuestCompleted(questId))
                return false;
            if (!string.IsNullOrWhiteSpace(def.prerequisiteQuestIdCompleted) &&
                !_completed.Contains(def.prerequisiteQuestIdCompleted))
                return false;
            return true;
        }

        public bool TryGetDefinition(string questId, out QuestDefinition definition) =>
            _definitions.TryGetValue(questId, out definition);

        /// <summary>Short summary for LLM system context.</summary>
        public string BuildPromptContext()
        {
            var sb = new StringBuilder();
            foreach (var kv in _activeObjectiveIndex)
            {
                if (!_definitions.TryGetValue(kv.Key, out QuestDefinition def))
                    continue;
                sb.Append("Active quest: ").Append(def.title).Append(". ");
                if (def.objectiveEvents != null && kv.Value < def.objectiveEvents.Length)
                    sb.Append("Next objective signal: ").Append(def.objectiveEvents[kv.Value]).Append(". ");
            }

            foreach (string id in _completed)
            {
                if (_definitions.TryGetValue(id, out QuestDefinition def))
                    sb.Append("Completed quest: ").Append(def.title).Append(". ");
            }

            if (sb.Length == 0)
                sb.Append("No active quests.");
            return sb.ToString().Trim();
        }
    }
}
