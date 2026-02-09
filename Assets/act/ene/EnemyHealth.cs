using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("👾 敌人生命参数")]
    public float maxHealth = 50f;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        Debug.Log($"{gameObject.name} took {amount} damage. HP left: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        AudioManager.PlayEnemyDead();
        ComboRankSystem.RegisterKillGlobal();
        Destroy(gameObject);
    }
}
