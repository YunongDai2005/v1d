using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_HDRP || UNITY_RENDER_PIPELINE_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

/// <summary>
/// Applies recommended underwater lighting defaults at runtime.
/// Attach once in scene. It runs automatically on play.
/// </summary>
public class RuntimeLightingAutoSetup : MonoBehaviour
{
    [Header("Apply")]
    public bool applyOnStart = true;
    public bool applyOnce = true;

    [Header("Exposure (HDRP Volume)")]
    public float fixedExposure = 10.6f;

    [Header("Bloom (HDRP Volume)")]
    public float bloomThreshold = 1.35f;
    public float bloomIntensity = 0.18f;

    [Header("Point Lights")]
    public float maxPointLightIntensity = 900f;
    public bool disablePointLightShadows = true;

    [Header("Player Lighting")]
    public bool tunePlayerLights = true;
    public string playerTag = "Player";
    public float playerPointIntensity = 520f;
    public float playerPointRange = 5.2f;
    public Color playerPointColor = new Color(0.72f, 0.88f, 1f, 1f);
    public bool addPlayerLightIfMissing = true;
    public Vector3 playerLightLocalOffset = new Vector3(0f, 0.35f, -0.25f);

    [Header("Enemy Lighting")]
    public bool tuneEnemyLights = true;
    public string enemyTag = "enemy";
    public float enemyPointMaxIntensity = 120f;
    public float enemyPointRange = 2.1f;
    public Color enemyPointColor = new Color(1f, 0.16f, 0.2f, 1f);

    [Header("Debug")]
    public bool logResult = true;

    private bool _applied;

    void Start()
    {
        if (!applyOnStart) return;
        ApplyRecommendedLighting();
    }

    [ContextMenu("Apply Recommended Lighting")]
    public void ApplyRecommendedLighting()
    {
        if (applyOnce && _applied) return;

        int lightAdjusted = ApplyPointLights();
        int actorAdjusted = ApplyActorLights();
        string volumeMsg = ApplyVolumeOverrides();

        _applied = true;
        if (logResult)
        {
            Debug.Log($"[RuntimeLightingAutoSetup] Applied. Scene lights: {lightAdjusted}, Actor lights: {actorAdjusted}. {volumeMsg}");
        }
    }

    private int ApplyPointLights()
    {
        int adjusted = 0;
        Light[] lights = FindObjectsOfType<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null || l.type != LightType.Point) continue;

            if (l.intensity > maxPointLightIntensity)
            {
                l.intensity = maxPointLightIntensity;
            }

            if (disablePointLightShadows)
            {
                l.shadows = LightShadows.None;
            }

            adjusted++;
        }

        return adjusted;
    }

    private string ApplyVolumeOverrides()
    {
        Volume volume = FindGlobalVolume();
        if (volume == null || volume.profile == null)
        {
            return "No global Volume profile found.";
        }

#if UNITY_HDRP || UNITY_RENDER_PIPELINE_HDRP
        bool exposureApplied = false;
        bool bloomApplied = false;

        if (volume.profile.TryGet(out Exposure exposure))
        {
            exposure.mode.overrideState = true;
            exposure.mode.value = ExposureMode.Fixed;
            exposure.fixedExposure.overrideState = true;
            exposure.fixedExposure.value = fixedExposure;
            exposureApplied = true;
        }

        if (volume.profile.TryGet(out Bloom bloom))
        {
            bloom.threshold.overrideState = true;
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = bloomIntensity;
            bloomApplied = true;
        }

        if (!exposureApplied && !bloomApplied)
        {
            return "Global Volume found, but Exposure/Bloom overrides not found in profile.";
        }

        return $"Volume updated. Exposure:{exposureApplied} Bloom:{bloomApplied}";
#else
        return "Current compile target is not HDRP; skipped HDRP Volume settings.";
#endif
    }

    private int ApplyActorLights()
    {
        int adjusted = 0;

        if (tunePlayerLights)
        {
            adjusted += ApplyPlayerLighting();
        }

        if (tuneEnemyLights)
        {
            adjusted += ApplyEnemyLighting();
        }

        return adjusted;
    }

    private int ApplyPlayerLighting()
    {
        GameObject player = null;
        if (!string.IsNullOrEmpty(playerTag))
        {
            player = GameObject.FindGameObjectWithTag(playerTag);
        }
        if (player == null) return 0;

        Light[] lights = player.GetComponentsInChildren<Light>(true);
        int adjusted = 0;

        if (lights == null || lights.Length == 0)
        {
            if (!addPlayerLightIfMissing) return 0;
            GameObject go = new GameObject("Auto_PlayerLight");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = playerLightLocalOffset;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = playerPointIntensity;
            l.range = playerPointRange;
            l.color = playerPointColor;
            l.shadows = LightShadows.None;
            return 1;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null || l.type != LightType.Point) continue;
            l.intensity = Mathf.Min(l.intensity, playerPointIntensity);
            if (l.intensity < playerPointIntensity * 0.75f) l.intensity = playerPointIntensity;
            l.range = playerPointRange;
            l.color = playerPointColor;
            l.shadows = LightShadows.None;
            adjusted++;
        }

        return adjusted;
    }

    private int ApplyEnemyLighting()
    {
        if (string.IsNullOrEmpty(enemyTag)) return 0;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        int adjusted = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject e = enemies[i];
            if (e == null) continue;
            Light[] lights = e.GetComponentsInChildren<Light>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                Light l = lights[j];
                if (l == null || l.type != LightType.Point) continue;
                l.intensity = Mathf.Min(l.intensity, enemyPointMaxIntensity);
                l.range = enemyPointRange;
                l.color = enemyPointColor;
                l.shadows = LightShadows.None;
                adjusted++;
            }
        }

        return adjusted;
    }

    private Volume FindGlobalVolume()
    {
        Volume[] volumes = FindObjectsOfType<Volume>(true);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] != null && volumes[i].isGlobal)
            {
                return volumes[i];
            }
        }
        return null;
    }
}
