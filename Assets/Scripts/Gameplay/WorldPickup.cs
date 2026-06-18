using DungeonExporer.Player;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Walk-through pickup: adds stacks to <see cref="PlayerInventory"/> and optionally heals the player.
    /// </summary>
    public sealed class WorldPickup : MonoBehaviour
    {
        [SerializeField] private string _itemId = "dungeon_pebble";
        [SerializeField] private string _displayName = "Wobbly pebble";
        [SerializeField] private int _count = 1;
        [SerializeField] private float _healAmount;

        private bool _taken;

        public void Configure(string itemId, string displayName, int count, float healAmount)
        {
            _itemId = itemId;
            _displayName = displayName;
            _count = Mathf.Max(1, count);
            _healAmount = Mathf.Max(0f, healAmount);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_taken)
                return;
            if (other == null || other.GetComponentInParent<CharacterController>() == null)
                return;

            _taken = true;

            if (PlayerInventory.Instance != null && !string.IsNullOrWhiteSpace(_itemId))
                PlayerInventory.Instance.TryAdd(_itemId, _displayName, _count);

            QuestWorldEvents.NotifyPickup(_itemId);

            if (_healAmount > 0f && PlayerHealth.Instance != null)
                PlayerHealth.Instance.Heal(_healAmount);

            Destroy(gameObject);
        }
    }
}
