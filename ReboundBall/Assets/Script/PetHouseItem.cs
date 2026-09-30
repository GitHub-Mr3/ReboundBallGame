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

using Mr3;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PetHouseItem : MonoBehaviour
{
    //autoStart
    private Image Img_icon = null;
    private GameObject Obj_Lock = null;
    private TextMeshProUGUI TxtM_Name = null;
    private Button Btn_Click = null;

    /// <summary>「出战中」标记：预制体里加一个 bg/Obj_Battle 就会显示，没有这个节点不影响其它逻辑</summary>
    private GameObject Obj_Battle = null;

    private void Awake()
    {
        Img_icon = transform.Find("bg/Img_icon").GetComponent<Image>();
        Obj_Lock = transform.Find("bg/Obj_Lock").gameObject;
        TxtM_Name = transform.Find("bg/NameBg/TxtM_Name").GetComponent<TextMeshProUGUI>();
        Btn_Click = transform.Find("bg/Btn_Click").GetComponent<Button>();

        Transform battleNode = transform.Find("bg/Obj_Battle");
        if (battleNode != null) Obj_Battle = battleNode.gameObject;
    }

    //autoEnd
    //TODO
    private void Start()
    {
        Btn_Click.onClick.AddListener(OnClickChosePet);
    }

    //
    private void OnEnable()
    {
    }

    void OnClickChosePet()
    {
        EventMgr.Instance.Emit(GameData.Event_SelectPet, curID);
    }

    private int curID = -1;


    public void Refresh(int id)
    {
        curID = id;
        var petData = GameData.GetPetData(id);
        TxtM_Name.text = petData.name;
        var havepetIDs = GameData.GetHavePetID();
        Obj_Lock.SetActive(!havepetIDs.Contains(id));
        Img_icon.sprite = SpriteLibrary.Instance.GetPetTextureSprite(petData.spriteIndex);
        Img_icon.SetNativeSize();
        RefreshBattleMark();
    }

    /// <summary>刷新「出战中」标记：这只宠物是当前出战宠物时才亮</summary>
    public void RefreshBattleMark()
    {
        if (Obj_Battle == null) return;

        Obj_Battle.SetActive(PetSkillMgr.BattlePetId == curID);
    }
}