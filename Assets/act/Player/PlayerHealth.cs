using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("❤️ 角色血量设置")]
    public float maxHealth = 100f;  // 最大血量
    public float currentHealth = 100f;

    [Header("💥 受击参数")]
    public float damageK = 20f;     // 每次被enemy撞击扣除的血量
    public bool destroyOnDeath = false;
    [Min(0f)] public float invincibleDurationOnHit = 0.35f;
    [Min(0f)] public float hurtFlashDuration = 0.12f;
    [Range(0f, 1f)] public float hurtFlashMaxAlpha = 0.35f;

    [Header("🛡️ 菜单状态")]
    public bool lockHealthWhenMenuOpen = true;
    public bool resetHealthWhenMenuOpen = true;

    [Header("🔁 死亡流程")]
    public bool openMenuOnDeath = true;
    public bool resetHealthOnDeath = true;
    public float deathTransitionDuration = 0.7f;

    [Header("🧩 调试显示")]
    public bool showHealthBar = true;
    public Vector2 healthBarPos = new Vector2(30, 180);
    public float healthBarWidth = 200f;

    private bool _isDying;
    private float _invincibleTimer;
    private float _hurtFlashTimer;

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
        if (_isDying) return;
        if (_invincibleTimer > 0f) return;
        if (lockHealthWhenMenuOpen && IsMenuOpen()) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        AudioManager.PlayPlayerHurt();
        ComboRankSystem.ResetRunGlobal();
        _invincibleTimer = Mathf.Max(0f, invincibleDurationOnHit);
        _hurtFlashTimer = Mathf.Max(0f, hurtFlashDuration);

        Debug.Log($"Player hurt. Current HP: {currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // 死亡逻辑
    void Die()
    {
        if (_isDying) return;
        _isDying = true;

        Debug.Log("Player died.");
        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float wait = Mathf.Max(0f, deathTransitionDuration);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        if (openMenuOnDeath)
        {
            if (resetHealthOnDeath)
                currentHealth = maxHealth;

            MainMenuOverlay menu = FindObjectOfType<MainMenuOverlay>(true);
            if (menu != null)
            {
                menu.SetLastRunScore(CoinContainerDisplay.GlobalCoinScore);
                ComboRankSystem.ResetRunGlobal();
                menu.OpenMenu();
                _isDying = false;
                yield break;
            }
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            // 你也可以在这里添加死亡动画或禁用控制脚本
            if (rb != null) rb.isKinematic = true;
            this.enabled = false;
        }

        _isDying = false;
    }

    void LateUpdate()
    {
        if (_invincibleTimer > 0f) _invincibleTimer -= Time.deltaTime;
        if (_hurtFlashTimer > 0f) _hurtFlashTimer -= Time.deltaTime;

        if (resetHealthWhenMenuOpen && IsMenuOpen())
        {
            currentHealth = maxHealth;
        }
    }

    bool IsMenuOpen()
    {
        MainMenuOverlay menu = FindObjectOfType<MainMenuOverlay>(true);
        return menu != null && menu.IsMenuOpen;
    }

    // --- 可选：在屏幕上绘制血量条 ---
    void OnGUI()
    {
        if (_hurtFlashTimer > 0f)
        {
            float t = Mathf.Clamp01(_hurtFlashTimer / Mathf.Max(0.001f, hurtFlashDuration));
            float a = hurtFlashMaxAlpha * t;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

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
