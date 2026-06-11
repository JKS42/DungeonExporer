using System.Collections.Generic;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    public struct PlannedTrap
    {
        public Vector2Int Cell;
        public DungeonTrapType Type;
    }

    /// <summary>Validated trap layout from Ollama (or empty when falling back to procedural scatter).</summary>
    public sealed class DungeonTrapPlan
    {
        public readonly List<PlannedTrap> Traps = new List<PlannedTrap>(16);
        public string CapNote = string.Empty;
    }
}
