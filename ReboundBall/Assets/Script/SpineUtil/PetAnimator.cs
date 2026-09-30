using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 单只宠物的动画控制器
/// 挂载在带 Image 组件的物体上，实现：
/// - Idle：待机循环（轻微呼吸/摇摆）
/// - Select：选择（弹跳一下，播放一次）
/// - Feed：投喂（左右摇头+缩放，播放一次）
/// </summary>
[RequireComponent(typeof(Image))]
public class PetAnimator : MonoBehaviour
{
    [Header("动画参数 - 待机")]
    [Tooltip("待机时呼吸缩放幅度")]
    public float idleScaleAmplitude = 0.03f;
    [Tooltip("待机呼吸速度")]
    public float idleSpeed = 1.5f;

    [Header("动画参数 - 选择")]
    [Tooltip("选择时弹跳高度（像素）")]
    public float selectJumpHeight = 30f;
    [Tooltip("选择动画总时长（秒）")]
    public float selectDuration = 0.6f;

    [Header("动画参数 - 投喂")]
    [Tooltip("投喂时左右摇摆角度")]
    public float feedShakeAngle = 10f;
    [Tooltip("投喂时缩放峰值")]
    public float feedScalePeak = 1.15f;
    [Tooltip("投喂动画总时长（秒）")]
    public float feedDuration = 0.8f;

    // 内部状态
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    private Coroutine currentCoroutine;
    private bool isPlayingAction = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;
        originalRotation = rectTransform.localRotation;
    }

    void OnEnable()
    {
        // 物体激活时自动开始待机
        PlayIdle();
    }

    // ============ 对外接口 ============

    /// <summary>
    /// 待机动画（循环）
    /// </summary>
    public void PlayIdle()
    {
        StopCurrent();
        isPlayingAction = false;
        currentCoroutine = StartCoroutine(IdleLoop());
    }

    /// <summary>
    /// 选择动画（只播放一次，播完自动回到待机）
    /// </summary>
    public void PlaySelect()
    {
        StopCurrent();
        isPlayingAction = true;
        currentCoroutine = StartCoroutine(SelectOnce());
    }

    /// <summary>
    /// 投喂动画（只播放一次，播完自动回到待机）
    /// </summary>
    public void PlayFeed()
    {
        StopCurrent();
        isPlayingAction = true;
        currentCoroutine = StartCoroutine(FeedOnce());
    }

    // ============ 动画实现 ============

    private IEnumerator IdleLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * idleSpeed;
            float scaleOffset = Mathf.Sin(t) * idleScaleAmplitude;
            rectTransform.localScale = originalScale + new Vector3(scaleOffset, scaleOffset, 0);

            // 轻微的左右摇摆，让待机更生动
            float rotOffset = Mathf.Sin(t * 0.7f) * 1.5f;
            rectTransform.localRotation = originalRotation * Quaternion.Euler(0, 0, rotOffset);

            yield return null;
        }
    }

    private IEnumerator SelectOnce()
    {
        float elapsed = 0f;
        while (elapsed < selectDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / selectDuration;

            // 弹跳曲线：sin(π * progress) 会在中途到最高点
            float jump = Mathf.Sin(progress * Mathf.PI) * selectJumpHeight;

            // 缩放曲线：先放大再缩小
            float scaleCurve = 1f + Mathf.Sin(progress * Mathf.PI) * 0.2f;

            rectTransform.anchoredPosition = originalPosition + new Vector2(0, jump);
            rectTransform.localScale = originalScale * scaleCurve;

            yield return null;
        }

        // 复位
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = originalScale;

        // 回到待机
        PlayIdle();
    }

    private IEnumerator FeedOnce()
    {
        float elapsed = 0f;
        while (elapsed < feedDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / feedDuration;

            // 摇头曲线：sin(2π * progress * 2) 会左右摆两次
            float angle = Mathf.Sin(progress * Mathf.PI * 4f) * feedShakeAngle;
            rectTransform.localRotation = originalRotation * Quaternion.Euler(0, 0, angle);

            // 缩放曲线：0 → 峰值 → 1
            float scaleCurve = 1f + Mathf.Sin(progress * Mathf.PI) * (feedScalePeak - 1f);
            rectTransform.localScale = originalScale * scaleCurve;

            yield return null;
        }

        // 复位
        rectTransform.localRotation = originalRotation;
        rectTransform.localScale = originalScale;

        // 回到待机
        PlayIdle();
    }

    private void StopCurrent()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
        // 复位
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = originalScale;
        rectTransform.localRotation = originalRotation;
    }

    // ============ 右键菜单测试 ============
    [ContextMenu("Test/Play Idle")]
    private void TestIdle() => PlayIdle();

    [ContextMenu("Test/Play Select")]
    private void TestSelect() => PlaySelect();

    [ContextMenu("Test/Play Feed")]
    private void TestFeed() => PlayFeed();
}