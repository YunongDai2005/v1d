using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns varied rocks.
/// - Static mode: one-time generation in area box.
/// - Streaming mode: keep rocks around target, remove out-of-range rocks.
/// </summary>
public class RockVariantSpawner : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Project folder used by Auto Load (Editor only).")]
    public string stoneFolder = "Assets/stone";
    public GameObject[] rockPrefabs;
    public Transform spawnParent;

    [Header("Area (Static Mode, local to this object)")]
    public Vector3 areaCenter = Vector3.zero;
    public Vector3 areaSize = new Vector3(80f, 28f, 4f);
    [Min(1)] public int spawnCount = 120;
    [Min(0f)] public float minDistance = 0.8f;

    [Header("Streaming Around Target")]
    public bool streamAroundTarget = true;
    public Transform target;
    public string targetTag = "Player";
    [Min(1)] public int streamTargetCount = 70;
    [Min(0.1f)] public float streamSpawnRadius = 42f;
    [Min(0.1f)] public float streamDespawnRadius = 55f;
    [Min(0.05f)] public float streamUpdateInterval = 0.25f;
    [Tooltip("Center offset from target in world space.")]
    public Vector3 streamCenterOffset = Vector3.zero;
    [Tooltip("Flatten depth around this world Z in 2.5D scenes.")]
    public bool lockZToSpawner = true;
    public float streamZJitter = 0.8f;

    [Header("Transform Variation")]
    public Vector2 uniformScaleRange = new Vector2(0.55f, 2.25f);
    public Vector3 axisScaleJitter = new Vector3(0.2f, 0.15f, 0.2f);
    public bool allowMirrorX = true;
    public Vector2 yawRange = new Vector2(0f, 360f);
    public Vector2 pitchRange = new Vector2(-12f, 12f);
    public Vector2 rollRange = new Vector2(-10f, 10f);

    [Header("Color Variation")]
    [Tooltip("HSV.x hue shift, HSV.y saturation scale, HSV.z value scale.")]
    public bool useColorVariation = true;
    public Vector2 hueShiftRange = new Vector2(-0.03f, 0.03f);
    public Vector2 saturationMulRange = new Vector2(0.88f, 1.08f);
    public Vector2 valueMulRange = new Vector2(0.85f, 1.12f);
    [Range(0f, 2f)] public float emissionMul = 0.9f;

    [Header("Render / Physics")]
    public bool disableColliders = true;
    public bool castShadows = false;
    public bool receiveShadows = false;

    [Header("Runtime")]
    public bool generateOnStart = true;
    public int seed = 20260208;
    [Min(1)] public int placeTryPerRock = 20;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Vector3> _placed = new List<Vector3>(); // local for static, world for streaming
    private float _streamTimer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    void Start()
    {
        Random.InitState(seed);
        ResolveTargetIfNeeded();

        if (!generateOnStart) return;

        if (streamAroundTarget)
            UpdateStreaming(force: true);
        else
            Regenerate();
    }

    void Update()
    {
        if (!streamAroundTarget || !generateOnStart) return;
        ResolveTargetIfNeeded();
        if (target == null) return;

        _streamTimer -= Time.deltaTime;
        if (_streamTimer > 0f) return;
        _streamTimer = Mathf.Max(0.05f, streamUpdateInterval);
        UpdateStreaming(force: false);
    }

    [ContextMenu("Regenerate Rocks")]
    public void Regenerate()
    {
        ClearSpawned();
        if (rockPrefabs == null || rockPrefabs.Length == 0) return;

        Random.InitState(seed);
        _placed.Clear();

        int targetCount = Mathf.Max(1, spawnCount);
        float minDistSqr = minDistance * minDistance;
        int attempts = Mathf.Max(1, placeTryPerRock);

        for (int i = 0; i < targetCount; i++)
        {
            GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
            if (prefab == null) continue;

            bool placed = false;
            Vector3 localPos = Vector3.zero;
            for (int t = 0; t < attempts; t++)
            {
                localPos = RandomLocalPointInArea();
                if (CanPlace(localPos, minDistSqr))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed) continue;
            _placed.Add(localPos);
            SpawnRock(prefab, localPos, isLocal: true);
        }
    }

    [ContextMenu("Update Streaming Now")]
    public void UpdateStreamingNow()
    {
        ResolveTargetIfNeeded();
        UpdateStreaming(force: true);
    }

    private void UpdateStreaming(bool force)
    {
        if (target == null || rockPrefabs == null || rockPrefabs.Length == 0) return;
        if (streamDespawnRadius < streamSpawnRadius) streamDespawnRadius = streamSpawnRadius + 0.1f;

        Vector3 center = target.position + streamCenterOffset;
        float despawnSqr = streamDespawnRadius * streamDespawnRadius;
        float minDistSqr = minDistance * minDistance;

        // Remove far rocks
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            GameObject go = _spawned[i];
            if (go == null)
            {
                _spawned.RemoveAt(i);
                if (i < _placed.Count) _placed.RemoveAt(i);
                continue;
            }

            float d = (go.transform.position - center).sqrMagnitude;
            if (d > despawnSqr)
            {
                DestroyImmediateSafe(go);
                _spawned.RemoveAt(i);
                if (i < _placed.Count) _placed.RemoveAt(i);
            }
        }

        // Spawn near target until desired count
        int desired = Mathf.Max(1, streamTargetCount);
        int attempts = Mathf.Max(1, placeTryPerRock);
        while (_spawned.Count < desired)
        {
            bool placed = false;
            Vector3 worldPos = center;
            for (int t = 0; t < attempts; t++)
            {
                worldPos = RandomWorldPointAround(center, streamSpawnRadius);
                if (CanPlace(worldPos, minDistSqr))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                if (!force) break;
                // Force-add after max attempts to avoid deadlock in dense settings.
            }

            GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
            if (prefab == null) break;
            _placed.Add(worldPos);
            SpawnRock(prefab, worldPos, isLocal: false);

            if (!force && _spawned.Count >= desired) break;
        }
    }

    [ContextMenu("Clear Spawned Rocks")]
    public void ClearSpawned()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] != null)
            {
                DestroyImmediateSafe(_spawned[i]);
            }
        }
        _spawned.Clear();
        _placed.Clear();
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Load Rocks From Folder")]
    public void AutoLoadRocksFromFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { stoneFolder });
        var list = new List<GameObject>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path)) continue;
            if (!path.EndsWith(".glb") && !path.EndsWith(".fbx") && !path.EndsWith(".prefab")) continue;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }

        rockPrefabs = list.ToArray();
        EditorUtility.SetDirty(this);
        Debug.Log($"[RockVariantSpawner] Loaded {rockPrefabs.Length} rock prefabs from {stoneFolder}");
    }
#endif

    private void SpawnRock(GameObject prefab, Vector3 position, bool isLocal)
    {
        Transform parent = spawnParent ? spawnParent : transform;
        GameObject go;
        if (isLocal)
        {
            go = Instantiate(prefab, parent, false);
            go.transform.localPosition = position;
        }
        else
        {
            go = Instantiate(prefab, position, Quaternion.identity, parent);
        }

        go.transform.rotation = RandomRotation();
        go.transform.localScale = RandomScale();

        ApplyRenderSettings(go);
        if (useColorVariation) ApplyColorVariation(go);
        _spawned.Add(go);
    }

    private void ResolveTargetIfNeeded()
    {
        if (target != null) return;
        if (string.IsNullOrEmpty(targetTag)) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;
    }

    private Vector3 RandomLocalPointInArea()
    {
        Vector3 half = areaSize * 0.5f;
        return areaCenter + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z)
        );
    }

    private Vector3 RandomWorldPointAround(Vector3 center, float radius)
    {
        Vector2 inCircle = Random.insideUnitCircle * radius;
        float z = lockZToSpawner
            ? transform.position.z + Random.Range(-streamZJitter, streamZJitter)
            : center.z + Random.Range(-streamZJitter, streamZJitter);
        return new Vector3(center.x + inCircle.x, center.y + inCircle.y, z);
    }

    private bool CanPlace(Vector3 pos, float minDistSqr)
    {
        if (minDistance <= 0f) return true;
        for (int i = 0; i < _placed.Count; i++)
        {
            if ((_placed[i] - pos).sqrMagnitude < minDistSqr)
                return false;
        }
        return true;
    }

    private Quaternion RandomRotation()
    {
        float yaw = Random.Range(yawRange.x, yawRange.y);
        float pitch = Random.Range(pitchRange.x, pitchRange.y);
        float roll = Random.Range(rollRange.x, rollRange.y);
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private Vector3 RandomScale()
    {
        float s = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
        Vector3 jitter = new Vector3(
            1f + Random.Range(-axisScaleJitter.x, axisScaleJitter.x),
            1f + Random.Range(-axisScaleJitter.y, axisScaleJitter.y),
            1f + Random.Range(-axisScaleJitter.z, axisScaleJitter.z)
        );
        Vector3 final = Vector3.Scale(Vector3.one * s, jitter);
        if (allowMirrorX && Random.value > 0.5f) final.x = -final.x;
        return final;
    }

    private void ApplyRenderSettings(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (disableColliders) cols[i].enabled = false;
        }

        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            rs[i].shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            rs[i].receiveShadows = receiveShadows;
        }
    }

    private void ApplyColorVariation(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        var mpb = new MaterialPropertyBlock();

        float hueDelta = Random.Range(hueShiftRange.x, hueShiftRange.y);
        float satMul = Random.Range(saturationMulRange.x, saturationMulRange.y);
        float valMul = Random.Range(valueMulRange.x, valueMulRange.y);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r == null || r.sharedMaterial == null) continue;

            Color baseColor = Color.white;
            Material mat = r.sharedMaterial;
            if (mat.HasProperty(BaseColorId)) baseColor = mat.GetColor(BaseColorId);
            else if (mat.HasProperty(ColorId)) baseColor = mat.GetColor(ColorId);

            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueDelta, 1f);
            s = Mathf.Clamp01(s * satMul);
            v = Mathf.Clamp01(v * valMul);
            Color c = Color.HSVToRGB(h, s, v);
            c.a = baseColor.a;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(EmissionColorId, c * emissionMul);
            mpb.SetColor(EmissiveColorId, c * emissionMul);
            r.SetPropertyBlock(mpb);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.3f);

        // Static box
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.matrix = old;

        // Streaming circles
        if (streamAroundTarget && target != null)
        {
            Vector3 c = target.position + streamCenterOffset;
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(c, streamSpawnRadius);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(c, streamDespawnRadius);
        }
    }

    private static void DestroyImmediateSafe(Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(obj);
        else Destroy(obj);
#else
        Destroy(obj);
#endif
    }
}
