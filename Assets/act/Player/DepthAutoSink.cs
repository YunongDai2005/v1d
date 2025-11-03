using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DepthAutoSink : MonoBehaviour
{
    private Rigidbody rb;

    [Header("浮力参数")]
    public float K = 1f;                // 当前负重
    public float difficulty = 5f;       // 游戏难度系数
    public float adjustValue = 1.0f;    // 吸引强度（正数即可）
    public float damping = 0.9f;        // 阻尼（越小越漂浮）
    public float easyRange = 3f;        // 自由活动范围（±米）
    public float nonlinearity = 1.5f;   // 非线性斜率（越大越陡）

    [Header("调试状态显示")]
    public float zeroDepthY;
    public float targetDepth;
    public float currentDepth;
    public float buoyancyForce;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        zeroDepthY = transform.position.y;
        targetDepth = K * difficulty;
    }

    void FixedUpdate()
    {
        currentDepth = zeroDepthY - transform.position.y;
        targetDepth = K * difficulty;

        float depthDiff = currentDepth - targetDepth;

        // 🧮 非线性浮力曲线
        float normalized = depthDiff / easyRange;
        float nonlinearFactor = (float)System.Math.Tanh(normalized * nonlinearity);
        float acceleration = -nonlinearFactor * adjustValue;

        buoyancyForce = acceleration * damping;

        rb.AddForce(Vector3.up * buoyancyForce, ForceMode.Acceleration);
    }

    public void AddWeight(float delta)
    {
        K += delta;
    }

    // 🎨 Gizmos 调试显示
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 浮力方向箭头
        Gizmos.color = buoyancyForce > 0 ? Color.cyan : Color.red;
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * buoyancyForce * 0.2f; // 缩放箭头长度
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.05f);

        // 目标深度线
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Vector3 targetPos = new Vector3(transform.position.x, zeroDepthY - targetDepth, transform.position.z);
        Gizmos.DrawLine(targetPos + Vector3.left * 5f, targetPos + Vector3.right * 5f);

        // 可活动范围线（easyRange 上下界）
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Vector3 upperBound = new Vector3(transform.position.x, zeroDepthY - (targetDepth - easyRange), transform.position.z);
        Vector3 lowerBound = new Vector3(transform.position.x, zeroDepthY - (targetDepth + easyRange), transform.position.z);
        Gizmos.DrawLine(upperBound + Vector3.left * 2f, upperBound + Vector3.right * 2f);
        Gizmos.DrawLine(lowerBound + Vector3.left * 2f, lowerBound + Vector3.right * 2f);
    }
}
