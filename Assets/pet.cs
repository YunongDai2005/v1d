using UnityEngine;

public class pet : MonoBehaviour
{
    [Header("跟随目标")]
    public string targetTag = "Player";
    public Transform target; // 运行时自动寻找，同步引用可覆盖

    [Header("速度设置")]
    public float normalSpeed = 3f;     // 默认巡航速度
    public float accelSpeed = 6f;      // 加速追赶速度
    public float accelDistance = 8f;   // 超过此距离开始加速
    public float stopDistance = 1.5f;  // 进入此范围内减速至 0

    [Header("平滑与朝向")]
    public float acceleration = 10f; // 速度变换平滑系数
    public float turnSpeed = 10f;    // 朝向插值速度

    private float _currentSpeed;

    void Awake()
    {
        FindTargetIfNeeded();
    }

    void Update()
    {
        FindTargetIfNeeded();
        if (!target) return;

        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        float desiredSpeed = normalSpeed;
        if (distance <= stopDistance)
        {
            desiredSpeed = 0f;
        }
        else if (distance >= accelDistance)
        {
            desiredSpeed = accelSpeed;
        }

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, desiredSpeed, acceleration * Time.deltaTime);

        if (_currentSpeed > 0.01f && distance > 0.01f)
        {
            Vector3 dir = toTarget.normalized;
            transform.position += dir * (_currentSpeed * Time.deltaTime);

            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
        }
    }

    private void FindTargetIfNeeded()
    {
        if (target != null) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null)
        {
            target = go.transform;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, accelDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
