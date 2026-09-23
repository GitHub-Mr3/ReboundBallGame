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
    private Button Btn_Click = null;
    private TextMeshProUGUI TxtM_Tips = null;

    private void Awake()
    {
        Img_bg = transform.Find("Img_bg").GetComponent<Image>();
        Img_Icon = transform.Find("Img_Icon").GetComponent<Image>();
        Tran_Lock = transform.Find("Tran_Lock");
        Btn_Click = transform.Find("Btn_Click").GetComponent<Button>();
        TxtM_Tips = transform.Find("Btn_Click/TxtM_Tips").GetComponent<TextMeshProUGUI>();

    }
    //autoEnd
    //TODO
    [SerializeField] private Sprite[] icon;
    /// <summary>历史最大通关关卡 id：id <= 它的图鉴条目视为已解锁</summary>
    private int maxLevelId = -1;

    private void Start()
    {
        maxLevelId = PlayerPrefs.GetInt(GameData.MaxLevelIdKey, 0);
        Btn_Click.onClick.AddListener(OnClickHandItem);
    }

    private int curHandBookID;
    private bool Unlock = false;

    void OnClickHandItem()
    {
        GameData.HandBookInfoData data = new GameData.HandBookInfoData()
        {
            icon = curSprite,
            name = curName,
            desc = curDesc
        };
        EventMgr.Instance.Emit(GameData.EventID_HandBookInfo, data);
    }

    private Sprite curSprite;
    private string curName;
    private string curDesc;

    public void Refresh(int id)
    {
        curHandBookID = id;
        // 每次刷新都重读：玩家可能刚通关新关卡，解锁状态要跟着最新的通关 id 变
        maxLevelId = PlayerPrefs.GetInt(GameData.MaxLevelIdKey, 0);

        int index = id - 1;

        curSprite = icon[index];
        curName = _handBookDatas[index].name;


        Unlock = id <= maxLevelId;
        curDesc = Unlock ? _handBookDatas[index].desc : $"领养{curName}解锁";

        Img_Icon.sprite = curSprite;
        TxtM_Tips.text = $"{curName}";
        Tran_Lock.gameObject.SetActive(!Unlock);
    }

    private struct HandBookData
    {
        public string name;
        public string desc;
    }

    private List<HandBookData> _handBookDatas = new List<HandBookData>()
    {
        new HandBookData() { name = $"{GameData.GetLevelName(0)}", desc = "山野里跑出来的元气柴犬，红红的领巾配上金色吊牌，笑容永远热烈灿烂。把这团跳动的小火焰收入图鉴，感受源源不断的开心能量！" },
        new HandBookData() { name = $"{GameData.GetLevelName(1)}", desc = "像天上落下来的蓬松小雪团，一身雪白绒毛软乎乎。蓝色领结带着清脆铃铛，眨着圆溜溜大眼睛等待被发现。解锁雪球，收获一份纯净的甜蜜治愈" },
        new HandBookData() { name = $"{GameData.GetLevelName(2)}", desc = "温柔的阳光大男孩，金色被毛如同午后暖阳。性格宽厚又热忱，无论何时都带着开朗笑容。解锁暖阳，让这份温暖的陪伴，照亮你的收集冒险。" },
        new HandBookData() { name = $"{GameData.GetLevelName(3)}", desc = "诞生于静谧星夜之下的哈士奇，深邃冰蓝色眼眸藏着整片星空。深蓝色披巾随风轻扬，又酷又灵动。解锁星夜，捕获这只自带神秘感的星空小狗。" },
        new HandBookData() { name = $"{GameData.GetLevelName(4)}", desc = "林间短腿小精灵，翠绿领巾点缀小花，蹦蹦跳跳穿梭花草之间。短短的四肢藏着无限活力。解锁青芜，将裹挟草木花香的可爱，珍藏进图鉴" },
        new HandBookData() { name = $"{GameData.GetLevelName(5)}", desc = "粉系浪漫小公主，卷曲的棕毛柔软蓬松，头上缀着粉嫩桃花。红缎蝴蝶结精致雅致，少女感满满。解锁桃桃，邂逅这只温柔甜美的花间小狗" },
        new HandBookData() { name = $"{GameData.GetLevelName(6)}", desc = "热爱探险的小小冒险家，活力永远满格。亮黄色领巾随风飘荡，好奇心永不停歇，哪里有趣就往哪里跑。解锁小黄巾，和它一起开启全新的探索旅程。" },
        new HandBookData() { name = $"{GameData.GetLevelName(7)}", desc = "沉稳可靠的护卫伙伴，如同赤红岩石一般坚毅。红披风搭配华丽配饰，外表帅气气场十足，内心忠诚温柔。解锁赤岩，收获一位值得信赖的毛茸茸搭档。" },
        new HandBookData() { name = $"{GameData.GetLevelName(8)}", desc = "优雅的白毛小仙女，长长的丝滑白毛轻盈飘逸。紫色蝴蝶结和小花配饰典雅动人，举止温柔。解锁紫缨，把这只仙气满满的小狗收入你的收藏。" },
        new HandBookData() { name = $"{GameData.GetLevelName(9)}", desc = "自带小胡子的俏皮 “小老头”，灰白斑驳的毛发很有辨识度。藏青色围巾精致大方，冷静外表下藏着调皮灵魂。解锁灰砾，认识这只风趣独特的小狗。" },
        new HandBookData() { name = $"{GameData.GetLevelName(10)}", desc = "聪明绝顶的林间机灵鬼，斑驳的被毛色彩灵动，眼神充满灵气。绿系飘带随风摇曳，学习能力超强。解锁彩斑，收下这只聪慧又活泼的小伙伴。" },
        new HandBookData() { name = $"{GameData.GetLevelName(11)}", desc = "圆滚滚的厚实毛团子，肉乎乎的脸庞，一副慵懒惬意的模样。红绳挂着圆金牌，性格憨厚温顺。解锁圆墩，抱走这团沉甸甸、软乎乎的毛茸茸。" },
    };

    //
    private void OnEnable()
    {
    }
}