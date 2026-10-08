using Mr3;
using System;
using UnityEngine;
using UnityEngine.UI;

public class TestRedPoint : MonoBehaviour
{
    // Start is called before the first frame update


    public Text txt_main;
    public Text txt_Email;
    public Text txt_bag;

    public Text txt_SystemEmail;
    public Text txt_FriendEmail;
    public Text txt_TaskEmail;
    public Text txt_PropBag;
    public Text txt_EquipBag;


    public Button btn_SystemEmail;
    public Button btn_FriendEmail;
    public Button btn_TaskEmail;
    public Button btn_PropBag;
    public Button btn_EquipBag;


    public Button btnadd_SystemEmail;
    public Button btnadd_FriendEmail;
    public Button btnadd_TaskEmail;
    public Button btnadd_PropBag;
    public Button btnadd_EquipBag;




    void Start()
    {
       // EventMgr.Instance.Init();
        
       // EventMgr.Instance.AddListener(Constant.REDNPOINT_CHANGE, OnRedPointChange);
        InitData();


        AddBtnLister();
    }
    void InitData()
    {
        // RedPointManager.Instance.ChangeRedPointNum(NodeNames.SystemEmail, 1);
        // RedPointManager.Instance.ChangeRedPointNum(NodeNames.FriendEmail, 1);
        // RedPointManager.Instance.ChangeRedPointNum(NodeNames.TaskEmail, 1);
        // RedPointManager.Instance.ChangeRedPointNum(NodeNames.PropBag, 1);
        // RedPointManager.Instance.ChangeRedPointNum(NodeNames.EquipBag, 1);
    }
    void AddBtnLister()
    {
        // btn_SystemEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.SystemEmail, -1);
        // });
        // btn_FriendEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.FriendEmail, -1);
        // });
        // btn_TaskEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.TaskEmail, -1);
        // });
        // btn_PropBag.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.PropBag, -1);
        // });
        // btn_EquipBag.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.EquipBag, -1);
        // });
        //
        //
        //
        // btnadd_SystemEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.SystemEmail, 1);
        // });
        // btnadd_FriendEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.FriendEmail, 1);
        // });
        // btnadd_TaskEmail.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.TaskEmail, 1);
        // });
        // btnadd_PropBag.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.PropBag, 1);
        // });
        // btnadd_EquipBag.onClick.AddListener(() =>
        // {
        //     RedPointManager.Instance.ChangeRedPointNum(NodeNames.EquipBag, 1);
        // });

    }

    private void OnRedPointChange(string eventName, object udata)
    {
        var abc = (RedChangeData)udata;
        Text txt = null;
        switch (RedPointManager.Instance.GetNodePath(abc.name))
        {
            // case NodeNames.Main:
            //     txt = txt_main;
            //     break;
            // case NodeNames.Email:
            //     txt = txt_Email;
            //     break;
            // case NodeNames.FriendEmail:
            //     txt = txt_FriendEmail;
            //     break;
            // case NodeNames.SystemEmail:
            //     txt = txt_SystemEmail;
            //     break;
            // case NodeNames.TaskEmail:
            //     txt = txt_TaskEmail;
            //     break;
            // case NodeNames.Bag:
            //     txt = txt_bag;
            //     break;
            // case NodeNames.EquipBag:
            //     txt = txt_EquipBag;
            //     break;
            // case NodeNames.PropBag:
            //     txt = txt_PropBag;
            //     break;

            default:
                break;
        }
        SetTxt(txt, abc.redCount);
    }
    void SetTxt(Text txt, int num)
    {
        txt.text = num.ToString();
    }
    private void OnDestroy()
    {
     //   EventMgr.Instance.RemoveListener(Constant.REDNPOINT_CHANGE, OnRedPointChange);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
