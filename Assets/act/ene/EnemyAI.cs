using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 3f;           // 普通追踪速度
    public float dashSpeed = 10f;          // 冲刺速度
    public float dashDuration = 0.5f;      // 冲刺持续时间
    public float cooldownTime = 2f;        // 冲刺失败后等待时间

    [Header("范围参数")]
    public float detectionRange = 15f;     // 寻敌范围
    public float dashTriggerRange = 5f;    // 冲刺触发范围

    [Header("冲刺前减速准备时间")]
    public float dashPrepTime = 0.3f;      // 冲刺前停顿/减速时间

    private Transform player;
    private Rigidbody rb;
    private bool isDashing = false;
    private bool isCoolingDown = false;
    private RigidbodyConstraints initialConstraints;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        initialConstraints = rb.constraints; // 记录初始约束，便于冲刺结束后还原

        // 自动寻找玩家
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
            else
                Debug.LogWarning("Player not found. Make sure the player object has tag 'Player'.");
        }
    }

    void FixedUpdate()
    {
        // 冲刺时锁定旋转，避免碰撞导致旋转；非冲刺还原
        rb.constraints = isDashing ? (initialConstraints | RigidbodyConstraints.FreezeRotation)
                                   : initialConstraints;
        if (isDashing) rb.angularVelocity = Vector3.zero;

        if (player == null || isDashing || isCoolingDown) return;

        float distance = Vector3.Distance(transform.position, player.position);
        Vector3 direction = (player.position - transform.position).normalized;

        if (distance <= dashTriggerRange)
        {
            // 进入冲刺协程（冲刺前旋转面向玩家）
            StartCoroutine(DashTowardsPlayer(direction));
        }
        else if (distance <= detectionRange)
        {
            // 普通追踪玩家
            rb.linearVelocity = direction * moveSpeed;

            // 让敌人面向移动方向
            transform.forward = direction;
        }
        else
        {
            // 玩家太远，停止移动
            rb.linearVelocity = Vector3.zero;
        }
    }

    IEnumerator DashTowardsPlayer(Vector3 direction)
    {
        isDashing = true;

        // 冲刺前旋转面向玩家
        transform.rotation = Quaternion.LookRotation(direction);

        // 减速准备时间
        rb.linearVelocity = Vector3.zero;
        yield return new WaitForSeconds(dashPrepTime);

        // 冲刺阶段（沿旋转后的方向固定冲刺）
        float dashTime = 0f;
        while (dashTime < dashDuration)
        {
            rb.linearVelocity = transform.forward * dashSpeed;
            dashTime += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.zero;
        isDashing = false;

        // 冲刺失败 → 冷却一段时间
        StartCoroutine(Cooldown());
    }

    IEnumerator Cooldown()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(cooldownTime);
        isCoolingDown = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 碰到玩家 → 可以在这里加伤害逻辑
           //Destroy(gameObject);
        }
    }
}
