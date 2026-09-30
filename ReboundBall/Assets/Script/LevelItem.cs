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
using Mr3;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LevelItem : MonoBehaviour
{
    //autoStart
    private GameObject Obj_LevelUnlockBg = null;
    private GameObject Obj_LeveLockBg = null;
    private TextMeshProUGUI TxtM_LeveID = null;
    private GameObject Obj_Unlock = null;
    private GameObject Obj_Lock = null;
    private TextMeshProUGUI TxtM_LevelName = null;
    private Image Img_AwardIcon = null;
    private TextMeshProUGUI TxtM_Tips = null;
    private Button Btn_Click = null;

    private void Awake()
    {
        Obj_LevelUnlockBg = transform.Find("Obj_LevelUnlockBg").gameObject;
        Obj_LeveLockBg = transform.Find("Obj_LeveLockBg").gameObject;
        TxtM_LeveID = transform.Find("TxtM_LeveID").GetComponent<TextMeshProUGUI>();
        Obj_Unlock = transform.Find("LeveGroup/Obj_Unlock").gameObject;
        Obj_Lock = transform.Find("LeveGroup/Obj_Lock").gameObject;
        TxtM_LevelName = transform.Find("LeveGroup/TxtM_LevelName").GetComponent<TextMeshProUGUI>();
        Img_AwardIcon = transform.Find("Img_AwardIcon").GetComponent<Image>();
        TxtM_Tips = transform.Find("TxtM_Tips").GetComponent<TextMeshProUGUI>();
        Btn_Click = transform.Find("Btn_Click").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    private void Start()
    {
        Btn_Click.onClick.AddListener(OnClick);
    }

    private int curlevelID = -1;
    private bool isunlock = false;

    void OnClick()
    {
        if (isunlock)
        {
            //解锁，正常进入curlevelID关卡
            LevelManager.Instance.StartLevel(curlevelID);
        }
        else
        {
            //关卡未解锁
            EventMgr.Instance.Emit(GameData.EventID_LeveLock, null);
        }
    }

    public void Refresh(int id, int maxLevelId)
    {
        Awake();
        curlevelID = id;
        //    1            1
        isunlock = id <= maxLevelId + 1;
        TxtM_LeveID.text = $"{id}";
        Obj_Unlock.SetActive(isunlock);
        Obj_Lock.SetActive(!isunlock);
        TxtM_LevelName.text = GameData.GetLevelName(id - 1);
       // Img_AwardIcon.sprite = SpriteLibrary.Instance.GetLevelIconSprite(id - 1);
        Obj_LevelUnlockBg.SetActive(isunlock);
        Obj_LeveLockBg.SetActive(!isunlock);
        // 狗粮数量取关卡配置：LevelData.coinReward 没配时会按关卡号自动推导（第 1 关 10 枚，之后每关 +5）
        int coinReward = LevelConfig.GetLevel(id)?.CoinReward ?? 0;
        TxtM_Tips.text = "通关  <color=yellow>+" + coinReward + "</color>金币";
    }

    //
    private void OnEnable()
    {
    }
}