using UnityEngine;

/// <summary>
/// Runtime-created underwater dash burst effect.
/// Used as fallback when dash effect prefabs are not assigned.
/// </summary>
public class DashWaterBurstEffect : MonoBehaviour
{
    public static void Spawn(Vector3 position, Vector3 dashDirection, float lifeMultiplier = 1f)
    {
        Vector3 dir = dashDirection.sqrMagnitude > 1e-5f ? dashDirection.normalized : Vector3.right;

        GameObject go = new GameObject("DashWaterBurst");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up); // emit opposite dash direction

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ConfigureParticle(ps, lifeMultiplier);

        var auto = go.AddComponent<DashFxAutoDestroy>();
        auto.target = ps;

        ps.Play();
    }

    private static void ConfigureParticle(ParticleSystem ps, float lifeMultiplier)
    {
        var main = ps.main;
        main.duration = 0.35f * lifeMultiplier;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f * lifeMultiplier, 0.55f * lifeMultiplier);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.78f, 0.95f, 1f, 0.7f),
            new Color(0.5f, 0.78f, 0.95f, 0.45f)
        );

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, 22, 30, 1, 0f),
            new ParticleSystem.Burst(0.04f * lifeMultiplier, 10, 14, 1, 0f),
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.08f;
        shape.angle = 18f;
        shape.length = 0.15f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.6f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.88f, 0.98f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.75f, 0.9f), 1f),
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.15f),
                new GradientAlphaKey(0.35f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.3f, 0.9f),
            new Keyframe(1f, 1.15f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
    }
}

public class DashFxAutoDestroy : MonoBehaviour
{
    public ParticleSystem target;

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!target.IsAlive(true))
        {
            Destroy(gameObject);
        }
    }
}
