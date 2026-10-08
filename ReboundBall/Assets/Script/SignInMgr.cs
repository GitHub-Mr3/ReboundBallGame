using System;
using System.Collections.Generic;
using System.Globalization;
using Mr3;
using UnityEngine;

/// <summary>
/// 7 天签到系统：按<b>自然日</b>严格顺序签到（第 1 天 → 第 7 天 → 第 1 天 …），
/// 漏掉的天不补、也不重发奖励，下次上线时直接补到"上次没签的那一天"。
/// 第 7 天的奖励（宠物）按"第几轮"循环：第 1 轮 送 1 号，第 2 轮 送 3 号，第 3 轮 送 11 号，第 4 轮 送 12 号，第 5 轮 又回到 1 号……
///
/// 规则：
///   1. 一天只能签一次，按自然日（yyyyMMdd）判断，同一天重复调用不会重复发奖；
///   2. 严格按顺序：第 1 天没签就补第 1 天，第 2 天没签就补第 2 天，依此类推；
///   3. 漏签不重发：第 1 天没签到第 2 天上线，不会回到第 1 天重领，也不会发第 1 天的奖励——
///      直接补到第 1 天（领第 1 天的奖励），然后下次签第 2 天；
///   4. 7 天一个循环：第 7 天签完后下次回到第 1 天，第 7 天的宠物按"第几轮"切换；
///   5. 奖励发的是真东西：金币走 CurrencyMgr，碎片走 PetFragmentMgr，宠物直接解锁进 GameData 并存档；
///   6. 宠物奖励如果玩家已经有了，按品质折算成共用碎片（和扭蛋重复一致），不会白给；
///   7. 存档默认 Both（本地 + 微信托管双写），删游戏后进度还在。
///
/// 用法（公用入口就是 SignIn / TrySignIn）：
///     bool can = SignInMgr.CanSignInToday;              // 今天还能不能签
///     int day  = SignInMgr.TodayIndex;                  // 今天签的是第几天（1~7）
///     var rw   = SignInMgr.GetTodayReward();            // 今天这份奖励是什么（给 UI 展示）
///     var r    = SignInMgr.SignIn(out SignInMgr.SignInReward got, out string tip);  // 签到并发奖
///     if (SignInMgr.TrySignIn(out string tip))          // UI 版：tip 可直接弹提示
///         CommonUtil.Instance.ShowTips(tip);
///     SignInMgr.IsDayClaimed(3);                        // 本轮第 3 天领过了吗（画勾用）
///     SignInMgr.TotalSignDays;                          // 累计签了多少天
///
/// 改奖励配置（改完记得重开界面，或调 RefreshFromStorage）：
///     SignInMgr.Rewards[0] = SignInMgr.Coin(500);       // 第 1 天改成 500 金币
///     SignInMgr.Cycle7PetIds = new[] { 1, 3, 11, 12 };  // 第 7 天的宠物按"第几轮"循环
///
/// UI 监听（携带本次签到第几天，int 1~7）：
///     EventMgr.Instance.AddListener(GameData.EventID_SignInChanged, OnSignIn);
/// </summary>
public static class SignInMgr
{
    private const string LogTag = "[SignInMgr]";

    /// <summary>签到数据存哪：默认本地 + 托管数据双写</summary>
    public static StorageTarget StoreTarget = StorageTarget.Both;

    /// <summary>一轮几天</summary>
    public const int DaysPerCycle = 7;

    // ==================== 奖励配置 ====================

    /// <summary>奖励类型</summary>
    public enum RewardType
    {
        Coin, // 金币
        Fragment, // 宠物碎片
        Pet, // 宠物（直接解锁）
    }

    /// <summary>一天的奖励：类型 + 数量（宠物额外带 petId）</summary>
    public struct SignInReward
    {
        public RewardType type;

        /// <summary>数量：金币 / 碎片就是个数，宠物是几只（一般 1）</summary>
        public int amount;

        /// <summary>type == Pet 时有效：要送哪只宠物</summary>
        public int petId;

        /// <summary>配置是否合法（数量 > 0；宠物还要 id > 0）</summary>
        public bool IsValid
        {
            get
            {
                if (amount <= 0) return false;
                if (type == RewardType.Pet && petId <= 0) return false;
                return true;
            }
        }

        /// <summary>给 UI 展示的一句话，例："100 金币" / "5 碎片" / "罗威纳.赤岩 x1"</summary>
        public string Desc
        {
            get
            {
                switch (type)
                {
                    case RewardType.Coin:
                        return $"{amount} 金币";
                    case RewardType.Fragment:
                        return $"{amount} 碎片";
                    case RewardType.Pet:
                        string name = GameData.GetPetData(petId).name;
                        return amount > 1 ? $"{name} x{amount}" : name;
                    default:
                        return "";
                }
            }
        }

        public override string ToString() => Desc;
    }

    /// <summary>造一份金币奖励</summary>
    public static SignInReward Coin(int amount) => new SignInReward { type = RewardType.Coin, amount = amount };

    /// <summary>造一份碎片奖励</summary>
    public static SignInReward Fragment(int amount) => new SignInReward { type = RewardType.Fragment, amount = amount };

    /// <summary>造一份宠物奖励</summary>
    public static SignInReward Pet(int petId, int amount = 1) => new SignInReward
        { type = RewardType.Pet, amount = amount, petId = petId };

    /// <summary>
    /// 第 1~6 天的奖励表（下标 0 = 第 1 天 …… 下标 5 = 第 6 天）。
    /// 第 7 天的奖励按"第几轮"轮换，见 <see cref="Cycle7PetIds"/>。
    /// 想改直接改这里，或运行时整段替换：SignInMgr.Rewards = new[]{ ... };
    /// </summary>
    public static SignInReward[] Rewards =
    {
        Coin(100), // 第 1 天
        Fragment(5), // 第 2 天
        Coin(200), // 第 3 天
        Fragment(10), // 第 4 天
        Coin(300), // 第 5 天
        Fragment(20), // 第 6 天
    };

    /// <summary>
    /// 第 7 天的宠物奖励（按"第几轮"循环）：下标 0 = 第 1 轮，1 = 第 2 轮……，每 <see cref="Cycle7PetIds"/>.Length 轮循环一次。
    /// 默认：第 1 轮送 1 号、第 2 轮送 3 号、第 3 轮送 11 号、第 4 轮送 12 号、第 5 轮又回到 1 号。
    /// </summary>
    public static int[] Cycle7PetIds = { 1, 3, 11, 12 };

    /// <summary>重复（已拥有）宠物折算成共用碎片的数量，下标 = 品质（和扭蛋重复折算一致）</summary>


    // ==================== 状态 ====================

    /// <summary>最近一次签到的日期（yyyyMMdd），用来判断"今天签过没"</summary>
    private static string lastDate;

    /// <summary>下次要签的是第几天（1~7）。签完第 7 天后自动回到 1，进入下一轮</summary>
    private static int nextSignDay = 1;

    /// <summary>累计签到总天数（跨轮累加，只增不减）</summary>
    private static int totalDays;

    private static bool loaded;

    public static bool IsLoaded => loaded;

    /// <summary>签到结果</summary>
    public enum SignInResult
    {
        Success, // 签到成功，奖励已发放
        AlreadySigned, // 今天已经签过了
        InvalidConfig, // 这一天的奖励没配（数量 <= 0 或宠物 id 非法）
    }

    // ==================== 查询 ====================

    /// <summary>今天还能不能签到（false = 今天已经签过）</summary>
    public static bool CanSignInToday
    {
        get
        {
            EnsureLoaded();
            return lastDate != TodayKey();
        }
    }

    /// <summary>
    /// 今天点下去签的是第几天（1~7）：严格按"上次签到 → 下一天"的顺序推进，
    /// 漏签的天直接跳过、不补也不重发，所以 TodayIndex 永远等于 nextSignDay。
    /// 例：第 1 天签了，第 2 天没签，第 3 天上线 → TodayIndex 仍为 2（补昨天的进度）。
    /// </summary>
    public static int TodayIndex
    {
        get
        {
            EnsureLoaded();
            return nextSignDay;
        }
    }

    /// <summary>第 day 天（1~7）是否已经领过（画勾 / 置灰用）</summary>
    public static bool IsDayClaimed(int day)
    {
        EnsureLoaded();
        // nextSignDay 还没推进 = 今天还没签；推进过的 1 ~ nextSignDay-1 都算已领
        return day >= 1 && day < nextSignDay;
    }

    /// <summary>把任意整数天数换算回 1~7（支持跨轮、负数）</summary>
    private static int WrapDay(int day) => ((day - 1) % DaysPerCycle + DaysPerCycle) % DaysPerCycle + 1;

    /// <summary>指定那天（1~7）的奖励：第 1~6 天来自 <see cref="Rewards"/>，第 7 天按轮循环</summary>
    public static SignInReward GetReward(int day)
    {
        if (day < 1 || day > DaysPerCycle) return new SignInReward();

        if (day == DaysPerCycle)
        {
            // 第 7 天：按"累计签到/7"算当前是第几轮（0 起），取对应宠物的 id
            int round = (TotalSignDays / DaysPerCycle) % Mathf.Max(1, Cycle7PetIds.Length);
            return Pet(Cycle7PetIds[round]);
        }

        int index = day - 1;
        if (index >= Rewards.Length) return new SignInReward();
        return Rewards[index];
    }

    /// <summary>今天这份奖励（UI 展示用）</summary>
    public static SignInReward GetTodayReward() => GetReward(TodayIndex);

    /// <summary>累计签到总天数</summary>
    public static int TotalSignDays
    {
        get
        {
            EnsureLoaded();
            return totalDays;
        }
    }

    /// <summary>当前是第几轮（1 起）。第 1~6 天未签前为第 1 轮，签完第 7 天后变成第 2 轮</summary>
    public static int CycleIndex => TotalSignDays / DaysPerCycle + 1;

    /// <summary>预检：现在点签到会怎样（不发奖，UI 用来决定按钮亮不亮）</summary>
    public static SignInResult CheckSignIn()
    {
        if (!CanSignInToday) return SignInResult.AlreadySigned;
        if (!GetTodayReward().IsValid) return SignInResult.InvalidConfig;
        return SignInResult.Success;
    }

    // ==================== 签到（公用入口） ====================

    /// <summary>
    /// 【公用方法】签到并发奖：判定 → 发奖（金币 / 碎片 / 解锁宠物）→ 推进进度 → 写存档 → 广播事件。
    /// 同一天重复调用只会成功一次，第二次返回 AlreadySigned，不会重复发奖。
    /// 严格按顺序推进（漏掉的天不补、不重发）：nextSignDay 总是指向"上次没签的那一天"。
    /// </summary>
    /// <param name="reward">本次实际签到的天数对应的奖励配置</param>
    /// <param name="tip">可直接弹给玩家的文案，例："签到第 3 天，获得 200 金币"</param>
    /// <returns>签到结果</returns>
    public static SignInResult SignIn(out SignInReward reward, out string tip)
    {
        reward = new SignInReward();
        tip = "";

        SignInResult check = CheckSignIn();
        if (check != SignInResult.Success)
        {
            tip = check == SignInResult.AlreadySigned ? "今天已经签到过啦，明天再来" : "这一天的奖励还没配置";
            DebugMgr.Instance.Log($"{LogTag} 签到失败：{check}");
            return check;
        }

        int day = TodayIndex;
        reward = GetReward(day);

        // 发奖：宠物奖励可能因为"已经有了"变成碎片，用 realTip 描述真实到账的东西
        string realTip = GrantReward(reward, day);

        // 推进进度：下次该签的是下一天；签完第 7 天后回 1 进入下一轮
        nextSignDay = WrapDay(nextSignDay + 1);
        totalDays++;
        lastDate = TodayKey();
        SaveCore();

        tip = $"签到第 {day} 天，{realTip}";
        DebugMgr.Instance.Log($"{LogTag} 签到成功：第 {day} 天（第 {CycleIndex} 轮），{realTip}，累计 {totalDays} 天");

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_SignInChanged, day);

        return SignInResult.Success;
    }

    /// <summary>【公用方法 · UI 版】签到并拿到一句可直接弹窗的文案</summary>
    /// <param name="tip">成功："签到第 3 天，获得 200 金币"；失败："今天已经签到过啦，明天再来"</param>
    /// <returns>是否签到成功</returns>
    public static bool TrySignIn(out string tip)
    {
        SignInResult result = SignIn(out SignInReward _, out tip);
        return result == SignInResult.Success;
    }

    /// <summary>【公用方法 · UI 版】签到并同时拿到奖励配置（弹奖励界面时用）</summary>
    public static bool TrySignIn(out SignInReward reward, out string tip)
    {
        SignInResult result = SignIn(out reward, out tip);
        return result == SignInResult.Success;
    }

    // ==================== 发奖 ====================

    /// <summary>按配置把奖励发到对应系统里，返回"实际上到账了什么"的文案</summary>
    private static string GrantReward(SignInReward reward, int day)
    {
        string reason = $"7日签到 第{day}天";

        switch (reward.type)
        {
            case RewardType.Coin:
                CurrencyMgr.Add(reward.amount, reason);
                return $"获得 {reward.amount} 金币";

            case RewardType.Fragment:
                PetFragmentMgr.Add(reward.amount, reason);
                return $"获得 {reward.amount} 碎片";

            case RewardType.Pet:
                return GrantPet(reward.petId, reward.amount, reason);

            default:
                return "";
        }
    }

    public static int GetPetFragmentNum(int petId, int amount)
    {
        GameData.PetData pet = GameData.GetPetData(petId);
        if (pet.Id <= 0)
        {
            return 0;
        }

        List<int> owned = GameData.GetHavePetID();
        var DuplicateFragment = GameData.PetDuplicateFragment;
        if (owned.Contains(petId))
        {
            int quality = Mathf.Clamp(pet.quality, 0, DuplicateFragment.Length - 1);
            int fragment = DuplicateFragment[quality] * Mathf.Max(1, amount);
            return fragment;
        }

        // 多只时把剩余只数折算成碎片（一般 amount = 1，用不到）
        if (amount > 1)
        {
            int quality = Mathf.Clamp(pet.quality, 0, DuplicateFragment.Length - 1);
            int fragment = DuplicateFragment[quality] * (amount - 1);
            return fragment;
        }
        return 0;
    }

    /// <summary>
    /// 发宠物：没解锁的直接解锁并存档；已经有的按品质折算成共用碎片（不会白给）。
    /// </summary>
    private static string GrantPet(int petId, int amount, string reason)
    {
        GameData.PetData pet = GameData.GetPetData(petId);
        if (pet.Id <= 0)
        {
            DebugMgr.Instance.LogWarning($"{LogTag} 宠物奖励 id 非法：{petId}，改成发 10 碎片");
            PetFragmentMgr.Add(10, reason);
            return "获得 10 碎片";
        }

        List<int> owned = GameData.GetHavePetID();
        var DuplicateFragment = GameData.PetDuplicateFragment;
        if (owned.Contains(petId))
        {
            // 已经有了：按品质折算碎片（和扭蛋抽到重复一致）
            int quality = Mathf.Clamp(pet.quality, 0, DuplicateFragment.Length - 1);
            int fragment = DuplicateFragment[quality] * Mathf.Max(1, amount);
            PetFragmentMgr.Add(fragment, $"{reason}（重复宠物折算）");
            return $"已拥有 {pet.name}，折算 {fragment} 碎片";
        }

        GameData.AddPetID(petId);
        SaveOwnedPets();

        // 多只时把剩余只数折算成碎片（一般 amount = 1，用不到）
        if (amount > 1)
        {
            int quality = Mathf.Clamp(pet.quality, 0, DuplicateFragment.Length - 1);
            int fragment = DuplicateFragment[quality] * (amount - 1);
            if (fragment > 0) PetFragmentMgr.Add(fragment, $"{reason}（重复宠物折算）");
        }

        return $"解锁新宠物 {pet.name}！";
    }

    /// <summary>把已解锁宠物列表写回存档（和 LotteryView.SaveOwnedPets 逻辑一致）</summary>
    private static void SaveOwnedPets()
    {
        StorageMgr.SetString(GameData.HavePetIdsKey, string.Join(",", GameData.GetHavePetID()), StorageTarget.Both);
    }

    // ==================== 修改（调试 / 重置） ====================

    /// <summary>清空签到进度（重置存档用）</summary>
    public static void ResetAll()
    {
        loaded = true;
        lastDate = "";
        nextSignDay = 1;
        totalDays = 0;
        SaveCore();
    }

    /// <summary>外部绕过本类改了存档时，用它把缓存同步回来</summary>
    public static void RefreshFromStorage()
    {
        loaded = false;
        EnsureLoaded();
    }

    /// <summary>
    /// 【调试用】把"上次签到日期"往前挪 days 天（days = 1 相当于过了一天，2 = 过了两天）。
    /// 真机上不用调，纯粹用来在编辑器里验证"明天上线补昨天的 / 几天没签后上线"等场景。
    /// </summary>
    public static void DebugShiftDays(int days)
    {
        EnsureLoaded();

        DateTime baseDay = DateTime.Now.Date;
        if (TryParseDate(lastDate, out DateTime last)) baseDay = last.Date;

        lastDate = baseDay.AddDays(-days).ToString("yyyyMMdd");
        SaveCore();
        RefreshFromStorage();

        DebugMgr.Instance.Log($"{LogTag} [调试] 上次签到日期设为 {lastDate}，今天可签：{CanSignInToday}，签到第 {TodayIndex} 天");
    }

    // ==================== 内部 ====================

    /// <summary>懒加载：第一次访问时才读存储</summary>
    private static void EnsureLoaded()
    {
        if (loaded) return;

        lastDate = StorageMgr.GetString(GameData.SignInDateKey, "", StoreTarget);
        totalDays = Mathf.Max(0, StorageMgr.GetInt(GameData.SignInTotalKey, 0, StoreTarget));

        // 新版用"下次要签的天数"；老存档（按天循环模型）里存的是"已签次数 0~7"，
        // 按"已签 N 天 → 下次该签 N+1"还原到新字段
        int storedNext = StorageMgr.GetInt(GameData.SignInNextDayKey, 0, StoreTarget);
        if (storedNext >= 1 && storedNext <= DaysPerCycle)
        {
            nextSignDay = storedNext;
        }
        else
        {
            int oldDay = Mathf.Clamp(StorageMgr.GetInt(GameData.SignInDayKey, 0, StoreTarget), 0, DaysPerCycle);
            nextSignDay = WrapDay(oldDay + 1);
        }

        loaded = true;
    }

    private static void SaveCore()
    {
        StorageMgr.SetString(GameData.SignInDateKey, lastDate, StoreTarget);
        StorageMgr.SetInt(GameData.SignInNextDayKey, nextSignDay, StoreTarget);
        StorageMgr.SetInt(GameData.SignInTotalKey, totalDays, StoreTarget);
    }

    /// <summary>今天的日期键（yyyyMMdd），每次实时取，跨零点自动算新的一天</summary>
    private static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>把 yyyyMMdd 解析成日期，失败返回 false</summary>
    private static bool TryParseDate(string dateKey, out DateTime date)
        => DateTime.TryParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}