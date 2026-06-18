using System;
using System.Collections;
using System.Collections.Generic;
using DungeonExporer.Dungeon;
using DungeonExporer.Settings;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Asks Ollama for JSON loot, enemy, and sign placement; <see cref="DungeonLevelBuilder"/> validates every cell.
    /// </summary>
    public static class DungeonContentPlanner
    {
        [Serializable]
        private sealed class ContentPlanResponse
        {
            public LootEntryJson[] loot;
            public EnemyEntryJson[] enemies;
            public SignEntryJson[] signs;
            public string capNote;
        }

        [Serializable]
        private sealed class LootEntryJson
        {
            public int x;
            public int y;
            public string item;
        }

        [Serializable]
        private sealed class EnemyEntryJson
        {
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class SignEntryJson
        {
            public int x;
            public int y;
            public string text;
        }

        public static IEnumerator FetchPlanCoroutine(
            OllamaHandler ollama,
            DungeonLevelBuilder dungeon,
            int maxLoot,
            int maxEnemies,
            int maxSigns,
            int minCellsFromSpawn,
            Action<DungeonContentPlan> onSuccess,
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
                Debug.LogWarning("DungeonContentPlanner: " + (resolveError ?? "model unavailable"));
                onFallback?.Invoke();
                yield break;
            }

            int lootCap = Mathf.Clamp(maxLoot, 0, 32);
            int enemyCap = Mathf.Clamp(maxEnemies, 0, 20);
            int signCap = Mathf.Clamp(maxSigns, 0, 16);
            string prompt = BuildPrompt(dungeon, lootCap, enemyCap, signCap, minCellsFromSpawn);
            bool done = false;
            string raw = null;
            string err = null;

            ollama.RequestGeneration(model, prompt,
                onSuccess: text => { raw = text; done = true; },
                onError: e => { err = e; done = true; },
                saveToDialogueJson: false,
                updateResponseUiField: false,
                maxPredictTokens: ollama.GetEffectiveNpcMaxTokens() * 3,
                disableThinking: true,
                jsonResponse: true);

            while (!done)
                yield return null;

            if (!string.IsNullOrEmpty(err))
            {
                if (!OllamaHandler.IsSupersededAbortError(err))
                    Debug.LogWarning("DungeonContentPlanner: " + err);
                onFallback?.Invoke();
                yield break;
            }

            DungeonContentPlan plan = ParseAndValidate(raw, dungeon, lootCap, enemyCap, signCap, minCellsFromSpawn);
            if (plan.Loot.Count == 0 && plan.Enemies.Count == 0 && plan.Signs.Count == 0)
            {
                Debug.LogWarning("DungeonContentPlanner: no valid entries in model output; using procedural scatter.");
                onFallback?.Invoke();
                yield break;
            }

            onSuccess?.Invoke(plan);
        }

        private static string BuildPrompt(DungeonLevelBuilder dungeon, int maxLoot, int maxEnemies, int maxSigns,
            int minFromSpawn)
        {
            return
                "You are Cap, staging loot, foes, and wooden signposts in a cosy dungeon maze.\n" +
                dungeon.BuildMazePromptBlock() + "\n" +
                "Rules:\n" +
                "- Return JSON only.\n" +
                "- Up to " + maxLoot + " loot, " + maxEnemies + " enemies, " + maxSigns + " signs.\n" +
                "- Do NOT place on P.\n" +
                "- Stay at least " + minFromSpawn + " cells (Chebyshev) from P.\n" +
                "- loot item: pebble or ration. Place on . S or E.\n" +
                "- enemies: only on E tiles.\n" +
                "- signs: only on . corridors; text is 4-12 cosy-fantasy words, no quotes.\n" +
                "Schema: {\"loot\":[{\"x\":0,\"y\":0,\"item\":\"pebble\"}],\"enemies\":[{\"x\":0,\"y\":0}]," +
                "\"signs\":[{\"x\":0,\"y\":0,\"text\":\"Mind the mossy step\"}],\"capNote\":\"one short Cap sentence\"}";
        }

        public static DungeonContentPlan ParseAndValidate(
            string raw,
            DungeonLevelBuilder dungeon,
            int maxLoot,
            int maxEnemies,
            int maxSigns,
            int minFromSpawn)
        {
            var plan = new DungeonContentPlan();
            if (string.IsNullOrWhiteSpace(raw) || dungeon == null)
                return plan;

            string json = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
                return plan;

            ContentPlanResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<ContentPlanResponse>(json);
            }
            catch (Exception)
            {
                return plan;
            }

            if (parsed == null)
                return plan;

            plan.CapNote = parsed.capNote ?? string.Empty;
            var used = new HashSet<Vector2Int>();

            if (parsed.loot != null)
            {
                int limit = Mathf.Min(parsed.loot.Length, maxLoot);
                for (int i = 0; i < limit; i++)
                {
                    LootEntryJson entry = parsed.loot[i];
                    var cell = new Vector2Int(entry.x, entry.y);
                    if (!dungeon.IsLootEligibleCell(cell, minFromSpawn))
                        continue;
                    if (!used.Add(cell))
                        continue;

                    plan.Loot.Add(new PlannedLoot
                    {
                        Cell = cell,
                        ItemId = ParseLootItem(entry.item)
                    });
                }
            }

            if (parsed.enemies != null)
            {
                int limit = Mathf.Min(parsed.enemies.Length, maxEnemies);
                for (int i = 0; i < limit; i++)
                {
                    EnemyEntryJson entry = parsed.enemies[i];
                    var cell = new Vector2Int(entry.x, entry.y);
                    if (!dungeon.IsEnemyEligibleCell(cell, minFromSpawn))
                        continue;
                    if (!used.Add(cell))
                        continue;

                    plan.Enemies.Add(new PlannedEnemy { Cell = cell });
                }
            }

            if (parsed.signs != null)
            {
                int limit = Mathf.Min(parsed.signs.Length, maxSigns);
                for (int i = 0; i < limit; i++)
                {
                    SignEntryJson entry = parsed.signs[i];
                    var cell = new Vector2Int(entry.x, entry.y);
                    string text = SanitizeSignText(entry.text);
                    if (string.IsNullOrWhiteSpace(text))
                        continue;
                    if (!dungeon.IsSignEligibleCell(cell, minFromSpawn))
                        continue;
                    if (!used.Add(cell))
                        continue;

                    plan.Signs.Add(new PlannedSign { Cell = cell, Text = text });
                }
            }

            return plan;
        }

        private static string ParseLootItem(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
                return "dungeon_pebble";

            string t = item.Trim().ToLowerInvariant();
            return t == "ration" || t == "trail_ration" ? "trail_ration" : "dungeon_pebble";
        }

        private static string SanitizeSignText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\n", " ").Replace("\r", " ").Trim();
            if (text.Length > 120)
                text = text.Substring(0, 120).TrimEnd() + "…";
            return text;
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
