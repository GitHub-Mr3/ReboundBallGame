using Mr3;
using UnityEngine;

/// <summary>
/// 物体循环缩放脚本
/// 在最小值和最大值之间来回缩放（类似呼吸/脉冲效果）
/// </summary>
public class LoopScale : MonoBehaviour
{
    [Header("缩放设置")] [Tooltip("最小缩放值")] public Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("最大缩放值")] public Vector3 maxScale = new Vector3(0.8f, 0.8f, 0.8f);

    [Tooltip("缩放速度（值越大变化越快）")] public float speed = 1f;

    [Tooltip("缩放曲线类型")] public CurveType curveType = CurveType.SmoothPingPong;

    [Tooltip("是否在 Start 时自动开始")] public bool autoStart = true;

    // ==================== 私有变量 ====================

    private Vector3 originalScale;
    private float progress = 0f; // 0~1 之间的进度
    private bool isScaling = true;
    private bool isInitialized = false;

    /// <summary>
    /// 缩放曲线类型
    /// </summary>
    public enum CurveType
    {
        SmoothPingPong, // 平滑往返（正弦曲线）
        LinearPingPong, // 线性往返
        EaseIn, // 缓入（从慢到快）
        EaseOut, // 缓出（从快到慢）
        EaseInOut, // 缓入缓出
        Bounce, // 弹性效果
        Pulse // 脉冲（突跳）
    }

    // ==================== Unity 生命周期 ====================

    void Start()
    {
        // 保存初始缩放值
        originalScale = transform.localScale;
        isInitialized = true;

        if (autoStart)
        {
            StartScaling();
        }
        else
        {
            isScaling = false;
        }

        AddEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver", OnGameOver);
        EventMgr.Instance.AddListener("GameStar", OnGameStar);
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener("GameStar", OnGameStar);
    }

    void OnGameStar(string str, object obj)
    {
        ResetToMinAndStart();
    }

    void OnGameOver(string str, object obj)
    {
        ResetToMinAndStop();
    }


    void Update()
    {
        if (!isInitialized || !isScaling) return;

        // 更新进度（0~1 循环）
        progress += Time.deltaTime * speed;

        // 确保进度在 0~1 范围内循环
        if (progress > 1f)
        {
            progress -= 1f;
        }

        // 根据曲线类型计算缩放值
        float t = GetCurveValue(progress);

        // 在最小值和最大值之间插值
        Vector3 currentScale = Vector3.Lerp(minScale, maxScale, t);

        // 应用缩放
        transform.localScale = currentScale;
    }

    // ==================== 曲线计算 ====================

    /// <summary>
    /// 根据曲线类型计算 0~1 之间的值
    /// </summary>
    private float GetCurveValue(float p)
    {
        switch (curveType)
        {
            case CurveType.SmoothPingPong:
                // 平滑往返（正弦波）
                return (Mathf.Sin(p * Mathf.PI * 2 - Mathf.PI / 2) + 1f) / 2f;

            case CurveType.LinearPingPong:
                // 线性往返（锯齿波）
                return Mathf.PingPong(p, 1f);

            case CurveType.EaseIn:
                // 缓入（从慢到快）
                return p * p;

            case CurveType.EaseOut:
                // 缓出（从快到慢）
                return 1f - (1f - p) * (1f - p);

            case CurveType.EaseInOut:
                // 缓入缓出
                return p < 0.5f
                    ? 2f * p * p
                    : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

            case CurveType.Bounce:
                // 弹性效果
                return BounceCurve(p);

            case CurveType.Pulse:
                // 脉冲（突跳）
                return p < 0.1f ? p / 0.1f : 0f;

            default:
                return p;
        }
    }

    /// <summary>
    /// 弹性曲线
    /// </summary>
    private float BounceCurve(float p)
    {
        if (p < 1f / 2.75f)
        {
            return 7.5625f * p * p;
        }
        else if (p < 2f / 2.75f)
        {
            p -= 1.5f / 2.75f;
            return 7.5625f * p * p + 0.75f;
        }
        else if (p < 2.5f / 2.75f)
        {
            p -= 2.25f / 2.75f;
            return 7.5625f * p * p + 0.9375f;
        }
        else
        {
            p -= 2.625f / 2.75f;
            return 7.5625f * p * p + 0.984375f;
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始缩放动画
    /// </summary>
    public void StartScaling()
    {
        isScaling = true;
    }

    /// <summary>
    /// 停止缩放动画
    /// </summary>
    public void StopScaling()
    {
        isScaling = false;
    }

    /// <summary>
    /// 切换缩放状态
    /// </summary>
    public void ToggleScaling()
    {
        isScaling = !isScaling;
    }

    /// <summary>
    /// 判断是否正在缩放
    /// </summary>
    public bool IsScaling()
    {
        return isScaling;
    }

    /// <summary>
    /// 重置到最小缩放
    /// </summary>
    public void ResetToMin()
    {
        transform.localScale = minScale;
        progress = 0f;
    }

    /// <summary>
    /// 重置到最大缩放
    /// </summary>
    public void ResetToMax()
    {
        transform.localScale = maxScale;
        progress = 0.5f;
    }

    /// <summary>
    /// 重置到原始缩放
    /// </summary>
    public void ResetToOriginal()
    {
        transform.localScale = originalScale;
        progress = 0f;
    }

    /// <summary>
    /// 重置到最小缩放并停止
    /// </summary>
    public void ResetToMinAndStop()
    {
        ResetToMin();
        StopScaling();
    }

    /// <summary>
    /// 重置到最大缩放并停止
    /// </summary>
    public void ResetToMaxAndStop()
    {
        ResetToMax();
        StopScaling();
    }

    /// <summary>
    /// 重置到原始缩放并停止
    /// </summary>
    public void ResetToOriginalAndStop()
    {
        ResetToOriginal();
        StopScaling();
    }

    /// <summary>
    /// 重置到最小缩放并开始
    /// </summary>
    public void ResetToMinAndStart()
    {
        ResetToMin();
        StartScaling();
    }

    /// <summary>
    /// 重置到最大缩放并开始
    /// </summary>
    public void ResetToMaxAndStart()
    {
        ResetToMax();
        StartScaling();
    }

    /// <summary>
    /// 重置到原始缩放并开始
    /// </summary>
    public void ResetToOriginalAndStart()
    {
        ResetToOriginal();
        StartScaling();
    }

    /// <summary>
    /// 设置缩放范围
    /// </summary>
    public void SetScaleRange(Vector3 min, Vector3 max)
    {
        minScale = min;
        maxScale = max;
    }

    /// <summary>
    /// 设置缩放速度
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    /// <summary>
    /// 获取当前进度（0~1）
    /// </summary>
    public float GetProgress()
    {
        return progress;
    }

    /// <summary>
    /// 设置当前进度
    /// </summary>
    public void SetProgress(float p)
    {
        progress = Mathf.Clamp01(p);
    }
}