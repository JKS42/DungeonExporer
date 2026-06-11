using DungeonExporer.Gameplay;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Player
{
    /// <summary>
    /// Melee swipe from the camera; uses the shared Player action map Attack binding.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _damage = 22f;
        [SerializeField] private float _range = 3f;
        [SerializeField] private float _cooldown = 0.38f;
        [Tooltip("Sphere radius for melee hits (forgiving on short foes below the crosshair).")]
        [SerializeField] private float _hitRadius = 0.75f;

        private InputActionMap _playerMap;
        private InputAction _attackAction;
        private float _nextSwing;

        public void Wire(InputActionAsset inputActions, Transform cameraTransform)
        {
            if (inputActions != null)
                _inputActions = inputActions;
            if (cameraTransform != null)
                _cameraTransform = cameraTransform;
        }

        private void Awake()
        {
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (_inputActions == null)
                return;
            _playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: true);
            _attackAction = _playerMap.FindAction("Attack", throwIfNotFound: true);
            _playerMap.Enable();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
            _playerMap = null;
            _attackAction = null;
        }

        private void Update()
        {
            if (_attackAction == null || _cameraTransform == null)
                return;
            if (PauseMenuController.IsPaused || DialoguePanelController.IsOpen)
                return;
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
                return;
            if (Time.time < _nextSwing)
                return;
            if (!_attackAction.WasPressedThisFrame())
                return;

            TrySwing();
            _nextSwing = Time.time + _cooldown;
        }

        private void TrySwing()
        {
            Vector3 origin = _cameraTransform.position;
            Vector3 dir = _cameraTransform.forward;
            float radius = Mathf.Max(0.15f, _hitRadius);

            RaycastHit[] castHits = Physics.SphereCastAll(
                origin,
                radius,
                dir,
                _range,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (castHits != null && castHits.Length > 0)
            {
                System.Array.Sort(castHits, (a, b) => a.distance.CompareTo(b.distance));
                if (TryFirstEnemy(castHits))
                    return;
            }

            Vector3 probe = origin + dir * Mathf.Min(_range * 0.55f, 1.75f);
            Collider[] overlaps = Physics.OverlapSphere(probe, radius + 0.35f, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps == null || overlaps.Length == 0)
                return;

            System.Array.Sort(overlaps, (a, b) =>
            {
                float da = (a.transform.position - origin).sqrMagnitude;
                float db = (b.transform.position - origin).sqrMagnitude;
                return da.CompareTo(db);
            });

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider col = overlaps[i];
                Vector3 hitPoint = col.ClosestPoint(probe);
                if (TryDamageEnemy(col, hitPoint))
                    return;
            }
        }

        private bool TryFirstEnemy(RaycastHit[] hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryDamageEnemy(hits[i].collider, hits[i].point))
                    return true;
            }

            return false;
        }

        private bool TryDamageEnemy(Collider col, Vector3 hitPoint)
        {
            if (col == null || IsSelfCollider(col))
                return false;

            EnemyActor enemy = col.GetComponentInParent<EnemyActor>();
            if (enemy == null)
                return false;

            if (hitPoint.sqrMagnitude < 0.0001f)
                hitPoint = col.ClosestPoint(_cameraTransform.position);

            enemy.ApplyDamage(_damage, gameObject, hitPoint);
            return true;
        }

        private bool IsSelfCollider(Collider col)
        {
            Transform t = col.transform;
            return t == transform || t.IsChildOf(transform);
        }
    }
}
