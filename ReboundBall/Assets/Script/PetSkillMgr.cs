using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物技能系统：传入技能 ID（GameData._petSkillDatas 里的 SkillID），技能效果自动生效。
///
/// 技能强度随「出战宠物等级」成长：每个技能在 skillTable 里配了「1 级效果 → 满级效果」，
/// 中间按宠物等级线性插值（次数类四舍五入取整），所以出战宠物升级后技能立刻变强。
/// 满级 = 该宠物品质对应的最高等级（B 级 6 级 … SSS 级 10 级，见 PetLevelMgr），
/// 例：烈焰之瞳 1 级 +15%、满级 +20%，走的就是两端的插值。
///
/// 用法：
///     PetSkillMgr.SetSkillByPet(13);                    // 推荐：按宠物 id 生效，自动带它的等级
///     PetSkillMgr.SetSkill(7);                          // 直接给技能 id（按 1 级效果算）
///     PetSkillMgr.SetSkill(7, level: 5, maxLevel: 10);  // 手动指定等级
///     PetSkillMgr.AddSkill(2);                          // 多只同时生效（叠加）
///     PetSkillMgr.ClearSkills();                        // 清空
///     PetSkillMgr.HasSkill(7);                          // 是否生效中
///     PetSkillMgr.RefreshBattlePetSkill();              // 出战宠物升级后刷新效果
///
/// 两类效果：
///   A. 关卡参数类（1/2/3/5/6/8/9/13）：布置关卡时由 LevelManager 调 ApplyToLevel()，
///      在关卡数据的副本上改参数，不污染 LevelConfig 的静态配置；
///   B. 运行时类（4/7/10/11/12/13）：提供查询与消耗接口，由各模块在自己该管的时点调用
///      —— 护盾/暂停在 PlayerController，奖励倍率在 GameResult，免费复活在 GameResult。
///
/// 每关布置时 LevelManager 会调 ResetLevelRuntime()，把护盾次数、暂停次数、免广告次数重置。
/// </summary>
public static class PetSkillMgr
{
    private const string LogTag = "[PetSkillMgr]";

    // ==================== 技能效果表 ====================

    /// <summary>
    /// 一个技能对各项数值的影响。数值类用「乘区 / 增量」，开关类用 bool，次数类用 int。
    /// 这里存的是「某个等级下的效果」，1 级值和满级值分别配在 SkillRange 里，按宠物等级插值。
    /// </summary>
    private struct SkillEffect
    {
        /// <summary>目标点位大小乘区（1.15 = 判定范围 +15%）</summary>
        public float winScaleMul;

        /// <summary>转盘转速乘区（0.85 = 转速 -15%）</summary>
        public float rotateScaleMul;

        /// <summary>目标漂移速度乘区（0.7 = 漂移速度 -30%）</summary>
        public float winMoveSpeedMul;

        /// <summary>通关所需投喂次数的增减（-1 = 少投一次）</summary>
        public int feedTargetDelta;

        /// <summary>每关随机停用几台转盘（0 = 不停用，保底至少留 1 台）</summary>
        public int removeObstacleCount;

        /// <summary>所有转盘强制同向旋转</summary>
        public bool forceSameDirection;

        /// <summary>取消随机初始角，同一套布局</summary>
        public bool disableRandomAngle;

        /// <summary>每关失败免疫次数（撞到机关不判失败，弹回继续）</summary>
        public int shieldCount;

        /// <summary>每关可暂停次数（小球停下，点场景选新方向）</summary>
        public int pauseCount;

        /// <summary>通关货币奖励的额外倍率（0.8 = 奖励增加 80%）</summary>
        public float rewardBonus;

        /// <summary>小球碰撞判定乘区（0.85 = 缩小 15%）</summary>
        public float playerColliderMul;

        /// <summary>每关免看广告直接复活的次数</summary>
        public int adSkipCount;

        /// <summary>是否改用短时长激励视频广告位（AdManager 会据此切换 adUnitId）</summary>
        public bool shortAdUnit;
    }

    /// <summary>一个技能的成长区间：lv1 = 宠物 1 级时的效果，max = 宠物满级时的效果</summary>
    private struct SkillRange
    {
        public SkillEffect lv1;
        public SkillEffect max;
    }

    /// <summary>没有具体宠物信息时（例如直接 SetSkill）默认按「1 级效果 + 上限 10 级」算</summary>
    private const int DefaultMaxLevel = 10;

    /// <summary>紧凑构造效果：只写和默认值不一样的项，没写的就是「无影响」</summary>
    private static SkillEffect E(
        float winScale = 1f, float rotateScale = 1f, float winMoveSpeed = 1f, float playerCollider = 1f,
        int feedTargetDelta = 0, int removeObstacleCount = 0,
        bool forceSameDirection = false, bool disableRandomAngle = false,
        int shieldCount = 0, int pauseCount = 0, float rewardBonus = 0f,
        int adSkipCount = 0, bool shortAdUnit = false)
    {
        return new SkillEffect
        {
            winScaleMul = winScale,
            rotateScaleMul = rotateScale,
            winMoveSpeedMul = winMoveSpeed,
            playerColliderMul = playerCollider,
            feedTargetDelta = feedTargetDelta,
            removeObstacleCount = removeObstacleCount,
            forceSameDirection = forceSameDirection,
            disableRandomAngle = disableRandomAngle,
            shieldCount = shieldCount,
            pauseCount = pauseCount,
            rewardBonus = rewardBonus,
            adSkipCount = adSkipCount,
            shortAdUnit = shortAdUnit,
        };
    }

    /// <summary>拼一条成长区间：1 级效果 → 满级效果</summary>
    private static SkillRange R(SkillEffect lv1, SkillEffect max) => new SkillRange { lv1 = lv1, max = max };

    private static SkillEffect NewEffect() => E();

    /// <summary>
    /// 技能 ID -> 效果成长区间（与 GameData._petSkillDatas 一一对应）。
    /// 左值 = 出战宠物 1 级时的效果，右值 = 满级时的效果，中间按等级线性插值（次数类四舍五入取整）；
    /// 开关类（强制同向 / 取消随机角）不随等级变化。
    /// </summary>
    private static readonly Dictionary<int, SkillRange> skillTable = new Dictionary<int, SkillRange>
    {
        // 1 烈焰之瞳：目标判定范围 +15% → 满级 +20%
        [1] = R(E(winScale: 1.15f), E(winScale: 1.20f)),

        // 2 雪球减速：所有转盘转速 -15% → 满级 -25%
        [2] = R(E(rotateScale: 0.85f), E(rotateScale: 0.75f)),

        // 3 暖阳锁轨：目标漂移速度 -30% → 满级 -50%
        [3] = R(E(winMoveSpeed: 0.7f), E(winMoveSpeed: 0.5f)),

        // 4 星夜疾行：每关免费复活 1 次 → 满级 3 次
        //    该技能同时挂了短时长激励视频广告位：免费次数用完后看广告复活，播的是半时长版本。
        [4] = R(E(adSkipCount: 1, shortAdUnit: true), E(adSkipCount: 3, shortAdUnit: true)),

        // 5 青芜饱腹：本关通关所需投喂次数 -1 → 满级 -3（LevelData.FeedTarget 保底至少 1 次）
        [5] = R(E(feedTargetDelta: -1), E(feedTargetDelta: -3)),

        // 6 桃桃拆家：每关随机停用 1 台转盘 → 满级 3 台（保底至少留 1 台）
        [6] = R(E(removeObstacleCount: 1), E(removeObstacleCount: 3)),

        // 7 黄巾护体：首次撞到机关不判失败，弹回 → 满级 3 次
        [7] = R(E(shieldCount: 1), E(shieldCount: 3)),

        // 8 赤岩同心：所有转盘强制同向旋转（开关类，不随等级成长）
        [8] = R(E(forceSameDirection: true), E(forceSameDirection: true)),

        // 9 紫缨定局：取消随机初始角，同一套布局（开关类，不随等级成长）
        [9] = R(E(disableRandomAngle: true), E(disableRandomAngle: true)),

        // 10 灰砾丰饶：获得的奖励增加 80% → 满级 150%
        [10] = R(E(rewardBonus: 0.8f), E(rewardBonus: 1.5f)),

        // 11 彩斑穿针：小球碰撞判定缩小 15% → 满级 30%
        [11] = R(E(playerCollider: 0.85f), E(playerCollider: 0.7f)),

        // 12 圆墩暂歇：小球可暂停 1 次 → 满级 3 次
        [12] = R(E(pauseCount: 1), E(pauseCount: 3)),

        // 13 旺财三重奏：转速 -20% + 目标判定 +12% + 每关 2 次失败免疫
        //     → 满级：转速 -40% + 目标判定 +24% + 4 次失败免疫
        [13] = R(E(winScale: 1.12f, rotateScale: 0.8f, shieldCount: 2),
                 E(winScale: 1.24f, rotateScale: 0.6f, shieldCount: 4)),
    };

    // ==================== 生效中的技能 ====================

    /// <summary>生效中的一条技能：技能 id + 来源宠物等级 / 该宠物满级（等级决定技能强度）</summary>
    private struct ActiveSkill
    {
        public int skillId;
        public int level;
        public int maxLevel;
    }

    private static readonly List<ActiveSkill> activeSkills = new List<ActiveSkill>();

    /// <summary>ActiveSkills 属性用的 id 缓存，避免每次访问都新建 List</summary>
    private static readonly List<int> activeSkillIds = new List<int>();

    /// <summary>设置生效技能（覆盖式）：一般就是出战那只宠物的技能</summary>
    /// <param name="skillId">技能 id</param>
    /// <param name="level">来源宠物等级（1 级 = 图中左值，满级 = 图中「最高」值）</param>
    /// <param name="maxLevel">来源宠物满级（决定插值比例，不传按 10 级算）</param>
    public static void SetSkill(int skillId, int level = PetLevelMgr.DefaultLevel, int maxLevel = DefaultMaxLevel)
    {
        activeSkills.Clear();
        if (skillId > 0) activeSkills.Add(MakeActive(skillId, level, maxLevel));
    }

    /// <summary>按宠物 id 设置生效技能：内部取该宠物的 SkillID，并按它的等级决定强度</summary>
    public static void SetSkillByPet(int petId)
    {
        if (petId <= 0)
        {
            ClearSkills();
            return;
        }

        SetSkill(GameData.GetPetData(petId).SkillID, PetLevelMgr.GetLevel(petId), PetLevelMgr.GetMaxLevel(petId));
    }

    /// <summary>再叠加一个技能（多只宠物同时生效时用）</summary>
    public static void AddSkill(int skillId, int level = PetLevelMgr.DefaultLevel, int maxLevel = DefaultMaxLevel)
    {
        if (skillId <= 0 || HasSkill(skillId)) return;

        activeSkills.Add(MakeActive(skillId, level, maxLevel));
    }

    /// <summary>批量覆盖式设置（都按 1 级效果算，需要按等级生效请用 SetSkillByPet）</summary>
    public static void SetSkills(IEnumerable<int> skillIds)
    {
        activeSkills.Clear();
        if (skillIds == null) return;

        foreach (int id in skillIds)
        {
            AddSkill(id);
        }
    }

    /// <summary>移除某个技能</summary>
    public static void RemoveSkill(int skillId)
    {
        activeSkills.RemoveAll(s => s.skillId == skillId);
    }

    /// <summary>清空全部技能（没有宠物出战时的状态）</summary>
    public static void ClearSkills()
    {
        activeSkills.Clear();
    }

    public static bool HasSkill(int skillId)
    {
        for (int i = 0; i < activeSkills.Count; i++)
        {
            if (activeSkills[i].skillId == skillId) return true;
        }

        return false;
    }

    /// <summary>当前生效的技能 ID 列表（只读）</summary>
    public static IReadOnlyList<int> ActiveSkills
    {
        get
        {
            activeSkillIds.Clear();
            for (int i = 0; i < activeSkills.Count; i++) activeSkillIds.Add(activeSkills[i].skillId);
            return activeSkillIds;
        }
    }

    /// <summary>
    /// 出战宠物升级后调用：按新等级重新套用技能并刷新次数。
    /// 关卡参数类效果（判定范围 / 转速 / 漂移…）本关已经布置好，下一关生效；
    /// 次数类（护盾 / 暂停 / 免费复活）会立刻补到新等级的数量。
    /// </summary>
    public static void RefreshBattlePetSkill()
    {
        if (BattlePetId > 0) SetSkillByPet(BattlePetId);
        else ClearSkills();

        ResetLevelRuntime();
    }

    /// <summary>拼一条生效技能记录：等级至少 1 级，满级不超过传进来的上限</summary>
    private static ActiveSkill MakeActive(int skillId, int level, int maxLevel)
    {
        int safeMax = Mathf.Max(PetLevelMgr.DefaultLevel, maxLevel);
        return new ActiveSkill
        {
            skillId = skillId,
            level = Mathf.Clamp(level, PetLevelMgr.DefaultLevel, safeMax),
            maxLevel = safeMax
        };
    }

    /// <summary>合并所有生效技能的效果（每条先按它的等级插值出当前数值，再叠加）</summary>
    private static SkillEffect Combined()
    {
        SkillEffect result = NewEffect();

        for (int i = 0; i < activeSkills.Count; i++)
        {
            ActiveSkill active = activeSkills[i];
            if (!skillTable.TryGetValue(active.skillId, out SkillRange range)) continue;

            SkillEffect e = Interpolate(range, active.level, active.maxLevel);

            result.winScaleMul *= e.winScaleMul;
            result.rotateScaleMul *= e.rotateScaleMul;
            result.winMoveSpeedMul *= e.winMoveSpeedMul;
            result.playerColliderMul *= e.playerColliderMul;
            result.feedTargetDelta += e.feedTargetDelta;
            result.rewardBonus += e.rewardBonus;
            result.shieldCount += e.shieldCount;
            result.pauseCount += e.pauseCount;
            result.adSkipCount += e.adSkipCount;
            result.shortAdUnit |= e.shortAdUnit;
            result.removeObstacleCount += e.removeObstacleCount;
            result.forceSameDirection |= e.forceSameDirection;
            result.disableRandomAngle |= e.disableRandomAngle;
        }

        return result;
    }

    /// <summary>
    /// 按「宠物等级」把效果在 1 级值 ~ 满级值之间线性插值：
    /// level = 1 → 完全是 lv1 的值，level = maxLevel → 完全是 max 的值，中间平滑过渡。
    /// 次数类是整数，插值后四舍五入（例：暂停 1 次 → 3 次，Lv.4（上限 6）时得 2 次）。
    /// </summary>
    private static SkillEffect Interpolate(SkillRange range, int level, int maxLevel)
    {
        float t = maxLevel > PetLevelMgr.DefaultLevel
            ? Mathf.Clamp01((level - PetLevelMgr.DefaultLevel) / (float)(maxLevel - PetLevelMgr.DefaultLevel))
            : 0f;

        return new SkillEffect
        {
            winScaleMul = Mathf.Lerp(range.lv1.winScaleMul, range.max.winScaleMul, t),
            rotateScaleMul = Mathf.Lerp(range.lv1.rotateScaleMul, range.max.rotateScaleMul, t),
            winMoveSpeedMul = Mathf.Lerp(range.lv1.winMoveSpeedMul, range.max.winMoveSpeedMul, t),
            playerColliderMul = Mathf.Lerp(range.lv1.playerColliderMul, range.max.playerColliderMul, t),
            rewardBonus = Mathf.Lerp(range.lv1.rewardBonus, range.max.rewardBonus, t),

            // 次数类插值后取整
            feedTargetDelta = LerpInt(range.lv1.feedTargetDelta, range.max.feedTargetDelta, t),
            removeObstacleCount = LerpInt(range.lv1.removeObstacleCount, range.max.removeObstacleCount, t),
            shieldCount = LerpInt(range.lv1.shieldCount, range.max.shieldCount, t),
            pauseCount = LerpInt(range.lv1.pauseCount, range.max.pauseCount, t),
            adSkipCount = LerpInt(range.lv1.adSkipCount, range.max.adSkipCount, t),

            // 开关类不随等级变化
            forceSameDirection = range.lv1.forceSameDirection,
            disableRandomAngle = range.lv1.disableRandomAngle,
            shortAdUnit = range.lv1.shortAdUnit,
        };
    }

    private static int LerpInt(int from, int to, float t) => Mathf.RoundToInt(Mathf.Lerp(from, to, t));

    // ==================== A. 关卡参数类 ====================

    /// <summary>
    /// 把生效技能套用到关卡数据上，返回一份「本局生效」的副本（原配置不动）。
    /// 没有技能时直接返回原对象，避免无谓的拷贝。
    /// </summary>
    public static LevelData ApplyToLevel(LevelData source)
    {
        if (source == null) return null;
        if (activeSkills.Count == 0) return source;

        SkillEffect e = Combined();
        LevelData data = source.Clone();

        data.winScale *= e.winScaleMul;
        data.rotateScale *= e.rotateScaleMul;
        data.winMoveSpeed *= e.winMoveSpeedMul;
        data.feedTargetAdjust += e.feedTargetDelta;

        if (e.forceSameDirection) data.mixedDirection = false;
        if (e.disableRandomAngle) data.randomAngle = false;

        // 技能「桃桃拆家」：随机停用若干台转盘，保底至少留 1 台
        if (e.removeObstacleCount > 0 && data.obstacles != null && data.obstacles.Length > 1)
        {
            int remove = Mathf.Min(e.removeObstacleCount, data.obstacles.Length - 1);
            data.obstacles = RemoveRandom(data.obstacles, remove);
        }

        // 注意：这里打的是「关卡最终参数」，不是技能倍率（比如第 2 关本身 rotateScale 就是 0.8）
        DebugMgr.Instance.Log($"{LogTag} 关卡 {data.level} 应用技能 [{string.Join(",", activeSkills)}] → 关卡最终参数：" +
                  $"目标×{data.winScale:0.##} 转速×{data.rotateScale:0.##} 漂移×{data.winMoveSpeed:0.##} " +
                  $"投喂{data.FeedTarget}次 转盘{data.obstacles?.Length}");

        return data;
    }

    /// <summary>随机去掉 <paramref name="count"/> 台转盘（调用方保证 count 不会把转盘全去掉）</summary>
    private static int[] RemoveRandom(int[] source, int count)
    {
        count = Mathf.Clamp(count, 1, source.Length - 1);

        // 洗牌后取前 count 个下标作为要停用的转盘
        List<int> indexes = new List<int>(source.Length);
        for (int i = 0; i < source.Length; i++) indexes.Add(i);

        for (int i = 0; i < count; i++)
        {
            int swap = Random.Range(i, indexes.Count);
            (indexes[i], indexes[swap]) = (indexes[swap], indexes[i]);
        }

        HashSet<int> disabled = new HashSet<int>();
        for (int i = 0; i < count; i++) disabled.Add(indexes[i]);

        List<int> result = new List<int>(source.Length - count);
        for (int i = 0; i < source.Length; i++)
        {
            if (disabled.Contains(i)) continue;

            result.Add(source[i]);
        }

        return result.ToArray();
    }

    // ==================== 出战宠物存档 ====================

    /// <summary>当前出战的宠物 id（0 = 没有出战）</summary>
    public static int BattlePetId { get; private set; }

    /// <summary>
    /// 设置出战宠物并存档：内部取它的 SkillID 生效，下次启动会自动恢复。
    /// 想直接传技能 ID 用 SetSkill()，那不会被存档。
    /// </summary>
    public static void SetBattlePet(int petId)
    {
        BattlePetId = Mathf.Max(0, petId);
        StorageMgr.SetInt(GameData.BattlePetIdKey, BattlePetId);

        if (BattlePetId > 0) SetSkillByPet(BattlePetId);
        else ClearSkills();

        // 换宠物后立刻刷新暂停 / 护盾 / 免广告次数：
        // 这些次数是布置关卡时的快照，不刷新的话「游戏中途换宠」这一关的可用次数还是旧值（常见症状：暂停按钮一直是灰的）
        ResetLevelRuntime();

        DebugMgr.Instance.Log($"{LogTag} 出战宠物 {BattlePetId}，生效技能 [{string.Join(",", activeSkills)}]，" +
                  $"暂停次数 {PauseCount}");
    }

    /// <summary>启动时调用：按存档恢复出战宠物及其技能</summary>
    public static void RestoreBattlePet()
    {
        int petId = StorageMgr.GetInt(GameData.BattlePetIdKey, 0);
        BattlePetId = petId;

        if (petId > 0)
        {
            SetSkillByPet(petId);
        }
        else
        {
            ClearSkills();
        }

        ResetLevelRuntime(); // 同上：把次数刷新到「刚进游戏」的初始值
        DebugMgr.Instance.Log($"{LogTag} 恢复出战宠物 {petId}，生效技能 [{string.Join(",", activeSkills)}]，暂停次数 {PauseCount}");
    }

    // ==================== B. 运行时类 ====================

    /// <summary>本关剩余护盾次数（撞机关不判失败）</summary>
    public static int ShieldCount { get; private set; }

    /// <summary>本关剩余暂停次数</summary>
    public static int PauseCount { get; private set; }

    /// <summary>本关剩余免费复活次数</summary>
    public static int AdSkipCount { get; private set; }

    /// <summary>本关是否还能免费复活一次（失败结算面板用它决定按钮文案）</summary>
    public static bool CanUseAdSkip => AdSkipCount > 0;

    /// <summary>
    /// 通关货币奖励倍率（1 = 原样，2 = 翻倍）。
    /// 除技能加成外，还叠加出战宠物的等级加成（每级 +3%，见 PetLevelMgr.CoinBonusPerLevel）。
    /// </summary>
    public static float RewardMultiplier => 1f + Combined().rewardBonus + PetLevelMgr.BattlePetCoinBonus;

    /// <summary>本次播放广告是否使用短时长广告位（技能「广告时长减少 50%」）</summary>
    public static bool UseShortAdUnit => Combined().shortAdUnit;

    /// <summary>小球碰撞判定缩放（0.85 = 缩小 15%）</summary>
    public static float PlayerColliderScale => Combined().playerColliderMul;

    /// <summary>每关布置（以及换出战宠物）时重置消耗类次数：LevelManager.ApplyLevel 里调用</summary>
    public static void ResetLevelRuntime()
    {
        SkillEffect e = Combined();
        ShieldCount = e.shieldCount;
        PauseCount = e.pauseCount;
        AdSkipCount = e.adSkipCount;

        DebugMgr.Instance.Log($"{LogTag} 运行时次数刷新：护盾 {ShieldCount}，暂停 {PauseCount}，免广告 {AdSkipCount}" +
                  $"（生效技能 [{string.Join(",", activeSkills)}]）");
    }

    /// <summary>撞到机关时调用：还有护盾就消耗一层并返回 true（调用方让小球弹回继续）</summary>
    public static bool TryUseShield()
    {
        if (ShieldCount <= 0) return false;

        ShieldCount--;
        DebugMgr.Instance.Log($"{LogTag} 护盾抵消一次失败，剩余 {ShieldCount}");
        return true;
    }

    /// <summary>是否可以暂停（还有次数且小球正在移动时才用）</summary>
    public static bool CanPause => PauseCount > 0;

    /// <summary>暂停一次：成功返回 true</summary>
    public static bool TryUsePause()
    {
        if (PauseCount <= 0) return false;

        PauseCount--;
        DebugMgr.Instance.Log($"{LogTag} 小球暂停，本关剩余 {PauseCount} 次");
        return true;
    }

    /// <summary>失败后点「继续」时调用：还有免费复活次数就直接复活，不播广告</summary>
    public static bool TryUseAdSkip()
    {
        if (AdSkipCount <= 0) return false;

        AdSkipCount--;
        DebugMgr.Instance.Log($"{LogTag} 免广告复活，本关剩余 {AdSkipCount} 次");
        return true;
    }
}
