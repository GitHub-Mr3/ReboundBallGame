using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeChatWASM; // 引入微信SDK命名空间

public class GameMain : MonoBehaviour
{
    private Button Btn_StarGame;
    private Button Btn_GameQuan;


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
        Share();
    }

    void OnClickStarGame()
    {
        SceneManager.LoadSceneAsync("GameScence");
    }
    public void OpenGameClub()
    {
        // 1. 创建页面管理器实例
        WXPageManager pageManager = WX.CreatePageManager();

        // 2. 配置加载参数
        LoadOption option = new LoadOption();
        option.openlink = "-SSEykJvFV3pORt5kTNpS4wSBwrAO9YCvfQKjX2HN-OnArf05jbHiXBZXHaqmcPx_FvLK0Jg-6EzOqaYqFvejFLr6AMD26OcbNqOa7Nn8L2MWJMUm_-pbnaQtG6_Lr_bio7YnXvdvuhERf_7uSy2Og9bUFvaQbIceTwddC4CWjT9O-zVLYghUubWLBEo3gmuiYf1qvsqgjbUkoGaBsMyAj5G-kKzRBmNeyjGmpf15eFpRqj4TOto0MdI4tuuuGY_UQUyT_AFdCfzejsoo1H4lZgAYCjEHJEEBGuuQKVLckuwZqQOZSK9l-YhOnTvUd8BCquXzHIKXgA_aauNtza6EQ"; // 替换为你的实际链接

        option.success = (res) =>
        {
            Debug.Log($"游戏圈加载成功: {res.errMsg}");
            // 加载成功后，显示页面
            ShowOption showOption = new ShowOption();
            pageManager.Show(showOption);
        };

        option.fail = (res) =>
        {
            Debug.LogError($"游戏圈加载失败: {res.errMsg}, 错误码: {res.errCode}");
        };

        // 3. 执行加载
        pageManager.Load(option);
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

    // Update is called once per frame
    void Update()
    {
    }
}