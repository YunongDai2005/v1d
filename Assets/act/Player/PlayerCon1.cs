using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCon1 : MonoBehaviour
{
    [Header("🧭 基础移动速度")]
    public float moveSpeedX = 5f;
    public float baseSpeedY = 3f;

    [Header("🌊 水中运动特性")]
    [Range(0f, 10f)] public float acceleration = 5f;
    [Range(0f, 10f)] public float waterDrag = 3f;

    [Header("⚖️ 深度控制参数")]
    public float difficultyK = 5f;
    public float totalMass = 1f;
    public float adjustMultiplier = 0.5f;
    public float rangeJ = 2f;

    [Header("📊 调试信息 (只读)")]
    public float H0;       // 初始高度
    public float targetH;  // 目标深度
    public float currentH; // 当前深度
    public float deltaH;   // 深度差

    private Rigidbody rb;
    private Vector3 targetVelocity;
    private Vector3 smoothVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        H0 = transform.position.y;

        // ✅ 确保质量有值
        if (totalMass <= 0f) totalMass = 1f;

        // ✅ 计算目标深度（相对H0）
        targetH = H0 - (difficultyK * totalMass);
    }

    void Update()
    {
        HandleInput();
        ApplyMovement();
    }

    void HandleInput()
    {
        // 当前深度（相对H0）
        currentH = transform.position.y;
        deltaH = targetH - currentH;

        float inputX = Input.GetAxisRaw("Horizontal"); // A/D
        float inputY = Input.GetAxisRaw("Vertical");   // W/S

        float vx = inputX * moveSpeedX;
        float vy = 0f;

        // === 垂直控制逻辑 ===
        float depthAdjust = adjustMultiplier * Mathf.Abs(deltaH);

        // 超出允许范围时锁定方向
        bool aboveLimit = (deltaH < -rangeJ); // 玩家太高
        bool belowLimit = (deltaH > rangeJ);  // 玩家太低

        if (inputY > 0 && !aboveLimit) // 上浮
            vy = baseSpeedY - depthAdjust;
        else if (inputY < 0 && !belowLimit) // 下潜
            vy = -(baseSpeedY + depthAdjust);
        else
            vy = 0f;

        // 合成目标速度
        targetVelocity = new Vector3(vx, vy, 0f);
    }

    void ApplyMovement()
    {
        // 惯性模拟
        smoothVelocity = Vector3.Lerp(smoothVelocity, targetVelocity, Time.deltaTime * acceleration);
        smoothVelocity *= (1f - Time.deltaTime * waterDrag);

        rb.linearVelocity = smoothVelocity;
    }

    // 外部接口
    public void UpdateMass(float newMass)
    {
        totalMass = newMass;
        targetH = H0 - (difficultyK * totalMass);
    }

    public void UpdateDifficulty(float newK)
    {
        difficultyK = newK;
        targetH = H0 - (difficultyK * totalMass);
    }
}