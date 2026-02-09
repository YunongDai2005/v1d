using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual coin counter for a container object (e.g. "bo").
/// Call AddCoins when player gains coins to spawn coin meshes inside the container.
/// </summary>
public class CoinContainerDisplay : MonoBehaviour
{
    public enum LayoutMode
    {
        Grid,
        RandomInVolume
    }

    [Header("References")]
    [Tooltip("Coin prefab used as visual instance.")]
    public GameObject coinPrefab;
    [Tooltip("Spawn parent. If empty, use this object.")]
    public Transform coinRoot;
    [Tooltip("Optional spawn volume. If empty, uses this transform local box.")]
    public BoxCollider spawnVolume;

    [Header("Layout")]
    public LayoutMode layoutMode = LayoutMode.RandomInVolume;
    [Min(1)] public int maxVisualCoins = 100;
    [Min(1)] public int cols = 5;
    [Min(1)] public int rows = 4;
    public Vector3 startLocalPosition = new Vector3(-0.2f, -0.2f, 0f);
    public Vector3 spacing = new Vector3(0.1f, 0.1f, 0.05f);

    [Header("Random Volume")]
    [Tooltip("Local half-size when no BoxCollider is assigned.")]
    public Vector3 fallbackHalfExtents = new Vector3(0.35f, 0.35f, 0.2f);
    [Min(0f)] public float volumePadding = 0.02f;
    [Min(0f)] public float minRandomDistance = 0.04f;
    [Min(1)] public int randomTries = 16;

    [Header("Rotation")]
    public Vector3 coinEuler = new Vector3(90f, 0f, 0f);
    public Vector2 randomYaw = new Vector2(0f, 360f);
    
    [Header("Scale")]
    [Tooltip("Base local scale multiplier for spawned visual coins.")]
    public float coinScale = 0.2f;
    [Tooltip("Optional random multiplier range applied on top of coinScale.")]
    public Vector2 randomScaleMultiplier = new Vector2(1f, 1f);

    [Header("Visual Lock")]
    [Tooltip("Disable pickup/physics behavior on spawned visual coins.")]
    public bool forceVisualOnly = true;

    [Header("Motion")]
    [Tooltip("Enable floating movement and spinning inside the container.")]
    public bool enableCoinMotion = true;
    public Vector2 moveSpeedRange = new Vector2(0.02f, 0.08f);
    public Vector2 spinSpeedRange = new Vector2(30f, 120f);

    private readonly List<GameObject> _coins = new List<GameObject>();
    private static CoinContainerDisplay _instance;
    private static int _globalCoinScore;

    public static int GlobalCoinScore => _globalCoinScore;

    private void Awake()
    {
        _instance = this;
        if (coinRoot == null) coinRoot = transform;
        if (spawnVolume == null)
        {
            spawnVolume = GetComponent<BoxCollider>();
            if (spawnVolume == null && coinRoot != null)
            {
                spawnVolume = coinRoot.GetComponentInChildren<BoxCollider>();
            }
        }
    }

    public static void AddCoinsGlobal(int amount = 1)
    {
        if (amount <= 0) return;
        _globalCoinScore += amount;
        if (_instance == null)
        {
            _instance = FindObjectOfType<CoinContainerDisplay>();
        }
        if (_instance == null) return;
        _instance.AddCoins(amount);
    }

    public static void ResetGlobalScore()
    {
        _globalCoinScore = 0;
    }

    public void AddCoins(int amount)
    {
        if (coinPrefab == null || amount <= 0) return;

        for (int i = 0; i < amount; i++)
        {
            if (_coins.Count >= maxVisualCoins) return;

            Vector3 localPos = layoutMode == LayoutMode.RandomInVolume
                ? GetRandomLocalPosition()
                : GetGridLocalPosition(_coins.Count);
            localPos = ClampToBounds(localPos);
            Quaternion rot = Quaternion.Euler(coinEuler.x, coinEuler.y + Random.Range(randomYaw.x, randomYaw.y), coinEuler.z);
            GameObject go = Instantiate(coinPrefab, coinRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            float s = Mathf.Max(0.001f, coinScale) * Random.Range(randomScaleMultiplier.x, randomScaleMultiplier.y);
            go.transform.localScale = Vector3.one * s;
            if (forceVisualOnly) ConfigureAsVisualOnly(go);
            if (enableCoinMotion) ConfigureMotion(go);
            _coins.Add(go);
        }
    }

    [ContextMenu("Clear Visual Coins")]
    public void ClearVisualCoins()
    {
        for (int i = _coins.Count - 1; i >= 0; i--)
        {
            if (_coins[i] != null) Destroy(_coins[i]);
        }
        _coins.Clear();
    }

    private Vector3 GetGridLocalPosition(int index)
    {
        int layerSize = Mathf.Max(1, cols * rows);
        int layer = index / layerSize;
        int inLayer = index % layerSize;
        int col = inLayer % cols;
        int row = inLayer / cols;

        return startLocalPosition + new Vector3(col * spacing.x, row * spacing.y, layer * spacing.z);
    }

    private Vector3 GetRandomLocalPosition()
    {
        Vector3 center;
        Vector3 halfExtents;
        GetLocalBounds(out center, out halfExtents);

        float pad = Mathf.Max(0f, volumePadding);
        halfExtents = new Vector3(
            Mathf.Max(0.001f, halfExtents.x - pad),
            Mathf.Max(0.001f, halfExtents.y - pad),
            Mathf.Max(0.001f, halfExtents.z - pad)
        );

        Vector3 best = center;
        float bestDistSqr = -1f;
        for (int i = 0; i < Mathf.Max(1, randomTries); i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(center.x - halfExtents.x, center.x + halfExtents.x),
                Random.Range(center.y - halfExtents.y, center.y + halfExtents.y),
                Random.Range(center.z - halfExtents.z, center.z + halfExtents.z)
            );

            float nearest = NearestCoinDistSqr(candidate);
            if (nearest > bestDistSqr)
            {
                bestDistSqr = nearest;
                best = candidate;
            }
            if (IsFarEnough(candidate)) return candidate;
        }

        // If volume is crowded, nudge outward in a small spiral instead of collapsing.
        return ClampToBounds(SpiralNudge(best, _coins.Count));
    }

    private bool IsFarEnough(Vector3 localCandidate)
    {
        float minSqr = minRandomDistance * minRandomDistance;
        return NearestCoinDistSqr(localCandidate) >= minSqr;
    }

    private float NearestCoinDistSqr(Vector3 localCandidate)
    {
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < _coins.Count; i++)
        {
            if (_coins[i] == null) continue;
            Vector3 d = _coins[i].transform.localPosition - localCandidate;
            float ds = d.sqrMagnitude;
            if (ds < nearest) nearest = ds;
        }
        return nearest;
    }

    private void GetLocalBounds(out Vector3 center, out Vector3 halfExtents)
    {
        if (spawnVolume != null)
        {
            Vector3 c = spawnVolume.center;
            Vector3 e = spawnVolume.size * 0.5f;

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iy = -1; iy <= 1; iy += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        Vector3 localCorner = c + new Vector3(e.x * ix, e.y * iy, e.z * iz);
                        Vector3 worldCorner = spawnVolume.transform.TransformPoint(localCorner);
                        Vector3 rootLocalCorner = coinRoot.InverseTransformPoint(worldCorner);
                        min = Vector3.Min(min, rootLocalCorner);
                        max = Vector3.Max(max, rootLocalCorner);
                    }
                }
            }

            center = (min + max) * 0.5f;
            halfExtents = (max - min) * 0.5f;
            halfExtents = AbsVec(halfExtents);
            return;
        }

        if (TryGetRendererLocalBounds(out center, out halfExtents))
        {
            return;
        }

        center = Vector3.zero;
        halfExtents = AbsVec(fallbackHalfExtents);
    }

    private bool TryGetRendererLocalBounds(out Vector3 center, out Vector3 halfExtents)
    {
        center = Vector3.zero;
        halfExtents = Vector3.zero;

        if (coinRoot == null) return false;
        Renderer[] rs = coinRoot.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0) return false;

        bool has = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r == null) continue;
            Bounds b = r.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iy = -1; iy <= 1; iy += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        Vector3 worldCorner = c + Vector3.Scale(e, new Vector3(ix, iy, iz));
                        Vector3 localCorner = coinRoot.InverseTransformPoint(worldCorner);
                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                        has = true;
                    }
                }
            }
        }

        if (!has) return false;
        center = (min + max) * 0.5f;
        halfExtents = AbsVec((max - min) * 0.5f);
        return true;
    }

    private Vector3 ClampToBounds(Vector3 localPos)
    {
        Vector3 center;
        Vector3 halfExtents;
        GetLocalBounds(out center, out halfExtents);

        float pad = Mathf.Max(0f, volumePadding);
        halfExtents = new Vector3(
            Mathf.Max(0.001f, halfExtents.x - pad),
            Mathf.Max(0.001f, halfExtents.y - pad),
            Mathf.Max(0.001f, halfExtents.z - pad)
        );

        return new Vector3(
            Mathf.Clamp(localPos.x, center.x - halfExtents.x, center.x + halfExtents.x),
            Mathf.Clamp(localPos.y, center.y - halfExtents.y, center.y + halfExtents.y),
            Mathf.Clamp(localPos.z, center.z - halfExtents.z, center.z + halfExtents.z)
        );
    }

    private static Vector3 AbsVec(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private Vector3 SpiralNudge(Vector3 localPos, int index)
    {
        float r = Mathf.Max(minRandomDistance, 0.02f) * (1f + index * 0.08f);
        float a = index * 1.618f; // golden angle-ish
        Vector3 offset = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f, Mathf.Sin(a * 0.5f) * r * 0.4f);
        return localPos + offset;
    }

    private void ConfigureAsVisualOnly(GameObject go)
    {
        if (go == null) return;

        // Disable gameplay pickup behavior if coin prefab reuses exp prefab scripts.
        ExpPickup exp = go.GetComponent<ExpPickup>();
        if (exp != null) exp.enabled = false;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }
    }

    private void ConfigureMotion(GameObject go)
    {
        if (go == null) return;

        Vector3 center;
        Vector3 halfExtents;
        GetLocalBounds(out center, out halfExtents);

        float pad = Mathf.Max(0f, volumePadding);
        halfExtents = new Vector3(
            Mathf.Max(0.001f, halfExtents.x - pad),
            Mathf.Max(0.001f, halfExtents.y - pad),
            Mathf.Max(0.001f, halfExtents.z - pad)
        );

        CoinInContainerMotion motion = go.GetComponent<CoinInContainerMotion>();
        if (motion == null) motion = go.AddComponent<CoinInContainerMotion>();
        motion.Initialize(center, halfExtents, moveSpeedRange, spinSpeedRange);
    }
}
