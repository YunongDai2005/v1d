using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExpPickup : MonoBehaviour
{
    [Header("拾取参数")]
    public float addMass = 0.1f;
    public float moveSpeed = 2f;         // 飘动速度
    public float floatAmplitude = 0.2f;  // 上下浮动幅度
    public float floatFrequency = 1.5f;  // 上下浮动频率

    private Transform player;
    private PlayerUnderwaterController controller;
    private Vector3 basePos;

    void Start()
    {
        // 自动设置触发体
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // 确保有刚体
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        basePos = transform.position;

        // 找到玩家
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            controller = p.GetComponent<PlayerUnderwaterController>();
        }
    }

    void Update()
    {
        if (player == null || controller == null) return;

        // ✅ 目标位置：玩家当前水平位置 + 玩家目标深度的 y
        Vector3 target = new Vector3(player.position.x, controller.targetH, transform.position.z);

        // 平滑飘向目标位置
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * moveSpeed);

        // 增加轻微上下浮动效果
        float offset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position += new Vector3(0, offset * Time.deltaTime, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerUnderwaterController playerCtrl = other.GetComponent<PlayerUnderwaterController>();
            if (playerCtrl != null)
            {
                playerCtrl.totalMass += addMass;
                Debug.Log($"✅ 玩家拾取经验球，总质量变为 {playerCtrl.totalMass}");
            }

            Destroy(gameObject);
        }
    }
}
