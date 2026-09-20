using System;
using UnityEngine;
using WeChatWASM; // 引入微信SDK命名空间

/// <summary>
/// 广告管理类（激励视频）。
/// 用法：挂到场景中任意常驻物体上（Awake 里 DontDestroyOnLoad，切场景不会销毁），
///      其他脚本调用 AdManager.Instance.ShowRewardedVideo(complete => { if (complete) 发奖; });
/// </summary>
public class AdManager : SingletonBase<AdManager>
{

    /// <summary>激励视频广告位 ID：MP 后台创建广告位后获取，可在 Inspector 覆盖。</summary>
    [SerializeField] private string rewardedAdUnitId = "adunit-5e255ce449ce43f0";

    private WXRewardedVideoAd rewardedAd;

    /// <summary>本次播放的回调：true = 完整看完应发奖，false = 中途退出或播放失败。</summary>
    private Action<bool> rewardCallback;

    /// <summary>是否正在播放，防止连点重复触发。</summary>
    private bool playing;


    public void Init()
    {
        // 提前创建实例并预加载，避免玩家首次点击时等广告加载
        EnsureAdCreated();
    }

    /// <summary>创建广告实例（只创建一次），并注册关闭/错误/加载回调。</summary>
    private void EnsureAdCreated()
    {
        if (rewardedAd != null) return;

#if UNITY_EDITOR
        Debug.Log("[Ad] 编辑器环境不创建真实广告实例");
        return;
#endif
        rewardedAd = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
        {
            adUnitId = rewardedAdUnitId,
            multiton = false
        });

        rewardedAd.OnClose(OnRewardedClose);
        rewardedAd.OnLoad(res => Debug.Log("[Ad] 激励视频加载成功"));
        rewardedAd.OnError(res => Debug.LogError($"[Ad] 激励视频错误: {res.errMsg} ({res.errCode})"));

        Preload();
    }

    /// <summary>预加载下一次播放需要的广告素材。</summary>
    private void Preload()
    {
        rewardedAd.Load(
            res => { },
            err => Debug.LogWarning($"[Ad] 激励视频预加载失败: {err.errMsg} ({err.errCode})"));
    }

    /// <summary>
    /// 播放激励视频并等待结果。
    /// </summary>
    /// <param name="onFinish">true = 用户完整看完（应发奖）；false = 中途关闭或播放失败。</param>
    public void ShowRewardedVideo(Action<bool> onFinish)
    {
        EnsureAdCreated();

        if (playing)
        {
            Debug.LogWarning("[Ad] 上一次激励视频尚未结束，忽略重复点击");
            return;
        }

        playing = true;
        rewardCallback = onFinish;

#if UNITY_EDITOR
        Debug.Log("[Ad] 编辑器环境：模拟完整看完");
        Finish(true);
        return;
#endif
        rewardedAd.Show(
            res => Debug.Log("[Ad] 激励视频开始播放"),
            err =>
            {
                // 显示失败 → 重新加载后再显示一次（对应 JS 版 show().catch(() => load().then(show))）
                Debug.LogWarning($"[Ad] 激励视频显示失败，尝试重新加载: {err.errMsg} ({err.errCode})");
                rewardedAd.Load(
                    res => rewardedAd.Show(r => { }, OnShowFailed),  // 加载成功后再显示一次
                    OnLoadFailed);                                   // 加载也失败 → 不给奖励
            });
    }

    /// <summary>Show 失败回调（SDK 里 Show 的成功/失败回调均为 WXTextResponse）。</summary>
    private void OnShowFailed(WXTextResponse err)
    {
        Debug.LogError($"[Ad] 激励视频重试仍失败: {err.errMsg} ({err.errCode})");
        Finish(false);
    }

    /// <summary>Load 失败回调（SDK 里 Load 的失败回调为 WXADErrorResponse）。</summary>
    private void OnLoadFailed(WXADErrorResponse err)
    {
        Debug.LogError($"[Ad] 激励视频加载失败: {err.errMsg} ({err.errCode})");
        Finish(false);
    }

    private void OnRewardedClose(WXRewardedVideoAdOnCloseResponse res)
    {
        Debug.Log($"[Ad] 激励视频关闭，isEnded={res.isEnded}");
        Finish(res.isEnded);
        Preload(); // 为下一次播放提前准备素材
    }

    private void Finish(bool rewarded)
    {
        playing = false;
        var cb = rewardCallback;
        rewardCallback = null;
        cb?.Invoke(rewarded);
    }
}
