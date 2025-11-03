using UnityEngine;

public class EnemyDeathSpawner : MonoBehaviour
{
    [Header("EXP 预制体")]
    public GameObject expPrefab;

    private bool hasSpawned = false; // 防止重复生成

    void OnDestroy()
    {
        if (hasSpawned || expPrefab == null) return;
        hasSpawned = true;

        // 在敌人被摧毁的位置生成经验球
        Instantiate(expPrefab, transform.position, Quaternion.identity);
    }
}
