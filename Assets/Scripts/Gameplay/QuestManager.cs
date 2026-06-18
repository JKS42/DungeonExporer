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
        public event Action<QuestDefinition> QuestCompleted;

        private readonly Dictionary<string, QuestDefinition> _definitions = new();
        private readonly Dictionary<string, int> _activeObjectiveIndex = new();
        private readonly HashSet<string> _completed = new();
        private readonly HashSet<string> _dynamicQuestIds = new();
        private readonly List<string> _dynamicQuestOrder = new();

        private static string NormalizeQuestId(string questId) =>
            string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();

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
                briefing = "Cap wants you to clear a few squatters from the crimson encounter pits, then come back so they can pretend they planned it all along.",
                completionSummary = "You cleared the encounter pits. Cap is grinning like a cat in a creamery — go on, let them talk your ear off.",
                objectiveEvents = new[] { "defeated_dungeon_foe" },
                objectiveHudHints = new[] { "Defeat a foe on crimson encounter floor (E), then check in with Cap." }
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

        /// <summary>Registers or replaces an AI side quest (not active/completed). Id must start with <c>ai_</c>.</summary>
        public bool TryRegisterOrReplaceDynamicQuest(QuestDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                return false;

            string questId = NormalizeQuestId(def.id);
            if (!questId.StartsWith("ai_", StringComparison.Ordinal))
                return false;

            if (IsQuestActive(questId) || IsQuestCompleted(questId))
                return false;

            def.id = questId;
            Register(def);
            if (!_dynamicQuestIds.Contains(questId))
            {
                _dynamicQuestIds.Add(questId);
                _dynamicQuestOrder.Add(questId);
            }

            QuestStateChanged?.Invoke();
            return true;
        }

        public bool IsDynamicQuest(string questId) => _dynamicQuestIds.Contains(NormalizeQuestId(questId));

        public bool TryGetFirstActiveDynamicQuestId(out string questId)
        {
            questId = null;
            for (int i = 0; i < _dynamicQuestOrder.Count; i++)
            {
                string id = _dynamicQuestOrder[i];
                if (IsQuestActive(id))
                {
                    questId = id;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNextOfferableDynamicQuestId(out string questId)
        {
            questId = null;
            for (int i = 0; i < _dynamicQuestOrder.Count; i++)
            {
                string id = _dynamicQuestOrder[i];
                if (CanOfferQuest(id))
                {
                    questId = id;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFirstCompletedDynamicQuestId(out string questId)
        {
            questId = null;
            for (int i = 0; i < _dynamicQuestOrder.Count; i++)
            {
                string id = _dynamicQuestOrder[i];
                if (IsQuestCompleted(id))
                {
                    questId = id;
                    return true;
                }
            }

            return false;
        }

        public bool TryStartQuest(string questId)
        {
            questId = NormalizeQuestId(questId);
            Debug.Log($"QuestManager.TryStartQuest: attempting to start quest '{questId}'");

            if (!_definitions.TryGetValue(questId, out QuestDefinition def))
            {
                Debug.LogWarning($"QuestManager.TryStartQuest: unknown quest '{questId}'.");
                return false;
            }

            if (_completed.Contains(questId))
            {
                Debug.LogWarning($"QuestManager.TryStartQuest: quest '{questId}' is already completed.");
                return false;
            }

            if (_activeObjectiveIndex.ContainsKey(questId))
            {
                Debug.LogWarning($"QuestManager.TryStartQuest: quest '{questId}' is already active.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(def.prerequisiteQuestIdCompleted) &&
                !_completed.Contains(NormalizeQuestId(def.prerequisiteQuestIdCompleted)))
            {
                Debug.LogWarning($"QuestManager.TryStartQuest: prerequisite not met for '{questId}'. Required prerequisite: '{def.prerequisiteQuestIdCompleted}'");
                return false;
            }

            _activeObjectiveIndex[questId] = 0;
            Debug.Log($"QuestManager.TryStartQuest: quest '{questId}' started successfully. Title: {def.title}");
            QuestStateChanged?.Invoke();
            return true;
        }

        public void NotifyWorldEvent(string eventId)
        {
            eventId = NormalizeQuestId(eventId);
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
                {
                    Debug.Log($"Quest complete: {d.title}");
                    QuestCompleted?.Invoke(d);
                }
            }

            if (stateChanged)
                QuestStateChanged?.Invoke();
        }

        public bool IsQuestActive(string questId) => _activeObjectiveIndex.ContainsKey(NormalizeQuestId(questId));

        public bool IsQuestCompleted(string questId) => _completed.Contains(NormalizeQuestId(questId));

        public bool CanOfferQuest(string questId)
        {
            questId = NormalizeQuestId(questId);

            if (!_definitions.TryGetValue(questId, out QuestDefinition def))
                return false;
            if (IsQuestActive(questId) || IsQuestCompleted(questId))
                return false;
            if (!string.IsNullOrWhiteSpace(def.prerequisiteQuestIdCompleted) &&
                !_completed.Contains(NormalizeQuestId(def.prerequisiteQuestIdCompleted)))
                return false;
            return true;
        }

        public bool TryGetDefinition(string questId, out QuestDefinition definition) =>
            _definitions.TryGetValue(NormalizeQuestId(questId), out definition);

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

            var dynamic = new List<SaveQuestDefinition>(_dynamicQuestIds.Count);
            foreach (string id in _dynamicQuestOrder)
            {
                if (!_dynamicQuestIds.Contains(id))
                    continue;
                if (!_definitions.TryGetValue(id, out QuestDefinition def))
                    continue;
                dynamic.Add(ToSaveDefinition(def));
            }

            data.dynamicQuestDefinitions = dynamic.ToArray();
        }

        private static SaveQuestDefinition ToSaveDefinition(QuestDefinition def) => new SaveQuestDefinition
        {
            id = def.id,
            title = def.title,
            briefing = def.briefing,
            completionSummary = def.completionSummary,
            prerequisiteQuestIdCompleted = def.prerequisiteQuestIdCompleted,
            objectiveEvents = def.objectiveEvents,
            objectiveHudHints = def.objectiveHudHints
        };

        private void RegisterDynamicFromSave(SaveQuestDefinition saved)
        {
            if (string.IsNullOrWhiteSpace(saved.id))
                return;

            string questId = NormalizeQuestId(saved.id);
            if (!questId.StartsWith("ai_", StringComparison.Ordinal))
                return;

            var def = new QuestDefinition
            {
                id = questId,
                title = saved.title,
                briefing = saved.briefing,
                completionSummary = saved.completionSummary,
                prerequisiteQuestIdCompleted = saved.prerequisiteQuestIdCompleted,
                objectiveEvents = saved.objectiveEvents,
                objectiveHudHints = saved.objectiveHudHints
            };

            Register(def);
            if (!_dynamicQuestIds.Contains(questId))
            {
                _dynamicQuestIds.Add(questId);
                _dynamicQuestOrder.Add(questId);
            }
        }

        public void ApplyFromSave(GameSaveData data)
        {
            if (data == null)
                return;

            if (data.dynamicQuestDefinitions != null)
            {
                for (int i = 0; i < data.dynamicQuestDefinitions.Length; i++)
                    RegisterDynamicFromSave(data.dynamicQuestDefinitions[i]);
            }

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
                    string questId = NormalizeQuestId(a.questId);
                    if (string.IsNullOrWhiteSpace(questId))
                        continue;
                    if (!_definitions.ContainsKey(questId))
                        continue;
                    _activeObjectiveIndex[questId] = Mathf.Max(0, a.objectiveIndex);
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
