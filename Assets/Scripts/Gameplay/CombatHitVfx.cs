using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Procedural melee feedback: swing whoosh, impact sparks, and kill burst.
    /// </summary>
    public static class CombatHitVfx
    {
        public static void PlaySwing(Vector3 origin, Vector3 forward, float reach)
        {
            Vector3 n = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 point = origin + n * Mathf.Clamp(reach * 0.42f, 0.55f, 1.6f);
            SpawnBurst(
                "MeleeSwingVfx",
                point,
                Quaternion.LookRotation(n),
                lethal: false,
                swing: true);
        }

        public static void Play(Vector3 worldPoint, Vector3 normal, bool lethal)
        {
            Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            SpawnBurst(
                lethal ? "EnemyKillVfx" : "EnemyHitVfx",
                worldPoint,
                Quaternion.LookRotation(n),
                lethal,
                swing: false);
            SpawnImpactRing(worldPoint, n, lethal);
        }

        private static void SpawnImpactRing(Vector3 worldPoint, Vector3 normal, bool lethal)
        {
            var go = new GameObject(lethal ? "EnemyKillRingVfx" : "EnemyHitRingVfx");
            go.transform.SetPositionAndRotation(worldPoint, Quaternion.LookRotation(normal));

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.12f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lethal ? 0.28f : 0.2f;
            main.startSpeed = 0f;
            main.startSize = lethal ? 0.55f : 0.38f;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = lethal
                ? new Color(1f, 0.35f, 0.12f, 0.75f)
                : new Color(1f, 0.92f, 0.5f, 0.7f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(1f, 1f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(main.startColor.color, 0f),
                    new GradientColorKey(main.startColor.color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ApplyParticleMaterial(go.GetComponent<ParticleSystemRenderer>(), main.startColor.color);
            ps.Play();
            Object.Destroy(go, lethal ? 0.45f : 0.32f);
        }

        private static void SpawnBurst(
            string name,
            Vector3 worldPoint,
            Quaternion rotation,
            bool lethal,
            bool swing)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(worldPoint, rotation);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = swing ? 0.15f : 0.65f;

            if (swing)
            {
                main.duration = 0.1f;
                main.startLifetime = 0.16f;
                main.startSpeed = 2.6f;
                main.startSize = 0.07f;
                main.maxParticles = 14;
                main.startColor = new Color(0.82f, 0.9f, 1f, 0.55f);
            }
            else
            {
                main.duration = lethal ? 0.22f : 0.14f;
                main.startLifetime = lethal ? 0.34f : 0.22f;
                main.startSpeed = lethal ? 4.8f : 3.4f;
                main.startSize = lethal ? 0.16f : 0.11f;
                main.maxParticles = lethal ? 36 : 22;
                main.startColor = lethal
                    ? new Color(1f, 0.42f, 0.18f, 1f)
                    : new Color(1f, 0.88f, 0.45f, 1f);
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(swing ? 10 : lethal ? 28 : 16))
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = swing ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Hemisphere;
            shape.angle = swing ? 18f : 0f;
            shape.radius = swing ? 0.02f : 0.04f;

            ApplyParticleMaterial(go.GetComponent<ParticleSystemRenderer>(), main.startColor.color);
            ps.Play();
            Object.Destroy(go, swing ? 0.28f : lethal ? 0.75f : 0.45f);
        }

        private static void ApplyParticleMaterial(ParticleSystemRenderer renderer, Color color)
        {
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                return;

            renderer.material = new Material(shader) { color = color };
        }
    }
}
