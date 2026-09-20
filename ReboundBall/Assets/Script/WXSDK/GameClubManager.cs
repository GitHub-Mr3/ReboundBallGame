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
///   GameClubManager.Instance.Init(openLink, cloudEnv, cloudFunctionName);  // 可选，不传用默认值
///   GameClubManager.Instance.Open();                                       // 打开游戏圈
///   GameClubManager.Instance.Open(result => Debug.Log(result));            // 打开并接收解密后的数据
///   GameClubManager.Instance.RequestData(result => { });                   // 只拉数据不打开页面
///   GameClubManager.Instance.Dispose();                                    // 退出游戏时释放（可选）
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

    public string OpenLink { get; set; } = DefaultOpenLink;

    public string CloudEnv { get; set; } = DefaultCloudEnv;

    public string CloudFunctionName { get; set; } = DefaultCloudFunctionName;

    public int[] DataTypes { get; set; } = (int[])DefaultDataTypes.Clone();

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
    /// 初始化：云开发 Init + 注册返回游戏监听。可重复调用，只有第一次真正生效。
    /// 传 null 表示沿用当前属性值。
    /// </summary>
    public void Init(string openLink = null, string cloudEnv = null, string cloudFunctionName = null)
    {
        if (!string.IsNullOrEmpty(openLink)) OpenLink = openLink;
        if (!string.IsNullOrEmpty(cloudEnv)) CloudEnv = cloudEnv;
        if (!string.IsNullOrEmpty(cloudFunctionName)) CloudFunctionName = cloudFunctionName;

        if (inited) return;
        inited = true;

        // CallFunction 之前必须 Init
        WX.cloud.Init(new ICloudConfig
        {
            env = CloudEnv,
            traceUser = true
        });

        onAppShow = OnAppShow;
        WX.OnShow(onAppShow);
        Debug.Log($"[GameClub] 初始化完成 env={CloudEnv} func={CloudFunctionName}");
    }

    /// <summary>
    /// 打开游戏圈半屏页。
    /// </summary>
    /// <param name="onResult">从游戏圈返回游戏后回调，参数是云函数解密后的数据(JSON 字符串)；不需要可以不传。</param>
    public void Open(Action<string> onResult = null)
    {
        Init();

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
    /// </summary>
    /// <param name="onResult">云函数返回的数据(JSON 字符串)。</param>
    public void RequestData(Action<string> onResult = null)
    {
        Init();
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
                    Debug.Log($"[GameClub] authSetting {item.Key} = {item.Value}");
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

                Debug.Log($"[GameClub] 需要用户同意隐私指引: {res.privacyContractName}");
                WX.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption
                {
                    success = (_) => callback(true),
                    fail = (_) =>
                    {
                        Debug.LogWarning("[GameClub] 用户拒绝隐私授权，无法使用游戏圈数据");
                        callback(false);
                    }
                });
            },
            fail = (res) =>
            {
                // 基础库过低或接口不可用时放行，只影响数据获取，不影响打开圈子
                Debug.LogWarning($"[GameClub] 隐私授权查询失败: {res.errMsg}");
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
                    Debug.Log($"[GameClub] 页面加载成功: {res.errMsg}");
                    clubPageLoaded = true;
                    if (pendingShowPage) DoShowPage();
                },
                fail = (res) => Debug.LogError($"[GameClub] 页面加载失败: {res.errMsg}, 错误码: {res.errCode}")
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
            fail = (res) => Debug.LogError($"[GameClub] 页面显示失败: {res.errMsg}, 错误码: {res.errCode}")
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
                fail = (res) => Debug.LogError($"[GameClub] wx.login 失败: {res.errMsg}")
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
                Debug.Log("[GameClub] 数据获取成功");
                ReportToCloudFunction(res.cloudID, onResult);
            },
            fail = (res) => Debug.LogError($"[GameClub] getGameClubData 失败: {res.errMsg}")
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
            Debug.LogWarning("[GameClub] 未拿到 cloudID：请先在 MP 后台开通云开发");
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
                Debug.Log($"[GameClub] 云函数返回: {res.result}");
                onResult?.Invoke(res.result);
            },
            fail = (res) => Debug.LogError($"[GameClub] 云函数调用失败: {res.errMsg}")
        });
    }

    [Serializable]
    private class CloudFunctionClubParam
    {
        public string clubData;
    }

    #endregion
}
