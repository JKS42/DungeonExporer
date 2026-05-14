using UnityEngine;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// Marks encounter-zone floor cells. Gameplay (combat, LLM hooks) can query triggers on this layer later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEncounterVolume : MonoBehaviour
    {
    }
}
