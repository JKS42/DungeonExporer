using System.Collections.Generic;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    public struct PlannedLoot
    {
        public Vector2Int Cell;
        public string ItemId;
    }

    public struct PlannedEnemy
    {
        public Vector2Int Cell;
    }

    public struct PlannedSign
    {
        public Vector2Int Cell;
        public string Text;
    }

    /// <summary>Validated loot, enemy, and sign layout from Ollama.</summary>
    public sealed class DungeonContentPlan
    {
        public readonly List<PlannedLoot> Loot = new List<PlannedLoot>(24);
        public readonly List<PlannedEnemy> Enemies = new List<PlannedEnemy>(16);
        public readonly List<PlannedSign> Signs = new List<PlannedSign>(12);
        public string CapNote = string.Empty;
    }
}
