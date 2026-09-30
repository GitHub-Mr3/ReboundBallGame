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
using System.Collections.Generic;
using Mr3;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.UI;

public class PetHouseView : MonoBehaviour
{
    //autoStart
    private Button Btn_PetHouseMask = null;
    private Transform Tran_PetHouseViewRoot = null;
    private Image Img_Icon = null;
    private TextMeshProUGUI TxtM_Name = null;
    private Transform Tran_Content = null;
    private TextMeshProUGUI TxtM_UpgradeCost = null;
    private Button Btn_Upgrade = null;
    private Button Btn_GoBattle = null;
    private Button Btn_Back = null;
    private Image Img_Stamina = null;
    private Button Btn_AddStamina = null;
    private TextMeshProUGUI TxtM_Desc = null;
    private TextMeshProUGUI TxtM_MoneyNum = null;
    private TextMeshProUGUI TxtM_ShuiPianNum = null;
    private TextMeshProUGUI TxtM_SkillName = null;
    private TextMeshProUGUI TxtM_PetLv = null;

    private void Awake()
    {
        Btn_PetHouseMask = transform.Find("Btn_PetHouseMask").GetComponent<Button>();
        Tran_PetHouseViewRoot = transform.Find("Tran_PetHouseViewRoot");
        Img_Icon = transform.Find("Tran_PetHouseViewRoot/Img_Icon").GetComponent<Image>();
        TxtM_Name = transform.Find("Tran_PetHouseViewRoot/NameBG/TxtM_Name").GetComponent<TextMeshProUGUI>();
        Tran_Content = transform.Find("Tran_PetHouseViewRoot/Scroll View/Viewport/Tran_Content");
        TxtM_UpgradeCost = transform.Find("Tran_PetHouseViewRoot/TxtM_UpgradeCost").GetComponent<TextMeshProUGUI>();
        Btn_Upgrade = transform.Find("Tran_PetHouseViewRoot/Btn_Upgrade").GetComponent<Button>();
        Btn_GoBattle = transform.Find("Tran_PetHouseViewRoot/Btn_GoBattle").GetComponent<Button>();
        Btn_Back = transform.Find("Tran_PetHouseViewRoot/Btn_Back").GetComponent<Button>();
        Img_Stamina = transform.Find("Tran_PetHouseViewRoot/Image/Img_Stamina").GetComponent<Image>();
        Btn_AddStamina = transform.Find("Tran_PetHouseViewRoot/Btn_AddStamina").GetComponent<Button>();
        TxtM_Desc = transform.Find("Tran_PetHouseViewRoot/Scroll View/Viewport/TxtM_Desc").GetComponent<TextMeshProUGUI>();
        TxtM_MoneyNum = transform.Find("Tran_PetHouseViewRoot/Image/TxtM_MoneyNum").GetComponent<TextMeshProUGUI>();
        TxtM_ShuiPianNum = transform.Find("Tran_PetHouseViewRoot/suipian/TxtM_ShuiPianNum").GetComponent<TextMeshProUGUI>();
        TxtM_SkillName = transform.Find("Tran_PetHouseViewRoot/TxtM_SkillName").GetComponent<TextMeshProUGUI>();
        TxtM_PetLv = transform.Find("Tran_PetHouseViewRoot/TxtM_PetLv").GetComponent<TextMeshProUGUI>();

    }
    //autoEnd
    //TODO

    private PetAnimator _petAnimator;
    private GameObject petHouseItem;

    /// <summary>当前选中的宠物 id（点列表项或打开面板自动选中时更新），「出战」「升级」按钮都按它来</summary>
    private int curPetId = 0;

    /// <summary>升级按钮上的文字（Btn_Upgrade 下的 Text (TMP)，显示本次升级的消耗 / 满级 / 未解锁）</summary>



    private void Start()
    {
        if (_petAnimator == null)
            _petAnimator = Img_Icon.GetComponent<PetAnimator>();


        AddBtnLister();
        AddEvent();
        RefreshMoneyText();
        RefreshShuiPianText();
        Img_Icon.enabled = false;
        CreateHandBookItem(); // 列表里会顺带刷新「出战中」标记
        SelectDefaultPet(); // 默认展示出战宠物（或第一只已解锁的）的技能
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener(GameData.EventID_CoinChanged, OnCoinChanged);
        EventMgr.Instance.AddListener(GameData.Event_SelectPet, RefreshSelectInfo);
        // 碎片 / 等级变化都会影响「升级」按钮能不能点
        EventMgr.Instance.AddListener(GameData.EventID_PetFragmentChanged, OnPetFragmentChanged);
        EventMgr.Instance.AddListener(GameData.EventID_PetLevelChanged, OnPetLevelChanged);
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener(GameData.EventID_CoinChanged, OnCoinChanged);
        EventMgr.Instance.RemoveListener(GameData.Event_SelectPet, RefreshSelectInfo);
        EventMgr.Instance.RemoveListener(GameData.EventID_PetFragmentChanged, OnPetFragmentChanged);
        EventMgr.Instance.RemoveListener(GameData.EventID_PetLevelChanged, OnPetLevelChanged);
    }

    void OnCoinChanged(string eventStr, object data)
    {
        RefreshMoneyText();
        RefreshUpgradeBtn(); // 金币变了，可能刚好够 / 不够升级
    }

    void OnPetFragmentChanged(string eventStr, object data)
    {
        RefreshUpgradeBtn();
        int sumNum = (int)data;
        TxtM_ShuiPianNum.text = sumNum.ToString();
    }


    void OnPetLevelChanged(string eventStr, object data)
    {
        if (curPetId > 0) RefreshSkillInfo(curPetId);
        RefreshUpgradeBtn();
    }

    void RefreshShuiPianText()
    {
        TxtM_ShuiPianNum.text = PetFragmentMgr.Get().ToString();
    }

    void RefreshMoneyText()
    {
        if (TxtM_MoneyNum == null) return;

        TxtM_MoneyNum.text = $"{CurrencyMgr.Get()}";
    }

    void AddBtnLister()
    {
        Btn_Back.onClick.AddListener(OnClickClose);
        Btn_PetHouseMask.onClick.AddListener(OnClickClose);
        Btn_AddStamina.onClick.AddListener(OnClickStamina);
        Btn_GoBattle.onClick.AddListener(OnGoBattle);
        Btn_Upgrade.onClick.AddListener(OnClickUpgrade);
    }

    /// <summary>
    /// 点击「升级」：给当前选中的宠物升一级，消耗宠物碎片 + 金币（数量按宠物品质的等级曲线）。
    /// 没解锁 / 已满级 / 材料不够时不会扣任何东西，直接把原因弹给玩家。
    /// </summary>
    void OnClickUpgrade()
    {
        if (curPetId <= 0)
        {
            CommonUtil.Instance.ShowTips("先在左边选一只小狗，再点升级");
            return;
        }

        bool success = PetLevelMgr.TryUpgradePet(curPetId, out string tip);
        CommonUtil.Instance.ShowTips(tip); // 成功："旺财 升到 Lv.2！"；失败："碎片不够，升级需要 30 个"

        if (!success) return;

        RefreshSkillInfo(curPetId); // 技能名上的 Lv.x/y 跟着变，强度描述也刷新
        RefreshUpgradeBtn();

        DebugMgr.Instance.Log($"[PetHouseView] 宠物 {curPetId} 升级完成，当前 Lv.{PetLevelMgr.GetLevel(curPetId)}");

        if (_petAnimator != null) _petAnimator.PlaySelect(); // 升级成功的反馈动画
    }

    /// <summary>
    /// 刷新升级按钮：未解锁 / 已满级 / 材料不够时置灰，按钮文字直接写本次升级要多少碎片和金币。
    /// </summary>
    void RefreshUpgradeBtn()
    {
        if (Btn_Upgrade == null) return;

        bool owned = curPetId > 0 && GameData.GetHavePetID().Contains(curPetId);
        bool maxed = owned && PetLevelMgr.IsMaxLevel(curPetId);

        PetLevelMgr.PetUpgradeCost cost = PetLevelMgr.GetUpgradeCost(curPetId);
        bool affordable = owned && !maxed &&
                          PetFragmentMgr.HasEnough(cost.fragment) && CurrencyMgr.CanAfford(cost.coin);

        Btn_Upgrade.interactable = affordable;

        if (TxtM_UpgradeCost == null) return;

        if (!owned) TxtM_UpgradeCost.text = "未解锁";
        else if (maxed) TxtM_UpgradeCost.text = "已满级";
        else TxtM_UpgradeCost.text = $"升级 {cost.fragment} 碎片 {cost.coin} 金币 每提升一级，增加10%的通关收益";
    }

    /// <summary>点击「出战」：把当前选中的宠物设为出战宠物，它的技能立刻生效并存档</summary>
    void OnGoBattle()
    {
        if (curPetId <= 0)
        {
            ShowBattleTip("先在左边选一只小狗，再点出战");
            return;
        }

        if (!GameData.GetHavePetID().Contains(curPetId))
        {
            ShowBattleTip("这只小狗还没解锁，先去抽卡或通关解锁它");
            return;
        }

        if (PetSkillMgr.BattlePetId == curPetId)
        {
            ShowBattleTip($"{GameData.GetPetData(curPetId).name} 已经在阵了");
            return;
        }

        // 上阵：写入存档 + 让它的技能从这一刻起生效（下一关布置时套用到关卡参数上）
        PetSkillMgr.SetBattlePet(curPetId);

        GameData.PetSkillData skill = GameData.GetPetSkillData(GameData.GetPetData(curPetId).SkillID);
        ShowBattleTip($"{GameData.GetPetData(curPetId).name} 已出战！技能【{skill.SkillName}】{skill.SkillDesc}");
        DebugMgr.Instance.Log($"[PetHouseView] 出战宠物：{curPetId}，技能 {skill.SkillID} {skill.SkillName}");

        RefreshBattleMark();
        RefreshSkillInfo(curPetId);
    }

    /// <summary>刷新列表里的「出战中」标记（出战宠物换了之后同步高亮）</summary>
    void RefreshBattleMark()
    {
        for (int i = 0; i < petHomeItems.Count; i++)
        {
            petHomeItems[i].RefreshBattleMark();
        }
    }

    /// <summary>没有专门提示框时，把提示打到技能描述那一栏（再点一次列表项就会恢复成技能描述）</summary>
    void ShowBattleTip(string tip)
    {
        DebugMgr.Instance.Log($"[PetHouseView] {tip}");
        if (TxtM_Desc != null) TxtM_Desc.text = tip;
    }

    void OnClickStamina()
    {
        _petAnimator.PlayFeed();
    }

    void OnClickClose()
    {
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RemoveEvent();
    }

    private List<PetHouseItem> petHomeItems = new();

    private void CreateHandBookItem()
    {
        var allPetID = GameData.GetSortedPetID(); // 已拥有的排前面 → 品质高的排前面 → 同品质按宠物 id 排
        for (int i = 0; i < allPetID.Count; i++)
        {
            if (petHomeItems.Count < i + 1)
            {
                var obj = GameObject.Instantiate(petHouseItem, Tran_Content);
                var petHouseItemScript = obj.GetComponent<PetHouseItem>();
                petHomeItems.Add(petHouseItemScript);
            }

            petHomeItems[i].Refresh(allPetID[i]);
        }
    }

  

    void RefreshSelectInfo(string str, object obj)
    {
        int id = (int)obj;
        curPetId = id; // 记下当前选中的宠物，「出战」「升级」按钮都用它
        RefreshSkillInfo(id);
        RefreshUpgradeBtn(); // 换宠物后按钮上写的消耗 / 能不能点要跟着变

        Img_Icon.sprite = SpriteLibrary.Instance.GetPetTextureSprite(GameData.GetPetData(id).spriteIndex);
        Img_Icon.enabled = true;

        Img_Icon.SetNativeSize();

        _petAnimator.PlaySelect();
    }

    /// <summary>刷新右侧详情：名字 + 技能名（带等级）+ 技能描述</summary>
    void RefreshSkillInfo(int id)
    {
        var petData = GameData.GetPetData(id);
        TxtM_Name.text = petData.name;

        var skillData = GameData.GetPetSkillData(petData.SkillID);
        // 技能强度随宠物等级成长，把当前等级 / 满级一起显示，方便对上「1 级值 → 满级值」的描述
        TxtM_SkillName.text = "技能名称:" + skillData.SkillName;
        TxtM_Desc.text = skillData.SkillDesc;
        TxtM_PetLv.text ="LV."+ PetLevelMgr.GetLevel(id) + "/" + PetLevelMgr.GetMaxLevel(id);
    }

    /// <summary>
    /// 打开面板时默认选中一只：优先出战宠物，其次第一只已解锁的，什么都没有就展示第 1 只（未解锁，仅看技能）。
    /// </summary>
    void SelectDefaultPet()
    {
        List<int> owned = GameData.GetHavePetID();
        int target = curPetId;
        if (target <= 0 || !owned.Contains(target))
        {
            target = PetSkillMgr.BattlePetId;
            if (target <= 0 || !owned.Contains(target))
            {
                target = owned.Count > 0 ? owned[0] : GameData.GetAllPetID()[0];
            }
        }

        RefreshSelectInfo(GameData.Event_SelectPet, target);
    }

    //
    private void OnEnable()
    {
        if (petHouseItem == null)
        {
            petHouseItem = ResLoader.Load(GameData.PetHouseItem);
        }


        if (_petAnimator == null)
        {
            if (Img_Icon == null)
                Img_Icon = transform.Find("Tran_PetHouseViewRoot/Img_Icon").GetComponent<Image>();
            _petAnimator = Img_Icon.GetComponent<PetAnimator>();
        }


        RefreshUpgradeBtn(); // 每次打开面板按最新的碎片 / 金币刷新按钮

        CommonMonoUtils.Instance.SetPopEffect(Tran_PetHouseViewRoot);
    }
}