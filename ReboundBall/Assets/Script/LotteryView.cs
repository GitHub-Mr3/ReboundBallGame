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
using UnityEngine.UI;
using System.Resources;

/// <summary>
/// 扭蛋抽奖面板：单抽 / 十连 / 每日免费单抽。
///
/// 抽奖规则：
///   1. 奖池 = GameData.GetAllPetID()（_allPetID，1~13 号宠物）；
///   2. 先按品质权重抽品质：品质1=60%、品质2=25%、品质3=10%、品质4=4%、品质5=1%，
///      同品质内的宠物等概率；
///   3. 品质 5（SSS）双重限制，尽量不让它出现：
///      - 解禁门槛：累计抽满 Rare5UnlockDraws（默认 100 抽）之前，品质 5 直接不进奖池，
///        它的 1% 按比例分摊回其余品质（仍是 60:25:10:4 的相对关系）；
///      - 冷却：抽中一次品质 5 后，要再抽满 Rare5CooldownDraws（默认 200 抽）才会重新进池。
///      也就是说实测概率远低于 1%，且永远不会连续出。
///   4. 十连保底：十连里连一个 S 级（品质 >= 3）都没有时，最后一抽强制补一只品质 3；
///   5. 抽到已有宠物不重复入袋，按品质折算成共用碎片（DuplicateFragment，走 PetFragmentMgr），
///      十连里重复多只会自动累加，后续可用于升星 / 兑换。
/// </summary>
public class LotteryView : MonoBehaviour
{
    //autoStart
    private Transform Tran_Root = null;
	private Button Btn_Box = null;
	private Button Btn_Free = null;
	private GameObject Obj_FreeRed = null;
	private Button Btn_One = null;
	private Button Btn_multiple = null;
	private Button Btn_Back = null;
	private TextMeshProUGUI TxtM_MoneyTxt = null;
	private TextMeshProUGUI TxtM_ShuipianTxt = null;
	
    private void Awake()
	{
		Tran_Root = transform.Find("Tran_Root");
		Btn_Box = transform.Find("Tran_Root/box/Btn_Box").GetComponent<Button>();
		Btn_Free = transform.Find("Tran_Root/Btn_Free").GetComponent<Button>();
		Obj_FreeRed = transform.Find("Tran_Root/Btn_Free/Obj_FreeRed").gameObject;
		Btn_One = transform.Find("Tran_Root/Btn_One").GetComponent<Button>();
		Btn_multiple = transform.Find("Tran_Root/Btn_multiple").GetComponent<Button>();
		Btn_Back = transform.Find("Tran_Root/Btn_Back").GetComponent<Button>();
		TxtM_MoneyTxt = transform.Find("Tran_Root/moneyBg/TxtM_MoneyTxt").GetComponent<TextMeshProUGUI>();
		TxtM_ShuipianTxt = transform.Find("Tran_Root/shuipianBg /TxtM_ShuipianTxt").GetComponent<TextMeshProUGUI>();
		
	}
    //autoEnd
    //TODO
    private void Start()
    {
        AddBtn();
        AddEvent();
        SyncOwnedPets();
        RefreshFreeBtn();
    }

    private void OnDestroy()
    {
        RemoveEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener(GameData.EventID_CoinChanged, OnCoinChanged);
        EventMgr.Instance.AddListener(GameData.EventID_PetFragmentChanged, OnPetFragmentChanged);
      
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener(GameData.EventID_CoinChanged, OnCoinChanged);
        EventMgr.Instance.RemoveListener(GameData.EventID_PetFragmentChanged, OnPetFragmentChanged);
        
    }

    void OnPetFragmentChanged(string str,object obj)
    {
        int sunNum=(int)obj;
        TxtM_ShuipianTxt.text = sunNum.ToString();
    }

    void OnCoinChanged(string eventStr, object data)
    {
        RefreshMoneyText();
    }

    void RefreshMoneyText()
    {
        if (TxtM_MoneyTxt == null) return;

        TxtM_MoneyTxt.text = $"{CurrencyMgr.Get()}";
    }

    void RefefreshShuiPian()
    {
        TxtM_ShuipianTxt.text = PetFragmentMgr.Get().ToString();
    }

    void AddBtn()
    {
        Btn_Box.onClick.AddListener(OnClickBox);
        Btn_Back.onClick.AddListener(OnClickBack);
        Btn_One.onClick.AddListener(OnClickOne);
        Btn_Free.onClick.AddListener(OnClickFree);
        Btn_multiple.onClick.AddListener(OnClickMultiple);
    }

    void OnClickBack()
    {
        gameObject.SetActive(false);
        EventMgr.Instance.Emit(GameData.EventID_ShowParticleObj, true);
    }

    void OnClickBox()
    {
        ResLoader.CreateUIOnce(GameData.LotteryTipsView, transform);
    }

    void OnClickFree()
    {
        if (!IsFreeAvailable())
        {
            DebugMgr.Instance.LogError("[LotteryView] 今日免费单抽已使用");
            return;
        }

        MarkFreeUsed();
        DoDraw(1);
        RefreshFreeBtn();
        RedPointManager.Instance.SetRedPointCount(NodeNames.FreeShop,0);
    }

    void OnClickOne()
    {
        if (!TrySpend(OneCost, "单抽")) return;

        DoDraw(1);
    }

    void OnClickMultiple()
    {
        if (!TrySpend(TenCost, "十连")) return;

        DoDraw(TenDrawCount);
    }

    //
    private void OnEnable()
    {
        RefreshMoneyText();
        RefefreshShuiPian();
        RefreshFreeBtn();
    }

    // ==================== 抽奖实现 ====================

    #region 抽奖配置

    /// <summary>单抽消耗狗粮</summary>
    public const int OneCost = 200;

    /// <summary>十连消耗狗粮（相当于打 9 折）</summary>
    public const int TenCost = 1800;

    /// <summary>十连的抽取次数</summary>
    private const int TenDrawCount = 10;

    /// <summary>品质权重（百分比，下标即品质）：品质1=60%、品质2=25%、品质3=10%、品质4=4%、品质5=1%</summary>
    private static readonly int[] QualityWeights = { 0, 60, 25, 10, 4, 1 };

    /// <summary>品质 5 解禁门槛：累计抽到这么多次之前，品质 5 完全不进奖池</summary>
    public const int Rare5UnlockDraws = 100;

    /// <summary>品质 5 冷却：抽中一次后要再抽这么多次才会重新进池</summary>
    public const int Rare5CooldownDraws = 200;

    /// <summary>十连保底的品质下限：十连里连 S 级都没有时，最后一抽补这个品质</summary>
    private const int TenGuaranteeQuality = 3;

    /// <summary>抽到已有宠物时，按品质折算成「该宠物自己的碎片」的数量（下标即品质）</summary>
 

    #endregion

    /// <summary>累计抽奖次数（本地 + 托管数据双写，删游戏后进度还在）</summary>
    private int DrawCount
    {
        get { return StorageMgr.GetInt(GameData.LotteryDrawCountKey, 0); }
        set { StorageMgr.SetInt(GameData.LotteryDrawCountKey, value); }
    }

    /// <summary>执行 count 连抽：先摇结果，再累加抽数、发奖、展示</summary>
    void DoDraw(int count)
    {
        List<int> results = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            results.Add(RollPet());
        }

        // 十连保底：连一个 S 级都没有，最后一抽强制补一只品质 3
        if (count >= TenDrawCount && GetMaxQuality(results) < TenGuaranteeQuality)
        {
            results[results.Count - 1] = RollPetOfQuality(TenGuaranteeQuality);
        }

        int total = DrawCount + count;
        DrawCount = total;

        ApplyResults(results);
    }

    /// <summary>摇一只宠物：先按权重定品质，再在该品质里等概率取一只</summary>
    int RollPet()
    {
        return RollPetOfQuality(RollQuality());
    }

    /// <summary>按权重摇品质；被封印的品质（品质 5 未解禁/冷却中）权重为 0，其余保持 60:25:10:4 的相对关系</summary>
    int RollQuality()
    {
        int total = 0;
        for (int q = 1; q < QualityWeights.Length; q++)
        {
            if (IsQualityOpen(q)) total += QualityWeights[q];
        }

        if (total <= 0) return 1;

        int roll = UnityEngine.Random.Range(0, total);
        int sum = 0;
        for (int q = 1; q < QualityWeights.Length; q++)
        {
            if (!IsQualityOpen(q)) continue;

            sum += QualityWeights[q];
            if (roll < sum) return q;
        }

        return 1;
    }

    /// <summary>品质 5（SSS）的双重限制：门槛 + 冷却，目的就是让它几乎抽不到</summary>
    bool IsQualityOpen(int quality)
    {
        if (quality < 5) return true;

        int draws = DrawCount;
        if (draws < Rare5UnlockDraws) return false;

        int lastRare5 = StorageMgr.GetInt(GameData.LotteryLastRare5Key, 0);
        return draws - lastRare5 >= Rare5CooldownDraws;
    }

    /// <summary>指定品质里等概率取一只；该品质池为空时逐级向下兜底，最后兜到品质 1</summary>
    int RollPetOfQuality(int quality)
    {
        for (int q = quality; q >= 1; q--)
        {
            List<int> pool = GetPoolByQuality(q);
            if (pool.Count > 0) return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        List<int> ids = GameData.GetAllPetID();
        return ids.Count > 0 ? ids[0] : 1;
    }

    /// <summary>按品质缓存的宠物 id 池（数据量就 13 条，建一次一直复用）</summary>
    private static readonly Dictionary<int, List<int>> qualityPools = new Dictionary<int, List<int>>();

    List<int> GetPoolByQuality(int quality)
    {
        if (!qualityPools.TryGetValue(quality, out List<int> pool))
        {
            pool = new List<int>();
            List<int> all = GameData.GetAllPetID();
            for (int i = 0; i < all.Count; i++)
            {
                if (GameData.GetPetData(all[i]).quality == quality) pool.Add(all[i]);
            }

            qualityPools[quality] = pool;
        }

        return pool;
    }

    /// <summary>结果列表里的最高品质</summary>
    int GetMaxQuality(List<int> results)
    {
        int max = 0;
        for (int i = 0; i < results.Count; i++)
        {
            max = Mathf.Max(max, GameData.GetPetData(results[i]).quality);
        }

        return max;
    }

    /// <summary>
    /// 发奖：results 里只保留「新获得」的宠物，已拥有的会被移除并折算成共用碎片
    /// （PetFragmentMgr，十连里多只重复会累加）；
    /// 抽中品质 5 时记录本次抽数作为冷却起点；最后展示结果并广播事件（携带的也是过滤后的新宠列表）。
    /// </summary>
    void ApplyResults(List<int> results)
    {
        List<int> owned = GameData.GetHavePetID();
        int fragment = 0;    // 本次折算的共用碎片总数
        int duplicate = 0;   // 本次重复（已拥有）的只数
        bool hitRare5 = false;

        // 倒序遍历：重复的当场从 results 里移除，结果列表最后只剩新获得的宠物
        for (int i = results.Count - 1; i >= 0; i--)
        {
            int id = results[i];
            GameData.PetData pet = GameData.GetPetData(id);

            if (owned.Contains(id))
            {
                fragment += GetDuplicateFragment(pet.quality);
                duplicate++;
                results.RemoveAt(i);
                continue;
            }

            GameData.AddPetID(id);
            if (pet.quality >= 5) hitRare5 = true;
        }

        SaveOwnedPets();
        if (fragment > 0) PetFragmentMgr.Add(fragment, "抽奖重复折算");
        if (hitRare5) StorageMgr.SetInt(GameData.LotteryLastRare5Key, DrawCount);

        DebugMgr.Instance.Log($"[LotteryView] 抽 {results.Count + duplicate} 次，新获得：{DescribeResults(results)}" +
                              (fragment > 0 ? $"，重复 {duplicate} 只折算碎片 +{fragment}（共 {PetFragmentMgr.Get()}）" : ""));

        ShowResults(results,fragment);
        EventMgr.Instance.Emit(GameData.EventID_LotteryResult, results);
    }

    /// <summary>结果列表的日志文案：全重复（列表为空）时给出明确说明</summary>
    string DescribeResults(List<int> results)
    {
        return results.Count > 0 ? string.Join(",", results) : "无（本次全是已拥有宠物）";
    }

    /// <summary>重复一只宠物能折多少共用碎片（品质越高给越多）</summary>
    int GetDuplicateFragment(int quality)
    {
        var DuplicateFragment = GameData.PetDuplicateFragment;
        return quality >= 0 && quality < DuplicateFragment.Length ? DuplicateFragment[quality] : 0;
    }

    /// <summary>扣狗粮，余额不足时只提示不扣费</summary>
    bool TrySpend(int cost, string reason)
    {
        if (CurrencyMgr.CanAfford(cost))
        {
            CurrencyMgr.Spend(cost, reason);
            return true;
        }

        CommonUtil.Instance.ShowTips($"金币不足：{reason} 需要 {cost}，当前 {CurrencyMgr.Get()}");
        DebugMgr.Instance.LogError($"[LotteryView] 金币不足：{reason} 需要 {cost}，当前 {CurrencyMgr.Get()}");
        return false;
    }

    // ==================== 免费单抽（每天一次） ====================

    bool IsFreeAvailable()
    {
        return StorageMgr.GetString(GameData.LotteryFreeDateKey, "") != DateTime.Now.ToString("yyyyMMdd");
    }

    void MarkFreeUsed()
    {
        StorageMgr.SetString(GameData.LotteryFreeDateKey, DateTime.Now.ToString("yyyyMMdd"));
    }

    void RefreshFreeBtn()
    {
        bool isFreeAvailable = IsFreeAvailable();
        if (Btn_Free != null) Btn_Free.interactable = isFreeAvailable;
        Obj_FreeRed?.SetActive(isFreeAvailable);
    }

    // ==================== 已拥有宠物的存取 ====================

    /// <summary>同步已拥有宠物：统一走 GameData.RefreshUnlockedPets（通关解锁 + 扭蛋解锁一起重建）</summary>
    void SyncOwnedPets()
    {
        GameData.RefreshUnlockedPets();
    }

    void SaveOwnedPets()
    {
        StorageMgr.SetString(GameData.HavePetIdsKey, string.Join(",", GameData.GetHavePetID()));
    }

    /// <summary>把抽到的宠物铺到结果容器上（容器不存在时只走日志与事件，不影响抽奖本身）</summary>
    void ShowResults(List<int> results,int fragment)
    {
        var rewardResults = ResLoader.CreateUIOnce<GetRewardView>(GameData.GetRewardView, transform);
        rewardResults.RefreshRewardItem(results,fragment);
        // for (int i = 0; i < results.Count; i++)
        // {
        //     ShopTipItem item = ResLoader.Create<ShopTipItem>("RewardItem", Tran_Result);
        //     if (item != null) item.Refresh(GameData.GetPetData(results[i]));
        // }
    }
}