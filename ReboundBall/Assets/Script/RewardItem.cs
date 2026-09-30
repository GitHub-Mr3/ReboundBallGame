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

public class RewardItem : MonoBehaviour
{
    //autoStart
    private Image Img_QualityBg = null;
    private Image Img_Icon = null;
    private TextMeshProUGUI TxtM_QualityTxt = null;
    private TextMeshProUGUI TxtM_Num = null;
    private Button Btn_Click = null;

    private void Awake()
    {
        Img_QualityBg = transform.Find("Img_QualityBg").GetComponent<Image>();
        Img_Icon = transform.Find("Img_Icon").GetComponent<Image>();
        TxtM_QualityTxt = transform.Find("TxtM_QualityTxt").GetComponent<TextMeshProUGUI>();
        TxtM_Num = transform.Find("TxtM_Num").GetComponent<TextMeshProUGUI>();
        Btn_Click = transform.Find("Btn_Click").GetComponent<Button>();
    }

    //autoEnd
    //TODO
    private Transform _Canvas;

    private void Start()
    {
        _Canvas = GameObject.Find("GameMain").transform;
        Btn_Click.onClick.AddListener(OnClickRewardItem);
    }

    void OnClickRewardItem()
    {
        if (isClickPet)
        {
            var uiScr = ResLoader.CreateUIOnce<HandBookInfos>(GameData.HandBookInfos, _Canvas);
            uiScr?.Refresh(_petData);
        }
        else
        {
            var uiScr = ResLoader.CreateUIOnce<HandBookInfos>(GameData.HandBookInfos, _Canvas);
            uiScr?.RefreshFragment();
        }
    }

    private bool isClickPet = false;

    //
    private void OnEnable()
    {
    }

    private GameData.PetData _petData;

    public void Refresh(int id)
    {
        var data = GameData.GetPetData(id);
        _petData = data;
        isClickPet = true;
        Img_QualityBg.sprite = SpriteLibrary.Instance.GetQualityBgSprite(data.quality);
        Img_Icon.sprite = SpriteLibrary.Instance.GetPetTextureSprite(data.spriteIndex);
        Img_Icon.SetNativeSize();
        TxtM_QualityTxt.text = GameData.GetQualityName(data.quality);
        TxtM_Num.text = "";
    }

    public void CreateFragment(int fragment)
    {
        isClickPet = false;
        Img_QualityBg.sprite = SpriteLibrary.Instance.GetQualityBgSprite(1);
        Img_Icon.sprite = SpriteLibrary.Instance.GetPetShuipianSprite();
        Img_Icon.SetNativeSize();
        TxtM_QualityTxt.text = GameData.GetQualityName(1);
        TxtM_Num.text = fragment.ToString();
    }
}