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

    //TODO
    private void Start()
    {
        audioSource = GameObject.FindAnyObjectByType<AudioSourceMono>().transform.GetComponent<AudioSource>();

        Sli_Slider.onValueChanged.AddListener(SliderChanged);
        Btn_Mask.onClick.AddListener(OnClose);
        Btn_Close.onClick.AddListener(OnClose);
    }

    //
    private void OnEnable()
    {
        // ParticleSystem.SetActive(false);
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);
        if (audioSource == null)
            audioSource = GameObject.FindAnyObjectByType<AudioSourceMono>().transform.GetComponent<AudioSource>();
        curAudioVolume = StorageMgr.GetFloat(GameData.AudioVolumData, -1.0f);
        Sli_Slider.value = curAudioVolume != -1 ? curAudioVolume : audioSource.volume;
        audioSource.volume = curAudioVolume;
        curAudioVolume = Sli_Slider.value;
    }

    private float curAudioVolume = -1.0f;

    void OnClose()
    {
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
        gameObject.SetActive(false);
        if (curAudioVolume != -1.0f)
            StorageMgr.SetFloat(GameData.AudioVolumData, curAudioVolume);
    }

    void SliderChanged(float v)
    {
        curAudioVolume = Mathf.Clamp01(v);
        audioSource.volume = Mathf.Clamp01(v);
    }
}