using UnityEngine;

/// <summary>
/// Keeps the total spawned prefabs equal to floor(difficultyK / difficultyDivisor) while playing.
/// Prefabs are now spawned directly at the attached target's local origin (0,0,0).
/// </summary>
public class DifficultySpawnController : MonoBehaviour
{
    [Header("Player & Prefab")]
    [SerializeField] private PlayerUnderwaterController playerUnderwater;
    [SerializeField] private GameObject mountPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Spawn Volume (local offsets)")]
    [SerializeField] private Transform volumeOrigin;
    [SerializeField] private Vector2 xRange = new Vector2(-5f, 5f);
    [SerializeField] private Vector2 yRange = new Vector2(-2f, 2f);
    [SerializeField] private Vector2 zRange = new Vector2(-5f, 5f);

    [Header("Cylinder Surface")]
    [SerializeField] private float cylinderRadius = 4f;
    [SerializeField] private Vector3 cylinderCenterOffset = Vector3.zero;

    [Header("Surfaces To Use")]
    [SerializeField] private bool spawnOnBottomPlane = true;
    [SerializeField] private bool spawnOnTopPlane = true;
    [SerializeField] private bool spawnOnCylinderWall = true;

    [Header("Difficulty Based Spawn")]
    [Tooltip("Number of coins spawned = floor(difficultyK / difficultyDivisor).")]
    [SerializeField, Min(0.01f)] private float difficultyDivisor = 10f;

    private int spawnedCount;

    private void Start()
    {
        spawnedCount = 0;
        TrySpawnCoins();
    }

    private void Update()
    {
        TrySpawnCoins();
    }

    private void TrySpawnCoins()
    {
        if (playerUnderwater == null || mountPrefab == null)
            return;

        float kValue = Mathf.Max(0f, playerUnderwater.difficultyK);
        int targetCount = Mathf.FloorToInt(kValue / Mathf.Max(0.01f, difficultyDivisor));
        int coinsToSpawn = targetCount - spawnedCount;
        if (coinsToSpawn <= 0)
            return;

        for (int i = 0; i < coinsToSpawn; i++)
            InstantiatePrefab(GetTargetOriginPosition());

        spawnedCount += coinsToSpawn;
    }

    private void InstantiatePrefab(Vector3 worldPosition)
    {
        Instantiate(mountPrefab, worldPosition, Quaternion.identity, spawnParent);
    }

    private Vector3 GetTargetOriginPosition()
    {
        Transform origin = volumeOrigin != null ? volumeOrigin : transform;
        return origin.position;
    }
}
