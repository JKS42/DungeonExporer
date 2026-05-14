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

        private float _nextTick;

        private void OnTriggerStay(Collider other)
        {
            if (PlayerHealth.Instance == null || PlayerHealth.Instance.IsDead)
                return;
            if (other == null || other.GetComponentInParent<CharacterController>() == null)
                return;

            if (Time.time < _nextTick)
                return;

            _nextTick = Time.time + Mathf.Max(0.05f, _tickInterval);
            PlayerHealth.Instance.TakeDamage(_damagePerTick, gameObject);
        }
    }
}
