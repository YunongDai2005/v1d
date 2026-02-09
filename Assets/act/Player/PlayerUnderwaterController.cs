using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerUnderwaterController : MonoBehaviour
{
    public bool IsUsingExternalInput => useExternalInput;

    [Header("?? 基础移动参数")]
    public float maxSpeedX = 5f;
    public float maxSpeedY = 4f;
    public float acceleration = 8f;
    public float drag = 4f;

    [Header("?? 深度控制参数")]
    public float difficultyK = 5f;
    public float totalMass = 1f;
    public float rangeJ = 3f;
    public bool useCosineCurve = true;

    [Header("?? 自动浮力参数")]
    public bool enableAutoBuoyancy = true;
    public float autoBuoyancyStrength = 0.6f;
    public float autoBuoyancySpeed = 2f;

    [Header("?? 边界速度调制")]
    [Range(0f, 1f)] public float boundarySlowMin = 0.2f;
    [Min(1f)] public float boundaryBoostMax = 1.5f;

    [Header("?? 冲刺参数（分方向）")]
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

    [Header("?? 冲刺特效（可选）")]
    public GameObject dashEffectHorizontal;
    public GameObject dashEffectVertical;
    public Transform effectSpawnPoint;
    [Tooltip("When no dash effect prefab is assigned, auto spawn runtime underwater burst effect.")]
    public bool useProceduralDashEffectFallback = true;
    [Range(0.5f, 2f)] public float proceduralDashFxLifeScale = 1f;

    [Header("?? 冲刺残影（斯安威斯坦感）")]
    public bool enableDashGhostTrail = true;
    [Tooltip("建议使用半透明材质的玩家残影预制体。")]
    public GameObject dashGhostPrefab;
    [Min(1)] public int dashGhostCount = 4;
    [Min(0.05f)] public float dashGhostSpacing = 0.45f;
    [Min(0.05f)] public float dashGhostLifetime = 0.35f;
    [Min(0.005f)] public float dashGhostMinInterval = 0.02f;
    public Color dashGhostStartColor = new Color(0.45f, 0.95f, 1f, 0.45f);
    public Color dashGhostEndColor = new Color(0.9f, 0.2f, 1f, 0f);

    [Header("?? 调试UI设置")]
    public bool showDebugUI = true;
    public Vector2 debugUIPos = new Vector2(30, 30);
    public float debugUIScale = 1f;

    [Header("?? 调试信息 (只读)")]
    public float H0;
    public float targetH;
    public float currentH;
    public float deltaH;
    public Vector2 inputDir;
    public float verticalSpeed;

    private Vector2 rawInputDir;
    private bool useExternalInput;
    private Vector2 externalInputDir;
    private bool externalDashPressed;
    private Rigidbody rb;
    private Vector3 targetVelocity;
    private Vector3 smoothVelocity;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float currentDashDuration = 0f;
    private float dashCooldownTimer = 0f;
    private bool dashHasHorizontalInput = false;
    private bool dashHasVerticalInput = false;
    private Coroutine _ghostTrailRoutine;

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

        float moveX = useExternalInput ? externalInputDir.x : Input.GetAxisRaw("Horizontal");
        float moveY = useExternalInput ? externalInputDir.y : Input.GetAxisRaw("Vertical");
        rawInputDir = new Vector2(moveX, moveY);
        inputDir = rawInputDir.normalized;

        float vx = inputDir.x * maxSpeedX;
        float vy = 0f;

        if (Mathf.Abs(moveX) > 0.01f)
        {
            if (moveX > 0f)
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            else if (moveX < 0f)
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
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

        bool wantDash = useExternalInput ? externalDashPressed : Input.GetKeyDown(KeyCode.Space);
        if (wantDash && !isDashing && dashCooldownTimer <= 0f)
        {
            if (rawInputDir.sqrMagnitude > 0.01f)
                StartDash(rawInputDir);
        }

        // External dash is a pulse signal and must be consumed this frame.
        externalDashPressed = false;
    }

    void StartDash(Vector2 dashInputRaw)
    {
        isDashing = true;

        bool hasHorizontal = Mathf.Abs(dashInputRaw.x) > 0.01f;
        bool hasVertical = Mathf.Abs(dashInputRaw.y) > 0.01f;

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
            SpawnDashEffect(fallbackDir);
            SpawnDashGhostTrail(fallbackDir, currentDashDuration);
            AudioManager.PlayDash();
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

        rb.linearVelocity += dashImpulse;

        Vector3 worldDashDir = new Vector3(dashImpulse.x, dashImpulse.y, 0f);
        SpawnDashEffect(worldDashDir);
        SpawnDashGhostTrail(worldDashDir, currentDashDuration);
        AudioManager.PlayDash();
    }

    void SpawnDashEffect(Vector3 dashDirectionWorld)
    {
        Vector3 spawnPos = effectSpawnPoint ? effectSpawnPoint.position : transform.position;
        bool spawned = false;

        if (dashHasVerticalInput && dashEffectVertical != null)
        {
            Quaternion rot = dashDirectionWorld.sqrMagnitude > 1e-5f
                ? Quaternion.LookRotation(dashDirectionWorld.normalized, Vector3.up)
                : Quaternion.identity;
            Instantiate(dashEffectVertical, spawnPos, rot);
            spawned = true;
        }

        if (dashHasHorizontalInput && dashEffectHorizontal != null)
        {
            Quaternion rot = dashDirectionWorld.sqrMagnitude > 1e-5f
                ? Quaternion.LookRotation(dashDirectionWorld.normalized, Vector3.up)
                : Quaternion.identity;
            Instantiate(dashEffectHorizontal, spawnPos, rot);
            spawned = true;
        }

        if (!spawned && useProceduralDashEffectFallback)
        {
            DashWaterBurstEffect.Spawn(spawnPos, dashDirectionWorld, proceduralDashFxLifeScale);
        }
    }

    void SpawnDashGhostTrail(Vector3 dashDirectionWorld, float dashDuration)
    {
        if (!enableDashGhostTrail || dashGhostPrefab == null) return;

        if (_ghostTrailRoutine != null)
        {
            StopCoroutine(_ghostTrailRoutine);
            _ghostTrailRoutine = null;
        }

        Vector3 dir = dashDirectionWorld.sqrMagnitude > 1e-5f
            ? dashDirectionWorld.normalized
            : transform.forward;
        if (dir.sqrMagnitude <= 1e-5f) dir = Vector3.right;

        _ghostTrailRoutine = StartCoroutine(SpawnDashGhostTrailRoutine(dir, Mathf.Max(0.05f, dashDuration)));
    }

    IEnumerator SpawnDashGhostTrailRoutine(Vector3 dir, float dashDuration)
    {
        int count = Mathf.Max(1, dashGhostCount);
        float interval = Mathf.Max(dashGhostMinInterval, dashDuration / Mathf.Max(1, count));

        for (int i = 0; i < count; i++)
        {
            Vector3 basePos = effectSpawnPoint ? effectSpawnPoint.position : transform.position;
            Quaternion rot = transform.rotation;
            Vector3 p = basePos - dir * dashGhostSpacing;
            GameObject ghost = Instantiate(dashGhostPrefab, p, rot);

            DashGhostAfterimage fade = ghost.GetComponent<DashGhostAfterimage>();
            if (fade == null) fade = ghost.AddComponent<DashGhostAfterimage>();

            float k = (count <= 1) ? 0f : (float)i / (count - 1);
            Color ghostColor = Color.Lerp(dashGhostStartColor, dashGhostEndColor, k);
            fade.InitializeFixedColor(dashGhostLifetime, ghostColor);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        _ghostTrailRoutine = null;
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
        if (isDashing) return;
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

        GUI.Box(new Rect(debugUIPos.x - 10, baseY - 10, 260, 160), "Player Debug Info");

        GUI.Label(new Rect(debugUIPos.x, baseY + 10, 250, 20), $"Depth Delta (dH): {deltaH:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 30, 250, 20), $"Target Depth: {targetH:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 50, 250, 20), $"Vertical Speed: {verticalSpeed:F2}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 70, 250, 20), $"Input: {inputDir}");
        GUI.Label(new Rect(debugUIPos.x, baseY + 90, 250, 20), $"State: {GetStateText()}");

        string dashState = isDashing ? "Dashing" :
                           dashCooldownTimer > 0 ? $"Cooldown {dashCooldownTimer:F1}s" : "Ready";
        GUI.Label(new Rect(debugUIPos.x, baseY + 110, 250, 20), $"Dash: {dashState}");
        if (isDashing)
            GUI.Label(new Rect(debugUIPos.x, baseY + 130, 250, 20),
                $"Direction: {GetDashOrientationText()}");

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
        if (isDashing) return "Dashing";
        if (Mathf.Abs(inputDir.y) > 0.01f)
        {
            if (inputDir.y > 0) return "Manual Up";
            else return "Manual Down";
        }
        else if (enableAutoBuoyancy)
        {
            if (Mathf.Abs(deltaH) < 0.05f) return "Balanced";
            return deltaH > 0 ? "Auto Sink" : "Auto Rise";
        }
        else
        {
            return "Idle";
        }
    }

    string GetDashOrientationText()
    {
        switch (dashOrientation)
        {
            case DashOrientation.Vertical:
                return "Vertical (W/S)";
            case DashOrientation.Horizontal:
                return "Horizontal (A/D)";
            case DashOrientation.Diagonal:
                return "Diagonal";
            default:
                return "None";
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

    public void SetUseExternalInput(bool enabled)
    {
        useExternalInput = enabled;
        if (!enabled)
        {
            externalInputDir = Vector2.zero;
            externalDashPressed = false;
        }
    }

    public void SetExternalInput(Vector2 moveInput, bool pressDash)
    {
        externalInputDir = Vector2.ClampMagnitude(moveInput, 1f);
        if (pressDash) externalDashPressed = true;
    }
}
