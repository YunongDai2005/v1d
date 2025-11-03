using UnityEngine;
using UnityEngine.UI;

public class DepthBoundaryHUD : MonoBehaviour
{
    [Header("🎮 绑定")]
    public PlayerUnderwaterController playerController;  // 访问深度数据
    public Canvas canvas;                                // HUD 用的 Canvas（Screen Space - Overlay）
    public Image topOverlay;                             // 顶部渐变图片
    public Image bottomOverlay;                          // 底部渐变图片

    [Header("⚙️ 视觉参数")]
    [Range(0f, 1f)] public float maxAlpha = 0.8f;        // 最大透明度
    [Range(0.1f, 1f)] public float fadeRange = 0.5f;     // 提示出现比例（距离边界的百分比）
    public float fadeSmooth = 5f;                        // 淡入淡出速度

    private Color topColorTarget;
    private Color bottomColorTarget;

    void Start()
    {
        if (!canvas)
        {
            Debug.LogWarning("请绑定 HUD Canvas！");
            return;
        }

        if (topOverlay)
        {
            topOverlay.color = new Color(1f, 0.3f, 0.3f, 0f); // 红色透明
        }

        if (bottomOverlay)
        {
            bottomOverlay.color = new Color(0.3f, 0.6f, 1f, 0f); // 蓝色透明
        }
    }

    void Update()
    {
        if (!playerController) return;

        float h = playerController.deltaH;
        float j = playerController.rangeJ;

        float ratio = Mathf.Clamp01(Mathf.Abs(h) / j);

        // 上边界提示
        if (h > 0)
        {
            float alpha = Mathf.Clamp01((ratio - fadeRange) / (1f - fadeRange)) * maxAlpha;
            topColorTarget = new Color(1f, 0.3f, 0.3f, alpha);
            bottomColorTarget = new Color(0.3f, 0.6f, 1f, 0f);
        }
        // 下边界提示
        else if (h < 0)
        {
            float alpha = Mathf.Clamp01((ratio - fadeRange) / (1f - fadeRange)) * maxAlpha;
            bottomColorTarget = new Color(0.3f, 0.6f, 1f, alpha);
            topColorTarget = new Color(1f, 0.3f, 0.3f, 0f);
        }
        // 中间区域
        else
        {
            topColorTarget.a = 0f;
            bottomColorTarget.a = 0f;
        }

        // 平滑过渡
        if (topOverlay)
            topOverlay.color = Color.Lerp(topOverlay.color, topColorTarget, Time.deltaTime * fadeSmooth);
        if (bottomOverlay)
            bottomOverlay.color = Color.Lerp(bottomOverlay.color, bottomColorTarget, Time.deltaTime * fadeSmooth);
    }
}
