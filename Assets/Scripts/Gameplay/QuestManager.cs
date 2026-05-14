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
        [Tooltip("Optional HUD / player hint per objective index (parallel to objectiveEvents).")]
        public string[] objectiveHudHints;
    }

    /// <summary>
    /// Tracks active and completed quests. Objectives advance when <see cref="NotifyWorldEvent"/> matches the next required event id.
    /// </summary>
    public sealed class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public event Action QuestStateChanged;

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
                objectiveEvents = new[] { "defeated_training_dummy" },
                objectiveHudHints = new[] { "Defeat the training dummy down the hall, then check in with Cap." }
            });

            Register(new QuestDefinition
            {
                id = "echoes_in_the_dark",
                title = "Echoes in the dark",
                briefing = "Cap swears the marked encounter tiles hum when something listens back. Step onto one of those crimson floors and see if the dungeon answers.",
                completionSummary = "You stood where the floor felt wrong and lived to tell. Cap will want every detail.",
                prerequisiteQuestIdCompleted = "cap_training",
                objectiveEvents = new[] { "entered_encounter_zone" },
                objectiveHudHints = new[] { "Stand on a crimson encounter tile until the floor \"answers\"." }
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
            QuestStateChanged?.Invoke();
            return true;
        }

        public void NotifyWorldEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            var toComplete = new List<string>();
            bool stateChanged = false;
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
                {
                    toComplete.Add(questId);
                    stateChanged = true;
                }
                else
                {
                    _activeObjectiveIndex[questId] = next;
                    stateChanged = true;
                }
            }

            foreach (string q in toComplete)
            {
                _activeObjectiveIndex.Remove(q);
                _completed.Add(q);
                if (_definitions.TryGetValue(q, out QuestDefinition d))
                    Debug.Log($"Quest complete: {d.title}");
            }

            if (stateChanged)
                QuestStateChanged?.Invoke();
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

        public bool TryGetPrimaryObjectiveHudLine(out string line)
        {
            line = null;
            foreach (var kv in _activeObjectiveIndex)
            {
                if (!_definitions.TryGetValue(kv.Key, out QuestDefinition def))
                    continue;

                int idx = kv.Value;
                string hint = string.Empty;
                if (def.objectiveHudHints != null && idx < def.objectiveHudHints.Length &&
                    !string.IsNullOrWhiteSpace(def.objectiveHudHints[idx]))
                {
                    hint = def.objectiveHudHints[idx].Trim();
                }
                else if (def.objectiveEvents != null && idx < def.objectiveEvents.Length)
                    hint = "Objective signal: " + def.objectiveEvents[idx];

                line = "Next: " + def.title + " — " + hint;
                return true;
            }

            return false;
        }

        public void ExportToSave(GameSaveData data)
        {
            if (data == null)
                return;

            var completed = new string[_completed.Count];
            int i = 0;
            foreach (string id in _completed)
                completed[i++] = id;
            data.completedQuestIds = completed;

            var active = new SaveQuestProgress[_activeObjectiveIndex.Count];
            i = 0;
            foreach (var kv in _activeObjectiveIndex)
            {
                active[i++] = new SaveQuestProgress
                {
                    questId = kv.Key,
                    objectiveIndex = kv.Value
                };
            }

            data.activeQuests = active;
        }

        public void ApplyFromSave(GameSaveData data)
        {
            if (data == null)
                return;

            _completed.Clear();
            if (data.completedQuestIds != null)
            {
                foreach (string id in data.completedQuestIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        _completed.Add(id.Trim());
                }
            }

            _activeObjectiveIndex.Clear();
            if (data.activeQuests != null)
            {
                foreach (SaveQuestProgress a in data.activeQuests)
                {
                    if (string.IsNullOrWhiteSpace(a.questId))
                        continue;
                    if (!_definitions.ContainsKey(a.questId.Trim()))
                        continue;
                    _activeObjectiveIndex[a.questId.Trim()] = Mathf.Max(0, a.objectiveIndex);
                }
            }

            QuestStateChanged?.Invoke();
        }

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
