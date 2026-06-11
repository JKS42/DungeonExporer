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
            Action<Vector3> spawnEnemy = null,
            HashSet<Vector2Int> reservedCells = null)
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
            var reserved = reservedCells ?? new HashSet<Vector2Int>();
            int minDist = Mathf.Max(2, config.minCellsFromSpawn);

            ScatterLoot(dungeon, config, null, reserved, spawnPickup, rng, minDist);
            if (spawnEnemy != null)
                ScatterEnemies(dungeon, config, null, reserved, spawnEnemy, rng, minDist);
        }

        /// <summary>Places AI-chosen loot first, then fills pebble/ration quotas procedurally.</summary>
        public static void ScatterLoot(
            DungeonLevelBuilder dungeon,
            ScatterConfig config,
            IReadOnlyList<PlannedLoot> aiLoot,
            HashSet<Vector2Int> reserved,
            Action<Vector3, string, string, int, float, Color> spawnPickup,
            System.Random rng = null,
            int minDist = -1)
        {
            if (dungeon == null || spawnPickup == null)
                return;

            int seed = config.randomSeed != 0 ? config.randomSeed : Environment.TickCount;
            rng ??= new System.Random(seed);
            if (reserved == null)
                reserved = new HashSet<Vector2Int>();
            if (minDist < 0)
                minDist = Mathf.Max(2, config.minCellsFromSpawn);

            int pebblesPlaced = 0;
            int rationsPlaced = 0;

            if (aiLoot != null)
            {
                for (int i = 0; i < aiLoot.Count; i++)
                {
                    PlannedLoot entry = aiLoot[i];
                    if (!dungeon.IsLootEligibleCell(entry.Cell, minDist))
                        continue;
                    if (!reserved.Add(entry.Cell))
                        continue;

                    Vector3 pos = dungeon.CellCenterWorld(entry.Cell);
                    pos.y = config.pickupHeight;
                    if (entry.ItemId == "trail_ration")
                    {
                        spawnPickup(pos, "trail_ration", "Trail ration", 1, 18f, new Color(0.55f, 0.78f, 0.45f, 1f));
                        rationsPlaced++;
                    }
                    else
                    {
                        spawnPickup(pos, "dungeon_pebble", "Wobbly pebble", 1, 0f, new Color(0.72f, 0.68f, 0.55f, 1f));
                        pebblesPlaced++;
                    }
                }
            }

            int pebblesTarget = Mathf.Max(0, config.pebbleCount);
            while (pebblesPlaced < pebblesTarget)
            {
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, null, out Vector3 pos))
                    break;
                pos.y = config.pickupHeight;
                spawnPickup(pos, "dungeon_pebble", "Wobbly pebble", 1, 0f, new Color(0.72f, 0.68f, 0.55f, 1f));
                pebblesPlaced++;
            }

            int rationsTarget = Mathf.Max(0, config.rationCount);
            while (rationsPlaced < rationsTarget)
            {
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, null, out Vector3 pos))
                    break;
                pos.y = config.pickupHeight;
                spawnPickup(pos, "trail_ration", "Trail ration", 1, 18f, new Color(0.55f, 0.78f, 0.45f, 1f));
                rationsPlaced++;
            }
        }

        /// <summary>Places AI-chosen enemies on E tiles first, then fills remainder procedurally.</summary>
        public static void ScatterEnemies(
            DungeonLevelBuilder dungeon,
            ScatterConfig config,
            IReadOnlyList<PlannedEnemy> aiEnemies,
            HashSet<Vector2Int> reserved,
            Action<Vector3> spawnEnemy,
            System.Random rng = null,
            int minDist = -1)
        {
            if (dungeon == null || spawnEnemy == null)
                return;

            int seed = config.randomSeed != 0 ? config.randomSeed : Environment.TickCount;
            rng ??= new System.Random(seed ^ 0xE11E);
            if (reserved == null)
                reserved = new HashSet<Vector2Int>();
            if (minDist < 0)
                minDist = Mathf.Max(2, config.minCellsFromSpawn);

            int placed = 0;
            int target = Mathf.Max(0, config.encounterEnemyCount);

            if (aiEnemies != null)
            {
                for (int i = 0; i < aiEnemies.Count && placed < target; i++)
                {
                    PlannedEnemy entry = aiEnemies[i];
                    if (!dungeon.IsEnemyEligibleCell(entry.Cell, minDist))
                        continue;
                    if (!reserved.Add(entry.Cell))
                        continue;

                    Vector3 pos = dungeon.CellCenterWorld(entry.Cell);
                    pos.y = config.enemyHeight;
                    spawnEnemy(pos);
                    placed++;
                }
            }

            int foeAttempts = 0;
            int foeMaxAttempts = Mathf.Max((target - placed) * 16, dungeon.WalkableCells.Count);
            while (placed < target && foeAttempts < foeMaxAttempts)
            {
                foeAttempts++;
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, dungeon.IsEncounterCell, out Vector3 pos))
                    continue;

                pos.y = config.enemyHeight;
                spawnEnemy(pos);
                placed++;
            }
        }

        /// <summary>
        /// Places AI-chosen traps first, then fills remaining quota with procedural spikes.
        /// </summary>
        public static void ScatterTraps(
            DungeonLevelBuilder dungeon,
            ScatterConfig config,
            IReadOnlyList<PlannedTrap> aiTraps,
            HashSet<Vector2Int> reserved,
            Action<Vector3, DungeonTrapType> spawnTrap)
        {
            if (dungeon == null || spawnTrap == null)
                return;

            int seed = config.randomSeed != 0 ? config.randomSeed : Environment.TickCount;
            var rng = new System.Random(seed ^ 0x5A17);
            int minDist = Mathf.Max(2, config.minCellsFromSpawn);
            if (reserved == null)
                reserved = new HashSet<Vector2Int>();

            int target = Mathf.Max(0, config.spikeTrapCount);
            int placed = 0;

            if (aiTraps != null)
            {
                for (int i = 0; i < aiTraps.Count && placed < target; i++)
                {
                    PlannedTrap trap = aiTraps[i];
                    if (!dungeon.IsTrapEligibleCell(trap.Cell, minDist))
                        continue;
                    if (!reserved.Add(trap.Cell))
                        continue;

                    Vector3 pos = dungeon.CellCenterWorld(trap.Cell);
                    pos.y = config.hazardHeight;
                    spawnTrap(pos, trap.Type);
                    placed++;
                }
            }

            Predicate<Vector2Int> hazardFilter = config.preferCorridorsForHazards
                ? dungeon.IsCorridorCell
                : null;

            int hazardAttempts = 0;
            int maxAttempts = Mathf.Max((target - placed) * 12, dungeon.WalkableCells.Count);
            while (placed < target && hazardAttempts < maxAttempts)
            {
                hazardAttempts++;
                if (!dungeon.TryPickScatterCell(rng, minDist, reserved, hazardFilter, out Vector3 pos))
                {
                    if (config.preferCorridorsForHazards && hazardFilter != null)
                        hazardFilter = null;
                    continue;
                }

                pos.y = config.hazardHeight;
                spawnTrap(pos, DungeonTrapType.Spike);
                placed++;
            }
        }
    }
}
