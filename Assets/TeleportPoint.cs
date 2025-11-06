using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 传送点与交互逻辑
/// - 玩家进入范围显示提示，按键交互传送
/// - 支持控制器切换：PlayerUnderwaterController 与 playercon
/// - 支持刷怪开关（可指定需要控制的 EnemySpawner 列表）
/// </summary>
[RequireComponent(typeof(Collider))]
public class TeleportPoint : MonoBehaviour
{
    [Header("➡ 传送目标设置")]
    [Tooltip("目标位置（传送到该点）")]
    public Transform targetPoint;

    [Tooltip("传送到目标时的偏移 (世界坐标方向)")]
    public Vector3 positionOffset = new Vector3(0, 0, 0);

    [Tooltip("交互按键")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("传送冷却时间 (秒)")]
    public float cooldown = 5f;

    [Header("🎚 控制器切换")]
    [Tooltip("为 true 时：禁用 PlayerUnderwaterController，启用 playercon；为 false 时反之")]
    public bool usePccController = false;

    [Header("👹 刷怪控制")]
    [Tooltip("指定需要启用/禁用的刷怪器（为空则尝试自动查找活动的 EnemySpawner）")]
    public EnemySpawner[] spawnersToToggle;

    [Header("🪧 UI 提示")]
    public GameObject interactPromptUI; // 可选：提示面板（Canvas 下的对象）
    public string promptText = "按 [E] 传送";

    [Header("📏 交互范围 (可配置)")]
    [Tooltip("是否使用单独的 SphereCollider 作为交互触发范围")]
    public bool useDedicatedTrigger = true;
    [Tooltip("交互半径 (仅对 SphereCollider 生效)")]
    public float interactRadius = 2f;

    [Header("🛠 调试显示")]
    public bool showDebugGizmos = true;
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.5f);

    private bool isPlayerNearby = false;
    private bool onCooldown = false;
    private Transform player;
    private Vector3 lastPositionBeforeTeleport;

    void Start()
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);

        if (useDedicatedTrigger)
        {
            // 使用/配置 SphereCollider 作为交互范围，不影响其他碰撞体
            var sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = Mathf.Max(0.01f, interactRadius);
        }
        else
        {
            // 回退方案：将当前碰撞体设为触发器
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }

    void Update()
    {
        if (!isPlayerNearby || onCooldown) return;

        if (Input.GetKeyDown(interactKey))
        {
            StartCoroutine(DoTeleport());
        }
    }

    private IEnumerator DoTeleport()
    {
        onCooldown = true;
        if (interactPromptUI != null) interactPromptUI.SetActive(false);

        if (player == null || targetPoint == null)
        {
            Debug.LogWarning("TeleportPoint: 缺少 player 或 targetPoint");
            yield break;
        }

        // 控制器切换
        ApplyControllerMode(usePccController);

        // 记录上次位置（如果需要实现返回功能）
        lastPositionBeforeTeleport = player.position;

        // 执行传送
        Vector3 targetPos = targetPoint.position + positionOffset;
        player.position = targetPos;

        // 刷怪开关：true 关闭刷怪，false 开启刷怪
        SetSpawnerEnabled(!usePccController);

        // 冷却
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private void ApplyControllerMode(bool usePcc)
    {
        if (player == null) return;

        var uw = player.GetComponent<PlayerUnderwaterController>();
        var pcc = player.GetComponent<playercon>();

        if (usePcc)
        {
            if (uw != null) uw.enabled = false;
            if (pcc != null) pcc.enabled = true;
            if (uw == null)
                Debug.Log("[TeleportPoint] PlayerUnderwaterController 未找到，已跳过禁用。");
            if (pcc == null)
                Debug.Log("[TeleportPoint] playercon 未找到，无法启用 Pcc 控制器。");
        }
        else
        {
            if (uw != null) uw.enabled = true;
            if (pcc != null) pcc.enabled = false;
            if (uw == null)
                Debug.Log("[TeleportPoint] PlayerUnderwaterController 未找到，无法启用水下控制器。");
            if (pcc == null)
                Debug.Log("[TeleportPoint] playercon 未找到，已跳过禁用。");
        }
    }

    private void SetSpawnerEnabled(bool enabled)
    {
        if (spawnersToToggle != null && spawnersToToggle.Length > 0)
        {
            foreach (var s in spawnersToToggle)
            {
                if (s != null) s.enabled = enabled;
            }
            return;
        }

        // 回退：自动查找当前场景中的活动刷怪器（无法找到已禁用组件）
        var found = Object.FindObjectsOfType<EnemySpawner>();
        foreach (var s in found)
        {
            if (s != null) s.enabled = enabled;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            player = other.transform;

            if (interactPromptUI != null)
            {
                interactPromptUI.SetActive(true);
                // 自动填充 UI.Text 的内容（如有）
                var uiText = interactPromptUI.GetComponentInChildren<Text>(true);
                if (uiText != null)
                {
                    uiText.text = promptText;
                }
            }
            else
            {
                Debug.Log($"提示: {promptText}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactPromptUI != null)
                interactPromptUI.SetActive(false);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || targetPoint == null) return;

        Gizmos.color = gizmoColor;
        Vector3 targetPos = targetPoint.position + positionOffset;
        Gizmos.DrawLine(transform.position, targetPos);
        Gizmos.DrawSphere(targetPos, 0.2f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
