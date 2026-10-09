using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 爆炸特效动画：缩放放大 + 淡出，播完自动回收进对象池
/// 挂在特效 Image 上
/// </summary>
[RequireComponent(typeof(Image))]
public class ExplosionEffect : MonoBehaviour
{
    [Header("动画参数")]
    [Tooltip("动画时长（秒）")]
    public float duration = 0.5f;

    [Tooltip("起始缩放")]
    public float startScale = 0.3f;

    [Tooltip("峰值缩放")]
    public float peakScale = 1.2f;

    [Tooltip("起始透明度")]
    public float startAlpha = 1f;

    [Tooltip("结束透明度")]
    public float endAlpha = 0f;

    [Header("旋转（可选）")]
    [Tooltip("每次播放随机旋转角度，让效果更自然")]
    public bool randomRotation = true;

    private Image image;
    private RectTransform rectTransform;
    private Coroutine currentRoutine;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        // 从池中取出 SetActive(true) 时自动播放
        Play();
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    public void Play()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // 重置状态
        rectTransform.localScale = Vector3.one * startScale;
        SetAlpha(startAlpha);

        // 随机旋转
        if (randomRotation)
            rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        float time = 0f;
        Vector3 startScaleVec = Vector3.one * startScale;
        Vector3 peakScaleVec = Vector3.one * peakScale;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // 缩放：EaseOutBack 有轻微"弹一下"的感觉
            float scaleT = EaseOutBack(t);
            rectTransform.localScale = Vector3.LerpUnclamped(startScaleVec, peakScaleVec, scaleT);

            // 透明度：后半段开始淡出
            float alphaT = Mathf.Clamp01((t - 0.3f) / 0.7f);
            SetAlpha(Mathf.Lerp(startAlpha, endAlpha, alphaT));

            yield return null;
        }

        // 精确收尾
        rectTransform.localScale = peakScaleVec;
        SetAlpha(endAlpha);
        currentRoutine = null;

        // 回收进对象池
        PoolMgr.Instance.Despawn(gameObject);
    }

    private void SetAlpha(float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    /// <summary>
    /// EaseOutBack：先冲过峰值再回弹一点，适合爆炸效果
    /// </summary>
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}