using UnityEngine;
using System.Collections;

/// <summary>
/// 通用传送触发器：
/// - 可挂在任意交互物品上
/// - 指定目标物体 + 偏移坐标
/// - 支持提示 UI + 冷却时间
/// </summary>
[RequireComponent(typeof(Collider))]
public class TeleportPoint : MonoBehaviour
{
    [Header("🎯 传送目标设置")]
    [Tooltip("目标位置（场景内任意物体）")]
    public Transform targetPoint;

    [Tooltip("传送到目标时的偏移 (世界坐标方向)")]
    public Vector3 positionOffset = new Vector3(0, 0, 0);

    [Tooltip("交互按键")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("传送冷却时间 (秒)")]
    public float cooldown = 5f;

    [Header("💡 UI 提示")]
    public GameObject interactPromptUI; // 可选提示物体（例如Canvas子物体）
    public string promptText = "按 [E] 传送";

    [Header("⚙️ 调试设置")]
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

        Collider col = GetComponent<Collider>();
        col.isTrigger = true; // 确保为触发器
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
        interactPromptUI?.SetActive(false);

        if (player == null || targetPoint == null)
        {
            Debug.LogWarning("⚠️ TeleportPoint: 缺少 player 或 targetPoint。");
            yield break;
        }

        // 记录上次位置（可用于返回功能）
        lastPositionBeforeTeleport = player.position;

        // 执行传送
        Vector3 targetPos = targetPoint.position + positionOffset;
        player.position = targetPos;

        // TODO：可加屏幕淡入淡出效果等
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            player = other.transform;

            if (interactPromptUI != null)
                interactPromptUI.SetActive(true);
            else
                Debug.Log($"💡 提示: {promptText}");
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
