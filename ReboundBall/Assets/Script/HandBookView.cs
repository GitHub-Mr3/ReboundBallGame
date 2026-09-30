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
using System.Collections;
using System.Collections.Generic;
using Mr3;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.UI;

public class HandBookView : MonoBehaviour
{
    //autoStart
    private Transform Tran_BookViewRoot = null;
    private Transform Tran_Content = null;
    private Image Img_slider = null;
    private Button Btn_Return = null;
    private Button Btn_GetReward = null;
    private TextMeshProUGUI TxtM_HandbookSchedu = null;
    private Transform Tran_AwardTipsRoot = null;
    private Button Btn_BookViewMask;

    private void Awake()
    {
        Tran_BookViewRoot = transform.Find("Tran_BookViewRoot");
        Tran_Content = transform.Find("Tran_BookViewRoot/Scroll View/Viewport/Tran_Content");
        Img_slider = transform.Find("Tran_BookViewRoot/sliderbg/Img_slider").GetComponent<Image>();
        Btn_Return = transform.Find("Tran_BookViewRoot/Btn_Return").GetComponent<Button>();
        Btn_GetReward = transform.Find("Tran_BookViewRoot/Btn_GetReward").GetComponent<Button>();
        TxtM_HandbookSchedu = transform.Find("Tran_BookViewRoot/TxtM_HandbookSchedu").GetComponent<TextMeshProUGUI>();
        Tran_AwardTipsRoot = transform.Find("GetAwardTips/Tran_AwardTipsRoot");
        Btn_BookViewMask = transform.Find("Btn_BookViewMask").GetComponent<Button>();
    }

    //autoEnd
    private GameObject handBookItem;
    private List<HandBookItem> handBookItems = new();
    [SerializeField] private Sprite bossSprite;


    //TODO

    private void Start()
    {
        AddBtnLister();
        AddEvent();
        // 注：按钮显隐在 OnEnable 里按收集进度刷新（集齐 13 只才出现）
    }

    /// <summary>历史最大通关关卡 id = 已解锁的图鉴数量（上限 12，无尽关不再往上算）</summary>
  //  private int maxLevelId = -1;

    private void OnEnable()
    {
        if (handBookItem == null)
        {
            handBookItem = ResLoader.Load(GameData.HandBookItem);
        }

      
        CommonMonoUtils.Instance.SetPopEffect(Tran_BookViewRoot);

      //  maxLevelId = Mathf.Clamp(StorageMgr.GetInt(GameData.MaxLevelIdKey, 0), 0, 12);
        var havePetID = GameData.GetHavePetID();
        Img_slider.fillAmount = havePetID.Count / 12.0f;
        TxtM_HandbookSchedu.text = $"收集进度: {havePetID.Count}/13";
        Btn_GetReward.gameObject.SetActive(havePetID.Count >= 13); // 集齐后才露出领奖按钮
        CreateHandBookItem();
    }

    private void OnDisable()
    {
    }

    void AddBtnLister()
    {
        Btn_Return.onClick.AddListener(OnCloseHandBook);
        Btn_GetReward.onClick.AddListener(OnClickGetReward);
        Btn_BookViewMask.onClick.AddListener(OnCloseHandBook);
    }

    void OnCloseHandBook()
    {
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
        gameObject.SetActive(false);
    }

    /// <summary>图鉴集齐礼包发放的宠物碎片数量</summary>
    private const int CollectGiftFragment = 1000;

    /// <summary>集齐礼包是否已领过（存档：1 = 已领，只发一次）</summary>
    private static bool GiftClaimed
        => StorageMgr.GetInt(GameData.HandBookGiftClaimedKey, 0, StorageTarget.Both) == 1;

    void OnClickGetReward()
    {
        var havePetID = GameData.GetHavePetID();
        if (havePetID.Count < 13)
        {
            CommonUtil.Instance.ShowTips($"还差 {13 - havePetID.Count} 只小狗\n集齐13只可领<color=red>豪华大礼包");
            return;
        }

        // 只可领取一次：发过之后按钮再点就只提示
        if (GiftClaimed)
        {
            CommonUtil.Instance.ShowTips("豪华大礼包已经领取过啦");
            return;
        }

        PetFragmentMgr.Add(CollectGiftFragment, "图鉴集齐礼包");           // 发碎片（内部写存档 + 广播）
        StorageMgr.SetInt(GameData.HandBookGiftClaimedKey, 1, StorageTarget.Both); // 标记已领

        var rewardResults = ResLoader.CreateUIOnce<GetRewardView>(GameData.GetRewardView, transform);
        rewardResults.RefreshRewardItem(new List<int>(), CollectGiftFragment);
    }


    private void OnDestroy()
    {
        RemoveEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener(GameData.EventID_HandBookInfo, OnOpenHandBookInfo);
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener(GameData.EventID_HandBookInfo, OnOpenHandBookInfo);
    }

    private void OnOpenHandBookInfo(string str, object obj)
    {
        var data = (GameData.PetData)obj;

        var uiScr = ResLoader.CreateUIOnce<HandBookInfos>(GameData.HandBookInfos, transform);
        uiScr?.Refresh(data);
    }

    private void CreateHandBookItem()
    {
        var allPetID = GameData.GetSortedPetID();
        for (int i = 0; i < allPetID.Count; i++)
        {
            if (handBookItems.Count < i + 1)
            {
                var obj = GameObject.Instantiate(handBookItem, Tran_Content);
                var handBookItemScript = obj.GetComponent<HandBookItem>();
                handBookItems.Add(handBookItemScript);
            }

            handBookItems[i].Refresh(allPetID[i]);
        }
    }
}