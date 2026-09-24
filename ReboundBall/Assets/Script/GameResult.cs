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

    /// <summary>历史最大通关关卡 id，存本地</summary>
    private int maxLevelId = 0;

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
        Txt_SourceNum = transform.Find("Image/Txt_SourceNum").GetComponent<Text>();
        maxLevelId = PlayerPrefs.GetInt(GameData.MaxLevelIdKey, 0);

        Txt_WinContinueLabel = FindLabel(Btn_WinContinue.transform);
        Txt_ContinueLabel = FindLabel(Btn_Continue.transform);

        //默认先按第 1 关给出文案，进入关卡后会由 LevelChanged 刷新
        RefreshLevelLabel(new LevelData { level = 1 });

        ResetSourceNum();
        SetCanVsGroup(LoseGroup, false);
        SetCanVsGroup(WinGroup, false);
        Img_Mask.gameObject.SetActive(false);
      //  Btn_Quit.gameObject.SetActive(false);
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

        RefreshScoreText();
    }

    /// <summary>
    /// 记录历史最大通关关卡 id：只有本关真的通关（投够最后一次）时才写入本地，
    /// 单次投喂未通关不记，回头重打低关卡也不会把 id 往小改。
    /// </summary>
    void TryRecordMaxLevelId()
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null || !levelManager.LastFeedClearedLevel) return;

        int clearedLevel = levelManager.CurrentLevelNumber;
        if (clearedLevel <= maxLevelId) return;

        maxLevelId = clearedLevel;
        PlayerPrefs.SetInt(GameData.MaxLevelIdKey, maxLevelId);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 进度显示：当前已投喂次数 / 本关通关所需投喂次数（取自 LevelManager，投喂或换关时都会刷新）。
    /// 拿不到关卡数据时兜底显示本局投喂次数。
    /// </summary>
    void RefreshScoreText()
    {
        if (Txt_SourceNum == null) return;

        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            // 拿不到关卡数据时兜底：显示本局投喂次数 / 历史最大通关 id
            Txt_SourceNum.text = $"{curNum}";
            return;
        }

        int total = Mathf.Max(1, levelManager.RequiredFeedCount);
        int done = Mathf.Clamp(levelManager.CurrentFeedCount, 0, total);
        Txt_SourceNum.text = $"{done}/{total}";
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

    /// <summary>
    /// 重新开始：重玩当前关卡，本关投喂进度清零，从当前关卡的第一个进度阶段开始（不回到第 1 关）。
    /// 累计分数保留（与失败后重试一致）；如需一并清分，在这里加一行 ResetSourceNum() 即可。
    /// </summary>
    void OnClickRestart()
    {
        CloseResultPanel(false);
        EventMgr.Instance.Emit(LevelManager.RestartLevelEvent, null);
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
    /// 关卡切换时刷新结算面板文案：LevelManager 布置关卡（进入新关 / 重玩当前关 / 通关后点继续）都会抛这个事件，
    /// 所以这里的关卡号就是接下来真正要打的那一关
    /// </summary>
    void OnLevelChanged(string eventStr, object data)
    {
        RefreshLevelLabel(data as LevelData);
        RefreshScoreText(); // 换关 / 重玩当前关：进度归零，显示当前关所需投喂次数
    }

    void RefreshLevelLabel(LevelData levelData)
    {
        if (levelData == null) return;

        string levelName = levelData.level <= LevelConfig.DesignedLevelCount
            ? $"{levelData.levelName}"
            : $"无尽·第{levelData.level}关";

        if (Txt_WinContinueLabel != null)
        {
            Txt_WinContinueLabel.text = $"投喂{SpliteLeveName(levelName)}";
        }

        if (Txt_ContinueLabel != null)
        {
            Txt_ContinueLabel.text = "继续投喂";//$"重试{SpliteLeveName(levelName)}";
        }
    }
    private string SpliteLeveName(string levelName)
    {
        var names = levelName.Split('.');
        if (names.Length > 1)
        {
            return names[names.Length - 1];
        }
        return levelName;
    }

    void OnGameOver(string eventStr, object data)
    {
        bool isWin = (bool)data;
        if (isWin)
        {
            SetSourceNum(1);
            TryRecordMaxLevelId();
        }

        // LevelManager 先于结算面板处理 GameOver：拿它给的结论决定按钮文案是「进入下一关」还是「继续投喂」
        bool levelCleared = LevelManager.Instance == null || LevelManager.Instance.LastFeedClearedLevel;
        RefreshResultLabels(isWin && levelCleared);

        SetResultView(isWin);
    }

    /// <summary>
    /// 已通关：关卡要等玩家点「继续」才切换，这里只把按钮文案预写成下一关（HUD 仍停在本关最后一条进度）；
    /// 未通关（本次只是完成一次投喂）：把「继续」按钮改成继续投喂，避免看起来像已经换关。
    /// </summary>
    void RefreshResultLabels(bool levelCleared)
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null) return;

        if (levelCleared)
        {
            RefreshLevelLabel(levelManager.NextLevel);
            return;
        }

        int total = levelManager.RequiredFeedCount;
        if (Txt_WinContinueLabel == null || total <= 1) return;

        int done = levelManager.CurrentFeedCount;
        //string stage = levelManager.CurrentFeedStage;
        Txt_WinContinueLabel.text = $"继续{done}/{total}";
    }

    //是否已推荐，如果已推荐就不再执行
    private bool isRecommend = false;

    void SetResultView(bool isWin)
    {
        Img_Mask.gameObject.SetActive(true);
        Btn_Quit.gameObject.SetActive(true);
        SetCanVsGroup(LoseGroup, !isWin);
        SetCanVsGroup(WinGroup, isWin);
        // 首次通关第 1 关时弹一次推荐（maxLevelId 恰好为 1 说明刚通了第 1 关）
        if (isWin && !isRecommend && maxLevelId == 1)
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
        // 本关通关时，关卡推进推迟到这一刻：此前 LevelHud 一直停留在当前关最后一条投喂进度，
        // 点「继续」后才进入下一关，LevelHud 随之刷新成下一关的第一条（等待投喂 0/N）。
        // 未通关（只完成一次投喂）时该调用内部会直接忽略，继续留在本关。
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager != null) levelManager.AdvanceToNextLevel();

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