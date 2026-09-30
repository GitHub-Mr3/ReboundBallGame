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

public class LotteryTipsView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Transform Tran_Content = null;
    private Button Btn_Close = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Tran_Content = transform.Find("Tran_Root/Scroll View/Viewport/Tran_Content");
        Btn_Close = transform.Find("Tran_Root/Btn_Close").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    private void Start()
    {
        AddBtnLister();
        CreateItem();
    }

    void AddBtnLister()
    {
        Btn_Mask.onClick.AddListener(OnClick);
        Btn_Close.onClick.AddListener(OnClick);
    }

    void CreateItem()
    {
        var allPetID = GameData.GetAllPetID();
        List<GameData.PetData> allPetData = new();
        for (int i = 0; i < allPetID.Count; i++)
        {
            var tempPetData = GameData.GetPetData(allPetID[i]);
            allPetData.Add(tempPetData);
        }

        allPetData.Sort((a, b) => a.quality.CompareTo(b.quality));

        for (int i = 0; i < allPetData.Count; i++)
        {
            var uiScr = ResLoader.Create<ShopTipItem>(GameData.ShopTipItem, Tran_Content);
            uiScr.Refresh(allPetData[i]);
        }
    }

    void OnClick()
    {
        gameObject.SetActive(false);
    }

    //
    private void OnEnable()
    {
    }
}