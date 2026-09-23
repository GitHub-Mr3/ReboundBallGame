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
    private Button Btn_HandBookItem;
    private Button Btn_SettingBtn;
    private Button Btn_HellpBtn;
    [SerializeField] private GameObject HandBookView;
    [SerializeField] private GameObject SettingView;
    [SerializeField] private GameObject LeveView;
    [SerializeField] private GameObject HelpView;

    private void Awake()
    {
        Btn_StarGame = transform.Find("Btn_StarGame").GetComponent<Button>();
        Btn_GameQuan = transform.Find("Btn_GameQuan").GetComponent<Button>();
        Btn_HandBookItem = transform.Find("Btn_HandBookItem").GetComponent<Button>();
        Btn_SettingBtn = transform.Find("Btn_Setting").GetComponent<Button>();
        Btn_HellpBtn = transform.Find("Btn_Help").GetComponent<Button>();
    }

    // Start is called before the first frame update
    void Start()
    {
        Btn_StarGame.onClick.AddListener(OnClickStarGame);
        Btn_GameQuan.onClick.AddListener(() => GameClubManager.Instance.Open());
        Btn_HandBookItem.onClick.AddListener(OnClickHankBook);
        Btn_SettingBtn.onClick.AddListener(OnClickSetting);
        Btn_HellpBtn.onClick.AddListener(() => { HelpView.SetActive(true); });
        Share();
        TM_Score.text = "version:1.0.0.0";
        AdManager.Instance.Init();
    }

    void OnClickSetting()
    {
        SettingView.SetActive(true);
    }

    void OnDestroy()
    {
    }

    void OnClickHankBook()
    {
        HandBookView.gameObject.SetActive(true);
    }

    void OnClickStarGame()
    {
        LeveView.SetActive(true);
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
        HandBookView.gameObject.SetActive(false);
        LeveView.gameObject.SetActive(false);
        SettingView.SetActive(false);
        HelpView.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
    }
}