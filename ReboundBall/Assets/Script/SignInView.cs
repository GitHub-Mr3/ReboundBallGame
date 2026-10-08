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

using System.Collections.Generic;
using Mr3;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SignInView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Transform Tran_ItemRoot = null;
    private Button Btn_Return = null;
    private Button Btn_ResSignature = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Tran_ItemRoot = transform.Find("Tran_Root/bg/Scroll View/Viewport/Tran_ItemRoot");
        Btn_Return = transform.Find("Tran_Root/bg/Btn_Return").GetComponent<Button>();
        Btn_ResSignature = transform.Find("Tran_Root/bg/Btn_ResSignature").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    void AddBtnLister()
    {
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Return.onClick.AddListener(OnClose);
        Btn_ResSignature.onClick.AddListener(OnResSignature);
    }

    /// <summary>
    /// 补签：把昨天漏签的那天补回来，奖励照发、进度接上（一天只能补一次）。
    /// 不管成功失败都弹一句提示，然后把 7 个格子和补签按钮一起刷新。
    /// </summary>
    void OnResSignature()
    {
        AdManager.Instance.ShowRewardedVideo((isReward) =>
        {
            if (isReward)
            {
                SignInMgr.TryResign(out string tip);
                CommonUtil.Instance.ShowTips(tip);
                CreateSigninItem();
                RefreshResignBtn();
            }
        });
    }

    /// <summary>没有可补的天时把补签按钮置灰（有得补才亮）</summary>
    void RefreshResignBtn()
    {
        if (Btn_ResSignature == null) return;

        Btn_ResSignature.interactable = SignInMgr.CanResignToday;
    }

    private List<SigninItem> _signinItems = new();

    void CreateSigninItem()
    {
        for (int i = 0; i < 7; i++)
        {
            if (_signinItems.Count <= i)
            {
                var uiScr = ResLoader.Create<SigninItem>(GameData.SigninItem, Tran_ItemRoot);
                _signinItems.Add(uiScr);
            }

            _signinItems[i].Refresh(i);
        }
    }

    void OnClose()
    {
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
        gameObject.SetActive(false);
    }

    private void Start()
    {
        AddBtnLister();
    }

    //
    private void OnEnable()
    {
        CreateSigninItem();
        RefreshResignBtn();
    }
}