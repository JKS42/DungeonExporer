using System;
using System.Collections;
using System.Collections.Generic;
using DungeonExporer.Dungeon;
using DungeonExporer.Settings;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Asks Ollama for a JSON trap layout; <see cref="DungeonLevelBuilder"/> validates every cell before spawn.
    /// </summary>
    public static class DungeonTrapPlanner
    {
        [Serializable]
        private sealed class TrapPlanResponse
        {
            public TrapEntryJson[] traps;
            public string capNote;
        }

        [Serializable]
        private sealed class TrapEntryJson
        {
            public int x;
            public int y;
            public string type;
        }

        public static IEnumerator FetchPlanCoroutine(
            OllamaHandler ollama,
            DungeonLevelBuilder dungeon,
            int maxTraps,
            int minCellsFromSpawn,
            Action<DungeonTrapPlan> onSuccess,
            Action onFallback)
        {
            if (ollama == null || dungeon == null || !GameSettings.LlmEnabled)
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
                Debug.LogWarning("DungeonTrapPlanner: " + (resolveError ?? "model unavailable"));
                onFallback?.Invoke();
                yield break;
            }

            int cap = Mathf.Clamp(maxTraps, 1, 24);
            string prompt = BuildPrompt(dungeon, cap, minCellsFromSpawn);
            bool done = false;
            string raw = null;
            string err = null;

            ollama.RequestGeneration(model, prompt,
                onSuccess: text => { raw = text; done = true; },
                onError: e => { err = e; done = true; },
                saveToDialogueJson: false,
                updateResponseUiField: false,
                maxPredictTokens: ollama.GetEffectiveNpcMaxTokens() * 2,
                disableThinking: true,
                jsonResponse: true);

            while (!done)
                yield return null;

            if (!string.IsNullOrEmpty(err))
            {
                if (!OllamaHandler.IsSupersededAbortError(err))
                    Debug.LogWarning("DungeonTrapPlanner: " + err);
                onFallback?.Invoke();
                yield break;
            }

            DungeonTrapPlan plan = ParseAndValidate(raw, dungeon, cap, minCellsFromSpawn);
            if (plan.Traps.Count == 0)
            {
                Debug.LogWarning("DungeonTrapPlanner: no valid traps in model output; using procedural scatter.");
                onFallback?.Invoke();
                yield break;
            }

            onSuccess?.Invoke(plan);
        }

        private static string BuildPrompt(DungeonLevelBuilder dungeon, int maxTraps, int minFromSpawn)
        {
            return
                "You are Cap, a mischievous NPC who booby-trapped a dungeon maze.\n" +
                "Given the ASCII map below, choose trap locations.\n" +
                dungeon.BuildMazePromptBlock() + "\n" +
                "Rules:\n" +
                "- Return JSON only.\n" +
                "- Up to " + maxTraps + " traps.\n" +
                "- Do NOT place on P or S.\n" +
                "- Stay at least " + minFromSpawn + " cells (Chebyshev) from P.\n" +
                "- Prefer corridors (.) and tiles adjacent to E.\n" +
                "- type must be one of: spike, ember, slime.\n" +
                "Schema: {\"traps\":[{\"x\":0,\"y\":0,\"type\":\"spike\"}],\"capNote\":\"one short in-character sentence from Cap\"}";
        }

        public static DungeonTrapPlan ParseAndValidate(string raw, DungeonLevelBuilder dungeon, int maxTraps, int minFromSpawn)
        {
            var plan = new DungeonTrapPlan();
            if (string.IsNullOrWhiteSpace(raw) || dungeon == null)
                return plan;

            string json = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
                return plan;

            TrapPlanResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<TrapPlanResponse>(json);
            }
            catch (Exception)
            {
                return plan;
            }

            if (parsed == null)
                return plan;

            plan.CapNote = parsed.capNote ?? string.Empty;
            if (parsed.traps == null || parsed.traps.Length == 0)
                return plan;

            var used = new HashSet<Vector2Int>();
            int limit = Mathf.Min(parsed.traps.Length, maxTraps);
            for (int i = 0; i < limit; i++)
            {
                TrapEntryJson entry = parsed.traps[i];
                var cell = new Vector2Int(entry.x, entry.y);
                if (!dungeon.IsTrapEligibleCell(cell, minFromSpawn))
                    continue;
                if (!used.Add(cell))
                    continue;

                plan.Traps.Add(new PlannedTrap
                {
                    Cell = cell,
                    Type = ParseTrapType(entry.type)
                });
            }

            return plan;
        }

        private static DungeonTrapType ParseTrapType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return DungeonTrapType.Spike;

            string t = type.Trim().ToLowerInvariant();
            if (t == "ember")
                return DungeonTrapType.Ember;
            if (t == "slime")
                return DungeonTrapType.Slime;
            return DungeonTrapType.Spike;
        }

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
