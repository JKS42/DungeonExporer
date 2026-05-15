using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Simple damage sponge; notifies <see cref="QuestManager"/> when defeated.
    /// </summary>
    public sealed class EnemyActor : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 45f;
        [SerializeField] private string _defeatQuestEventId = "defeated_dungeon_foe";

        private float _health;

        private void Awake()
        {
            _health = _maxHealth;
        }

        public void Configure(float maxHealth, string defeatQuestEventId)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
            _health = _maxHealth;
            if (!string.IsNullOrWhiteSpace(defeatQuestEventId))
                _defeatQuestEventId = defeatQuestEventId.Trim();
        }

        public void ApplyDamage(float amount, GameObject attacker)
        {
            if (amount <= 0f || _health <= 0f)
                return;

            _health -= amount;
            if (_health > 0f)
                return;

            _health = 0f;
            if (QuestManager.Instance != null && !string.IsNullOrWhiteSpace(_defeatQuestEventId))
                QuestManager.Instance.NotifyWorldEvent(_defeatQuestEventId);

            Destroy(gameObject);
        }
    }
}
