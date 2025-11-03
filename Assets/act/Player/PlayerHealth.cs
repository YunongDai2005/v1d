using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("❤️ 角色血量设置")]
    public float maxHealth = 100f;  // 最大血量
    public float currentHealth = 100f;

    [Header("💥 受击参数")]
    public float damageK = 20f;     // 每次被enemy撞击扣除的血量
    public bool destroyOnDeath = true;

    [Header("🧩 调试显示")]
    public bool showHealthBar = true;
    public Vector2 healthBarPos = new Vector2(30, 180);
    public float healthBarWidth = 200f;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // 当触发器或碰撞器检测到接触时调用（自动检测两种情况）
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("enemy"))
        {
            TakeDamage(damageK);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("enemy"))
        {
            TakeDamage(damageK);
        }
    }

    // 处理伤害逻辑
    void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        Debug.Log($"⚠️ Player 受伤！当前血量：{currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // 死亡逻辑
    void Die()
    {
        Debug.Log("💀 Player 已死亡！");
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            // 你也可以在这里添加死亡动画或禁用控制脚本
            GetComponent<Rigidbody>().isKinematic = true;
            this.enabled = false;
        }
    }

    // --- 可选：在屏幕上绘制血量条 ---
    void OnGUI()
    {
        if (!showHealthBar) return;

        float ratio = currentHealth / maxHealth;
        ratio = Mathf.Clamp01(ratio);

        GUI.Box(new Rect(healthBarPos.x - 2, healthBarPos.y - 2, healthBarWidth + 4, 24), "");
        GUI.color = Color.Lerp(Color.red, Color.green, ratio);
        GUI.Box(new Rect(healthBarPos.x, healthBarPos.y, healthBarWidth * ratio, 20), "");
        GUI.color = Color.white;

        GUI.Label(new Rect(healthBarPos.x + 10, healthBarPos.y - 18, 150, 20), $"HP: {currentHealth:F0}/{maxHealth}");
    }
}
