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
using Unity.VisualScripting;
using UnityEngine.UI;

public class LevelView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Button Btn_Return = null;
    private Transform Tran_Content = null;
    private GameObject Obj_LockTips = null;
    private Button Btn_LockTipsMask = null;
    private Transform Tran_LockTipsRoot = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Btn_Return = transform.Find("Tran_Root/bg/Btn_Return").GetComponent<Button>();
        Tran_Content = transform.Find("Tran_Root/bg/Scroll View/Viewport/Tran_Content");
        Obj_LockTips = transform.Find("Tran_Root/Obj_LockTips").gameObject;
        Btn_LockTipsMask = transform.Find("Tran_Root/Obj_LockTips/Btn_LockTipsMask").GetComponent<Button>();
        Tran_LockTipsRoot = transform.Find("Tran_Root/Obj_LockTips/Tran_LockTipsRoot");
    }

    //autoEnd
    //TODO
    private List<LevelItem> levelItemScr = new();

    [SerializeField] private GameObject _levelItem;
    [SerializeField] private GameObject particleSystem;

    private void Start()
    {
        AddBtnLister();
        AddEvent();
    }

    void OnDestroy()
    {
        RemoveEvent();
    }

    void AddBtnLister()
    {
        Btn_Mask.onClick.AddListener(OnClickClose);
        Btn_Return.onClick.AddListener(OnClickClose);
        Btn_LockTipsMask.onClick.AddListener(() => { Obj_LockTips.SetActive(false); });
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener(GameData.EventID_LeveLock, OnLevelLock);
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener(GameData.EventID_LeveLock, OnLevelLock);
    }

    void OnLevelLock(string str, object data)
    {
        Obj_LockTips.SetActive(true);
        CommonMonoUtils.Instance.SetPopEffect(Tran_LockTipsRoot);
    }


    void CreateLevelItem()
    {
        var maxLeveNum = GameData.MaxLevelNum+1;

        var maxLevelId = PlayerPrefs.GetInt(GameData.MaxLevelIdKey, 0);
       // Debug.LogError("当前解锁最高关卡id:"+maxLevelId);
        for (int i = 1; i <= maxLeveNum; i++)
        {
            if (levelItemScr.Count < i)
            {
                var obj = GameObject.Instantiate(_levelItem, Tran_Content);
                var scr = obj.GetComponent<LevelItem>();
                levelItemScr.Add(scr);
            }

            levelItemScr[i - 1].Refresh(i, maxLevelId);
        }
    }

    void OnClickClose()
    {
        particleSystem?.gameObject?.SetActive(true);
        gameObject.SetActive(false);
    }

    //
    private void OnEnable()
    {
        particleSystem?.gameObject?.SetActive(false);
        Obj_LockTips.SetActive(false);
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);
        CreateLevelItem();
    }
}