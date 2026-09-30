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

    /// <summary>常规激励视频广告位 ID：MP 后台创建广告位后获取，可在 Inspector 覆盖。</summary>
    [SerializeField] private string rewardedAdUnitId = "adunit-5e255ce449ce43f0";

    /// <summary>短时长激励视频广告位 ID：对应宠物技能「星夜疾行 / 广告时长减少 50%」。</summary>
    [SerializeField] private string shortRewardedAdUnitId = "adunit-bc04059fbf6c3937";

    /// <summary>
    /// 同一时刻只保留一个广告实例：微信 SDK 对不同 adUnitId 会创建不同实例，
    /// 两个实例同时存在时它们的 close / load 回调会互相串（实践里出现过播的是常规位、
    /// 关闭回调却从短时长位那个实例回来的情况），所以切广告位时直接换实例，不做多实例缓存。
    /// </summary>
    private WXRewardedVideoAd currentAd;

    /// <summary>currentAd 对应的广告位 ID</summary>
    private string currentAdUnitId = "";

    /// <summary>本次播放的回调：true = 完整看完应发奖，false = 中途退出或播放失败。</summary>
    private Action<bool> rewardCallback;

    /// <summary>是否正在播放，防止连点重复触发。</summary>
    private bool playing;


    public void Init()
    {
        // 提前创建常规位实例并预加载，避免玩家首次点击时等广告加载
        EnsureAd(NormalAdUnitId());
    }

    /// <summary>当前该用的常规广告位（短时长 ID 为空时退回常规位）。</summary>
    private string NormalAdUnitId() => rewardedAdUnitId;

    /// <summary>当前该用的短时长广告位（携带对应宠物技能时才用，没配 ID 就退回常规位）。</summary>
    private string ShortAdUnitId()
        => string.IsNullOrEmpty(shortRewardedAdUnitId) ? rewardedAdUnitId : shortRewardedAdUnitId;

    /// <summary>确保 currentAd 是 adUnitId 对应的实例；广告位变了就重建（旧的不再引用）。</summary>
    private void EnsureAd(string adUnitId)
    {
        if (string.IsNullOrEmpty(adUnitId)) return;
        if (currentAd != null && currentAdUnitId == adUnitId) return;

        currentAdUnitId = adUnitId;

#if UNITY_EDITOR
        currentAd = null;
        DebugMgr.Instance.Log($"[Ad] 编辑器环境不创建真实广告实例：{adUnitId}");
        return;
#endif
        string unitId = adUnitId; // 闭包里用它打印，避免误用后来的 currentAdUnitId

        WXRewardedVideoAd ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
        {
            adUnitId = unitId,
            multiton = false
        });

        ad.OnClose(res =>
        {
            DebugMgr.Instance.Log($"[Ad] 激励视频关闭（{unitId}），isEnded={res.isEnded}");

            // 回调不是当前广告位发出来的（SDK 多实例串台）：仍然按本次观看结果结算，
            // 否则玩家看完了却收不到奖励。这里只打日志提醒。
            if (unitId != currentAdUnitId)
            {
                DebugMgr.Instance.LogWarning($"[Ad] 关闭回调来自 {unitId}，但当前广告位是 {currentAdUnitId}（多实例串台，已按观看结果结算）");
            }

            Finish(res.isEnded);
            Preload(currentAd ?? ad); // 为下一次播放提前准备素材
        });
        ad.OnLoad(res => DebugMgr.Instance.Log($"[Ad] 激励视频加载成功（{unitId}）"));
        ad.OnError(res => DebugMgr.Instance.LogError($"[Ad] 激励视频错误（{unitId}）: {res.errMsg} ({res.errCode})"));

        currentAd = ad;
        Preload(ad);
    }

    /// <summary>预加载指定实例下一次播放需要的广告素材。</summary>
    private void Preload(WXRewardedVideoAd ad)
    {
        if (ad == null) return;

        ad.Load(
            res => { },
            err => DebugMgr.Instance.LogWarning($"[Ad] 激励视频预加载失败: {err.errMsg} ({err.errCode})"));
    }

    /// <summary>
    /// 播放激励视频并等待结果。广告位按当前出战宠物技能自动选择：
    /// 携带「广告时长减少 50%」时播短时长广告位，否则播常规广告位。
    /// </summary>
    /// <param name="onFinish">true = 用户完整看完（应发奖）；false = 中途关闭或播放失败。</param>
    public void ShowRewardedVideo(Action<bool> onFinish)
        => ShowRewardedVideo(onFinish, PetSkillMgr.UseShortAdUnit);

    /// <summary>
    /// 播放激励视频并等待结果。
    /// </summary>
    /// <param name="onFinish">true = 用户完整看完（应发奖）；false = 中途关闭或播放失败。</param>
    /// <param name="useShortAdUnit">true = 用短时长广告位（广告时长减少 50%）</param>
    public void ShowRewardedVideo(Action<bool> onFinish, bool useShortAdUnit)
    {
        if (playing)
        {
            DebugMgr.Instance.LogWarning("[Ad] 上一次激励视频尚未结束，忽略重复点击");
            return;
        }

        string adUnitId = useShortAdUnit ? ShortAdUnitId() : NormalAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            DebugMgr.Instance.LogError("[Ad] 广告位 ID 为空，无法播放");
            onFinish?.Invoke(false);
            return;
        }

        EnsureAd(adUnitId); // 广告位换了才重建实例，避免同时挂着两个广告位实例导致回调串台

        playing = true;
        rewardCallback = onFinish;

#if UNITY_EDITOR
        DebugMgr.Instance.Log($"[Ad] 编辑器环境：模拟完整看完 {adUnitId}（短时长={useShortAdUnit}）");
        Finish(true);
        return;
#endif
        WXRewardedVideoAd ad = currentAd;
        if (ad == null)
        {
            DebugMgr.Instance.LogError($"[Ad] 广告实例创建失败：{adUnitId}");
            Finish(false);
            return;
        }

        ad.Show(
            res => DebugMgr.Instance.Log($"[Ad] 激励视频开始播放（{adUnitId}）"),
            err =>
            {
                // 显示失败 → 重新加载后再显示一次（对应 JS 版 show().catch(() => load().then(show))）
                DebugMgr.Instance.LogWarning($"[Ad] 激励视频显示失败，尝试重新加载: {err.errMsg} ({err.errCode})");
                ad.Load(
                    res => ad.Show(r => { }, OnShowFailed),  // 加载成功后再显示一次
                    OnLoadFailed);                           // 加载也失败 → 不给奖励
            });
    }

    /// <summary>Show 失败回调（SDK 里 Show 的成功/失败回调均为 WXTextResponse）。</summary>
    private void OnShowFailed(WXTextResponse err)
    {
        DebugMgr.Instance.LogError($"[Ad] 激励视频重试仍失败: {err.errMsg} ({err.errCode})");
        Finish(false);
    }

    /// <summary>Load 失败回调（SDK 里 Load 的失败回调为 WXADErrorResponse）。</summary>
    private void OnLoadFailed(WXADErrorResponse err)
    {
        DebugMgr.Instance.LogError($"[Ad] 激励视频加载失败: {err.errMsg} ({err.errCode})");
        Finish(false);
    }

    private void Finish(bool rewarded)
    {
        playing = false;
        var cb = rewardCallback;
        rewardCallback = null;
        cb?.Invoke(rewarded);
    }
}
