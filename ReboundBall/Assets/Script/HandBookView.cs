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
    private Button Btn_Mask = null;
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
        Btn_Mask = transform.Find("GetAwardTips/Btn_Mask").GetComponent<Button>();
        Tran_AwardTipsRoot = transform.Find("GetAwardTips/Tran_AwardTipsRoot");
        Btn_BookViewMask = transform.Find("Btn_BookViewMask").GetComponent<Button>();
    }

    //autoEnd
    [SerializeField] private HandBookItem handBookItem;
    private List<HandBookItem> handBookItems = new();
    [SerializeField] private GameObject HandBookInfos;
    [SerializeField] private GameObject GetAwardTips;
    [SerializeField] private Sprite bossSprite;
    [SerializeField] private GameObject particleEffect;

    //TODO

    private void Start()
    {
        AddBtnLister();
        AddEvent();
    }

    /// <summary>历史最大通关关卡 id = 已解锁的图鉴数量（上限 12，无尽关不再往上算）</summary>
    private int maxLevelId = -1;

    private void OnEnable()
    {
        GetAwardTips.gameObject.SetActive(false);
        particleEffect.gameObject.SetActive(false);
        CommonMonoUtils.Instance.SetPopEffect(Tran_BookViewRoot);

        maxLevelId = Mathf.Clamp(PlayerPrefs.GetInt(GameData.MaxLevelIdKey, 0), 0, 12);
        Img_slider.fillAmount = maxLevelId / 12.0f;
        TxtM_HandbookSchedu.text = $"收集进度: {maxLevelId}/12";
        CreateHandBookItem();
    }

    private void OnDisable()
    {
        particleEffect.gameObject.SetActive(true);
    }

    void AddBtnLister()
    {
        Btn_Return.onClick.AddListener(OnCloseHandBook);
        Btn_GetReward.onClick.AddListener(OnClickGetReward);
        Btn_Mask.onClick.AddListener(() => { GetAwardTips.gameObject.SetActive(false); });
        Btn_BookViewMask.onClick.AddListener(OnCloseHandBook);
    }

    void OnCloseHandBook()
    {
        gameObject.SetActive(false);
    }

    void OnClickGetReward()
    {
        if (maxLevelId < 12)
        {
            GetAwardTips.gameObject.SetActive(true);
            CommonMonoUtils.Instance.SetPopEffect(Tran_AwardTipsRoot);
        }
        else
        {
            GameData.HandBookInfoData data = new GameData.HandBookInfoData()
            {
                icon = bossSprite,
                name = "星冕.灵犀",
                desc = "传说中只有完成全部图鉴收集才会现身的珍稀小狗。它拥有星辰般的双眼与皇家勋章，仿佛能读懂主人的心意。解锁星冕.灵犀，象征着你已成为真正的毛茸茸大师！"
            };
            EventMgr.Instance.Emit(GameData.EventID_HandBookInfo, data);
        }
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
        var data = (GameData.HandBookInfoData)obj;
        var handbookScript = HandBookInfos.GetComponent<HandBookInfos>();
        if (handbookScript != null)
        {
            handbookScript.Refresh(data);
        }

        HandBookInfos.gameObject.SetActive(true);
    }

    private void CreateHandBookItem()
    {
        for (int i = 1; i <= 12; i++)
        {
            if (handBookItems.Count < i)
            {
                var obj = GameObject.Instantiate(handBookItem.transform.gameObject, Tran_Content);
                var handBookItemScript = obj.GetComponent<HandBookItem>();
                handBookItems.Add(handBookItemScript);
            }

            handBookItems[i - 1].Refresh(i);
        }
    }
}