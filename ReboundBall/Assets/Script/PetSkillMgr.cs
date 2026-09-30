using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物技能系统：传入技能 ID（GameData._petSkillDatas 里的 SkillID），技能效果自动生效。
///
/// 用法：
///     PetSkillMgr.SetSkill(7);                 // 出战一只宠物：只生效它的技能
///     PetSkillMgr.SetSkillByPet(13);           // 直接传宠物 id，内部取它的 SkillID
///     PetSkillMgr.AddSkill(2);                 // 多只同时生效（叠加）
///     PetSkillMgr.ClearSkills();               // 清空
///     PetSkillMgr.HasSkill(7);                 // 是否生效中
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
    /// 全部按 GameData._petSkillDatas 里的 SkillDesc 描述实现。
    /// </summary>
    private struct SkillEffect
    {
        /// <summary>目标点位大小乘区（1.15 = 判定范围 +15%）</summary>
        public float winScaleMul;

        /// <summary>转盘转速乘区（0.85 = 转速 -15%）</summary>
        public float rotateScaleMul;

        /// <summary>目标漂移速度乘区（0.6 = 漂移速度 -40%）</summary>
        public float winMoveSpeedMul;

        /// <summary>通关所需投喂次数的增减（-1 = 少投一次）</summary>
        public int feedTargetDelta;

        /// <summary>每关随机停用 1 台转盘</summary>
        public bool removeOneObstacle;

        /// <summary>所有转盘强制同向旋转</summary>
        public bool forceSameDirection;

        /// <summary>取消随机初始角，同一套布局</summary>
        public bool disableRandomAngle;

        /// <summary>每关失败免疫次数（撞到机关不判失败，弹回继续）</summary>
        public int shieldCount;

        /// <summary>每关可暂停次数（小球停下，再点继续沿原方向走）</summary>
        public int pauseCount;

        /// <summary>通关货币奖励的额外倍率（1 = 奖励增加 100%，即 ×2）</summary>
        public float rewardBonus;

        /// <summary>小球碰撞判定乘区（0.85 = 缩小 15%）</summary>
        public float playerColliderMul;

        /// <summary>每关免看广告直接复活的次数</summary>
        public int adSkipCount;

        /// <summary>是否改用短时长激励视频广告位（AdManager 会据此切换 adUnitId）</summary>
        public bool shortAdUnit;
    }

    private static SkillEffect NewEffect()
    {
        return new SkillEffect
        {
            winScaleMul = 1f,
            rotateScaleMul = 1f,
            winMoveSpeedMul = 1f,
            playerColliderMul = 1f
        };
    }

    /// <summary>技能 ID -> 效果（与 GameData._petSkillDatas 一一对应）</summary>
    private static readonly Dictionary<int, SkillEffect> skillTable = new Dictionary<int, SkillEffect>
    {
        // 1 烈焰之瞳：目标判定范围 +15%
        [1] = new SkillEffect { winScaleMul = 1.15f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f },

        // 2 雪球减速：所有转盘转速 -15%
        [2] = new SkillEffect { winScaleMul = 1f, rotateScaleMul = 0.85f, winMoveSpeedMul = 1f, playerColliderMul = 1f },

        // 3 暖阳锁轨：目标漂移速度 -40%
        [3] = new SkillEffect { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 0.6f, playerColliderMul = 1f },

        // 4 星夜疾行：每关免费复活一次
        //    本关第一次失败后点「继续」可以直接复活，不用看广告；
        //    再次失败才回到「看广告复活」，换关卡 / 重玩时重置。
        [4] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, adSkipCount = 1 },

        // 5 青芜饱腹：本关通关所需投喂次数 -1
        [5] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, feedTargetDelta = -1 },

        // 6 桃桃拆家：每关随机停用 1 台转盘
        [6] = new SkillEffect
        {
            winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, removeOneObstacle = true
        },

        // 7 黄巾护体：首次撞到机关不判失败，弹回
        [7] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, shieldCount = 1 },

        // 8 赤岩同心：所有转盘强制同向旋转
        [8] = new SkillEffect
        {
            winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f,
            forceSameDirection = true
        },

        // 9 紫缨定局：取消随机初始角，同一套布局
        [9] = new SkillEffect
        {
            winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f,
            disableRandomAngle = true
        },

        // 10 灰砾丰饶：获得的奖励增加 100%
        [10] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, rewardBonus = 1f },

        // 11 彩斑穿针：小球碰撞判定缩小 15%
        [11] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 0.85f },

        // 12 圆墩暂歇：小球可暂停一次，再点继续
        [12] = new SkillEffect
            { winScaleMul = 1f, rotateScaleMul = 1f, winMoveSpeedMul = 1f, playerColliderMul = 1f, pauseCount = 1 },

        // 13 旺财三重奏：转速 -20% + 目标判定 +12% + 每关 2 次失败免疫
        [13] = new SkillEffect
        {
            winScaleMul = 1.12f, rotateScaleMul = 0.8f, winMoveSpeedMul = 1f, playerColliderMul = 1f, shieldCount = 2
        },
    };

    // ==================== 生效中的技能 ====================

    private static readonly List<int> activeSkills = new List<int>();

    /// <summary>设置生效技能（覆盖式）：一般就是出战那只宠物的技能</summary>
    public static void SetSkill(int skillId)
    {
        activeSkills.Clear();
        if (skillId > 0) activeSkills.Add(skillId);
    }

    /// <summary>按宠物 id 设置生效技能：内部取该宠物的 SkillID</summary>
    public static void SetSkillByPet(int petId)
    {
        SetSkill(GameData.GetPetData(petId).SkillID);
    }

    /// <summary>再叠加一个技能（多只宠物同时生效时用）</summary>
    public static void AddSkill(int skillId)
    {
        if (skillId <= 0 || activeSkills.Contains(skillId)) return;

        activeSkills.Add(skillId);
    }

    /// <summary>批量覆盖式设置</summary>
    public static void SetSkills(IEnumerable<int> skillIds)
    {
        activeSkills.Clear();
        if (skillIds == null) return;

        foreach (int id in skillIds)
        {
            if (id > 0 && !activeSkills.Contains(id)) activeSkills.Add(id);
        }
    }

    /// <summary>移除某个技能</summary>
    public static void RemoveSkill(int skillId)
    {
        activeSkills.Remove(skillId);
    }

    /// <summary>清空全部技能（没有宠物出战时的状态）</summary>
    public static void ClearSkills()
    {
        activeSkills.Clear();
    }

    public static bool HasSkill(int skillId)
    {
        return activeSkills.Contains(skillId);
    }

    /// <summary>当前生效的技能 ID 列表（只读）</summary>
    public static IReadOnlyList<int> ActiveSkills => activeSkills;

    /// <summary>合并所有生效技能的效果</summary>
    private static SkillEffect Combined()
    {
        SkillEffect result = NewEffect();

        for (int i = 0; i < activeSkills.Count; i++)
        {
            if (!skillTable.TryGetValue(activeSkills[i], out SkillEffect e)) continue;

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
            result.removeOneObstacle |= e.removeOneObstacle;
            result.forceSameDirection |= e.forceSameDirection;
            result.disableRandomAngle |= e.disableRandomAngle;
        }

        return result;
    }

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

        if (e.removeOneObstacle && data.obstacles != null && data.obstacles.Length > 1)
        {
            data.obstacles = RemoveOneAtRandom(data.obstacles);
        }

        // 注意：这里打的是「关卡最终参数」，不是技能倍率（比如第 2 关本身 rotateScale 就是 0.8）
        DebugMgr.Instance.Log($"{LogTag} 关卡 {data.level} 应用技能 [{string.Join(",", activeSkills)}] → 关卡最终参数：" +
                  $"目标×{data.winScale:0.##} 转速×{data.rotateScale:0.##} 漂移×{data.winMoveSpeed:0.##} " +
                  $"投喂{data.FeedTarget}次 转盘{data.obstacles?.Length}");

        return data;
    }

    /// <summary>随机去掉一台转盘（至少保留 1 台）</summary>
    private static int[] RemoveOneAtRandom(int[] source)
    {
        int remove = Random.Range(0, source.Length);
        int[] result = new int[source.Length - 1];
        for (int i = 0, j = 0; i < source.Length; i++)
        {
            if (i == remove) continue;

            result[j++] = source[i];
        }

        return result;
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

    /// <summary>通关货币奖励倍率（1 = 原样，2 = 翻倍）</summary>
    public static float RewardMultiplier => 1f + Combined().rewardBonus;

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
