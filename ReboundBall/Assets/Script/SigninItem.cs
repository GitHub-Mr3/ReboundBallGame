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

using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 签到面板里的一个「第 N 天」格子，由 SignInView 创建并调 Refresh 填数据。
///
/// 显示规则（核心）：
///   - Img_ItemIcon：这一天的奖励图标（金币 / 宠物碎片 / 宠物头像）；
///   - TxtM_StatsTxt：这一天的状态 —— 已领取 / 可领取 / 未解锁；
///   - Btn_GetAward：只有「当前可领取的那一天」才显示，其它天直接隐藏（SetActive(false)）；
///   - TxtM_Day：第几天。
///
/// 用法：
///     signinItem.Refresh(0);   // 下标 0 = 第 1 天，1 = 第 2 天 …… 6 = 第 7 天
/// </summary>
public class SigninItem : MonoBehaviour
{
    //autoStart
    private Image Img_ItemBg = null;
    private TextMeshProUGUI TxtM_Day = null;
    private Image Img_ItemIcon = null;
    private Button Btn_GetAward = null;
    private Image Img_StatsBG = null;
    private TextMeshProUGUI TxtM_StatsTxt = null;

    private void Awake()
    {
        Img_ItemBg = transform.Find("Img_ItemBg").GetComponent<Image>();
        TxtM_Day = transform.Find("Img_ItemBg/title/TxtM_Day").GetComponent<TextMeshProUGUI>();
        Img_ItemIcon = transform.Find("Img_ItemBg/Img_ItemIcon").GetComponent<Image>();
        Btn_GetAward = transform.Find("Img_ItemBg/Btn_GetAward").GetComponent<Button>();
        Img_StatsBG = transform.Find("Img_ItemBg/Img_StatsBG").GetComponent<Image>();
        TxtM_StatsTxt = transform.Find("Img_ItemBg/Img_StatsBG/TxtM_StatsTxt").GetComponent<TextMeshProUGUI>();
    }
    //autoEnd
    //TODO

    /// <summary>本格子代表第几天（下标，0 = 第 1 天），刷新同层其它格子时用</summary>
    private int _dayIndex = 0;

    /// <summary>签到成功后抛出（SignInView 可以监听它刷新"累计签到 N 天"之类的顶部信息）</summary>
    public static event Action OnSignInSuccess;

    private void Start()
    {
        // 领取按钮：只有当前可领取那天会显示出来，点了就直接调 SignInMgr 签到
        if (Btn_GetAward != null)
        {
            Btn_GetAward.onClick.RemoveAllListeners();
            Btn_GetAward.onClick.AddListener(OnClickGetAward);
        }
    }

    //
    private void OnEnable()
    {
    }

    public void Refresh(int dayIndex)
    {
        _dayIndex = dayIndex;

        int day = dayIndex + 1; // 下标 0 = 第 1 天
        SignInMgr.SignInReward reward = SignInMgr.GetReward(day);

        if (TxtM_Day != null) TxtM_Day.text = $"第 {day} 天";

        RefreshIcon(reward);

        bool missed = SignInMgr.IsDayMissed(day); // 断签漏掉的（优先判断：它进度算过去了，但奖励没发）
        bool claimed = !missed && SignInMgr.IsDayClaimed(day); // 本轮这天已经领过
        bool canGet = !missed && !claimed && SignInMgr.CanSignInToday && SignInMgr.TodayIndex == day; // 就是今天该领的那天

        if (TxtM_StatsTxt != null)
            TxtM_StatsTxt.text = missed ? "漏签" : (claimed ? "已领取" : (canGet ? "可领取" : "未解锁"));
        Img_ItemBg.color = claimed ? Color.gray : Color.white;

        // 只有当前可领取的那天显示按钮，其余（已领 / 还没轮到）直接隐藏
        if (Btn_GetAward != null) Btn_GetAward.gameObject.SetActive(canGet);
        Img_StatsBG.gameObject.SetActive(!canGet);
    }

    /// <summary>按奖励类型取图标：金币 / 碎片 / 宠物头像</summary>
    private void RefreshIcon(SignInMgr.SignInReward reward)
    {
        if (Img_ItemIcon == null) return;

        SpriteLibrary library = SpriteLibrary.Instance;
        if (library == null)
        {
            DebugMgr.Instance.LogWarning("[SigninItem] 场景里没有 SpriteLibrary，奖励图标取不到");
            return;
        }

        Sprite sprite = null;
        switch (reward.type)
        {
            case SignInMgr.RewardType.Coin:
                sprite = library.GetCoinSprite();
                break;

            case SignInMgr.RewardType.Fragment:
                sprite = library.GetPetShuipianSprite();
                break;

            case SignInMgr.RewardType.Pet:
                sprite = library.GetPetTextureSprite(GameData.GetPetData(reward.petId).spriteIndex);
                break;
        }

        if (sprite == null)
        {
            DebugMgr.Instance.LogWarning(
                $"[SigninItem] 第 {_dayIndex + 1} 天的奖励图标为空（{reward.type}），检查一下 SpriteLibrary 配置");
            return;
        }

        Img_ItemIcon.sprite = sprite;
        Img_ItemIcon.SetNativeSize();
    }

    /// <summary>点「领取」：调 SignInMgr 签到，弹提示，然后把同层的 7 个格子一起刷新</summary>
    private void OnClickGetAward()
    {
        bool isSignin = SignInMgr.TrySignIn(out SignInMgr.SignInReward reward, out string tip);
        CommonUtil.Instance.ShowTips(tip);

        RefreshAllSiblings();
        OnSignInSuccess?.Invoke();
        if (isSignin)
        {
            RedPointManager.Instance.SetRedPointCount(NodeNames.Signin, 0);
        }
    }

    /// <summary>刷新同一个父节点下的所有签到格子（领完要更新"已领取 / 可领取"状态）</summary>
    private void RefreshAllSiblings()
    {
        Transform root = transform.parent;
        if (root == null)
        {
            Refresh(_dayIndex);
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            SigninItem item = root.GetChild(i).GetComponent<SigninItem>();
            if (item != null) item.Refresh(item._dayIndex);
        }
    }
}