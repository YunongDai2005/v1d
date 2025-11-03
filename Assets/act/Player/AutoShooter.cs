using UnityEngine;

public class AutoShooter : MonoBehaviour
{
    [Header("🔫 子弹参数")]
    public GameObject bulletPrefab;     // 子弹预制体（挂有 HomingBullet.cs）
    public float shootInterval = 1f;
    public float bulletSpeed = 15f;
    public float bulletDamage = 20f;
    public float searchRadius = 30f;
    public string enemyTag = "enemy";

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= shootInterval)
        {
            timer = 0f;
            ShootAtClosestEnemy();
        }
    }

    void ShootAtClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies.Length == 0) return;

        GameObject closest = null;
        float minDist = Mathf.Infinity;
        Vector3 playerPos = transform.position;

        foreach (var e in enemies)
        {
            float dist = Vector3.Distance(e.transform.position, playerPos);
            if (dist < minDist && dist <= searchRadius)
            {
                minDist = dist;
                closest = e;
            }
        }

        if (closest == null) return;

        // 创建子弹并初始化
        GameObject bullet = Instantiate(
            bulletPrefab,
            transform.position,
            Quaternion.LookRotation((closest.transform.position - transform.position).normalized)
        );

        // 获取子弹脚本
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.target = closest.transform;
            b.damage = bulletDamage;
            b.speed = bulletSpeed;
        }
    }
    void OnDrawGizmosSelected()
    {
        // 只有选中玩家对象时才会显示
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // 橙色透明圈
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        // 可以额外画个文字提示
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
            $"攻击范围: {searchRadius:F1}");
#endif
    }

}
