using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonExporer.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private Transform _cameraTransform;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintMultiplier = 1.65f;
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -24f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float pitchMin = -88f;
        [SerializeField] private float pitchMax = 88f;

        [Header("Character")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private float cameraHeightStanding = 1.6f;
        [SerializeField] private float cameraHeightCrouching = 1f;

        private CharacterController _characterController;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;

        private float _yawDegrees;
        private float _pitchDegrees;
        private Vector3 _verticalVelocity;
        private float _currentHeight;
        private float _currentCameraLocalY;

#if UNITY_EDITOR
        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }
#endif

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (_inputActions == null)
                return;

            _playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: true);
            _moveAction = _playerMap.FindAction("Move", throwIfNotFound: true);
            _lookAction = _playerMap.FindAction("Look", throwIfNotFound: true);
            _jumpAction = _playerMap.FindAction("Jump", throwIfNotFound: true);
            _sprintAction = _playerMap.FindAction("Sprint", throwIfNotFound: true);
            _crouchAction = _playerMap.FindAction("Crouch", throwIfNotFound: true);
            _playerMap.Enable();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
            _playerMap = null;
        }

        private void Start()
        {
            _currentHeight = standingHeight;
            _currentCameraLocalY = cameraHeightStanding;
            PushCharacterControllerDimensions();
            ApplyCursorLock(true);
            SyncLookFromTransform();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyCursorLock(true);
        }

        private void Update()
        {
            if (_playerMap == null)
                return;

            if (PauseMenuController.IsPaused)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ApplyCursorLock(false);

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !Cursor.visible)
                ApplyCursorLock(true);

            HandleCrouch();
            HandleLook();
            HandleMove();
        }

        private static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void SyncLookFromTransform()
        {
            _yawDegrees = transform.eulerAngles.y;
            if (_cameraTransform == null)
                return;
            _pitchDegrees = NormalizePitch(_cameraTransform.localEulerAngles.x);
        }

        private static float NormalizePitch(float eulerX)
        {
            return eulerX > 180f ? eulerX - 360f : eulerX;
        }

        private void HandleLook()
        {
            if (_cameraTransform == null)
                return;

            Vector2 look = _lookAction.ReadValue<Vector2>();
            _yawDegrees += look.x * lookSensitivity;
            _pitchDegrees -= look.y * lookSensitivity;
            _pitchDegrees = Mathf.Clamp(_pitchDegrees, pitchMin, pitchMax);

            transform.rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
            _cameraTransform.localRotation = Quaternion.Euler(_pitchDegrees, 0f, 0f);
        }

        private void HandleCrouch()
        {
            bool wantsCrouch = _crouchAction.IsPressed();
            float targetHeight = wantsCrouch ? crouchHeight : standingHeight;
            float targetCamY = wantsCrouch ? cameraHeightCrouching : cameraHeightStanding;
            float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, t);
            _currentCameraLocalY = Mathf.Lerp(_currentCameraLocalY, targetCamY, t);
            PushCharacterControllerDimensions();
        }

        private void PushCharacterControllerDimensions()
        {
            _characterController.height = _currentHeight;
            _characterController.center = new Vector3(0f, _currentHeight * 0.5f, 0f);
            if (_cameraTransform == null)
                return;
            Vector3 p = _cameraTransform.localPosition;
            _cameraTransform.localPosition = new Vector3(p.x, _currentCameraLocalY, p.z);
        }

        private void HandleMove()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>();
            float speed = walkSpeed * (_sprintAction.IsPressed() ? sprintMultiplier : 1f);
            Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;

            bool grounded = _characterController.isGrounded;
            if (grounded && _verticalVelocity.y < 0f)
                _verticalVelocity.y = -2f;

            if (_jumpAction.WasPressedThisFrame() && grounded)
                _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 motion = new Vector3(wish.x, _verticalVelocity.y, wish.z);
            _characterController.Move(motion * Time.deltaTime);
        }
    }
}
