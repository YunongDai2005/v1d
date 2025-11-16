using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随的目标（玩家）")]
    public Transform target;

    [Header("相机相对玩家的偏移量")]
    public Vector3 offset = new Vector3(0, 5, -10);

    [Header("跟随平滑度（越大越跟手）")]
    [Range(0f, 20f)]
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // 相机的目标位置 = 玩家位置 + 偏移
        Vector3 desiredPosition = target.position + offset;

        // 使用插值让相机平滑移动，而不是瞬间跳过去
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 应用相机位置
        transform.position = smoothedPosition;

        // 如果你不想相机旋转，注释掉下面这行
        // transform.LookAt(target);
    }
}
