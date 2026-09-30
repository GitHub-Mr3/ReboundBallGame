using System;
using System.Collections.Generic;
using System.Globalization;
using Mr3;
using UnityEngine;

/// <summary>
/// 7 天签到系统：每天能签到一次，7 天一个循环，第 8 天自动回到第 1 天重新签。
/// 每天的奖励<b>类型</b>（金币 / 宠物碎片 / 宠物）和<b>数量</b>都可以单独配置。
///
/// 规则：
///   1. 一天只能签一次，按自然日（yyyyMMdd）判断，同一天重复调用不会重复发奖；
///   2. 连着签：第 1 天 → 第 7 天，第 8 天回到第 1 天，奖励按新一轮再发一遍（循环无限轮）；
///   3. 断签：进度按日历顺延 —— 漏了 1 天，今天就领"下下天"，漏掉那天标成「漏签」，可以用补签找回
///      （不会回到第 1 天重领，避免同一天的奖励被反复领；想回到第 1 天重签就把 ResetOnMiss 设 true）；
///   4. 奖励发的是真东西：金币走 CurrencyMgr，碎片走 PetFragmentMgr，宠物直接解锁进 GameData 并存档；
///   5. 宠物奖励如果玩家已经有了，按品质折算成共用碎片（和扭蛋重复一致），不会白给；
///   6. 补签：昨天漏签了可以补回来（奖励照发，进度接上，相当于昨天真的签到了），一天只能补一次；
///      断签时进度虽然在页面上回到第 1 天，但补签能把它原样接回去；
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
/// 补签（昨天漏签时用它）：
///     bool can = SignInMgr.CanResignToday;              // 今天还能不能补签
///     int day  = SignInMgr.ResignDayIndex;              // 补的是第几天（0 = 没得补）
///     if (SignInMgr.TryResign(out string tip))          // 补签，tip 可直接弹提示
///         CommonUtil.Instance.ShowTips(tip);
///     SignInMgr.ResignCostCoin = 100;                   // 想让补签收费就设这个（0 = 免费）
///
/// 改奖励配置（改完记得重开界面，或调 RefreshFromStorage）：
///     SignInMgr.Rewards[6] = SignInMgr.Pet(13, 1);      // 第 7 天改成送中华田园犬
///     SignInMgr.Rewards[0] = SignInMgr.Coin(500);       // 第 1 天改成 500 金币
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

    /// <summary>
    /// 断签是否把进度清零、回到第 1 天重新签。
    ///   false（默认）：进度按日历推进 —— 漏了 1 天，今天就领"下下天"，漏掉的那天标成「漏签」，可以用补签找回来；
    ///   true：断签直接回到第 1 天重签（注意：这样第 1 天的奖励能被反复领，且和补签功能冲突，一般不推荐）。
    /// </summary>
    public static bool ResetOnMiss = false;

    // ==================== 奖励配置 ====================

    /// <summary>奖励类型</summary>
    public enum RewardType
    {
        Coin,     // 金币
        Fragment, // 宠物碎片
        Pet,      // 宠物（直接解锁）
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
    public static SignInReward Pet(int petId, int amount = 1) => new SignInReward { type = RewardType.Pet, amount = amount, petId = petId };

    /// <summary>
    /// 7 天奖励表（下标 0 = 第 1 天 …… 下标 6 = 第 7 天）。
    /// 想改直接改这里，或运行时整段替换：SignInMgr.Rewards = new[]{ ... };
    /// </summary>
    public static SignInReward[] Rewards =
    {
        Coin(100),        // 第 1 天
        Fragment(5),      // 第 2 天
        Coin(200),        // 第 3 天
        Fragment(10),     // 第 4 天
        Coin(300),        // 第 5 天
        Fragment(20),     // 第 6 天
        Pet(8),           // 第 7 天：送罗威纳.赤岩（SS 级）。第 8 天起进入下一轮，重新从第 1 天发
    };

    /// <summary>重复（已拥有）宠物折算成共用碎片的数量，下标 = 品质（和扭蛋重复折算一致）</summary>
    private static readonly int[] PetDuplicateFragment = { 0, 2, 5, 10, 20, 50 };

    // ==================== 状态 ====================

    /// <summary>最近一次签到的日期（yyyyMMdd）</summary>
    private static string lastDate;

    /// <summary>本轮已签到次数（0~7，7 = 这一轮 7 天全签完）</summary>
    private static int signedCount;

    /// <summary>累计签到总天数（跨轮累加）</summary>
    private static int totalDays;

    /// <summary>最近一次补签的日期（yyyyMMdd），用来保证"一天只能补一次"</summary>
    private static string resignDate;

    /// <summary>本轮漏签、还没补回来的天数（签到时跨过几天就 +几，补签一次 -1）</summary>
    private static int pendingMiss;

    /// <summary>
    /// 补签一次要花多少金币：0 = 免费（默认）。
    /// 想收费就改成数字（如 100）；想改成看广告，把 Resign 里的扣费换成广告回调即可。
    /// </summary>
    public static int ResignCostCoin = 0;

    private static bool loaded;

    public static bool IsLoaded => loaded;

    /// <summary>签到结果</summary>
    public enum SignInResult
    {
        Success,       // 签到成功，奖励已发放
        AlreadySigned, // 今天已经签过了
        InvalidConfig, // 这一天的奖励没配（数量 <= 0 或宠物 id 非法）
    }

    /// <summary>补签结果</summary>
    public enum ResignResult
    {
        Success,        // 补签成功，奖励已发放
        NeverSigned,    // 一次都没签过，没有"漏签"可补
        NoMiss,         // 昨天签到了，没漏，不用补
        AlreadyResigned,// 昨天已经补过，一天只能补一次
        NotEnoughCoin,  // 补签要花金币，余额不够
        InvalidConfig,  // 要补的那天奖励没配
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
    /// 今天点下去签的是第几天（1~7），按<b>日历</b>算：
    ///   今天已签 = 今天签的那天；
    ///   昨天签的 = 下一天（第 8 天自动回到 1）；
    ///   中间断了 k 天 = 顺延 k 天（断的那几天不会重新领，而是标成「漏签」等补签）。
    /// </summary>
    public static int TodayIndex
    {
        get
        {
            EnsureLoaded();
            ApplyResetOnMiss();

            if (string.IsNullOrEmpty(lastDate)) return 1;                                  // 从没签过
            if (lastDate == TodayKey()) return Mathf.Clamp(signedCount, 1, DaysPerCycle);   // 今天已签
            if (!TryParseDate(lastDate, out DateTime last)) return 1;

            int gap = (DateTime.Now.Date - last.Date).Days;  // 距上次签到过了几天（昨天签 = 1）
            if (gap <= 0) return Mathf.Clamp(signedCount, 1, DaysPerCycle);

            return WrapDay(signedCount + gap);              // 第 8 天（signedCount 7 + 1）自动绕回第 1 天
        }
    }

    /// <summary>本轮第 day 天是否已经领过（画勾 / 置灰用，day 取 1~7）</summary>
    public static bool IsDayClaimed(int day)
    {
        EnsureLoaded();
        ApplyResetOnMiss();
        return day >= 1 && day <= signedCount;
    }

    /// <summary>
    /// 本轮第 day 天是不是「漏签」：已经过去了（在本轮今天之前）但没领到的天，可以用补签找回。
    /// 例：签完第 1 天断签一天 → 第 2 天就是漏签，今天该领第 3 天。
    /// </summary>
    public static bool IsDayMissed(int day)
    {
        EnsureLoaded();
        ApplyResetOnMiss();

        if (day < 1 || day > DaysPerCycle) return false;

        // ① 已经签到过、但之前断签跨过去还没补回来的天（这些天进度算过去了，但奖励其实没发）
        for (int i = 1; i <= pendingMiss; i++)
        {
            if (WrapDay(signedCount - i) == day) return true;
        }

        if (IsDayClaimed(day)) return false;

        // ② 今天还没签到时：已领的最后一天 ~ 今天之间、没领到的那几天
        return IsInOpenRange(day, signedCount, TodayIndex);
    }

    /// <summary>day 是否落在 from 的"下一天"到 to 的"前一天"之间（按 7 天环形算，跨轮也成立）</summary>
    private static bool IsInOpenRange(int day, int from, int to)
    {
        int steps = (to - from - 1 + DaysPerCycle) % DaysPerCycle; // from 与 to 之间隔了几天（0~6）
        int cur = from;
      //  cur = cur % DaysPerCycle;
        for (int i = 0; i < steps; i++)
        {
          
            if (cur == day) return true;
        }

        return false;
    }

    /// <summary>把任意整数天数换算回 1~7（支持跨轮、负数）</summary>
    private static int WrapDay(int day) => ((day - 1) % DaysPerCycle + DaysPerCycle) % DaysPerCycle + 1;

    /// <summary>断签清零（只有 ResetOnMiss = true 时才真的清零并写存档）</summary>
    private static void ApplyResetOnMiss()
    {
        if (!ResetOnMiss) return;
        if (string.IsNullOrEmpty(lastDate)) return;
        if (lastDate == TodayKey() || IsYesterday(lastDate)) return;

        signedCount = 0;
        lastDate = "";
        pendingMiss = 0;
        SaveCore();
    }

    /// <summary>距上次签到过了几天（昨天签 = 1，今天签 = 0，从没签过 = 0）</summary>
    private static int GetDaysSinceLastSign()
    {
        if (string.IsNullOrEmpty(lastDate)) return 0;
        if (!TryParseDate(lastDate, out DateTime last)) return 0;

        return Mathf.Max(0, (DateTime.Now.Date - last.Date).Days);
    }

    /// <summary>指定那天（1~7）的奖励</summary>
    public static SignInReward GetReward(int day)
    {
        int index = day - 1;
        if (index < 0 || index >= Rewards.Length) return new SignInReward();
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

    /// <summary>当前是第几轮（第 1 天~第 7 天 = 第 1 轮，第 8 天起 = 第 2 轮……）</summary>
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
    /// 【公用方法】签到并发奖：判定 → 发奖（金币 / 碎片 / 解锁宠物）→ 写存档 → 广播事件。
    /// 同一天重复调用只会成功一次，第二次返回 AlreadySigned，不会重复发奖。
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

        // 断签后签到：中间跨过去的那几天记为"待补签"，之后随时能用补签找回来（一天补一次）
        int gap = GetDaysSinceLastSign();
        if (gap > 1) pendingMiss = Mathf.Min(DaysPerCycle, pendingMiss + gap - 1);

        // 发奖：宠物奖励可能因为"已经有了"变成碎片，用 realTip 描述真实到账的东西
        string realTip = GrantReward(reward, day);

        // 进度：本轮第 N 天 = 已签 N 次；签满 7 次后下一次调用 TodayIndex 会自动回到 1
        signedCount = day;
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

    // ==================== 补签 ====================

    /// <summary>今天可不可以补签（把漏掉的一天补回来，一天只能补一次）</summary>
    public static bool CanResignToday => CheckResign() == ResignResult.Success;

    /// <summary>
    /// 现在补签补的是本轮第几天（1~7）；返回 0 表示没有可补的（没漏 / 今天已补过 / 从没签过）。
    /// 有"待补签"记录时补最早漏掉的那天（补完一次，下一次补再往后一天）；
    /// 否则补的是"昨天"那天（今天还没签到的场景）。
    /// </summary>
    public static int ResignDayIndex
    {
        get
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(lastDate)) return 0;

            // 之前断签跨过去、还没补的天：从最早的那天开始补
            if (pendingMiss > 0) return WrapDay(signedCount - pendingMiss);

            if (lastDate == TodayKey() || lastDate == YesterdayKey()) return 0; // 昨天签到了，没漏

            if (!TryParseDate(lastDate, out DateTime last)) return 0;

            // 漏了几天 = 昨天距上次签到的天数；补的是"昨天"那天的奖励
            int missDays = (DateTime.Now.Date.AddDays(-1) - last.Date).Days;
            if (missDays <= 0) return 0;

            return WrapDay(signedCount + missDays);
        }
    }

    /// <summary>预检：现在点补签会怎样（不发奖，UI 用来决定补签按钮亮不亮）</summary>
    public static ResignResult CheckResign()
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(lastDate)) return ResignResult.NeverSigned;
        if (resignDate == TodayKey()) return ResignResult.AlreadyResigned; // 一天只能补一次
        if (pendingMiss <= 0)
        {
            if (lastDate == TodayKey() || lastDate == YesterdayKey()) return ResignResult.NoMiss; // 昨天签到了，没漏
        }

        if (!GetReward(ResignDayIndex).IsValid) return ResignResult.InvalidConfig;
        if (ResignCostCoin > 0 && !CurrencyMgr.CanAfford(ResignCostCoin)) return ResignResult.NotEnoughCoin;

        return ResignResult.Success;
    }

    /// <summary>
    /// 【公用方法】补签：把漏掉的那天补回来，奖励照发。
    /// 一天只能补一次；已经签到过也能补（补的是之前跨过去的天，进度早就走过去了，不影响今天的签到）。
    /// </summary>
    public static ResignResult Resign(out SignInReward reward, out string tip)
    {
        reward = new SignInReward();
        tip = "";

        ResignResult check = CheckResign();
        if (check != ResignResult.Success)
        {
            tip = GetResignTip(check);
            DebugMgr.Instance.Log($"{LogTag} 补签失败：{check}");
            return check;
        }

        int day = ResignDayIndex;
        reward = GetReward(day);

        if (ResignCostCoin > 0) CurrencyMgr.Spend(ResignCostCoin, "7日签到补签");

        string realTip = GrantReward(reward, day);

        if (pendingMiss > 0)
        {
            // 补的是之前断签跨过去的天：进度早就走过去了，这里只补发奖励
            pendingMiss--;
        }
        else
        {
            // 今天还没签到：把昨天那次补上，进度直接接上，今天再签就是下一天
            signedCount = day;
            lastDate = YesterdayKey();
        }

        resignDate = TodayKey();
        totalDays++;
        SaveCore();

        tip = $"补签第 {day} 天，{realTip}" + (ResignCostCoin > 0 ? $"（花费 {ResignCostCoin} 金币）" : "");
        DebugMgr.Instance.Log($"{LogTag} 补签成功：第 {day} 天，{realTip}，累计 {totalDays} 天");

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_SignInChanged, day);

        return ResignResult.Success;
    }

    /// <summary>【公用方法 · UI 版】补签并拿到一句可直接弹窗的文案</summary>
    public static bool TryResign(out string tip)
    {
        ResignResult result = Resign(out SignInReward _, out tip);
        return result == ResignResult.Success;
    }

    /// <summary>【公用方法 · UI 版】补签并同时拿到奖励配置</summary>
    public static bool TryResign(out SignInReward reward, out string tip)
    {
        ResignResult result = Resign(out reward, out tip);
        return result == ResignResult.Success;
    }

    /// <summary>把补签结果翻译成玩家能看懂的提示</summary>
    public static string GetResignTip(ResignResult result)
    {
        switch (result)
        {
            case ResignResult.Success:
                return "补签成功";
            case ResignResult.NeverSigned:
                return "还没有签到记录，先签一次才能补签";
            case ResignResult.NoMiss:
                return "昨天已经签到过了，不用补签";
            case ResignResult.AlreadyResigned:
                return "今天已经补签过啦，每天只能补一次";
            case ResignResult.NotEnoughCoin:
                return $"补签需要 {ResignCostCoin} 金币";
            case ResignResult.InvalidConfig:
                return "要补的那天奖励还没配置";
            default:
                return "补签失败";
        }
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

        if (owned.Contains(petId))
        {
            // 已经有了：按品质折算碎片（和扭蛋抽到重复一致）
            int quality = Mathf.Clamp(pet.quality, 0, PetDuplicateFragment.Length - 1);
            int fragment = PetDuplicateFragment[quality] * Mathf.Max(1, amount);
            PetFragmentMgr.Add(fragment, $"{reason}（重复宠物折算）");
            return $"已拥有 {pet.name}，折算 {fragment} 碎片";
        }

        GameData.AddPetID(petId);
        SaveOwnedPets();

        // 多只时把剩余只数折算成碎片（一般 amount = 1，用不到）
        if (amount > 1)
        {
            int quality = Mathf.Clamp(pet.quality, 0, PetDuplicateFragment.Length - 1);
            int fragment = PetDuplicateFragment[quality] * (amount - 1);
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
        signedCount = 0;
        totalDays = 0;
        resignDate = "";
        pendingMiss = 0;
        SaveCore();
    }

    /// <summary>外部绕过本类改了存档时，用它把缓存同步回来</summary>
    public static void RefreshFromStorage()
    {
        loaded = false;
        EnsureLoaded();
    }

    /// <summary>
    /// 【调试用】把"上次签到日期"往前挪 days 天（days = 1 相当于过了一天，2 = 断签一天）。
    /// 真机上不用调，纯粹用来在编辑器里验证"第二天 / 断签 / 第 8 天回到第 1 天"。
    /// </summary>
    public static void DebugShiftDays(int days)
    {
        EnsureLoaded();

        DateTime baseDay = DateTime.Now.Date;
        if (TryParseDate(lastDate, out DateTime last)) baseDay = last.Date;

        lastDate = baseDay.AddDays(-days).ToString("yyyyMMdd");
        resignDate = ""; // 模拟到了新的一天，补签次数也一起刷新，方便反复测
        SaveCore();
        RefreshFromStorage();

        DebugMgr.Instance.Log($"{LogTag} [调试] 上次签到日期设为 {lastDate}，今天可签：{CanSignInToday}，" +
                              $"签到第 {TodayIndex} 天，可补签：{CanResignToday}（补第 {ResignDayIndex} 天），漏签 {pendingMiss} 天");
    }

    // ==================== 内部 ====================

    /// <summary>懒加载：第一次访问时才读存储（断签在读取时按 ValidSignedCount 现算，不改动存档）</summary>
    private static void EnsureLoaded()
    {
        if (loaded) return;

        lastDate = StorageMgr.GetString(GameData.SignInDateKey, "", StoreTarget);
        signedCount = Mathf.Clamp(StorageMgr.GetInt(GameData.SignInDayKey, 0, StoreTarget), 0, DaysPerCycle);
        totalDays = Mathf.Max(0, StorageMgr.GetInt(GameData.SignInTotalKey, 0, StoreTarget));
        resignDate = StorageMgr.GetString(GameData.SignInResignDateKey, "", StoreTarget);
        pendingMiss = Mathf.Clamp(StorageMgr.GetInt(GameData.SignInPendingMissKey, 0, StoreTarget), 0, DaysPerCycle);
        loaded = true;
    }

    private static void SaveCore()
    {
        StorageMgr.SetString(GameData.SignInDateKey, lastDate, StoreTarget);
        StorageMgr.SetInt(GameData.SignInDayKey, signedCount, StoreTarget);
        StorageMgr.SetInt(GameData.SignInTotalKey, totalDays, StoreTarget);
        StorageMgr.SetString(GameData.SignInResignDateKey, resignDate, StoreTarget);
        StorageMgr.SetInt(GameData.SignInPendingMissKey, pendingMiss, StoreTarget);
    }

    /// <summary>今天的日期键（yyyyMMdd），每次实时取，跨零点自动算新的一天</summary>
    private static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>昨天的日期键（yyyyMMdd），补签补的就是这一天</summary>
    private static string YesterdayKey() => DateTime.Now.Date.AddDays(-1).ToString("yyyyMMdd");

    /// <summary>把 yyyyMMdd 解析成日期，失败返回 false</summary>
    private static bool TryParseDate(string dateKey, out DateTime date)
        => DateTime.TryParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>dateKey 是不是昨天</summary>
    private static bool IsYesterday(string dateKey)
    {
        if (!TryParseDate(dateKey, out DateTime day)) return false;

        return (DateTime.Now.Date - day.Date).Days == 1;
    }
}
