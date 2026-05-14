using System;
using System.Collections;
using DungeonExporer.Dungeon;
using DungeonExporer.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonExporer.Player
{
    /// <summary>
    /// Player vitality, void fall death, and respawn at the maze <c>P</c> spawn with a short fade.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action Died;
        public event Action Respawned;

        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _voidDeathY = -22f;
        [SerializeField] private float _fadeInSeconds = 0.45f;
        [SerializeField] private float _holdBlackSeconds = 0.55f;
        [SerializeField] private float _fadeOutSeconds = 0.5f;
        [SerializeField] private DungeonLevelBuilder _dungeon;

        private CharacterController _characterController;
        private FirstPersonController _firstPerson;
        private float _current;
        private bool _dead;
        private Coroutine _respawnRoutine;
        private CanvasGroup _fadeGroup;

        public bool IsDead => _dead;
        public float CurrentHealth => _current;
        public float MaxHealth => _maxHealth;

        private void Awake()
        {
            Instance = this;
            _characterController = GetComponent<CharacterController>();
            _firstPerson = GetComponent<FirstPersonController>();
            _current = _maxHealth;
            EnsureFadeOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (_dungeon == null)
                _dungeon = FindFirstObjectByType<DungeonLevelBuilder>();
            HealthChanged?.Invoke(_current, _maxHealth);
        }

        private void Update()
        {
            if (_dead || PauseMenuController.IsPaused)
                return;

            if (transform.position.y < _voidDeathY)
                Die();
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f)
                return;
            _current = Mathf.Min(_maxHealth, _current + amount);
            HealthChanged?.Invoke(_current, _maxHealth);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (_dead || amount <= 0f)
                return;

            _current -= amount;
            if (_current < 0f)
                _current = 0f;
            HealthChanged?.Invoke(_current, _maxHealth);

            if (_current <= 0f)
                Die();
        }

        private void Die()
        {
            if (_dead)
                return;

            _dead = true;
            Died?.Invoke();

            DialoguePanelController.Instance?.Close();
            FindFirstObjectByType<OllamaHandler>()?.AbortActiveRequest();

            if (_respawnRoutine != null)
                StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return FadeRoutine(0f, 1f, _fadeInSeconds);
            yield return new WaitForSecondsRealtime(_holdBlackSeconds);

            _characterController.enabled = false;
            Vector3 spawn = GetRespawnPosition();
            transform.SetPositionAndRotation(spawn, Quaternion.identity);
            _characterController.enabled = true;

            if (_firstPerson != null)
                _firstPerson.ResetOrientationFromTransform();

            _current = _maxHealth;
            _dead = false;
            HealthChanged?.Invoke(_current, _maxHealth);
            Respawned?.Invoke();

            yield return FadeRoutine(1f, 0f, _fadeOutSeconds);
            _respawnRoutine = null;
        }

        private Vector3 GetRespawnPosition()
        {
            if (_dungeon != null && _dungeon.HasRecordedPlayerSpawn)
            {
                Vector3 p = _dungeon.LastPlayerSpawnWorld;
                return new Vector3(p.x, 0f, p.z);
            }

            return new Vector3(transform.position.x, 0f, transform.position.z);
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (_fadeGroup == null)
                yield break;

            duration = Mathf.Max(0.01f, duration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }

            _fadeGroup.alpha = to;
        }

        private void EnsureFadeOverlay()
        {
            if (_fadeGroup != null)
                return;

            var canvasGo = new GameObject("DeathFadeCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.transform.SetParent(null, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var dim = new GameObject("Fade", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            dim.transform.SetParent(canvasGo.transform, false);
            var rt = dim.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = dim.GetComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            _fadeGroup = dim.GetComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
            _fadeGroup.interactable = false;
        }
    }
}
