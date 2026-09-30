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

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HandBookInfos : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_root = null;
    private Image Img_Icon = null;
    private TextMeshProUGUI TxtM_Name = null;
    private TextMeshProUGUI TxtM_Infos = null;
    private Button Btn_CloseBookInfo = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_root = transform.Find("Tran_root");
        Img_Icon = transform.Find("Tran_root/Img_Icon").GetComponent<Image>();
        TxtM_Name = transform.Find("Tran_root/TxtM_Name").GetComponent<TextMeshProUGUI>();
        TxtM_Infos = transform.Find("Tran_root/Scroll View/Viewport/TxtM_Infos").GetComponent<TextMeshProUGUI>();
        Btn_CloseBookInfo = transform.Find("Tran_root/Btn_CloseBookInfo").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    private void Start()
    {
        Btn_CloseBookInfo.onClick.AddListener(OnCloseBookInfo);
        Btn_Mask.onClick.AddListener(OnCloseBookInfo);
    }

    void OnCloseBookInfo()
    {
        gameObject.SetActive(false);
    }

    public void Refresh(GameData.PetData data)
    {
        Awake();
        CommonMonoUtils.Instance.SetPopEffect(Tran_root);

        Img_Icon.sprite = SpriteLibrary.Instance.GetLevelIconSprite(data.spriteIndex);
        TxtM_Name.text = data.name;
        var petSkill = GameData.GetPetSkillData(data.SkillID);
        TxtM_Infos.text = data.desc + "\n 携带技能:<color=red>" + petSkill.SkillName + "</color>\n技能效果:\n" +
                          petSkill.SkillDesc;
    }

    public void RefreshFragment()
    {
        Awake();
        CommonMonoUtils.Instance.SetPopEffect(Tran_root);

        Img_Icon.sprite = SpriteLibrary.Instance.GetPetShuipianSprite();
        TxtM_Name.text = "宠物碎片";

        TxtM_Infos.text = "名称：宠物碎片\n用途:可以用来给狗狗升级/兑换专用\n获取途径：仅抽奖可得，重复宠物自动转化";
    }

    //
    private void OnEnable()
    {
    }
}