using DungeonExporer.Gameplay;
using UnityEngine;

namespace DungeonExporer.Dungeon
{
    /// <summary>
    /// Encounter-zone trigger: fires a <see cref="QuestManager"/> world event once per volume when the player enters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEncounterVolume : MonoBehaviour
    {
        private string _questEventId = "entered_encounter_zone";
        private bool _hasEmitted;

        /// <summary>Called by <see cref="DungeonLevelBuilder"/> after the component is added.</summary>
        public void Configure(string questEventId)
        {
            _questEventId = string.IsNullOrWhiteSpace(questEventId) ? "entered_encounter_zone" : questEventId;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasEmitted)
                return;

            if (other == null)
                return;

            if (other.GetComponentInParent<CharacterController>() == null)
                return;

            _hasEmitted = true;
            QuestManager.Instance?.NotifyWorldEvent(_questEventId);
        }
    }
}
