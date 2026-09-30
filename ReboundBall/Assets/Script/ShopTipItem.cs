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

public class ShopTipItem : MonoBehaviour
{
    //autoStart
    private Image Img_IconBg = null;
    private Image Img_Icon = null;
    private TextMeshProUGUI TxtM_Quality = null;
    private Button Btn_Click = null;

    private void Awake()
    {
        Img_IconBg = transform.Find("Img_IconBg").GetComponent<Image>();
        Img_Icon = transform.Find("Img_Icon").GetComponent<Image>();
        TxtM_Quality = transform.Find("TxtM_Quality").GetComponent<TextMeshProUGUI>();
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
        var uiScr = ResLoader.CreateUIOnce<HandBookInfos>(GameData.HandBookInfos, _Canvas);
        uiScr?.Refresh(_petData);
    }

    //
    private void OnEnable()
    {
    }

    private GameData.PetData _petData;

    public void Refresh(GameData.PetData data)
    {
        _petData = data;
        Img_Icon.sprite = SpriteLibrary.Instance.GetPetTextureSprite(data.spriteIndex);
        TxtM_Quality.text = GameData.GetQualityName(data.quality);
        Img_IconBg.sprite = SpriteLibrary.Instance.GetQualityBgSprite(data.quality);
    }
}