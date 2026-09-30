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

public class HandBookItem : MonoBehaviour
{
    //autoStart
    private Image Img_bg = null;
	private Image Img_Icon = null;
	private Transform Tran_Lock = null;
	private TextMeshProUGUI TxtM_Tips = null;
	private Button Btn_Click = null;
	
    private void Awake()
	{
		Img_bg = transform.Find("Img_bg").GetComponent<Image>();
		Img_Icon = transform.Find("Img_Icon").GetComponent<Image>();
		Tran_Lock = transform.Find("Tran_Lock");
		TxtM_Tips = transform.Find("tipsbg/TxtM_Tips").GetComponent<TextMeshProUGUI>();
		Btn_Click = transform.Find("Btn_Click").GetComponent<Button>();
		
	}
    //autoEnd
    //TODO


    /// <summary>历史最大通关关卡 id：id <= 它的图鉴条目视为已解锁</summary>
    private int maxLevelId = -1;

    private void Start()
    {
        maxLevelId = StorageMgr.GetInt(GameData.MaxLevelIdKey, 0);
        Btn_Click.onClick.AddListener(OnClickHandItem);
    }

    private int curHandBookID;
    private bool Unlock = false;

    void OnClickHandItem()
    {
        EventMgr.Instance.Emit(GameData.EventID_HandBookInfo, _petData);
    }

    private Sprite curSprite;
    private string curName;
    private GameData.PetData _petData;

    public void Refresh(int id)
    {
        curHandBookID = id;
        // 每次刷新都重读：玩家可能刚通关新关卡，解锁状态要跟着最新的通关 id 变
        maxLevelId = StorageMgr.GetInt(GameData.MaxLevelIdKey, 0);


        var petData = GameData.GetPetData(id);
        _petData = petData;
        curSprite = SpriteLibrary.Instance.GetLevelIconSprite(petData.spriteIndex); //icon[index];
        curName = petData.name;

        var havePetID = GameData.GetHavePetID();
        Unlock = havePetID.Contains(id); //id <= maxLevelId;

        Img_Icon.sprite = curSprite;
        TxtM_Tips.text = $"{curName}";
        Tran_Lock.gameObject.SetActive(!Unlock);
    }

    private struct HandBookData
    {
        public string name;
        public string desc;
    }


    //
    private void OnEnable()
    {
    }
}