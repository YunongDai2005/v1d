using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BackgroundPrefabEntry
{
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
    [Range(0f, 1f)] public float minNoise = 0f;
    [Range(0f, 1f)] public float maxNoise = 1f;
    public Vector2 scaleRange = new Vector2(1f, 1f);
}

public class BackgroundPooledTag : MonoBehaviour
{
    public GameObject sourcePrefab;
}

public class PerlinBackgroundChunkSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform container;

    [Header("Chunk")]
    [SerializeField, Min(4)] private int chunkWidth = 24;
    [SerializeField, Min(1)] private int activeChunkRadius = 2;

    [Header("Placement")]
    [SerializeField, Min(0.2f)] private float xStep = 1.2f;
    [SerializeField] private float yMin = -14f;
    [SerializeField] private float yMax = 6f;
    [SerializeField] private float fixedZ = 0f;
    [SerializeField, Range(0f, 1f)] private float spawnThreshold = 0.62f;
    [SerializeField, Min(0.1f)] private float minDistance = 1.5f;

    [Header("Noise")]
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(0.0001f)] private float spawnNoiseScale = 0.055f;
    [SerializeField, Min(0.0001f)] private float heightNoiseScale = 0.035f;
    [SerializeField, Min(0.0001f)] private float typeNoiseScale = 0.07f;

    [Header("Visual Random")]
    [SerializeField] private Vector2 randomYawRange = new Vector2(0f, 360f);
    [SerializeField] private bool randomFlipX = true;

    [Header("Prefabs")]
    [SerializeField] private BackgroundPrefabEntry[] prefabEntries;

    private readonly Dictionary<int, List<GameObject>> chunkInstances = new Dictionary<int, List<GameObject>>();
    private readonly Dictionary<GameObject, Queue<GameObject>> poolByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly HashSet<int> desiredChunks = new HashSet<int>();

    private float minDistanceSqr;

    private void Awake()
    {
        if (container == null)
        {
            container = transform;
        }

        minDistanceSqr = minDistance * minDistance;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        RefreshChunks(force: true);
    }

    private void Update()
    {
        if (player == null) return;
        RefreshChunks(force: false);
    }

    private void RefreshChunks(bool force)
    {
        int centerChunk = Mathf.FloorToInt(player.position.x / chunkWidth);

        desiredChunks.Clear();
        for (int i = -activeChunkRadius; i <= activeChunkRadius; i++)
        {
            desiredChunks.Add(centerChunk + i);
        }

        for (int i = -activeChunkRadius; i <= activeChunkRadius; i++)
        {
            int chunkId = centerChunk + i;
            if (force || !chunkInstances.ContainsKey(chunkId))
            {
                SpawnChunk(chunkId);
            }
        }

        List<int> removeList = null;
        foreach (var kv in chunkInstances)
        {
            if (desiredChunks.Contains(kv.Key)) continue;
            if (removeList == null) removeList = new List<int>();
            removeList.Add(kv.Key);
        }

        if (removeList == null) return;
        for (int i = 0; i < removeList.Count; i++)
        {
            DespawnChunk(removeList[i]);
        }
    }

    private void SpawnChunk(int chunkId)
    {
        if (chunkInstances.ContainsKey(chunkId)) return;

        var spawned = new List<GameObject>();
        chunkInstances[chunkId] = spawned;

        float chunkStart = chunkId * chunkWidth;
        float chunkEnd = chunkStart + chunkWidth;
        var positions = new List<Vector3>();

        for (float x = chunkStart; x <= chunkEnd; x += xStep)
        {
            float nSpawn = Perlin(x, 11.73f, spawnNoiseScale);
            if (nSpawn < spawnThreshold) continue;

            float nHeight = Perlin(x, 37.79f, heightNoiseScale);
            float y = Mathf.Lerp(yMin, yMax, nHeight);
            var pos = new Vector3(x, y, fixedZ);
            if (!CanPlace(pos, positions)) continue;

            float nType = Perlin(x, 73.11f, typeNoiseScale);
            BackgroundPrefabEntry entry = ChooseEntry(nType);
            if (entry == null || entry.prefab == null) continue;

            GameObject go = SpawnObject(entry.prefab, pos);
            ApplyVisualVariation(go.transform, entry, x);
            spawned.Add(go);
            positions.Add(pos);
        }
    }

    private void DespawnChunk(int chunkId)
    {
        if (!chunkInstances.TryGetValue(chunkId, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            GameObject go = list[i];
            if (go == null) continue;
            go.SetActive(false);
            go.transform.SetParent(container, true);
            var tag = go.GetComponent<BackgroundPooledTag>();
            if (tag == null || tag.sourcePrefab == null)
            {
                Destroy(go);
                continue;
            }
            if (!poolByPrefab.TryGetValue(tag.sourcePrefab, out var q))
            {
                q = new Queue<GameObject>();
                poolByPrefab[tag.sourcePrefab] = q;
            }
            q.Enqueue(go);
        }

        chunkInstances.Remove(chunkId);
    }

    private bool CanPlace(Vector3 candidate, List<Vector3> placedInChunk)
    {
        for (int i = 0; i < placedInChunk.Count; i++)
        {
            if ((candidate - placedInChunk[i]).sqrMagnitude < minDistanceSqr)
            {
                return false;
            }
        }
        return true;
    }

    private BackgroundPrefabEntry ChooseEntry(float noiseValue)
    {
        if (prefabEntries == null || prefabEntries.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < prefabEntries.Length; i++)
        {
            var e = prefabEntries[i];
            if (e == null || e.prefab == null) continue;
            if (noiseValue < e.minNoise || noiseValue > e.maxNoise) continue;
            total += Mathf.Max(0f, e.weight);
        }
        if (total <= 0f) return null;

        float pick = Deterministic01(noiseValue, 101.37f) * total;
        float acc = 0f;
        for (int i = 0; i < prefabEntries.Length; i++)
        {
            var e = prefabEntries[i];
            if (e == null || e.prefab == null) continue;
            if (noiseValue < e.minNoise || noiseValue > e.maxNoise) continue;
            acc += Mathf.Max(0f, e.weight);
            if (pick <= acc) return e;
        }

        return prefabEntries[prefabEntries.Length - 1];
    }

    private GameObject SpawnObject(GameObject prefab, Vector3 position)
    {
        if (poolByPrefab.TryGetValue(prefab, out var q) && q.Count > 0)
        {
            GameObject reused = q.Dequeue();
            if (reused != null)
            {
                reused.transform.position = position;
                reused.transform.rotation = Quaternion.identity;
                reused.transform.localScale = Vector3.one;
                reused.transform.SetParent(container, true);
                reused.SetActive(true);
                return reused;
            }
        }

        GameObject created = Instantiate(prefab, position, Quaternion.identity, container);
        var tag = created.GetComponent<BackgroundPooledTag>();
        if (tag == null)
        {
            tag = created.AddComponent<BackgroundPooledTag>();
        }
        tag.sourcePrefab = prefab;
        return created;
    }

    private void ApplyVisualVariation(Transform t, BackgroundPrefabEntry entry, float x)
    {
        float yaw01 = Perlin(x, 139.15f, 0.091f);
        float yaw = Mathf.Lerp(randomYawRange.x, randomYawRange.y, yaw01);
        t.rotation = Quaternion.Euler(0f, yaw, 0f);

        float scale01 = Perlin(x, 159.27f, 0.083f);
        float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, scale01);
        t.localScale = new Vector3(scale, scale, scale);

        if (randomFlipX)
        {
            float flip = Perlin(x, 179.41f, 0.087f);
            if (flip > 0.5f)
            {
                Vector3 s = t.localScale;
                t.localScale = new Vector3(-s.x, s.y, s.z);
            }
        }
    }

    private float Perlin(float x, float yOffset, float scale)
    {
        return Mathf.PerlinNoise((x + seed) * scale, yOffset + seed * 0.001f);
    }

    private float Deterministic01(float x, float salt)
    {
        return Mathf.Repeat(Mathf.Sin((x + salt) * 128.371f) * 43758.5453f, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
        if (player != null)
        {
            int centerChunk = Mathf.FloorToInt(player.position.x / chunkWidth);
            for (int i = -activeChunkRadius; i <= activeChunkRadius; i++)
            {
                float startX = (centerChunk + i) * chunkWidth;
                Vector3 center = new Vector3(startX + chunkWidth * 0.5f, (yMin + yMax) * 0.5f, fixedZ);
                Vector3 size = new Vector3(chunkWidth, Mathf.Abs(yMax - yMin), 0.1f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
