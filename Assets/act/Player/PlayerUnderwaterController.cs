using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerUnderwaterController : MonoBehaviour
{
    [Header("🎮 基础移动参数")]
    public float maxSpeedX = 5f;
    public float maxSpeedY = 4f;
    public float acceleration = 8f;
    public float drag = 4f;

    [Header("⚖️ 深度控制参数")]
    public float difficultyK = 5f;
    public float totalMass = 1f;
    public float rangeJ = 3f;
    public bool useCosineCurve = true;

    [Header("🌊 自动浮力参数")]
    public bool enableAutoBuoyancy = true;
    public float autoBuoyancyStrength = 0.6f;
    public float autoBuoyancySpeed = 2f;

    [Header("⬆⬇ 边界速度调制")]
    [Range(0f, 1f)] public float boundarySlowMin = 0.2f;
    [Min(1f)] public float boundaryBoostMax = 1.5f;

    // 💨 冲刺分方向参数
    [Header("💨 冲刺参数（分方向）")]
    [Tooltip("水平冲刺（A/D）")]
    public float dashForceX = 10f;
    public float dashDurationX = 0.3f;
    public float dashCooldownX = 1.5f;

    [Tooltip("垂直冲刺（W/S）")]
    public float dashForceY = 7f;
    public float dashDurationY = 0.4f;
    public float dashCooldownY = 2.5f;

    [Tooltip("冲刺衰减曲线")]
    public AnimationCurve dashEase = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("💥 冲刺特效（可选）")]
    public GameObject dashEffectHorizontal; // 水平冲刺特效 prefab
    public GameObject dashEffectVertical;   // 垂直冲刺特效 prefab
    public Transform effectSpawnPoint;      // 特效生成点（可为空，默认角色位置）

    [Header("🧩 调试UI设置")]
    public bool showDebugUI = true;
    public Vector2 debugUIPos = new Vector2(30, 30);
    public float debugUIScale = 1f;

    [Header("📊 调试信息 (只读)")]
    public float H0;
    public float targetH;
    public float currentH;
    public float deltaH;
    public Vector2 inputDir;
    public float verticalSpeed;

    private Rigidbody rb;
    private Vector3 targetVelocity;
    private Vector3 smoothVelocity;

    // 冲刺状态
    private bool isDashing = false;
    private bool dashIsVertical = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private Vector3 dashDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        H0 = transform.position.y;
        if (totalMass <= 0f) totalMass = 1f;
        targetH = H0 - Mathf.Abs(difficultyK * totalMass);
    }

    void Update()
    {
        targetH = H0 - Mathf.Abs(difficultyK * totalMass);
        HandleInput();
        ApplyMovement();
        HandleDash();
    }

    void HandleInput()
    {
        currentH = transform.position.y;
        deltaH = currentH - targetH;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        inputDir = new Vector2(moveX, moveY).normalized;

        float vx = inputDir.x * maxSpeedX;
        float vy = 0f;

        // 左右朝向
        if (Mathf.Abs(moveX) > 0.01f)
        {
            if (moveX > 0f)
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // D → 右
            else if (moveX < 0f)
                transform.rotation = Quaternion.Euler(0f, 0f, 0f); // A → 左
        }

        float t = Mathf.Clamp01(Mathf.Abs(deltaH) / Mathf.Max(rangeJ, 1e-4f));

        if (Mathf.Abs(inputDir.y) > 0.01f)
        {
            int dirY = inputDir.y > 0 ? 1 : -1;
            float absH = Mathf.Abs(deltaH);
            float baseSpeed = GetDepthSpeed(Mathf.Min(absH, rangeJ - 1e-4f), rangeJ, maxSpeedY);
            if (absH >= rangeJ) baseSpeed = GetDepthSpeed(rangeJ - 1e-4f, rangeJ, maxSpeedY);

            float scale = 1f;
            if (dirY > 0)
            {
                if (deltaH > 0f) scale = Mathf.Lerp(1f, boundarySlowMin, t);
                else if (deltaH < 0f) scale = Mathf.Lerp(1f, boundaryBoostMax, t);
            }
            else
            {
                if (deltaH < 0f) scale = Mathf.Lerp(1f, boundarySlowMin, t);
                else if (deltaH > 0f) scale = Mathf.Lerp(1f, boundaryBoostMax, t);
            }

            float finalSpeed = baseSpeed * scale;
            vy = dirY * finalSpeed;
        }
        else if (enableAutoBuoyancy)
        {
            if (Mathf.Abs(deltaH) > 0.05f)
            {
                float autoSpeed = GetDepthSpeed(Mathf.Abs(deltaH), rangeJ, autoBuoyancySpeed);
                int buoyDir = deltaH > 0 ? -1 : 1;
                vy = buoyDir * autoSpeed * autoBuoyancyStrength;
            }
        }

        targetVelocity = new Vector3(vx, vy, 0f);
        verticalSpeed = vy;

        // 检测空格冲刺输入
        if (Input.GetKeyDown(KeyCode.Space) && !isDashing && dashCooldownTimer <= 0f)
        {
            StartDash();
        }
    }

    void StartDash()
    {
        isDashing = true;

        // 根据输入方向区分 WS / AD
        if (Mathf.Abs(inputDir.y) > Mathf.Abs(inputDir.x))
            dashIsVertical = true;
        else
            dashIsVertical = false;

        float dashForce = dashIsVertical ? dashForceY : dashForceX;
        float dashDuration = dashIsVertical ? dashDurationY : dashDurationX;
        float dashCooldown = dashIsVertical ? dashCooldownY : dashCooldownX;

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        // 计算冲刺方向
        if (inputDir.magnitude > 0.1f)
            dashDirection = new Vector3(inputDir.x, inputDir.y, 0f).normalized;
        else
            dashDirection = transform.right * (transform.rotation.eulerAngles.y == 180 ? -1f : 1f);

        // 冲刺瞬间添加脉冲力
        rb.linearVelocity += dashDirection * dashForce;

        // 生成特效
        SpawnDashEffect();
    }

    void SpawnDashEffect()
    {
        if (dashIsVertical && dashEffectVertical != null)
        {
            Instantiate(dashEffectVertical,
                effectSpawnPoint ? effectSpawnPoint.position : transform.position,
                Quaternion.identity);
        }
        else if (!dashIsVertical && dashEffectHorizontal != null)
        {
            Instantiate(dashEffectHorizontal,
                effectSpawnPoint ? effectSpawnPoint.position : transform.position,
                Quaternion.identity);
        }
    }

    void HandleDash()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            float totalDuration = dashIsVertical ? dashDurationY : dashDurationX;
            float t = 1f - (dashTimer / totalDuration);
            float ease = dashEase.Evaluate(t);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, ease * Time.deltaTime * 5f);

            if (dashTimer <= 0f)
                isDashing = false;
        }
    }

    void ApplyMovement()
    {
        if (isDashing) return; // 冲刺时跳过普通移动
        smoothVelocity = Vector3.Lerp(smoothVelocity, targetVelocity, Time.deltaTime * acceleration);
        smoothVelocity *= (1f - Time.deltaTime * drag);
        rb.linearVelocity = smoothVelocity;
    }

    float GetDepthSpeed(float h, float j, float p)
    {
        if (h >= j) return 0f;
        if (useCosineCurve)
            return p * 0.5f * (1f + Mathf.Cos(Mathf.PI * h / j));
        else
            return p * Mathf.Sqrt(1f - Mathf.Pow(h / j, 2f));
    }

    void OnGUI()
    {
        if (!showDebugUI) return;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * debugUIScale);
        float baseY = debugUIPos.y;

        GUI.Box(new Rect(debugUIPos.x - 10, baseY - 10, 260, 160), "🌊 Player Debug Info");

        GUI.Label(new Rect(debugUIPos.x, baseY + 10, 250, 20), $"当前深度差 ΔH: {deltaH:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 30, 250, 20), $"目标深度: {targetH:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 50, 250, 20), $"垂直速度: {verticalSpeed:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 70, 250, 20), $"输入方向: {inputDir}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 90, 250, 20), $"状态: {GetStateText()}");

        string dashState = isDashing ? "冲刺中 💨" :
                           dashCooldownTimer > 0 ? $"冷却中 {dashCooldownTimer:F1}s" : "可冲刺 ✅";
        GUI.Label(new Rect(debugUIPos.x, baseY + 110, 250, 20), $"冲刺状态: {dashState}");
        if (isDashing)
            GUI.Label(new Rect(debugUIPos.x, baseY + 130, 250, 20),
                $"方向: {(dashIsVertical ? "垂直WS 🧭" : "水平AD 🧭")}");

        float barWidth = 200f;
        float ratio = Mathf.Clamp01(Mathf.Abs(deltaH) / rangeJ);
        float fill = barWidth * (1f - ratio);
        GUI.color = Color.Lerp(Color.red, Color.green, 1f - ratio);
        GUI.Box(new Rect(debugUIPos.x, baseY + 150, barWidth, 10), GUIContent.none);
        GUI.color = Color.cyan;
        GUI.Box(new Rect(debugUIPos.x, baseY + 150, fill, 10), GUIContent.none);
        GUI.color = Color.white;
    }

    string GetStateText()
    {
        if (isDashing) return "冲刺中 💨";
        if (Mathf.Abs(inputDir.y) > 0.01f)
        {
            if (inputDir.y > 0) return "手动上浮 ↑";
            else return "手动下潜 ↓";
        }
        else if (enableAutoBuoyancy)
        {
            if (Mathf.Abs(deltaH) < 0.05f) return "静止平衡 ⚖️";
            return deltaH > 0 ? "自动下沉 🌀" : "自动上浮 🫧";
        }
        else
        {
            return "静止中";
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(-50, targetH, 0), new Vector3(50, targetH, 0));

        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawLine(new Vector3(-50, targetH + rangeJ, 0), new Vector3(50, targetH + rangeJ, 0));
        Gizmos.DrawLine(new Vector3(-50, targetH - rangeJ, 0), new Vector3(50, targetH - rangeJ, 0));
    }
}
