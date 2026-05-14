using DungeonExporer.Gameplay;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Player
{
    /// <summary>
    /// Melee ray from the camera; uses the shared Player action map Attack binding.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _damage = 22f;
        [SerializeField] private float _range = 2.4f;
        [SerializeField] private float _cooldown = 0.38f;

        private InputActionMap _playerMap;
        private InputAction _attackAction;
        private float _nextSwing;

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
            var hits = Physics.RaycastAll(origin, dir, _range, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit h in hits)
            {
                var enemy = h.collider.GetComponentInParent<EnemyActor>();
                if (enemy == null)
                    continue;
                enemy.ApplyDamage(_damage, gameObject);
                break;
            }
        }
    }
}
