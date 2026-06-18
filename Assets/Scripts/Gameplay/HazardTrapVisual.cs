using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Pulses emissive hazard colour and adds a small marker light so traps read in dim corridors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HazardTrapVisual : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Color _emissionTint = new(1f, 0.28f, 0.12f);
        [SerializeField] private float _pulseSpeed = 2.4f;
        [SerializeField] private float _emissionStrength = 1.65f;
        [SerializeField] private float _markerLightIntensity = 0.42f;
        [SerializeField] private float _markerLightRange = 1.85f;

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Light _markerLight;

        public void Configure(DungeonTrapType trapType)
        {
            _emissionTint = trapType switch
            {
                DungeonTrapType.Ember => new Color(1f, 0.45f, 0.1f),
                DungeonTrapType.Slime => new Color(0.35f, 1f, 0.42f),
                _ => new Color(1f, 0.28f, 0.12f)
            };
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
            EnsureMarkerLight();
        }

        private void Update()
        {
            if (_renderer == null)
                return;

            float pulse = 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed));
            Color emission = _emissionTint * (_emissionStrength * pulse);

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, emission);
            _renderer.SetPropertyBlock(_propertyBlock);

            if (_markerLight != null)
                _markerLight.intensity = _markerLightIntensity * pulse;
        }

        private void EnsureMarkerLight()
        {
            var lightGo = new GameObject("HazardMarkerLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);

            _markerLight = lightGo.AddComponent<Light>();
            _markerLight.type = LightType.Point;
            _markerLight.color = _emissionTint;
            _markerLight.intensity = _markerLightIntensity;
            _markerLight.range = _markerLightRange;
            _markerLight.shadows = LightShadows.None;
        }
    }
}
