 using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单关配置。
/// obstacles 里的数字是 RandomWallGroup.WallGroup 列表的下标：
/// 0=Wall_1、1=Wall_2、2=Wall_3、3=Wall_4、4=Wall_5
/// winSpriteIndex 是 LevelConfig.WinSprites（场景里 WinSpriteLibrary 组件拖入的数组）的下标。
/// winMoveSpeed &lt;= 0 表示目标点位静止（停在漂移点中心），&gt; 0 表示漂移。
/// feedStages 决定本关需要「投喂」几次才算通关：列表里每一项就是投中第 N 次后的进度状态，
/// 列表长度 = 通关所需投喂次数（例如 3 项 = 命中目标 3 次才通关）。列表为空时按 1 次计算。
/// 世界观：玩家给流浪小狗投喂食物，一关一只狗；每条文案 = 投喂进度 + 小狗好感度，
/// 投完最后一条好感拉满 → 小狗被认领 → 解锁对应狗狗图鉴。
/// </summary>
[Serializable]
public class LevelData
{
    [Header("关卡信息")]
    [Tooltip("本关要投喂的小狗名（品种·昵称），也是通关后解锁的图鉴条目名")]
    public int level = 1;
    public string levelName = "";

    [Header("投喂进度")]
    [Tooltip("每投中目标点位一次（投喂一次）对应的进度状态，下标 0 = 第一次投中后的状态；列表长度就是通关所需投喂次数，为空按 1 次算。文案保持简短：进度 + 好感度 + 小狗反应，最后一条写成好感拉满、认领并解锁图鉴")]
    public List<string> feedStages = new List<string> { "好感 100% · 吃饱啦" };

    /// <summary>
    /// 通关所需投喂次数 = feedStages 的长度 + feedTargetAdjust（列表为空时按 1 次算，最终结果最少 1 次）。
    /// feedTargetAdjust 由宠物技能在运行时填写（例如「投喂次数 -1」），配置表本身不用管。
    /// </summary>
    public int FeedTarget =>
        Mathf.Max(1, (feedStages != null && feedStages.Count > 0 ? feedStages.Count : 1) + feedTargetAdjust);

    /// <summary>投喂次数的运行时增减（负数 = 减少）：由 PetSkillMgr 在布置关卡时填，配置表不用填</summary>
    [HideInInspector] public int feedTargetAdjust = 0;

    /// <summary>
    /// 深拷贝一份关卡数据：技能系统要在本局生效的副本上改参数，不能污染静态配置表。
    /// feedStages / obstacles 会新建容器，改副本不会影响原对象。
    /// </summary>
    public LevelData Clone()
    {
        LevelData copy = (LevelData)MemberwiseClone();

        if (feedStages != null) copy.feedStages = new List<string>(feedStages);
        if (obstacles != null) copy.obstacles = (int[])obstacles.Clone();

        return copy;
    }

    /// <summary>取第 <paramref name="feedIndex"/> 次投喂（从 1 开始）后的进度文案；越界取最近的一项</summary>
    public string GetFeedStage(int feedIndex)
    {
        if (feedStages == null || feedStages.Count == 0) return "";
        int i = Mathf.Clamp(feedIndex - 1, 0, feedStages.Count - 1);
        return feedStages[i] ?? "";
    }

    [Header("旋转机关")]
    [Tooltip("本关启用的转盘下标")] public int[] obstacles = { 0 };

    [Tooltip("转速倍率：在场景初始转速基础上放大")] public float rotateScale = 1f;

    [Tooltip("转盘初始角度随机（每次进入关卡重新随机一次）")] public bool randomAngle = false;

    [Tooltip("部分转盘反向旋转，形成交叉的旋转趋势")] public bool mixedDirection = false;

    [Header("目标点位")]
    [Tooltip("目标点位大小倍率，越小越难点中")] public float winScale = 1f;

    [Tooltip("目标点位漂移速度：> 0 漂移，<= 0 停在漂移点中心不动")]
    public float winMoveSpeed = 0f;

    [Tooltip("目标点位图片下标：对应 LevelConfig.WinSprites；下标超出贴图数量时取最后一张；填 -1 表示不换图，保持场景里原本的贴图")]
    public int winSpriteIndex = 0;

    [Header("小球")][Tooltip("小球移动速度")] public float playerSpeed = 5f;

    [Header("奖励")]
    [Tooltip("本关通关发放的货币数量；<= 0 时按关卡号自动推导（第 1 关 10 枚，每往后一关 +5）")]
    public int coinReward = 0;

    /// <summary>本关通关实际发放的货币数量：没配 coinReward 时按关卡号自动推导</summary>
    public int CoinReward => coinReward > 0 ? coinReward : GetDefaultCoinReward(level);

    /// <summary>按关卡号推导的默认通关奖励（第 1 关 10 枚，之后每关 +5）</summary>
    public static int GetDefaultCoinReward(int levelNumber)
    {
        return 10 + Mathf.Max(0, levelNumber - 1) * 5;
    }
}

/// <summary>
/// 关卡总表。
/// 前 12 关为手工设计，难度沿「转盘数量 → 转速 → 随机初始角 → 目标漂移 → 目标变小 → 小球提速」
/// 六个维度逐关叠加；通关第 12 关后进入无尽模式，难度继续按公式上涨。
/// 每关的目标点位图片由 winSpriteIndex 指定（下标对应 WinSpriteLibrary 拖入的 Sprite 数组）。
/// 目标点位是否漂移由 winMoveSpeed 决定（&lt;= 0 静止）。
/// 主题：一关一只流浪小狗，levelName 就是本关的小狗（品种·昵称），也是通关后解锁的图鉴条目名；
/// feedStages 投完最后一条 = 好感拉满 = 小狗被认领 + 解锁图鉴。
/// </summary>
public static class LevelConfig
{
    /// <summary>手工设计关卡数量</summary>
    public const int DesignedLevelCount = 12;


    private static readonly LevelData[] Designed = new LevelData[]
    {
        // ---------------- 第 1 关 · 小土狗豆豆：教学，单转盘、大目标、慢球，投喂 1 次 ----------------
        new LevelData
        {
            level = 1, levelName = $"{GameData.GetLevelName(0)}",
            feedStages = new List<string> { $"好感拉满\n{GameData.GetLevelSortName(0)}跟你回家" },
            obstacles = new[] { 0 }, rotateScale = 0.6f, randomAngle = false, mixedDirection = false,
            winScale = 0.95f, winMoveSpeed = 0f, winSpriteIndex = 0, playerSpeed = 4f, coinReward = 10,
        },
        // ---------------- 第 2 关 · 柯基奶糖：两台转盘，投喂 2 次 ----------------
        new LevelData
        {
            level = 2, levelName = $"{GameData.GetLevelName(1)}",
            feedStages = new List<string> { $"好感 50% · 尾巴摇起来", $"好感拉满\n{GameData.GetLevelSortName(1)}认领 图鉴+1" },
            obstacles = new[] { 0, 1 }, rotateScale = 0.8f, randomAngle = false, mixedDirection = false,
            winScale = 0.9f, winMoveSpeed = 0f, winSpriteIndex = 1, playerSpeed = 4.2f, coinReward = 20
        },
        // ---------------- 第 3 关 · 柴犬团子：三台转盘，恢复原始转速，投喂 2 次 ----------------
        new LevelData
        {
            level = 3, levelName =  $"{GameData.GetLevelName(2)}",
            feedStages = new List<string> { "好感 50% · 团子坐下了", $"好感拉满\n{GameData.GetLevelSortName(2)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 2 }, rotateScale = 1f, randomAngle = false, mixedDirection = false,
            winScale = 0.85f, winMoveSpeed = 0f, winSpriteIndex = 2, playerSpeed = 4.5f, coinReward = 30
        },
        // ---------------- 第 4 关 · 边牧闪电：初始角度随机，落点不可预演，投喂 2 次 ----------------
        new LevelData
        {
            level = 4, levelName =  $"{GameData.GetLevelName(3)}",
            feedStages = new List<string> { "好感 50% · 一口接住了", $"好感拉满\n{GameData.GetLevelSortName(3)}认领 图鉴+1" },
            obstacles = new[] { 1, 2, 3 }, rotateScale = 1.2f, randomAngle = true, mixedDirection = false,
            winScale = 0.8f, winMoveSpeed = 0f, winSpriteIndex = 3, playerSpeed = 4.8f, coinReward = 40,
        },
        // ---------------- 第 5 关 · 金毛布丁：目标点位开始漂移，投喂 3 次 ----------------
        new LevelData
        {
            level = 5, levelName =  $"{GameData.GetLevelName(4)}",
            feedStages = new List<string> { "好感 33% · 慢慢凑近", "好感 67% · 疯狂摇尾巴", $"好感拉满\n{GameData.GetLevelSortName(4)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 2, 3 }, rotateScale = 1.35f, randomAngle = false, mixedDirection = true,
            winScale = 0.75f, winMoveSpeed = 1.5f, winSpriteIndex = 4, playerSpeed = 5f, coinReward = 50,
        },
        // ---------------- 第 6 关 · 拉布拉多可乐：五台转盘全开，投喂 3 次 ----------------
        new LevelData
        {
            level = 6, levelName =  $"{GameData.GetLevelName(5)}",
            feedStages = new List<string> { "好感 33% · 闻了闻食物", "好感 67% · 围着你转圈", $"好感拉满\n{GameData.GetLevelSortName(5)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 1.5f, randomAngle = true, mixedDirection = false,
            winScale = 0.7f, winMoveSpeed = 2.2f, winSpriteIndex = 5, playerSpeed = 5.2f, coinReward = 60,
        },
        // ---------------- 第 7 关 · 萨摩耶雪球：慢—快—慢组合，节奏被打乱，投喂 3 次 ----------------
        new LevelData
        {
            level = 7, levelName =  $"{GameData.GetLevelName(6)}",
            feedStages = new List<string> { "好感 33% · 冲你笑了", "好感 67% · 蹭你的手", $"好感拉满\n{GameData.GetLevelSortName(6)}认领 图鉴+1" },
            obstacles = new[] { 0, 2, 4 }, rotateScale = 1.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.65f, winMoveSpeed = 3f, winSpriteIndex = 6, playerSpeed = 5.5f, coinReward = 70,
        },
        // ---------------- 第 8 关 · 泰迪咖啡：四台转盘，安全缝隙变窄，投喂 4 次 ----------------
        new LevelData
        {
            level = 8, levelName =  $"{GameData.GetLevelName(7)}",
            feedStages = new List<string> { "好感 25% · 抬头看你", "好感 50% · 蹦了两下", "好感 75% · 扑进怀里", $"好感拉满\n{GameData.GetLevelSortName(7)}认领 图鉴+1" },
            obstacles = new[] { 1, 2, 3, 4 }, rotateScale = 1.9f, randomAngle = true, mixedDirection = true,
            winScale = 0.6f, winMoveSpeed = 3.6f, winSpriteIndex = 7, playerSpeed = 5.8f, coinReward = 80,
        },
        // ---------------- 第 9 关 · 哈士奇将军：全场提速，投喂 4 次 ----------------
        new LevelData
        {
            level = 9, levelName =  $"{GameData.GetLevelName(8)}",
            feedStages = new List<string> { "好感 25% · 哼了一声", "好感 50% · 开始撒娇", "好感 75% · 躺下露肚皮", $"好感拉满\n{GameData.GetLevelSortName(8)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 3, 4 }, rotateScale = 2.1f, randomAngle = true, mixedDirection = true,
            winScale = 0.55f, winMoveSpeed = 4.2f, winSpriteIndex = 8, playerSpeed = 6f, coinReward = 90,
        },
        // ---------------- 第 10 关 · 贵宾王子：少而极快，考验起手时机，投喂 4 次 ----------------
        new LevelData
        {
            level = 10, levelName =  $"{GameData.GetLevelName(9)}",
            feedStages = new List<string> { "好感 25% · 歪头看你", "好感 50% · 叼来小球", "好感 75% · 靠着你坐下", $"好感拉满\n{GameData.GetLevelSortName(9)}认领 图鉴+1" },
            obstacles = new[] { 0, 2, 3 }, rotateScale = 2.4f, randomAngle = true, mixedDirection = true,
            winScale = 0.5f, winMoveSpeed = 4.8f, winSpriteIndex = 9, playerSpeed = 6.2f, coinReward = 100,
        },
        // ---------------- 第 11 关 · 斗牛犬坦克：五台满速，投喂 5 次 ----------------
        new LevelData
        {
            level = 11, levelName =  $"{GameData.GetLevelName(10)}",
            feedStages = new List<string> { "好感 20% · 还在瞪你", "好感 40% · 哼哧哼哧", "好感 60% · 尾巴动了", "好感 80% · 翻肚皮啦", $"好感拉满\n{GameData.GetLevelSortName(10)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 2.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.48f, winMoveSpeed = 5.4f, winSpriteIndex = 10, playerSpeed = 6.5f, coinReward = 110,
        },
        // ---------------- 第 12 关 · 神犬旺财：终极，目标高速漂移 + 目标最小，投喂 5 次 ----------------
        new LevelData
        {
            level = 12, levelName =  $"{GameData.GetLevelName(11)}",
            feedStages = new List<string> { "好感 20% · 旺财现身", "好感 40% · 眼神变温柔", "好感 60% · 伸出爪子", "好感 80% · 蹭你裤腿", $"好感拉满\n{GameData.GetLevelSortName(11)}认领 图鉴+1" },
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 3f, randomAngle = true, mixedDirection = true,
            winScale = 0.45f, winMoveSpeed = 6f, winSpriteIndex = 11, playerSpeed = 7f, coinReward = 120,
        },
    };

    /// <summary>取某一关配置（关卡号从 1 开始，超出设计关卡后走无尽模式）</summary>
    public static LevelData GetLevel(int levelNumber)
    {
        if (levelNumber <= 1) return Designed[0];
        if (levelNumber <= DesignedLevelCount) return Designed[levelNumber - 1];
        return BuildEndlessLevel(levelNumber);
    }

    /// <summary>
    /// 无尽模式：通关 12 关后继续使用，难度按公式持续上涨并有上限，避免后期完全不可玩。
    /// 投喂次数也随关数上涨（5 次起步，最多 8 次），保证后期「多投几次才算过」的压力。
    /// </summary>
    private static LevelData BuildEndlessLevel(int levelNumber)
    {
        int over = levelNumber - DesignedLevelCount;

        return new LevelData
        {
            level = levelNumber,
            levelName = $"流浪汪·第 {over} 只",
            feedStages = BuildEndlessFeedStages(Mathf.Min(8, 5 + (over + 1) / 2)),
            obstacles = new[] { 0, 1, 2, 3, 4 },
            rotateScale = Mathf.Min(6f, 3f + 0.2f * over),
            randomAngle = true,
            mixedDirection = true,
            winScale = Mathf.Max(0.5f, 0.65f - 0.02f * over),
            winMoveSpeed = Mathf.Min(12f, 6f + 0.5f * over),
            // 接着设计关卡继续往后取，超出贴图数量时停在最后一张
            winSpriteIndex = DesignedLevelCount + over,
            playerSpeed = Mathf.Min(9f, 7f + 0.15f * over),
            // 无尽模式奖励高于设计关卡，并设上限避免后期无限膨胀
            coinReward = Mathf.Min(200, 130 + 10 * (over - 1)),
        };
    }

    /// <summary>无尽模式的投喂进度文案：按好感度百分比递增，最后一条好感拉满并认领、解锁图鉴</summary>
    private static List<string> BuildEndlessFeedStages(int total)
    {
        var list = new List<string>(total);
        for (int i = 1; i <= total; i++)
        {
            int percent = Mathf.RoundToInt(100f * i / total);
            list.Add(i < total ? $"好感 {percent}% · 又靠近一步" : "好感拉满 · 认领成功");
        }

        return list;
    }
}
