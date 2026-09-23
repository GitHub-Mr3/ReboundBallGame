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
using Unity.VisualScripting;
using UnityEngine.UI;

public class SettingView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Slider Sli_Slider = null;
    private Button Btn_Close = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Sli_Slider = transform.Find("Tran_Root/bg/Sli_Slider").GetComponent<Slider>();
        Btn_Close = transform.Find("Tran_Root/bg/Btn_Close").GetComponent<Button>();
    }

    //autoEnd
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject ParticleSystem;

    //TODO
    private void Start()
    {
        Sli_Slider.onValueChanged.AddListener(SliderChanged);
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Close.onClick.AddListener(OnClose);
    }

    //
    private void OnEnable()
    {
        ParticleSystem.SetActive(false);
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);
        curAudioVolume = PlayerPrefs.GetFloat(GameData.AudioVolumData, -1.0f);
        Sli_Slider.value = curAudioVolume != -1 ? curAudioVolume : audioSource.volume;
        audioSource.volume = curAudioVolume;
        curAudioVolume = Sli_Slider.value;
    }

    private float curAudioVolume = -1.0f;

    void OnClose()
    {
        gameObject.SetActive(false);
        ParticleSystem.SetActive(true);
        if (curAudioVolume != -1.0f)
            PlayerPrefs.SetFloat(GameData.AudioVolumData, curAudioVolume);
    }

    void SliderChanged(float v)
    {
        curAudioVolume = Mathf.Clamp01(v);
        audioSource.volume = Mathf.Clamp01(v);
    }
}