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

public class SettingView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Slider Sli_Slider = null;
    private Slider Sli_SoundSlider = null;
    private Button Btn_Close = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Sli_Slider = transform.Find("Tran_Root/bg/Sli_Slider").GetComponent<Slider>();
        Sli_SoundSlider = transform.Find("Tran_Root/bg/Sli_SoundSlider").GetComponent<Slider>();
        Btn_Close = transform.Find("Tran_Root/bg/Btn_Close").GetComponent<Button>();
    }
    //autoEnd

    //TODO
    private void Start()
    {
        Sli_Slider.onValueChanged.AddListener(SliderChanged);
        Sli_SoundSlider.onValueChanged.AddListener(OnSoundSliderChange);
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Close.onClick.AddListener(OnClose);
    }

    //
    private void OnEnable()
    {
        // ParticleSystem.SetActive(false);
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);

        curBGVolume = StorageMgr.GetFloat(GameData.AudioVolumData, 0.5f);
        Sli_Slider.value = curBGVolume;
        curBGVolume = Sli_Slider.value;

        curSoundVolume = StorageMgr.GetFloat(GameData.SoundVolumData, 0.5f);
        Sli_SoundSlider.value = curSoundVolume;
        curSoundVolume = Sli_SoundSlider.value;
    }

    private float curBGVolume = -1f;
    private float curSoundVolume = -1f;


    void OnClose()
    {
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
        gameObject.SetActive(false);
        if (curBGVolume != -1.0f)
            StorageMgr.SetFloat(GameData.AudioVolumData, curBGVolume);
        if (curSoundVolume != -1.0f)
            StorageMgr.SetFloat(GameData.SoundVolumData, curSoundVolume);
    }

    void SliderChanged(float v)
    {
        curBGVolume = Mathf.Clamp01(v);
        AudioMgr.Instance.BGMVolume = curBGVolume;
    }

    void OnSoundSliderChange(float v)
    {
        curSoundVolume = Mathf.Clamp01(v);
        AudioMgr.Instance.SFXVolume = curSoundVolume;
    }
}