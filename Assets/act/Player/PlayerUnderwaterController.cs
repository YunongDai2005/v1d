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

    private Vector2 rawInputDir;
    private Rigidbody rb;
    private Vector3 targetVelocity;
    private Vector3 smoothVelocity;

    // 冲刺状态
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float currentDashDuration = 0f;
    private float dashCooldownTimer = 0f;
    private bool dashHasHorizontalInput = false;
    private bool dashHasVerticalInput = false;

    private enum DashOrientation
    {
        None,
        Horizontal,
        Vertical,
        Diagonal
    }

    private DashOrientation dashOrientation = DashOrientation.None;

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
        rawInputDir = new Vector2(moveX, moveY);
        inputDir = rawInputDir.normalized;

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
            if (rawInputDir.sqrMagnitude > 0.01f)
                StartDash(rawInputDir);
        }
    }

    void StartDash(Vector2 dashInputRaw)
    {
        isDashing = true;

        bool hasHorizontal = Mathf.Abs(dashInputRaw.x) > 0.01f;
        bool hasVertical = Mathf.Abs(dashInputRaw.y) > 0.01f;

        // 如果仍然没有有效输入，则按面对方向进行水平冲刺
        if (!hasHorizontal && !hasVertical)
        {
            dashOrientation = DashOrientation.Horizontal;
            dashHasHorizontalInput = true;
            dashHasVerticalInput = false;
            dashTimer = dashDurationX;
            currentDashDuration = dashDurationX;
            dashCooldownTimer = dashCooldownX;
            Vector3 fallbackDir = transform.right * (transform.rotation.eulerAngles.y == 180 ? -1f : 1f);
            rb.linearVelocity += fallbackDir * dashForceX;
            SpawnDashEffect();
            return;
        }

        dashHasHorizontalInput = hasHorizontal;
        dashHasVerticalInput = hasVertical;
        dashOrientation = hasHorizontal && hasVertical
            ? DashOrientation.Diagonal
            : (hasVertical ? DashOrientation.Vertical : DashOrientation.Horizontal);

        float dashDuration = 0f;
        float dashCooldown = 0f;
        Vector3 dashImpulse = Vector3.zero;

        if (hasHorizontal)
        {
            dashDuration = Mathf.Max(dashDuration, dashDurationX);
            dashCooldown = Mathf.Max(dashCooldown, dashCooldownX);
            dashImpulse.x = Mathf.Sign(dashInputRaw.x) * dashForceX;
        }

        if (hasVertical)
        {
            dashDuration = Mathf.Max(dashDuration, dashDurationY);
            dashCooldown = Mathf.Max(dashCooldown, dashCooldownY);
            dashImpulse.y = Mathf.Sign(dashInputRaw.y) * dashForceY;
        }

        if (dashDuration <= 0f) dashDuration = 0.0001f;
        if (dashCooldown <= 0f) dashCooldown = Mathf.Max(dashCooldownX, dashCooldownY);

        dashTimer = dashDuration;
        currentDashDuration = dashDuration;
        dashCooldownTimer = dashCooldown;

        // 冲刺瞬间添加脉冲力（横纵分量叠加）
        rb.linearVelocity += dashImpulse;

        // 生成特效
        SpawnDashEffect();
    }

    void SpawnDashEffect()
    {
        Vector3 spawnPos = effectSpawnPoint ? effectSpawnPoint.position : transform.position;

        if (dashHasVerticalInput && dashEffectVertical != null)
        {
            Instantiate(dashEffectVertical, spawnPos, Quaternion.identity);
        }

        if (dashHasHorizontalInput && dashEffectHorizontal != null)
        {
            Instantiate(dashEffectHorizontal, spawnPos, Quaternion.identity);
        }
    }

    void HandleDash()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            float totalDuration = Mathf.Max(currentDashDuration, 1e-4f);
            float t = 1f - (dashTimer / totalDuration);
            float ease = dashEase.Evaluate(t);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, ease * Time.deltaTime * 5f);

            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashOrientation = DashOrientation.None;
                dashHasHorizontalInput = false;
                dashHasVerticalInput = false;
            }
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
                $"方向: {GetDashOrientationText()}");

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

    string GetDashOrientationText()
    {
        switch (dashOrientation)
        {
            case DashOrientation.Vertical:
                return "垂直WS 🧭";
            case DashOrientation.Horizontal:
                return "水平AD 🧭";
            case DashOrientation.Diagonal:
                return "斜向组合 🧭";
            default:
                return "未输入";
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
