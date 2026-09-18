using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeChatWASM; // 引入微信SDK命名空间

public class GameMain : MonoBehaviour
{
    private Button Btn_StarGame;
    private Button Btn_GameQuan;
    public TextMeshProUGUI TM_Score;

    /// <summary>
    /// 游戏圈首页 openlink：在 MP 后台-游戏圈-基础设置 获取。
    /// 注意：openlink 与包体版本必须匹配（开发版 / 体验版 / 正式版），否则打开的不是同一份圈子数据。
    /// </summary>
    private const string GameClubOpenLink =
        "-SSEykJvFV3pORt5kTNpS4wSBwrAO9YCvfQKjX2HN-OnArf05jbHiXBZXHaqmcPx_FvLK0Jg-6EzOqaYqFvejFLr6AMD26OcbNqOa7Nn8L2MWJMUm_-pbnaQtG6_Lr_bio7YnXvdvuhERf_7uSy2Og9bUFvaQbIceTwddC4CWjT9O-zVLYghUubWLBEo3gmuiYf1qvsqgjbUkoGaBsMyAj5G-kKzRBmNeyjGmpf15eFpRqj4TOto0MdI4tuuuGY_UQUyT_AFdCfzejsoo1H4lZgAYCjEHJEEBGuuQKVLckuwZqQOZSK9l-YhOnTvUd8BCquXzHIKXgA_aauNtza6EQ";

    /// <summary>
    /// 云开发环境 ID：MP 后台 → 云开发 → 环境 ID（形如 xxx-1a2b3c）。
    /// 没有自建服务器时，用云开发的云函数替代服务端，开通后才有 cloudID、才能取到开放数据。
    /// </summary>
    private const string CloudEnv = "cloud1-d3gpnpnu4d844087a";

    /// <summary>云函数名：接收游戏圈开放数据，负责入库（云数据库）或返回结果。</summary>
    private const string CloudFunctionName = "gameClubReport";

    private WXPageManager clubPage;
    private bool clubPageLoaded;
    private bool pendingShowPage;
    private bool pendingPullData;
    private Action<OnShowListenerResult> onAppShow;


    private void Awake()
    {
        Btn_StarGame = transform.Find("Btn_StarGame").GetComponent<Button>();
        Btn_GameQuan = transform.Find("Btn_GameQuan").GetComponent<Button>();
    }

    // Start is called before the first frame update
    void Start()
    {
        Btn_StarGame.onClick.AddListener(OnClickStarGame);
        Btn_GameQuan.onClick.AddListener(OpenGameClub);
        onAppShow = OnAppShow;
        WX.OnShow(onAppShow);
        InitCloud();
        Share();
        TM_Score.text = "version:1.0.0.4";
        AdManager.Instance.Init();
    }

    /// <summary>初始化云开发，CallFunction 之前必须 Init。</summary>
    private void InitCloud()
    {
        WX.cloud.Init(new ICloudConfig
        {
            env = CloudEnv,
            traceUser = true
        });
    }

    void OnDestroy()
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
    }

    void OnClickStarGame()
    {
        SceneManager.LoadSceneAsync("GameScence");
    }

    #region 游戏圈接入

    /// <summary>按钮：进入游戏圈。先确认授权，再打开半屏页，返回时拉取互动数据。</summary>
    public void OpenGameClub()
    {
        EnsureGameClubAuthorization(authorized =>
        {
            pendingPullData = authorized;
            PullGameClubData(); // 进入前的基线数据
            ShowGameClubPage();
        });
    }

    /// <summary>
    /// 1. 授权确认：游戏圈数据属于用户隐私数据，需要用户在《用户隐私保护指引》上同意后才能获取。
    /// </summary>
    private void EnsureGameClubAuthorization(Action<bool> callback)
    {
        // 官方建议：通过 wx.getSetting 查看当前授权情况（AuthSetting 是 key-value 字典）
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

    /// <summary>3. 打开游戏圈半屏页。实例复用，避免每次点击都新建页面管理器。</summary>
    private void ShowGameClubPage()
    {
        if (clubPage == null)
        {
            clubPageLoaded = false;
            clubPage = WX.CreatePageManager();
            clubPage.Load(new LoadOption
            {
                openlink = GameClubOpenLink,
                success = (res) =>
                {
                    Debug.Log($"[GameClub] 加载成功: {res.errMsg}");
                    clubPageLoaded = true;
                    if (pendingShowPage) DoShowGameClubPage();
                },
                fail = (res) => { Debug.LogError($"[GameClub] 加载失败: {res.errMsg}, 错误码: {res.errCode}"); }
            });
        }

        pendingShowPage = true;
        if (clubPageLoaded) DoShowGameClubPage();
    }

    private void DoShowGameClubPage()
    {
        pendingShowPage = false;
        clubPage.Show(new ShowOption
        {
            fail = (res) => Debug.LogError($"[GameClub] 页面显示失败: {res.errMsg}, 错误码: {res.errCode}")
        });
    }

    /// <summary>从游戏圈返回游戏时触发：重新拉取一次互动数据。</summary>
    private void OnAppShow(OnShowListenerResult res)
    {
        if (!pendingPullData) return;
        pendingPullData = false;
        PullGameClubData();
    }

    /// <summary>
    /// 2. 拉取当前用户的游戏圈活跃数据。返回的是加密数据，需服务端用 session_key 解密。
    /// </summary>
    public void PullGameClubData()
    {
        EnsureValidSession(RequestGameClubData);
    }

    /// <summary>getGameClubData 依赖有效登录态，失效时才重新 login。</summary>
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

    private void RequestGameClubData()
    {
        WX.GetGameClubData(new GetGameClubDataOption
        {
            dataTypeList = new DataType[]
            {
                new DataType { type = 1 }, // 加入游戏圈时间
                new DataType { type = 3 }, // 禁言状态：0 正常 1 禁言
                new DataType { type = 4 }, // 当天点赞帖子数
                new DataType { type = 5 }, // 当天评论帖子数
                new DataType { type = 6 }, // 当天发帖数
            },
            success = (res) =>
            {
                Debug.Log("[GameClub] 数据获取成功");
                ReportToCloudFunction(res.cloudID);
            },
            fail = (res) => Debug.LogError($"[GameClub] getGameClubData 失败: {res.errMsg}")
        });
    }

    /// <summary>
    /// 无自建服务器方案：把 cloudID 交给云函数，微信侧会自动替换成解密后的开放数据，
    /// 不再需要 code2session + AES 解密，也不需要保存 appsecret。
    /// 注意：只有已在 MP 后台开通云开发，才会返回 cloudID（有效期 5 分钟）。
    /// </summary>
    private void ReportToCloudFunction(string cloudID)
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
            success = (res) => Debug.Log($"[GameClub] 云函数返回: {res.result}"),
            fail = (res) => Debug.LogError($"[GameClub] 云函数调用失败: {res.errMsg}")
        });
    }

    [Serializable]
    private class CloudFunctionClubParam
    {
        public string clubData;
    }

    #endregion

    public void Share()
    {
        // 1. 开启分享菜单，并设置允许分享的类型
        WX.ShowShareMenu(new ShowShareMenuOption()
        {
            withShareTicket = true,
            menus = new string[] { "shareAppMessage", "shareTimeline" }
        });

        // 2. 设置默认分享内容（直接传参数对象，不要用 lambda）
        WX.OnShareAppMessage(new WXShareAppMessageParam()
        {
            title = "我的游戏分享",
            // imageUrl = "图片地址" // 可选，不填则自动截屏
        });

        // 3. 朋友圈分享的监听（按这个版本的签名写）
        WX.OnShareTimeline((callback) =>
        {
            callback(new OnShareTimelineListenerResult()
            {
                title = "我的游戏分享"
            });
        });
    }

    // Update is called once per frame
    void Update()
    {
    }
}