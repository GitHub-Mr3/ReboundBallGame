using System;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM; // 引入微信SDK命名空间

/// <summary>
/// 游戏圈管理类（微信小游戏）。整文件复制到任意项目即可用，不依赖其他脚本，不继承 MonoBehaviour。
///
/// 使用前准备（MP 后台）：
///   1. 开通「游戏圈」→ 基础设置里复制 openlink；
///   2. 开通「云开发」→ 复制环境 ID，并部署一个接收开放数据的云函数。
///
/// 用法：
///   GameClubManager.Instance.Init(openLink, cloudEnv, cloudFunctionName, enableCloudSync); // 可选，不传用默认值
///   GameClubManager.Instance.Open();                                       // 打开游戏圈
///   GameClubManager.Instance.Open(result => DebugMgr.Instance.Log(result));            // 打开并接收解密后的数据
///   GameClubManager.Instance.RequestData(result => { });                   // 只拉数据不打开页面
///   GameClubManager.Instance.Dispose();                                    // 退出游戏时释放（可选）
///
/// 云同步开关 EnableCloudSync：
///   true  —— 默认。初始化云开发、拉互动数据、回调云函数解密结果（需要已开通云开发）。
///   false —— 纯展示模式。只打开游戏圈半屏页，不 Init 云开发、不 login、不拉数据、不回调。
/// </summary>
public class GameClubManager : SingletonBase<GameClubManager>
{
    #region 单例



    #endregion

    #region 配置（默认值可按需替换，或用 Init / 属性覆盖）

    /// <summary>游戏圈首页 openlink。注意与包体版本（开发版/体验版/正式版）必须匹配。</summary>
    private const string DefaultOpenLink =
        "-SSEykJvFV3pORt5kTNpS4wSBwrAO9YCvfQKjX2HN-OnArf05jbHiXBZXHaqmcPx_FvLK0Jg-6EzOqaYqFvejFLr6AMD26OcbNqOa7Nn8L2MWJMUm_-pbnaQtG6_Lr_bio7YnXvdvuhERf_7uSy2Og9bUFvaQbIceTwddC4CWjT9O-zVLYghUubWLBEo3gmuiYf1qvsqgjbUkoGaBsMyAj5G-kKzRBmNeyjGmpf15eFpRqj4TOto0MdI4tuuuGY_UQUyT_AFdCfzejsoo1H4lZgAYCjEHJEEBGuuQKVLckuwZqQOZSK9l-YhOnTvUd8BCquXzHIKXgA_aauNtza6EQ";

    /// <summary>云开发环境 ID（MP 后台 → 云开发 → 环境 ID）。</summary>
    private const string DefaultCloudEnv = "cloud1-d3gpnpnu4d844087a";

    /// <summary>云函数名：接收游戏圈开放数据。</summary>
    private const string DefaultCloudFunctionName = "gameClubReport";

    /// <summary>数据类型：1入圈时间 3禁言 4点赞 5评论 6发帖。</summary>
    private static readonly int[] DefaultDataTypes = { 1, 3, 4, 5, 6 };

    /// <summary>默认是否连接云服务器（云开发）。true=同步云数据，false=只打开游戏圈。</summary>
    private const bool DefaultEnableCloudSync = false;

    public string OpenLink { get; set; } = DefaultOpenLink;

    public string CloudEnv { get; set; } = DefaultCloudEnv;

    public string CloudFunctionName { get; set; } = DefaultCloudFunctionName;

    public int[] DataTypes { get; set; } = (int[])DefaultDataTypes.Clone();

    /// <summary>
    /// 云同步总开关。
    /// true：Init 云开发 + 授权校验 + 拉取互动数据 + 云函数解密回调；
    /// false：只打开游戏圈半屏页，不连接云服务器、不做登录与授权校验、不回调数据。
    /// 运行时可随时切换；关闭后已注册的 OnShow 监听仍在，但不会再触发数据拉取。
    /// </summary>
    public bool EnableCloudSync { get; set; } = DefaultEnableCloudSync;

    #endregion

    private WXPageManager clubPage;
    private bool clubPageLoaded;
    private bool pendingShowPage;
    private bool pendingPullData;
    private bool inited;

    /// <summary>从游戏圈返回后，云函数解密结果的回调（JSON 字符串）。</summary>
    private Action<string> resultCallback;

    private Action<OnShowListenerResult> onAppShow;

    #region 对外接口

    /// <summary>
    /// 初始化：注册返回游戏监听 +（开启云同步时）云开发 Init。可重复调用，只有第一次真正生效。
    /// 传 null 表示沿用当前属性值；enableCloudSync 传 null 表示不修改当前开关。
    /// </summary>
    public void Init(string openLink = null, string cloudEnv = null, string cloudFunctionName = null, bool? enableCloudSync = null)
    {
        if (!string.IsNullOrEmpty(openLink)) OpenLink = openLink;
        if (!string.IsNullOrEmpty(cloudEnv)) CloudEnv = cloudEnv;
        if (!string.IsNullOrEmpty(cloudFunctionName)) CloudFunctionName = cloudFunctionName;
        if (enableCloudSync.HasValue) EnableCloudSync = enableCloudSync.Value;

        if (inited) return;
        inited = true;

        if (EnableCloudSync)
        {
            // CallFunction 之前必须 Init
            WX.cloud.Init(new ICloudConfig
            {
                env = CloudEnv,
                traceUser = true
            });
        }

        onAppShow = OnAppShow;
        WX.OnShow(onAppShow);
        DebugMgr.Instance.Log($"[GameClub] 初始化完成 env={(EnableCloudSync ? CloudEnv : "未连接云服务器")} func={CloudFunctionName} cloudSync={EnableCloudSync}");
    }

    /// <summary>运行时切换云同步开关。关闭后不再拉取/上报任何云数据，仅保留打开游戏圈的能力。</summary>
    public void SetEnableCloudSync(bool enable)
    {
        if (EnableCloudSync == enable) return;
        EnableCloudSync = enable;

        // 由关到开时补一次云开发初始化（CallFunction 前必须 Init）
        if (enable && inited)
        {
            WX.cloud.Init(new ICloudConfig
            {
                env = CloudEnv,
                traceUser = true
            });
        }

        if (!enable)
        {
            pendingPullData = false;
            resultCallback = null;
        }
    }

    /// <summary>
    /// 打开游戏圈半屏页。
    /// </summary>
    /// <param name="onResult">
    /// 从游戏圈返回游戏后回调，参数是云函数解密后的数据(JSON 字符串)；不需要可以不传。
    /// 注意：EnableCloudSync=false 时不会回调。
    /// </param>
    public void Open(Action<string> onResult = null)
    {
        Init();

        // 纯展示模式：不授权、不登录、不连云服务器，直接打开游戏圈
        if (!EnableCloudSync)
        {
            resultCallback = null;
            pendingPullData = false;
            ShowPage();
            return;
        }

        EnsureAuthorization(authorized =>
        {
            pendingPullData = authorized;
            resultCallback = onResult;
            RequestData(null); // 进入前的基线数据
            ShowPage();
        });
    }

    /// <summary>
    /// 主动拉取一次游戏圈互动数据（不打开页面）。
    /// EnableCloudSync=false 时直接返回，不会访问云服务器。
    /// </summary>
    /// <param name="onResult">云函数返回的数据(JSON 字符串)。</param>
    public void RequestData(Action<string> onResult = null)
    {
        Init();

        if (!EnableCloudSync)
        {
            DebugMgr.Instance.Log("[GameClub] 云同步开关已关闭，跳过数据拉取");
            return;
        }

        EnsureValidSession(() => RequestClubData(onResult));
    }

    /// <summary>释放：取消监听并销毁半屏页实例。退出游戏 / 不再使用游戏圈时调用。</summary>
    public void Dispose()
    {
        if (onAppShow != null)
        {
            WX.OffShow(onAppShow);
            onAppShow = null;
        }

        if (clubPage != null)
        {
            clubPage.Destroy();
            clubPage = null;
        }

        clubPageLoaded = false;
        pendingShowPage = false;
        pendingPullData = false;
        resultCallback = null;
        inited = false;
    }

    #endregion

    #region 内部流程

    /// <summary>1. 授权确认：游戏圈数据属用户隐私数据，需用户在《用户隐私保护指引》上同意。</summary>
    private void EnsureAuthorization(Action<bool> callback)
    {
        WX.GetSetting(new GetSettingOption
        {
            success = (res) =>
            {
                if (res.authSetting == null) return;
                foreach (var item in res.authSetting)
                {
                    DebugMgr.Instance.Log($"[GameClub] authSetting {item.Key} = {item.Value}");
                }
            }
        });

        WX.GetPrivacySetting(new GetPrivacySettingOption
        {
            success = (res) =>
            {
                if (!res.needAuthorization)
                {
                    callback(true);
                    return;
                }

                DebugMgr.Instance.Log($"[GameClub] 需要用户同意隐私指引: {res.privacyContractName}");
                WX.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption
                {
                    success = (_) => callback(true),
                    fail = (_) =>
                    {
                        DebugMgr.Instance.LogWarning("[GameClub] 用户拒绝隐私授权，无法使用游戏圈数据");
                        callback(false);
                    }
                });
            },
            fail = (res) =>
            {
                // 基础库过低或接口不可用时放行，只影响数据获取，不影响打开圈子
                DebugMgr.Instance.LogWarning($"[GameClub] 隐私授权查询失败: {res.errMsg}");
                callback(false);
            }
        });
    }

    /// <summary>2. 打开游戏圈半屏页。实例复用，避免每次点击都新建页面管理器。</summary>
    private void ShowPage()
    {
        if (clubPage == null)
        {
            clubPageLoaded = false;
            clubPage = WX.CreatePageManager();
            clubPage.Load(new LoadOption
            {
                openlink = OpenLink,
                success = (res) =>
                {
                    DebugMgr.Instance.Log($"[GameClub] 页面加载成功: {res.errMsg}");
                    clubPageLoaded = true;
                    if (pendingShowPage) DoShowPage();
                },
                fail = (res) => DebugMgr.Instance.LogError($"[GameClub] 页面加载失败: {res.errMsg}, 错误码: {res.errCode}")
            });
        }

        pendingShowPage = true;
        if (clubPageLoaded) DoShowPage();
    }

    private void DoShowPage()
    {
        pendingShowPage = false;
        clubPage.Show(new ShowOption
        {
            fail = (res) => DebugMgr.Instance.LogError($"[GameClub] 页面显示失败: {res.errMsg}, 错误码: {res.errCode}")
        });
    }

    /// <summary>3. 从游戏圈返回游戏时触发：重新拉取一次互动数据。</summary>
    private void OnAppShow(OnShowListenerResult res)
    {
        if (!pendingPullData) return;
        pendingPullData = false;

        var cb = resultCallback;
        resultCallback = null;
        RequestData(cb);
    }

    /// <summary>4. getGameClubData 依赖有效登录态，失效时才重新 login。</summary>
    private void EnsureValidSession(Action callback)
    {
        WX.CheckSession(new CheckSessionOption
        {
            success = (_) => callback(),
            fail = (_) => WX.Login(new LoginOption
            {
                success = (_) => callback(),
                fail = (res) => DebugMgr.Instance.LogError($"[GameClub] wx.login 失败: {res.errMsg}")
            })
        });
    }

    /// <summary>5. 拉取游戏圈数据，拿到的是加密的 cloudID。</summary>
    private void RequestClubData(Action<string> onResult)
    {
        var list = new List<DataType>();
        foreach (var type in DataTypes)
        {
            list.Add(new DataType { type = type });
        }

        WX.GetGameClubData(new GetGameClubDataOption
        {
            dataTypeList = list.ToArray(),
            success = (res) =>
            {
                DebugMgr.Instance.Log("[GameClub] 数据获取成功");
                ReportToCloudFunction(res.cloudID, onResult);
            },
            fail = (res) => DebugMgr.Instance.LogError($"[GameClub] getGameClubData 失败: {res.errMsg}")
        });
    }

    /// <summary>
    /// 6. 无自建服务器方案：把 cloudID 交给云函数，微信侧自动替换成解密后的开放数据，
    ///    无需 code2session + AES 解密，也无需保存 appsecret。
    ///    注意：只有已开通云开发才会返回 cloudID（有效期约 5 分钟）。
    /// </summary>
    private void ReportToCloudFunction(string cloudID, Action<string> onResult)
    {
        if (string.IsNullOrEmpty(cloudID))
        {
            DebugMgr.Instance.LogWarning("[GameClub] 未拿到 cloudID：请先在 MP 后台开通云开发");
            return;
        }

        // cloudID 必须放在 data 的顶层字段，嵌套字段不会被替换
        var param = new CloudFunctionClubParam
        {
            clubData = WX.cloud.CloudID(cloudID)
        };

        WX.cloud.CallFunction(new CallFunctionParam
        {
            name = CloudFunctionName,
            data = param,
            success = (res) =>
            {
                DebugMgr.Instance.Log($"[GameClub] 云函数返回: {res.result}");
                onResult?.Invoke(res.result);
            },
            fail = (res) => DebugMgr.Instance.LogError($"[GameClub] 云函数调用失败: {res.errMsg}")
        });
    }

    [Serializable]
    private class CloudFunctionClubParam
    {
        public string clubData;
    }

    #endregion
}
