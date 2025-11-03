using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("🎯 子弹参数")]
    public float damage = 20f;
    public float speed = 15f;
    public float rotateSpeed = 10f;     // 追踪旋转速度
    public float lifeTime = 5f;
    public string enemyTag = "enemy";
    public Transform target;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }

        // 5 秒后自动销毁
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        if (target == null)
        {
            // 若目标消失，子弹继续直飞
            if (rb != null)
                rb.linearVelocity = transform.forward * speed;
            return;
        }

        // 计算追踪方向
        Vector3 dir = (target.position - transform.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(dir);

        // 平滑旋转
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotateSpeed * Time.deltaTime);

        // 更新速度
        if (rb != null)
            rb.linearVelocity = transform.forward * speed;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(enemyTag))
        {
            EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            Destroy(gameObject); // 击中销毁
        }
        else if (!collision.gameObject.CompareTag("Player"))
        {
            Destroy(gameObject); // 撞到其他物体销毁
        }
    }
    void OnDrawGizmosSelected()
    {
        // 绘制子弹前进方向
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
        Gizmos.DrawSphere(transform.position + transform.forward * 3f, 0.2f);

        // 如果有目标，显示跟踪线和范围圈
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, 1f); // 目标范围圈

#if UNITY_EDITOR
            UnityEditor.Handles.Label(target.position + Vector3.up * 1f, "目标");
#endif
        }
    }


}