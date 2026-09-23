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

using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HelpView : MonoBehaviour
{
    //autoStart
    private Button Btn_Mask = null;
    private Transform Tran_Root = null;
    private Button Btn_CloseHelp = null;

    private void Awake()
    {
        Btn_Mask = transform.Find("Btn_Mask").GetComponent<Button>();
        Tran_Root = transform.Find("Tran_Root");
        Btn_CloseHelp = transform.Find("Tran_Root/Btn_CloseHelp").GetComponent<Button>();
    }

    //autoEnd
    [SerializeField] private GameObject particleSystem;

    //TODO
    private void Start()
    {
        Btn_Mask.onClick.AddListener(OnClickClose);
        Btn_CloseHelp.onClick.AddListener(OnClickClose);
    }

    void OnClickClose()
    {
        particleSystem.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

 

    //
    private void OnEnable()
    {
        CommonMonoUtils.Instance.SetPopEffect(Tran_Root);
        particleSystem.gameObject.SetActive(false);
    }
}