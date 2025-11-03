using UnityEngine;
using Unity.Cinemachine;

public class CameraDepthFollow : MonoBehaviour
{
    [Header("🎥 绑定")]
    public CinemachineVirtualCamera virtualCamera;  // 自动识别
    public Transform player;                        // 玩家
    public PlayerUnderwaterController playerController;

    [Header("📏 调整参数")]
    [Range(0.3f, 0.7f)] public float centerY = 0.5f;   // 屏幕中心
    [Range(0f, 0.5f)] public float offsetRange = 0.15f; // 偏移范围
    public float smoothSpeed = 2f;                      // 平滑速度

    private CinemachineFramingTransposer framing;

    void Start()
    {
        // ✅ 自动查找虚拟相机组件（即使没手动绑定也能工作）
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineVirtualCamera>();

      
        framing = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (framing == null)
        {
            Debug.LogError("❌ 没找到 Framing Transposer 组件！请在相机 Body 中设置 Framing Transposer。");
            enabled = false;
            return;
        }
    }

    void LateUpdate()
    {
        if (!playerController || !framing) return;

        float deltaH = playerController.deltaH;
        float rangeJ = playerController.rangeJ;

        // 归一化比例 [-1,1]
        float normalized = Mathf.Clamp(deltaH / rangeJ, -1f, 1f);

        // 根据深度偏差调整相机 framing
        float targetY = centerY - normalized * offsetRange;

        framing.m_ScreenY = Mathf.Lerp(framing.m_ScreenY, targetY, Time.deltaTime * smoothSpeed);
    }
}