using DungeonExporer.Player;
using DungeonExporer.UI;
using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// When the player enters aggro range, faces them, closes distance, and applies melee damage in attack range.
    /// </summary>
    public sealed class EnemyMeleeAI : MonoBehaviour
    {
        [SerializeField] private float _aggroRange = 12f;
        [SerializeField] private float _attackRange = 2.4f;
        [SerializeField] private float _moveSpeed = 2.6f;
        [SerializeField] private float _damage = 14f;
        [SerializeField] private float _attackCooldown = 1.1f;
        [SerializeField] private float _turnSpeed = 420f;

        private Transform _player;
        private CapsuleCollider _body;
        private float _nextAttack;

        public void Configure(
            float aggroRange,
            float attackRange,
            float moveSpeed,
            float damage,
            float attackCooldown)
        {
            _aggroRange = Mathf.Max(0.5f, aggroRange);
            _attackRange = Mathf.Max(0.4f, attackRange);
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _damage = Mathf.Max(0f, damage);
            _attackCooldown = Mathf.Max(0.1f, attackCooldown);
        }

        private void Awake()
        {
            _body = GetComponent<CapsuleCollider>();
        }

        private void Update()
        {
            if (PauseMenuController.IsPaused || DialoguePanelController.IsOpen)
                return;

            PlayerHealth playerHealth = PlayerHealth.Instance;
            if (playerHealth == null || playerHealth.IsDead)
                return;

            _player ??= playerHealth.transform;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float distSq = toPlayer.sqrMagnitude;
            if (distSq > _aggroRange * _aggroRange)
                return;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    _turnSpeed * Time.deltaTime);
            }

            if (distSq <= _attackRange * _attackRange)
            {
                TryAttack(playerHealth);
                return;
            }

            TryMoveToward(toPlayer);
        }

        private void TryMoveToward(Vector3 flatToPlayer)
        {
            if (_moveSpeed <= 0f || flatToPlayer.sqrMagnitude < 0.0001f)
                return;

            Vector3 delta = flatToPlayer.normalized * (_moveSpeed * Time.deltaTime);
            if (_body == null)
            {
                transform.position += delta;
                return;
            }

            Vector3 center = transform.TransformPoint(_body.center);
            float half = Mathf.Max(0.01f, _body.height * 0.5f - _body.radius);
            Vector3 p1 = center + Vector3.up * half;
            Vector3 p2 = center - Vector3.up * half;
            float radius = _body.radius * 0.92f;

            if (!Physics.CapsuleCast(p1, p2, radius, delta.normalized, out _, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                transform.position += delta;
        }

        private void TryAttack(PlayerHealth playerHealth)
        {
            if (Time.time < _nextAttack)
                return;

            _nextAttack = Time.time + _attackCooldown;
            playerHealth.TakeDamage(_damage, gameObject);
        }
    }
}
