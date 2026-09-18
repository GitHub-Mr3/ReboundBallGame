using System;
using System.Collections;
using System.Collections.Generic;
using Mr3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private Button Btn_Quit;

    //结算面板上跟随关卡变化的按钮文案
    private TextMeshProUGUI Txt_WinContinueLabel;
    private TextMeshProUGUI Txt_ContinueLabel;


    private int curNum = 0;
    private Button Btn_Restart;

    /// <summary>历史最高分，存本地</summary>
    private const string MaxScoreKey = "maxScore";

    private int maxNum = 0;

    public WxRecommend wxRecommendManager;

    private void Awake()
    {
        LoseGroup = transform.Find("LoseGroup").GetComponent<CanvasGroup>();
        WinGroup = transform.Find("WinGroup").GetComponent<CanvasGroup>();
        Btn_Continue = LoseGroup.transform.Find("Btn_Continue").GetComponent<Button>();
        Btn_Restart = LoseGroup.transform.Find("Btn_Restart").GetComponent<Button>();
        Btn_WinContinue = WinGroup.transform.Find("Btn_WinContinue").GetComponent<Button>();
        Btn_Quit = transform.Find("Btn_Quit").GetComponent<Button>();

        Img_Mask = transform.Find("Img_Mask").gameObject;
        Txt_SourceNum = transform.Find("sourceNum/Txt_SourceNum").GetComponent<Text>();
        maxNum = PlayerPrefs.GetInt(MaxScoreKey, 0);

        Txt_WinContinueLabel = FindLabel(Btn_WinContinue.transform);
        Txt_ContinueLabel = FindLabel(Btn_Continue.transform);

        //默认先按第 1 关给出文案，进入关卡后会由 LevelChanged 刷新
        RefreshLevelLabel(new LevelData { level = 1 });

        ResetSourceNum();
        SetCanVsGroup(LoseGroup, false);
        SetCanVsGroup(WinGroup, false);
        Img_Mask.gameObject.SetActive(false);
        Btn_Quit.gameObject.SetActive(false);
    }

    /// <summary>取按钮下的 TMP 文本（面板是隐藏状态也要能取到，所以用含隐藏物体的查找）</summary>
    TextMeshProUGUI FindLabel(Transform root)
    {
        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        return labels != null && labels.Length > 0 ? labels[0] : null;
    }

    void ResetSourceNum()
    {
        curNum = 0;
        SetSourceNum(0);
    }


    void SetSourceNum(int num)
    {
        curNum += num;

        if (curNum > maxNum)
        {
            maxNum = curNum;
            PlayerPrefs.SetInt(MaxScoreKey, maxNum);
            PlayerPrefs.Save();
        }

        RefreshScoreText();
    }

    /// <summary>当前分数 / 最高记录</summary>
    void RefreshScoreText()
    {
        if (Txt_SourceNum != null) Txt_SourceNum.text = $"{curNum}/{maxNum}";
    }


    // Start is called before the first frame update
    void Start()
    {
        Btn_Continue.onClick.AddListener(OnLoseContinue);
        Btn_WinContinue.onClick.AddListener(OnWinContinue);
        Btn_Restart.onClick.AddListener(OnClickRestart);
        Btn_Quit.onClick.AddListener(OnClick_Quit);
        AddEvent();
        isRecommend = false;
        wxRecommendManager.PreloadRecommend();
    }

    void OnClick_Quit()
    {
        SceneManager.LoadSceneAsync("GameMain");
    }

    void OnClickRestart()
    {
        ResetSourceNum();
        CloseResultPanel(false);
        EventMgr.Instance.Emit("RestartGame", null);
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver", OnGameOver);
        EventMgr.Instance.AddListener(LevelManager.LevelChangedEvent, OnLevelChanged);
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener(LevelManager.LevelChangedEvent, OnLevelChanged);
    }

    /// <summary>
    /// 关卡切换时刷新结算面板文案：
    /// 通关后 LevelManager 会先推进到下一关再抛事件，所以这里的关卡号就是点“继续”要打的那一关
    /// </summary>
    void OnLevelChanged(string eventStr, object data)
    {
        RefreshLevelLabel(data as LevelData);
    }

    void RefreshLevelLabel(LevelData levelData)
    {
        if (levelData == null) return;

        string levelTag = levelData.level <= LevelConfig.DesignedLevelCount
            ? $"第 {levelData.level} 关"
            : $"无尽 · 第 {levelData.level} 关";

        if (Txt_WinContinueLabel != null)
        {
            Txt_WinContinueLabel.text = $"进入{levelTag}";
        }

        if (Txt_ContinueLabel != null)
        {
            Txt_ContinueLabel.text = $"重试{levelTag}";
        }
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

    //是否已推荐，如果已推荐就不再执行
    private bool isRecommend = false;

    void SetResultView(bool isWin)
    {
        Img_Mask.gameObject.SetActive(true);
        Btn_Quit.gameObject.SetActive(true);
        SetCanVsGroup(LoseGroup, !isWin);
        SetCanVsGroup(WinGroup, isWin);
        if (isWin && !isRecommend)
        {
            isRecommend = true;
            wxRecommendManager.ShowRecommend();
        }
    }

    void OnLoseContinue()
    {
        AdManager.Instance.ShowRewardedVideo((isReward) =>
        {
            if (isReward)
            {
                CloseResultPanel(false);
            }
            else
            {
                
                
            }
        });
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
        EventMgr.Instance.Emit("GameStar", isWin);
    }


    void SetCanVsGroup(CanvasGroup _canVasG, bool isShow)
    {
        _canVasG.alpha = isShow ? 1f : 0f;
        _canVasG.interactable = isShow;
        _canVasG.blocksRaycasts = isShow;
    }
}