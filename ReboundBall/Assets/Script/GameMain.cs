using System;
using System.Collections;
using System.Collections.Generic;
using Mr3;
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
    private Button Btn_HandBookItem;
    private Button Btn_SettingBtn;

    private Button Btn_HellpBtn;
    private Button Btn_PetHome;
    private Button Btn_ShopBtn;
    private Button Btn_SigninBtn;
    private GameObject Obj_ShopRed;
    private GameObject Obj_SigninRed;


    [SerializeField] private GameObject ParticleSystem;

    private void Awake()
    {
        Btn_StarGame = transform.Find("Btn_StarGame").GetComponent<Button>();
        Btn_GameQuan = transform.Find("Btn_GameQuan").GetComponent<Button>();
        Btn_HandBookItem = transform.Find("Btn_HandBookItem").GetComponent<Button>();
        Btn_SettingBtn = transform.Find("Btn_Setting").GetComponent<Button>();
        Btn_HellpBtn = transform.Find("Btn_Help").GetComponent<Button>();
        Btn_PetHome = transform.Find("Btn_PetHome").GetComponent<Button>();
        Btn_ShopBtn = transform.Find("Btn_Shop").GetComponent<Button>();
        Btn_SigninBtn = transform.Find("Btn_Signin").GetComponent<Button>();

        Obj_ShopRed = Btn_ShopBtn.transform.Find("Obj_ShopRed").gameObject;
        Obj_SigninRed = Btn_SigninBtn.transform.Find("Obj_SigninRed").gameObject;
    }

    // Start is called before the first frame update
    void Start()
    {
        // 老版本用 PlayerPrefs 存的进度（关卡、音量等）搬到微信存储，只跑一次
        StorageMgr.MigrateFromPlayerPrefs();

        // 按存档重建已解锁宠物列表（通关进度 + 扭蛋抽到的），图鉴 / 小窝靠它显示解锁状态
        GameData.RefreshUnlockedPets();

        // 恢复出战宠物，让它的技能从一开始就生效
        PetSkillMgr.RestoreBattlePet();

        Btn_StarGame.onClick.AddListener(OnClickStarGame);
        Btn_GameQuan.onClick.AddListener(() => GameClubManager.Instance.Open());
        Btn_HandBookItem.onClick.AddListener(OnClickHankBook);
        Btn_SettingBtn.onClick.AddListener(OnClickSetting);
        Btn_HellpBtn.onClick.AddListener(() => { OpenUIView(GameData.HelpView); });
        Btn_PetHome.onClick.AddListener(() => { OpenUIView(GameData.PetHouseView); });
        Btn_ShopBtn.onClick.AddListener(() => { OpenUIView(GameData.LotteryView); });
        Btn_SigninBtn.onClick.AddListener(OnClickSignin);
        Share();
        TM_Score.text = "version:1.0.0.3";
        AdManager.Instance.Init();
        AddEvent();
        InitRedData();
    }

    void InitRedData()
    {
        var cansignintoday = SignInMgr.CanSignInToday;
        int signinRedNum = cansignintoday ? 1 : 0;
        RedPointManager.Instance.SetRedPointCount(NodeNames.Signin, signinRedNum);

        var canfreeShop = StorageMgr.GetString(GameData.LotteryFreeDateKey, "") != DateTime.Now.ToString("yyyyMMdd");
        int fresshopNum = canfreeShop ? 1 : 0;
        RedPointManager.Instance.SetRedPointCount(NodeNames.FreeShop, fresshopNum);
    }

    void OnClickSignin()
    {
        OpenUIView(GameData.SignInView);
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener(GameData.EventID_ShowParticleObj, OnShowParticleObj);
        EventMgr.Instance.AddListener(GameData.Event_RedPointChange, OnRedPointChange);
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener(GameData.EventID_ShowParticleObj, OnShowParticleObj);
        EventMgr.Instance.RemoveListener(GameData.Event_RedPointChange, OnRedPointChange);
    }

    void OnRedPointChange(string eventName, object udata)
    {
        var reddata = (RedChangeData)udata;
        DebugMgr.Instance.LogError($"red name:{reddata.name} red count :{reddata.redCount}");
        switch (RedPointManager.Instance.GetNodePath(reddata.name))
        {
            case NodeNames.Shop:
                Obj_ShopRed.SetActive(reddata.redCount > 0);
                break;
            case NodeNames.FreeShop:
                break;
            case NodeNames.Signin:
                Obj_SigninRed.SetActive(reddata.redCount > 0);
                break;
        }
    }

    void OnShowParticleObj(string str, object obj)
    {
        bool isShow = (bool)obj;
        ParticleSystem.SetActive(isShow);
    }

    void OnClickSetting()
    {
        OpenUIView(GameData.SettingView);
    }

    private void OpenUIView(string viewName)
    {
        // 图鉴 / 小窝的解锁状态直接读 GameData 的已解锁列表，打开前按最新存档刷新一次，
        // 避免刚抽到的宠物因为列表没同步而显示成未解锁
        if (viewName == GameData.HandBookView || viewName == GameData.PetHouseView)
        {
            GameData.RefreshUnlockedPets();
        }

        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, false);
        ResLoader.CreateUIOnce(viewName, transform);
    }

    void OnDestroy()
    {
        RemoveEvent();
    }

    void OnClickHankBook()
    {
        OpenUIView(GameData.HandBookView);
    }

    void OnClickStarGame()
    {
        OpenUIView(GameData.LevelView);
        //   SceneManager.LoadSceneAsync("GameScence");
    }

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

    private void OnEnable()
    {
        InitViewStats();
    }

    void InitViewStats()
    {
        //HandBookView.gameObject.SetActive(false);
        //  LeveView.gameObject.SetActive(false);
        //   SettingView.SetActive(false);
        //  HelpView.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        // 驱动托管数据按节流批量上报，内部有间隔判断，每帧调用无副作用
        StorageMgr.Tick();
        TestFunc();
    }

    void TestFunc()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SignInMgr.DebugShiftDays(1);
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            SignInMgr.DebugShiftDays(2);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            SignInMgr.ResetAll();
        }
    }

    /// <summary>切后台/失焦时强制落盘，避免最后一批存档没上报上去</summary>
    private void OnApplicationPause(bool pause)
    {
        StorageMgr.Save();
    }

    private void OnApplicationFocus(bool focus)
    {
        StorageMgr.Save();
    }
}