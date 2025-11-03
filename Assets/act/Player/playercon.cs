using UnityEngine;
[RequireComponent(typeof(Rigidbody))]

public class playercon : MonoBehaviour
{
    [Header("水平移动速度 (左右)")]
    public float moveSpeedRight = 5f; // D键（右）
    public float moveSpeedLeft = 5f;  // A键（左）

    [Header("垂直移动速度 (上下)")]
    public float moveSpeedUp = 4f;    // W键（上）
    public float moveSpeedDown = 3f;  // S键（下）

    [Header("水下惯性")]
    [Range(0f, 10f)]
    public float acceleration = 5f;   // 加速平滑程度
    [Range(0f, 10f)]
    public float waterDrag = 3f;      // 阻力（越大越滑）

    private Rigidbody rb;
    private Vector3 targetVelocity;
    private Vector3 currentVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 锁定 Z 轴运动和旋转，保持2.5D效
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    void Update()
    {
        float moveX = 0f;
        float moveY = 0f;

        // 左右
        if (Input.GetKey(KeyCode.D))
            moveX = moveSpeedRight;
        else if (Input.GetKey(KeyCode.A))
            moveX = -moveSpeedLeft;

        // 上下
        if (Input.GetKey(KeyCode.W))
            moveY = moveSpeedUp;
        else if (Input.GetKey(KeyCode.S))
            moveY = -moveSpeedDown;

        targetVelocity = new Vector3(moveX, moveY, 0f);
    }

    void FixedUpdate()
    {
        // 模拟水下惯性（平滑插值）
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * acceleration);

        // 应用阻力
        currentVelocity *= (1f - Time.fixedDeltaTime * waterDrag);

        rb.linearVelocity = currentVelocity;
    }
}