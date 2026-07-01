using System;
using System.Collections;
using System.Collections.Generic;
using Mr3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeChatWASM;

public class GameResult : MonoBehaviour
{
    private CanvasGroup LoseGroup;
    private CanvasGroup WinGroup;

    private GameObject Img_Mask;
    private Button Btn_Continue;
    private Button Btn_WinContinue;
    private Text Txt_SourceNum;


    private int curNum = 0;
    private Button Btn_Restart;

    private void Awake()
    {
        LoseGroup = transform.Find("LoseGroup").GetComponent<CanvasGroup>();
        WinGroup = transform.Find("WinGroup").GetComponent<CanvasGroup>();
        Btn_Continue = LoseGroup.transform.Find("Btn_Continue").GetComponent<Button>();
        Btn_Restart=LoseGroup.transform.Find("Btn_Restart").GetComponent<Button>();
        Btn_WinContinue = WinGroup.transform.Find("Btn_WinContinue").GetComponent<Button>();

        Img_Mask = transform.Find("Img_Mask").gameObject;
        Txt_SourceNum=transform.Find("sourceNum/Txt_SourceNum").GetComponent<Text>();

        ResetSourceNum();
        SetCanVsGroup(LoseGroup, false);
        SetCanVsGroup(WinGroup, false);
        Img_Mask.gameObject.SetActive(false);

    }

    void ResetSourceNum()
    {
        curNum = 0;
        SetSourceNum(0);
    }
    

    void SetSourceNum(int num)
    {
        curNum += num;
        Txt_SourceNum.text = curNum.ToString();
    }

   

    // Start is called before the first frame update
    void Start()
    {
        Btn_Continue.onClick.AddListener(OnLoseContinue);
        Btn_WinContinue.onClick.AddListener(OnWinContinue);
        Btn_Restart.onClick.AddListener(OnClickRestart);
        AddEvent();
    }

    void OnClickRestart()
    {
        ResetSourceNum();
        CloseResultPanel(false);
        EventMgr.Instance.Emit("RestartGame",null);
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver", OnGameOver);
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
    }

    void OnGameOver(string eventStr, object data)
    {
        bool isWin = (bool)data;
        if (isWin)
        {
            SetSourceNum(1);
        }
        SetResultView(isWin);
    }

    void SetResultView(bool isWin)
    {
        Img_Mask.gameObject.SetActive(true);
        SetCanVsGroup(LoseGroup, !isWin);
        SetCanVsGroup(WinGroup, isWin);
    }

    void OnLoseContinue()
    {
        CloseResultPanel(false);
    }

    void OnWinContinue()
    {
        CloseResultPanel(true);
    }

    void CloseResultPanel(bool isWin)
    {
        SetCanVsGroup(LoseGroup, false);
        SetCanVsGroup(WinGroup, false);
        Img_Mask.gameObject.SetActive(false);
        EventMgr.Instance.Emit("GameStar",isWin);
    }



    void SetCanVsGroup(CanvasGroup _canVasG, bool isShow)
    {
        _canVasG.alpha = isShow ? 1f : 0f;
        _canVasG.interactable = isShow;
        _canVasG.blocksRaycasts = isShow;
    }
}