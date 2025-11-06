using UnityEngine;

public class bgcon : MonoBehaviour
{
    [Header("触发对象（自动寻找Tag=Player）")]
    public Transform player;               // 玩家 Transform，可留空自动寻找
    public string playerTag = "Player";    // 玩家标签

    [Header("生成预制件设置")]
    public GameObject spawnPrefab;         // 要生成的预制件

    [Header("触发条件与生成位置")]
    public float alignTolerance = 0.1f;    // X轴对齐容差（避免浮点误差）
    public float offsetX = 100f;           // 左右±距离
    public bool triggerOnce = true;        // 只触发一次

    private bool hasTriggered = false;     // 记录是否已触发（当triggerOnce为true时生效）
    private bool wasAligned = false;       // 边沿检测（当triggerOnce为false时使用）

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        float px = player.position.x;
        float bx = transform.position.x;
        bool aligned = Mathf.Abs(px - bx) <= alignTolerance;

        if (triggerOnce)
        {
            if (!hasTriggered && aligned)
            {
                TriggerSpawnAndDestroy();
                hasTriggered = true;
            }
        }
        else
        {
            if (aligned && !wasAligned)
            {
                TriggerSpawnAndDestroy();
            }
            wasAligned = aligned;
        }
    }

    private void TriggerSpawnAndDestroy()
    {
        Vector3 basePos = transform.position; // 与此背景的 y、z 相同

        // 先删除除自身外，所有 tag 为 "bg" 的物体
        var allBg = GameObject.FindGameObjectsWithTag("bg");
        foreach (var go in allBg)
        {
            if (go != null && go != gameObject)
            {
                Destroy(go);
            }
        }

        // 在被挂载目标的 X±offsetX 生成所选预制件（Y、Z 与本体相同）
        if (spawnPrefab != null)
        {
            // 改为沿物体本地X方向偏移，但使用单位方向（不受缩放影响），避免偏移被放大导致生成过远
            Vector3 dir = transform.right.normalized; // 世界空间的本地X方向，单位长度
            float d = Mathf.Abs(offsetX);
            Vector3 leftPos = basePos - dir * d;
            Vector3 rightPos = basePos + dir * d;
            Instantiate(spawnPrefab, leftPos, Quaternion.identity);
            Instantiate(spawnPrefab, rightPos, Quaternion.identity);
        }
    }
}
