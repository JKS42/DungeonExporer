using System;
using System.Collections.Generic;
using DungeonExporer.Dungeon;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Spawns pickups and spike hazards on walkable maze cells from <see cref="DungeonLevelBuilder"/>.
    /// </summary>
    public static class DungeonLootScatter
    {
        public struct ScatterConfig
        {
            public int pebbleCount;
            public int rationCount;
            public int spikeTrapCount;
            public int encounterEnemyCount;
            public int minCellsFromSpawn;
            public int randomSeed;
            public float pickupHeight;
            public float hazardHeight;
            public float enemyHeight;
            public bool preferCorridorsForHazards;
        }

        public static void Scatter(
            DungeonLevelBuilder dungeon,
            Transform parent,
            ScatterConfig config,
            Action<Vector3, string, string, int, float, Color> spawnPickup,
            Action<Vector3> spawnHazard,
            Action<Vector3> spawnEnemy = null)
        {
            if (dungeon == null || parent == null || spawnPickup == null || spawnHazard == null)
                return;

            if (dungeon.WalkableCells == null || dungeon.WalkableCells.Count == 0)
            {
                Debug.LogWarning("DungeonLootScatter: no walkable cells; maze may not be built yet.");
                return;
            }

            int seed = config.randomSeed != 0 ? config.randomSeed : Environment.TickCount;
            var rng = new System.Random(seed);
            var reserved = new HashSet<Vector2Int>();
            int minDist = Mathf.Max(2, config.minCellsFromSpawn);

            int pebbles = Mathf.Max(0, config.pebbleCount);
            for (int i = 0; i < pebbles; i++)
            {
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, null, out Vector3 pos))
                    break;
                pos.y = config.pickupHeight;
                spawnPickup(pos, "dungeon_pebble", "Wobbly pebble", 1, 0f, new Color(0.72f, 0.68f, 0.55f, 1f));
            }

            int rations = Mathf.Max(0, config.rationCount);
            for (int i = 0; i < rations; i++)
            {
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, null, out Vector3 pos))
                    break;
                pos.y = config.pickupHeight;
                spawnPickup(pos, "trail_ration", "Trail ration", 1, 18f, new Color(0.55f, 0.78f, 0.45f, 1f));
            }

            int spikes = Mathf.Max(0, config.spikeTrapCount);
            Predicate<Vector2Int> hazardFilter = config.preferCorridorsForHazards
                ? dungeon.IsCorridorCell
                : null;

            int hazardAttempts = 0;
            int hazardPlaced = 0;
            int maxAttempts = Mathf.Max(spikes * 12, dungeon.WalkableCells.Count);
            while (hazardPlaced < spikes && hazardAttempts < maxAttempts)
            {
                hazardAttempts++;
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, hazardFilter, out Vector3 pos))
                {
                    if (config.preferCorridorsForHazards && hazardFilter != null)
                        hazardFilter = null;
                    continue;
                }

                pos.y = config.hazardHeight;
                spawnHazard(pos);
                hazardPlaced++;
            }

            if (spawnEnemy == null)
                return;

            int foes = Mathf.Max(0, config.encounterEnemyCount);
            int foeAttempts = 0;
            int foesPlaced = 0;
            int foeMaxAttempts = Mathf.Max(foes * 16, dungeon.WalkableCells.Count);
            while (foesPlaced < foes && foeAttempts < foeMaxAttempts)
            {
                foeAttempts++;
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, dungeon.IsEncounterCell, out Vector3 pos))
                    continue;

                pos.y = config.enemyHeight;
                spawnEnemy(pos);
                foesPlaced++;
            }
        }
    }
}
