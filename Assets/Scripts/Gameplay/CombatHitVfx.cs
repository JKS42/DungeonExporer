using UnityEngine;

namespace DungeonExporer.Gameplay
{
    /// <summary>
    /// Short-lived procedural burst when melee connects with a foe.
    /// </summary>
    public static class CombatHitVfx
    {
        public static void Play(Vector3 worldPoint, Vector3 normal, bool lethal)
        {
            Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            SpawnSparkBurst(worldPoint, n, lethal);
        }

        private static void SpawnSparkBurst(Vector3 worldPoint, Vector3 normal, bool lethal)
        {
            var go = new GameObject(lethal ? "EnemyKillVfx" : "EnemyHitVfx");
            go.transform.SetPositionAndRotation(worldPoint, Quaternion.LookRotation(normal));

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = lethal ? 0.22f : 0.14f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lethal ? 0.34f : 0.22f;
            main.startSpeed = lethal ? 4.8f : 3.4f;
            main.startSize = lethal ? 0.16f : 0.11f;
            main.maxParticles = lethal ? 36 : 22;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.65f;
            main.startColor = lethal
                ? new Color(1f, 0.42f, 0.18f, 1f)
                : new Color(1f, 0.88f, 0.45f, 1f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(lethal ? 28 : 16))
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.04f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                renderer.material = new Material(shader)
                {
                    color = lethal
                        ? new Color(1f, 0.42f, 0.18f, 1f)
                        : new Color(1f, 0.88f, 0.45f, 1f)
                };
            }

            ps.Play();
            Object.Destroy(go, lethal ? 0.75f : 0.45f);
        }
    }
}
