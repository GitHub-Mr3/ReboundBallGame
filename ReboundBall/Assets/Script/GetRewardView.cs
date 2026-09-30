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
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GetRewardView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Transform Tran_Content = null;
    private Button Btn_Return = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Tran_Content = transform.Find("Tran_Root/bg/Scroll View/Viewport/Tran_Content");
        Btn_Return = transform.Find("Tran_Root/bg/Btn_Return").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    private void Start()
    {
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Return.onClick.AddListener(OnClose);
    }

    void OnClose()
    {
        gameObject.SetActive(false);
    }

    //
    private void OnEnable()
    {
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);
    }


    private List<RewardItem> _rewardItems = new();

    public void RefreshRewardItem(List<int> results, int fragment)
    {
        for (int i = 0; i < _rewardItems.Count; i++)
        {
            _rewardItems[i].gameObject.SetActive(false);
        }

        for (int i = 0; i <= results.Count; i++)
        {
            if (_rewardItems.Count <= i)
            {
                RewardItem scr = ResLoader.Create<RewardItem>(GameData.RewardItem, Tran_Content);
                _rewardItems.Add(scr);
            }

            _rewardItems[i].gameObject.SetActive(true);
            if (i == results.Count)
            {
                _rewardItems[i].CreateFragment(fragment);
            }
            else
            {
                _rewardItems[i].Refresh(results[i]);
            }
        }
    }
}