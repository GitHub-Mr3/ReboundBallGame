using UnityEngine;
using WeChatWASM;

public class WxRecommend : MonoBehaviour
{
    /// <summary>
    /// 预加载推荐组件（在游戏场景 Load 时调用）
    /// </summary>
    public void PreloadRecommend()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WX.CallJSFunction("GameRecommend", "preload", new object[0]);
#else
        DebugMgr.Instance.Log("PreloadRecommend called (仅在小游戏环境生效)");
#endif
    }

    /// <summary>
    /// 展示推荐组件（在玩家高光时刻调用）
    /// </summary>
    public void ShowRecommend()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WX.CallJSFunction("GameRecommend", "show", new object[0]);
#else
        DebugMgr.Instance.Log("ShowRecommend called (仅在小游戏环境生效)");
#endif
    }
}