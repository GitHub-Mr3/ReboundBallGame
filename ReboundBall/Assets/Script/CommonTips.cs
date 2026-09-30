
/********************************************************************
              _ooOoo_
             o8888888o
             88". "88
             (| -_- |)
             O\  =  /O
          ____/`---'\____
        .'  \\|     |//  `.
       /  \\|||  :  |||//  \
      /  _||||| -:- |||||-  \
      |   | \\\  -  /// |   |
      | \_|  ''\---/''  |   |
      \  .-\__  `-`  ___/-. /
    ___`. .'  /--.--\  `. . __
 ." '<  `.___\_<|>_/___.'  >'".
| | :  `- \`.;`\ _ /`;.`/ - ` : | |
\  \ `-.   \_ __\ /__ _/   .-` /  /
======`-.____`-.___\_____/___.-`____.-'======
              `=---='
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
佛祖保佑 永无BUG       永不修改

*********************************************************************/

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CommonTips : MonoBehaviour
{
    //autoStart
    private TextMeshProUGUI TxtM_Tips = null;



    private void Awake()
    {
        TxtM_Tips = transform.Find("bg/TxtM_Tips").GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }

    //autoEnd
    //TODO
    /// <summary>向上飘的距离（像素）</summary>
    [Tooltip("提示出现后向上移动的距离，单位像素")]
    [SerializeField] private float floatDistance = 200f;

    /// <summary>上飘 + 停留的总时长（秒），到点后自动隐藏</summary>
    [Tooltip("从出现到自动关闭的总时长，单位秒")]
    [SerializeField] private float duration = 1.2f;

    private RectTransform rectTransform;

    private Coroutine floatRoutine;

    public void Refresh(string desc)
    {
        TxtM_Tips.text = desc;

        gameObject.SetActive(true); // 复用实例时可能处于隐藏状态，隐藏时协程不会跑
        PlayFloat();                // 每次刷新都重新飘一次
    }

    /// <summary>从原点向上飘 floatDistance 像素后自动关闭</summary>
    public void PlayFloat()
    {
        if (floatRoutine != null) StopCoroutine(floatRoutine);
        floatRoutine = StartCoroutine(FloatUpAndClose());
    }

    private IEnumerator FloatUpAndClose()
    {
        Vector3 origin = Vector3.zero; // 原点（anchoredPosition 归零处，由 CreateUIOnce 也已复位）
        ApplyPosition(origin);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 用真实时间：游戏暂停 / TimeScale 变动时提示也能正常飘完关掉
            elapsed += Time.unscaledDeltaTime;

            float k = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ApplyPosition(origin + Vector3.up * (floatDistance * EaseOut(k)));
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        ApplyPosition(origin); // 复位：下次复用同一个实例还是从原点开始飘
        floatRoutine = null;
        
        gameObject.SetActive(false);
    }

    /// <summary>缓出曲线：起步快、末尾慢</summary>
    private float EaseOut(float k)
    {
        return 1f - (1f - k) * (1f - k);
    }

    private void ApplyPosition(Vector3 pos)
    {
        if (rectTransform != null) rectTransform.anchoredPosition = pos;
        else transform.localPosition = pos;
    }

    private void Start()
    {

    }
    //
    private void OnEnable()
    {

    }
}
