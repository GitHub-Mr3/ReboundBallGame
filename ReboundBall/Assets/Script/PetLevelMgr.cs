using System.Collections.Generic;
using System.Text;
using Mr3;
using UnityEngine;

/// <summary>
/// 宠物升级系统：传入宠物 id 就能把它升一级，消耗「宠物碎片 + 金币」，消耗量按<b>宠物品质</b>和当前等级计算。
///
/// 规则：
///   1. 每只宠物独立等级，默认 Lv.1，等级上限按品质（B 级 6 级 → SSS 级 10 级）；
///   2. 升下一级的消耗 = 品质底数 +（当前等级 - 1）× 品质递增，品质越高、等级越高越贵；
///   3. 只有已解锁（GameData.GetHavePetID 里有）的宠物才能升级，未解锁 / 满级 / 材料不足都会明确返回原因；
///   4. 先扣碎片再扣金币，任一步不满足就整体不扣（CheckUpgrade 提前判定，不会扣一半）；
///   5. 等级存在内存字典里并同步写存储（默认 Both：本地 + 微信托管双写，删游戏后还在）。
///
/// 用法（公用入口就是 UpgradePet / TryUpgradePet）：
///     int lv = PetLevelMgr.GetLevel(13);                     // 当前等级（没数据 = 1 级）
///     var cost = PetLevelMgr.GetUpgradeCost(13);             // 升下一级要多少碎片 / 金币
///     var r = PetLevelMgr.UpgradePet(13);                    // 升级：扣费 + 存档 + 广播事件
///     if (PetLevelMgr.TryUpgradePet(13, out string tip))     // UI 版：tip 可直接弹提示（成功/失败都有文案）
///         CommonUtil.Instance.ShowTips(tip);
///     float bonus = PetLevelMgr.GetCoinBonus(13);            // 等级收益：金币加成比例（0.09 = +9%）
///
/// UI 监听（携带升级的宠物 id，int）：
///     EventMgr.Instance.AddListener(GameData.EventID_PetLevelChanged, OnPetLevelChanged);
/// </summary>
public static class PetLevelMgr
{
    private const string LogTag = "[PetLevelMgr]";

    /// <summary>等级存在哪：默认本地 + 托管数据双写，删游戏后还能回来</summary>
    public static StorageTarget StoreTarget = StorageTarget.Both;

    /// <summary>初始等级</summary>
    public const int DefaultLevel = 1;

    // ==================== 配置（按品质 1~5 取值，下标 = 品质） ====================

    /// <summary>等级上限：B 级 6 级，A 级 7 级，S 级 8 级，SS 级 9 级，SSS 级 10 级</summary>
    private static readonly int[] MaxLevelByQuality = { 0, 6, 7, 8, 9, 10 };

    /// <summary>碎片消耗底数（升 1→2 时的量）</summary>
    private static readonly int[] FragmentBase = { 0, 5, 8, 12, 20, 30 };

    /// <summary>碎片消耗每级递增</summary>
    private static readonly int[] FragmentStep = { 0, 5, 6, 8, 12, 18 };

    /// <summary>金币消耗底数（升 1→2 时的量）</summary>
    private static readonly int[] CoinBase = { 0, 100, 180, 300, 500, 800 };

    /// <summary>金币消耗每级递增</summary>
    private static readonly int[] CoinStep = { 0, 80, 120, 180, 260, 400 };

    /// <summary>每级带来的收益：通关金币奖励 +10%（Lv.1 = 0，Lv.5 = +12%）</summary>
    public const float CoinBonusPerLevel = 0.1f;

    // ==================== 数据 ====================

    private static readonly Dictionary<int, int> levels = new Dictionary<int, int>();

    private static bool loaded;

    public static bool IsLoaded => loaded;

    /// <summary>升一级需要的材料</summary>
    public struct PetUpgradeCost
    {
        public int fragment;
        public int coin;

        public override string ToString() => $"{fragment} 碎片 + {coin} 金币";
    }

    /// <summary>升级结果：Success 之外都说明为什么没升成，UI 直接用 GetResultTip 取文案</summary>
    public enum PetUpgradeResult
    {
        Success,
        InvalidPet,         // 没有这只宠物
        NotOwned,           // 还没解锁
        MaxLevel,           // 已经满级
        NotEnoughFragment,  // 碎片不够
        NotEnoughCoin,      // 金币不够
    }

    // ==================== 查询 ====================

    /// <summary>宠物当前等级（没升过级 = 1 级，id 非法 = 0）</summary>
    public static int GetLevel(int petId)
    {
        if (!IsValidPet(petId)) return 0;

        EnsureLoaded();
        return levels.TryGetValue(petId, out int lv) ? lv : DefaultLevel;
    }

    /// <summary>该宠物能升到的最高等级（按品质）</summary>
    public static int GetMaxLevel(int petId)
    {
        int quality = Mathf.Clamp(GetQuality(petId), 1, MaxLevelByQuality.Length - 1);
        return MaxLevelByQuality[quality];
    }

    /// <summary>是否已经满级</summary>
    public static bool IsMaxLevel(int petId) => GetLevel(petId) >= GetMaxLevel(petId);

    /// <summary>
    /// 升下一级的消耗：品质定底数和递增幅度，等级越高越贵。
    /// 例：S 级（品质 3）Lv.1 → Lv.2 要 12 碎片 + 300 金币；Lv.2 → Lv.3 要 20 碎片 + 480 金币。
    /// </summary>
    public static PetUpgradeCost GetUpgradeCost(int petId)
    {
        int quality = Mathf.Clamp(GetQuality(petId), 1, FragmentBase.Length - 1);
        int step = Mathf.Max(0, GetLevel(petId) - DefaultLevel); // 升 1→2 时 step = 0

        return new PetUpgradeCost
        {
            fragment = FragmentBase[quality] + step * FragmentStep[quality],
            coin = CoinBase[quality] + step * CoinStep[quality],
        };
    }

    /// <summary>
    /// 预检：这只宠物现在能不能升一级（不扣任何东西）。
    /// 返回 Success 表示材料齐全，可以放心调 UpgradePet。
    /// </summary>
    public static PetUpgradeResult CheckUpgrade(int petId)
    {
        if (!IsValidPet(petId)) return PetUpgradeResult.InvalidPet;
        if (!GameData.GetHavePetID().Contains(petId)) return PetUpgradeResult.NotOwned;
        if (IsMaxLevel(petId)) return PetUpgradeResult.MaxLevel;

        PetUpgradeCost cost = GetUpgradeCost(petId);
        if (!PetFragmentMgr.HasEnough(cost.fragment)) return PetUpgradeResult.NotEnoughFragment;
        if (!CurrencyMgr.CanAfford(cost.coin)) return PetUpgradeResult.NotEnoughCoin;

        return PetUpgradeResult.Success;
    }

    /// <summary>等级带来的金币收益加成比例（Lv.1 = 0，Lv.5 = 0.12 即 +12%）</summary>
    public static float GetCoinBonus(int petId)
        => Mathf.Max(0, GetLevel(petId) - DefaultLevel) * CoinBonusPerLevel;

    /// <summary>
    /// 出战宠物的等级加成（没上阵宠物时为 0）。
    /// 已叠加进 PetSkillMgr.RewardMultiplier，出战宠物升级后下一关的通关金币立刻变多。
    /// </summary>
    public static float BattlePetCoinBonus
        => PetSkillMgr.BattlePetId > 0 ? GetCoinBonus(PetSkillMgr.BattlePetId) : 0f;

    // ==================== 升级（公用入口） ====================

    /// <summary>
    /// 【公用方法】升级指定宠物：判定 → 扣碎片 → 扣金币 → 等级 +1 → 存档 → 广播事件。
    /// 材料不足、未解锁、满级时不会扣任何东西，直接返回原因。
    /// </summary>
    /// <param name="petId">要升级的宠物 id</param>
    /// <returns>升级结果，Success 之外配合 GetResultTip 直接展示给玩家</returns>
    public static PetUpgradeResult UpgradePet(int petId)
    {
        PetUpgradeResult check = CheckUpgrade(petId);
        if (check != PetUpgradeResult.Success)
        {
            DebugMgr.Instance.Log($"{LogTag} 升级失败：{check}（petId={petId}）");
            return check;
        }

        PetUpgradeCost cost = GetUpgradeCost(petId);
        PetFragmentMgr.Spend(cost.fragment, $"升级宠物 {petId}");
        CurrencyMgr.Spend(cost.coin, $"升级宠物 {petId}");

        int newLevel = GetLevel(petId) + 1;
        SetLevelCore(petId, newLevel);

        // 升的是出战宠物：技能效果立刻按新等级重算（次数类当场补上，关卡参数类下一关生效）
        if (petId == PetSkillMgr.BattlePetId) PetSkillMgr.RefreshBattlePetSkill();

        DebugMgr.Instance.Log($"{LogTag} 宠物 {petId}（{GetPetName(petId)}）升到 Lv.{newLevel}，消耗 {cost}");

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_PetLevelChanged, petId);

        return PetUpgradeResult.Success;
    }

    /// <summary>
    /// 【公用方法 · UI 版】升级并把结果翻译成一句可直接弹给玩家的话。
    /// </summary>
    /// <param name="petId">要升级的宠物 id</param>
    /// <param name="tip">成功："旺财 升到 Lv.2！"；失败："碎片不够，升级需要 30 个"</param>
    /// <returns>是否升级成功</returns>
    public static bool TryUpgradePet(int petId, out string tip)
    {
        PetUpgradeResult result = UpgradePet(petId);
        tip = GetResultTip(result, petId);
        return result == PetUpgradeResult.Success;
    }

    /// <summary>把升级结果翻译成玩家能看懂的提示（成功 / 失败都有文案）</summary>
    public static string GetResultTip(PetUpgradeResult result, int petId = 0)
    {
        switch (result)
        {
            case PetUpgradeResult.Success:
                return $"{GetPetName(petId)} 升到 Lv.{GetLevel(petId)}！";
            case PetUpgradeResult.InvalidPet:
                return "没有这只宠物";
            case PetUpgradeResult.NotOwned:
                return "这只小狗还没解锁，先去扭蛋或通关解锁它";
            case PetUpgradeResult.MaxLevel:
                return $"{GetPetName(petId)} 已经满级（Lv.{GetMaxLevel(petId)}）";
            case PetUpgradeResult.NotEnoughFragment:
                return $"碎片不够，升级需要 {GetUpgradeCost(petId).fragment} 个";
            case PetUpgradeResult.NotEnoughCoin:
                return $"金币不够，升级需要 {GetUpgradeCost(petId).coin} 金币";
            default:
                return "升级失败";
        }
    }

    // ==================== 修改（存档 / 调试） ====================

    /// <summary>直接设置等级（调试 / GM 用）：会夹在 1 ~ 品质上限之间，并存档 + 广播</summary>
    public static void SetLevel(int petId, int level)
    {
        if (!IsValidPet(petId)) return;

        int newLevel = Mathf.Clamp(level, DefaultLevel, GetMaxLevel(petId));
        SetLevelCore(petId, newLevel);

        if (petId == PetSkillMgr.BattlePetId) PetSkillMgr.RefreshBattlePetSkill();

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_PetLevelChanged, petId);
    }

    /// <summary>把某只宠物的等级数据抹掉（回到 Lv.1）</summary>
    public static void ResetPet(int petId)
    {
        if (!IsValidPet(petId)) return;

        EnsureLoaded();
        if (levels.Remove(petId)) SaveCore();
    }

    /// <summary>清空所有宠物等级（重置存档用）</summary>
    public static void ResetAll()
    {
        EnsureLoaded();
        levels.Clear();
        SaveCore();
    }

    /// <summary>外部绕过本类改了存档时，用它把缓存同步回来</summary>
    public static void RefreshFromStorage()
    {
        loaded = false;
        EnsureLoaded();
    }

    // ==================== 内部 ====================

    private static bool IsValidPet(int petId)
    {
        if (petId <= 0) return false;
        return GameData.GetAllPetID().Contains(petId);
    }

    private static int GetQuality(int petId) => petId <= 0 ? 0 : GameData.GetPetData(petId).quality;

    private static string GetPetName(int petId) => IsValidPet(petId) ? GameData.GetPetData(petId).name : "";

    private static void SetLevelCore(int petId, int level)
    {
        EnsureLoaded();
        levels[petId] = level;
        SaveCore();
    }

    /// <summary>懒加载：第一次访问时才读存储</summary>
    private static void EnsureLoaded()
    {
        if (loaded) return;

        levels.Clear();

        // 存档格式："13:4,2:2" —— 宠物id:等级，只存升过级的（默认 1 级不占空间）
        string saved = StorageMgr.GetString(GameData.PetLevelKey, "", StoreTarget);
        if (!string.IsNullOrEmpty(saved))
        {
            string[] parts = saved.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] kv = parts[i].Split(':');
                if (kv.Length != 2) continue;
                if (!int.TryParse(kv[0], out int id) || !int.TryParse(kv[1], out int lv)) continue;

                levels[id] = Mathf.Max(DefaultLevel, lv);
            }
        }

        loaded = true;
    }

    private static void SaveCore()
    {
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<int, int> kv in levels)
        {
            if (kv.Value <= DefaultLevel) continue; // 1 级就是没升过，不用存
            if (sb.Length > 0) sb.Append(',');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
        }

        StorageMgr.SetString(GameData.PetLevelKey, sb.ToString(), StoreTarget);
    }
}
