using DungeonExporer.Player;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Trigger volume that applies damage to the player on an interval (spikes, slime, etc.).
    /// </summary>
    public sealed class HazardVolume : MonoBehaviour
    {
        [SerializeField] private float _damagePerTick = 10f;
        [SerializeField] private float _tickInterval = 0.45f;
        [Tooltip("Feet must be at or below hazard top + this margin to take damage (allows jump-overs).")]
        [SerializeField] private float _feetClearanceAboveTop = 0.12f;

        private float _nextTick;
        private Collider _volume;

        private void Awake()
        {
            _volume = GetComponent<Collider>();
        }

        private void OnTriggerStay(Collider other)
        {
            if (PlayerHealth.Instance == null || PlayerHealth.Instance.IsDead)
                return;
            if (other == null)
                return;

            CharacterController player = other.GetComponentInParent<CharacterController>();
            if (player == null)
                return;

            if (_volume != null)
            {
                float feetY = player.transform.position.y;
                float hazardTop = _volume.bounds.max.y;
                if (feetY > hazardTop + _feetClearanceAboveTop)
                    return;
            }

            if (Time.time < _nextTick)
                return;

            _nextTick = Time.time + Mathf.Max(0.05f, _tickInterval);
            PlayerHealth.Instance.TakeDamage(_damagePerTick, gameObject);
        }
    }
}
