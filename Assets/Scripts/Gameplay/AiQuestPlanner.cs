using System;
using System.Collections;
using System.Collections.Generic;
using DungeonExporer.Dungeon;
using DungeonExporer.Settings;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Asks Ollama for cosy side-quest JSON; C# validates objective ids and registers quests on <see cref="QuestManager"/>.
    /// </summary>
    public static class AiQuestPlanner
    {
        public const string SideQuestAId = "ai_cap_side_a";
        public const string SideQuestBId = "ai_cap_side_b";

        private static readonly string[] SlotIds = { SideQuestAId, SideQuestBId };

        [Serializable]
        private sealed class QuestPlanResponse
        {
            public QuestEntryJson[] quests;
            public string capNote;
        }

        [Serializable]
        private sealed class QuestEntryJson
        {
            public string id;
            public string title;
            public string briefing;
            public string completionSummary;
            public string prerequisiteQuestIdCompleted;
            public string[] objectiveEvents;
            public string[] objectiveHudHints;
        }

        public static void RegisterFallbackQuests(QuestManager manager)
        {
            if (manager == null)
                return;

            TryRegisterSlot(manager, SideQuestAId, BuildFallbackSideA());
            TryRegisterSlot(manager, SideQuestBId, BuildFallbackSideB());
        }

        public static IEnumerator FetchQuestsCoroutine(
            OllamaHandler ollama,
            QuestManager manager,
            Action<int> onSuccess,
            Action onFallback)
        {
            if (manager == null)
            {
                onFallback?.Invoke();
                yield break;
            }

            RegisterFallbackQuests(manager);

            if (ollama == null || !GameSettings.LlmEnabled)
            {
                onFallback?.Invoke();
                yield break;
            }

            string model = null;
            string resolveError = null;
            yield return ollama.ResolveModelTagCoroutine(ollama.GetPreferredModelName(),
                resolved => model = resolved,
                err => resolveError = err);

            if (string.IsNullOrEmpty(model))
            {
                Debug.LogWarning("AiQuestPlanner: " + (resolveError ?? "model unavailable"));
                onFallback?.Invoke();
                yield break;
            }

            bool done = false;
            string raw = null;
            string err = null;

            ollama.RequestGeneration(model, BuildPrompt(),
                onSuccess: text => { raw = text; done = true; },
                onError: e => { err = e; done = true; },
                saveToDialogueJson: false,
                updateResponseUiField: false,
                maxPredictTokens: ollama.GetEffectiveNpcMaxTokens() * 4,
                disableThinking: true,
                jsonResponse: true);

            while (!done)
                yield return null;

            if (!string.IsNullOrEmpty(err))
            {
                if (!OllamaHandler.IsSupersededAbortError(err))
                    Debug.LogWarning("AiQuestPlanner: " + err);
                onFallback?.Invoke();
                yield break;
            }

            int registered = ParseAndRegister(raw, manager, out string capNote);
            if (registered == 0)
            {
                Debug.LogWarning("AiQuestPlanner: no valid quests in model output; using fallback side quests.");
                onFallback?.Invoke();
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(capNote))
                DungeonFlavorHudBridge.PublishFlavorToast?.Invoke(capNote.Trim(), 6f);

            onSuccess?.Invoke(registered);
        }

        private static string BuildPrompt()
        {
            return
                "You are Cap, inventing optional cosy side errands for a first-person dungeon maze.\n" +
                "Return JSON only.\n" +
                "Create exactly TWO side quests using these fixed ids (do not invent other ids):\n" +
                "- ai_cap_side_a\n" +
                "- ai_cap_side_b\n" +
                "Rules:\n" +
                "- Lighthearted cosy fantasy tone; no horror.\n" +
                "- Each quest has 1-3 objectives chosen ONLY from this list (repeat an id for multi-step, e.g. two pebble pickups):\n" +
                QuestWorldEvents.BuildCatalogForPrompt() + "\n" +
                "- prerequisiteQuestIdCompleted must be \"cap_training\" or \"echoes_in_the_dark\" (player must finish Cap's main errands first).\n" +
                "- title: 3-8 words. briefing + completionSummary: 1-2 short sentences each.\n" +
                "- objectiveHudHints: one short player hint per objective (parallel array).\n" +
                "Schema: {\"quests\":[{\"id\":\"ai_cap_side_a\",\"title\":\"...\",\"briefing\":\"...\",\"completionSummary\":\"...\"," +
                "\"prerequisiteQuestIdCompleted\":\"cap_training\",\"objectiveEvents\":[\"collected_pebble\"]," +
                "\"objectiveHudHints\":[\"Pick up a wobbly pebble in the maze.\"]}],\"capNote\":\"one short Cap toast when quests are ready\"}";
        }

        private static int ParseAndRegister(string raw, QuestManager manager, out string capNote)
        {
            capNote = string.Empty;
            if (string.IsNullOrWhiteSpace(raw) || manager == null)
                return 0;

            string json = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
                return 0;

            QuestPlanResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<QuestPlanResponse>(json);
            }
            catch (Exception)
            {
                return 0;
            }

            if (parsed == null || parsed.quests == null || parsed.quests.Length == 0)
                return 0;

            capNote = parsed.capNote ?? string.Empty;
            int registered = 0;
            for (int i = 0; i < parsed.quests.Length && i < SlotIds.Length; i++)
            {
                QuestDefinition def = ValidateEntry(parsed.quests[i], SlotIds[i]);
                if (def == null)
                    continue;

                if (manager.TryRegisterOrReplaceDynamicQuest(def))
                    registered++;
            }

            return registered;
        }

        private static QuestDefinition ValidateEntry(QuestEntryJson entry, string slotId)
        {
            if (entry == null)
                return null;

            string title = SanitizeLine(entry.title, 80);
            string briefing = SanitizeLine(entry.briefing, 280);
            string completion = SanitizeLine(entry.completionSummary, 280);
            if (title.Length == 0 || briefing.Length == 0)
                return null;

            if (completion.Length == 0)
                completion = "Cap pretends they planned your victory all along.";

            string[] events = ValidateObjectiveEvents(entry.objectiveEvents);
            if (events == null || events.Length == 0)
                return null;

            string[] hints = ValidateHints(entry.objectiveHudHints, events.Length);

            string prerequisite = NormalizePrerequisite(entry.prerequisiteQuestIdCompleted);

            return new QuestDefinition
            {
                id = slotId,
                title = title,
                briefing = briefing,
                completionSummary = completion,
                prerequisiteQuestIdCompleted = prerequisite,
                objectiveEvents = events,
                objectiveHudHints = hints
            };
        }

        private static string[] ValidateObjectiveEvents(string[] raw)
        {
            if (raw == null || raw.Length == 0)
                return null;

            var kept = new List<string>(4);
            for (int i = 0; i < raw.Length && kept.Count < 4; i++)
            {
                string id = raw[i]?.Trim();
                if (!QuestWorldEvents.IsAllowed(id))
                    continue;
                kept.Add(id);
            }

            return kept.Count == 0 ? null : kept.ToArray();
        }

        private static string[] ValidateHints(string[] raw, int objectiveCount)
        {
            var hints = new string[objectiveCount];
            for (int i = 0; i < objectiveCount; i++)
            {
                string hint = raw != null && i < raw.Length ? SanitizeLine(raw[i], 120) : string.Empty;
                hints[i] = hint.Length > 0 ? hint : "Explore the maze and follow Cap's errand.";
            }

            return hints;
        }

        private static string NormalizePrerequisite(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "cap_training";

            string t = raw.Trim();
            return t == "echoes_in_the_dark" ? t : "cap_training";
        }

        private static string SanitizeLine(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length > maxLen)
                text = text.Substring(0, maxLen).TrimEnd() + "…";
            return text;
        }

        private static void TryRegisterSlot(QuestManager manager, string slotId, QuestDefinition def)
        {
            if (manager.TryGetDefinition(slotId, out _))
                return;
            manager.TryRegisterOrReplaceDynamicQuest(def);
        }

        private static QuestDefinition BuildFallbackSideA() => new QuestDefinition
        {
            id = SideQuestAId,
            title = "Cap's pebble patrol",
            briefing = "Cap swears the corridor pebbles are \"morale talismans.\" Collect two before they change their mind.",
            completionSummary = "Your pockets jingle with dubious luck. Cap looks unbearably pleased.",
            prerequisiteQuestIdCompleted = "cap_training",
            objectiveEvents = new[] { QuestWorldEvents.CollectedPebble, QuestWorldEvents.CollectedPebble },
            objectiveHudHints = new[]
            {
                "Pick up a wobbly pebble loot bubble.",
                "Find another pebble somewhere in the maze."
            }
        };

        private static QuestDefinition BuildFallbackSideB() => new QuestDefinition
        {
            id = SideQuestBId,
            title = "Ration and rumble",
            briefing = "Grab a trail ration for the road, then wallop a squatter on crimson floor so Cap can gossip about your form.",
            completionSummary = "Fed, fought, and fabulous. Cap is already drafting your legend.",
            prerequisiteQuestIdCompleted = "echoes_in_the_dark",
            objectiveEvents = new[] { QuestWorldEvents.CollectedTrailRation, QuestWorldEvents.DefeatedDungeonFoe },
            objectiveHudHints = new[]
            {
                "Pick up a trail ration (green loot bubble).",
                "Defeat a foe on a crimson encounter (E) floor."
            }
        };

        private static string ExtractJsonObject(string text)
        {
            text = text.Trim();
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                return string.Empty;
            return text.Substring(start, end - start + 1);
        }
    }
}
