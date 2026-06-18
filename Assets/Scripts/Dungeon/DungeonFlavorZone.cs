using DungeonExporer.Gameplay;
using UnityEngine;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// Trigger volume for short LLM "floor flavor" when the player enters safe vs encounter tiles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonFlavorZone : MonoBehaviour
    {
        private DungeonFlavorKind _kind;
        private string _questEventId;
        private bool _questEventEmitted;

        public void Configure(DungeonFlavorKind kind, string questEventId = null)
        {
            _kind = kind;
            _questEventId = string.IsNullOrWhiteSpace(questEventId) ? null : questEventId.Trim();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;
            if (other.GetComponentInParent<CharacterController>() == null)
                return;

            if (!_questEventEmitted && !string.IsNullOrWhiteSpace(_questEventId))
            {
                _questEventEmitted = true;
                QuestManager.Instance?.NotifyWorldEvent(_questEventId);
            }

            DungeonFlavorNarrator.NotifyFlavorEnter(_kind);
        }
    }
}
