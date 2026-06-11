using System.Collections;
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
        [SerializeField] private float _hitFlashSeconds = 0.12f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private float _health;
        private Coroutine _flashRoutine;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _health = _maxHealth;
            CacheRenderers();
        }

        public void Configure(float maxHealth, string defeatQuestEventId)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
            _health = _maxHealth;
            if (!string.IsNullOrWhiteSpace(defeatQuestEventId))
                _defeatQuestEventId = defeatQuestEventId.Trim();
            CacheRenderers();
        }

        public void ApplyDamage(float amount, GameObject attacker, Vector3 hitPoint)
        {
            if (amount <= 0f || _health <= 0f)
                return;

            bool lethal = amount >= _health;
            Vector3 point = ResolveHitPoint(hitPoint);
            Vector3 normal = ResolveHitNormal(point, attacker);

            CombatHitVfx.Play(point, normal, lethal);
            PlayHitFlash(lethal);

            _health -= amount;
            if (_health > 0f)
                return;

            _health = 0f;
            if (QuestManager.Instance != null && !string.IsNullOrWhiteSpace(_defeatQuestEventId))
                QuestManager.Instance.NotifyWorldEvent(_defeatQuestEventId);

            Destroy(gameObject);
        }

        private Vector3 ResolveHitPoint(Vector3 hitPoint)
        {
            if (hitPoint.sqrMagnitude > 0.0001f)
                return hitPoint;

            Collider body = GetComponent<Collider>();
            if (body != null)
                return body.bounds.center;

            return transform.position + Vector3.up * 0.55f;
        }

        private static Vector3 ResolveHitNormal(Vector3 hitPoint, GameObject attacker)
        {
            if (attacker == null)
                return Vector3.up;

            Vector3 dir = hitPoint - attacker.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return Vector3.up;

            return dir.normalized;
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            if (_renderers == null || _renderers.Length == 0)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = ReadRendererColor(_renderers[i]);
        }

        private void PlayHitFlash(bool lethal)
        {
            if (_renderers == null || _renderers.Length == 0)
                CacheRenderers();
            if (_renderers == null || _renderers.Length == 0)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(HitFlashRoutine(
                lethal ? new Color(1f, 0.28f, 0.18f, 1f) : new Color(1f, 0.92f, 0.55f, 1f)));
        }

        private IEnumerator HitFlashRoutine(Color flashColor)
        {
            SetRendererColors(flashColor);

            float elapsed = 0f;
            float duration = Mathf.Max(0.04f, _hitFlashSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null)
                        continue;
                    Color c = Color.Lerp(_baseColors[i], flashColor, t);
                    ApplyRendererColor(_renderers[i], c);
                }

                yield return null;
            }

            RestoreRendererColors();
            _flashRoutine = null;
        }

        private void SetRendererColors(Color color)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    ApplyRendererColor(_renderers[i], color);
            }
        }

        private void RestoreRendererColors()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    ApplyRendererColor(_renderers[i], _baseColors[i]);
            }
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static Color ReadRendererColor(Renderer renderer)
        {
            if (renderer == null)
                return Color.white;

            Material mat = renderer.sharedMaterial;
            if (mat != null && mat.HasProperty(BaseColorId))
                return mat.GetColor(BaseColorId);

            return Color.white;
        }
    }
}
