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
        // 补签功能已移除，这里只拿引用以便隐藏按钮，预制体上若还有 Btn_ResSignature 节点就直接隐藏
        Btn_ResSignature = transform.Find("Tran_Root/bg/Btn_ResSignature")?.GetComponent<Button>();
    }

    //autoEnd
    //TODO
    void AddBtnLister()
    {
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Return.onClick.AddListener(OnClose);
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
        // 补签按钮已废弃，界面打开时直接隐藏（不影响签到格子刷新）
        if (Btn_ResSignature != null) Btn_ResSignature.gameObject.SetActive(false);
        CreateSigninItem();
    }
}