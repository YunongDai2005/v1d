using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    public GameObject enemyPrefab;        // 敌人预制体
    public GameObject player;              // 玩家引用
    public float spawnInterval = 2f;      // 生成间隔时间
    public int maxEnemies = 10;           // 同时存在的最大数量
    public float spawnDistance = 10f;     // 与玩家的生成距离
    public float fixedZ = 0f;             // 固定Z轴（2.5D效果）

    private float timer = 0f;
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Update()
    {
        // 清理已经销毁的敌人
        activeEnemies.RemoveAll(e => e == null);

        timer += Time.deltaTime;

        if (timer >= spawnInterval && activeEnemies.Count < maxEnemies)
        {
            SpawnEnemy();
            timer = 0f;
        }
    }

    void SpawnEnemy()
    {
        if (player == null || enemyPrefab == null) return;

        // 随机方向生成敌人
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = player.transform.position + new Vector3(randomDir.x, randomDir.y, 0f) * spawnDistance;
        spawnPos.z = fixedZ; // 固定Z轴

        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        activeEnemies.Add(enemy);
    }
}
