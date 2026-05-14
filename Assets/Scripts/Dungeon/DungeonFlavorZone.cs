using UnityEngine;

namespace DungeonExporer.Dungeon
{
    public enum DungeonFlavorKind
    {
        Safe = 0,
        Encounter = 1
    }

    /// <summary>
    /// Trigger volume for short LLM “floor flavor” when the player enters safe vs encounter tiles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonFlavorZone : MonoBehaviour
    {
        private DungeonFlavorKind _kind;

        public void Configure(DungeonFlavorKind kind) => _kind = kind;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;
            if (other.GetComponentInParent<CharacterController>() == null)
                return;

            DungeonFlavorNarrator.NotifyFlavorEnter(_kind);
        }
    }
}
