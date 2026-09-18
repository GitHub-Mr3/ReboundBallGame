using System;
using UnityEngine;

/// <summary>
/// 单关配置。
/// obstacles 里的数字是 RandomWallGroup.WallGroup 列表的下标：
/// 0=Wall_1、1=Wall_2、2=Wall_3、3=Wall_4、4=Wall_5
/// </summary>
[Serializable]
public class LevelData
{
    [Header("关卡信息")] public int level = 1;
    public string levelName = "";
    public string tip = "";

    [Header("旋转机关")]
    [Tooltip("本关启用的转盘下标")] public int[] obstacles = { 0 };

    [Tooltip("转速倍率：在场景初始转速基础上放大")] public float rotateScale = 1f;

    [Tooltip("转盘初始角度随机（每次进入关卡重新随机一次）")] public bool randomAngle = false;

    [Tooltip("部分转盘反向旋转，形成交叉的旋转趋势")] public bool mixedDirection = false;

    [Header("目标点位")]
    [Tooltip("目标点位大小倍率，越小越难点中")] public float winScale = 1f;

    [Tooltip("目标点位是否漂移")] public bool winMoving = false;

    [Tooltip("目标点位漂移速度")] public float winMoveSpeed = 5f;

    [Header("小球")] [Tooltip("小球移动速度")] public float playerSpeed = 5f;
}

/// <summary>
/// 关卡总表。
/// 前 12 关为手工设计，难度沿「转盘数量 → 转速 → 随机初始角 → 目标漂移 → 目标变小 → 小球提速」
/// 六个维度逐关叠加；通关第 12 关后进入无尽模式，难度继续按公式上涨。
/// </summary>
public static class LevelConfig
{
    /// <summary>手工设计关卡数量</summary>
    public const int DesignedLevelCount = 12;

    private static readonly LevelData[] Designed = new LevelData[]
    {
        // ---------------- 第 1 关：教学，单转盘、大目标、慢球 ----------------
        new LevelData
        {
            level = 1, levelName = "开胃小点", tip = "一个转盘，慢慢瞄准",
            obstacles = new[] { 0 }, rotateScale = 0.6f, randomAngle = false, mixedDirection = false,
            winScale = 1.4f, winMoving = false, winMoveSpeed = 0f, playerSpeed = 4f,
        },
        // ---------------- 第 2 关：两台转盘 ----------------
        new LevelData
        {
            level = 2, levelName = "左右翻烤", tip = "两台转盘反向转",
            obstacles = new[] { 0, 1 }, rotateScale = 0.8f, randomAngle = false, mixedDirection = false,
            winScale = 1.2f, winMoving = false, winMoveSpeed = 0f, playerSpeed = 4.2f,
        },
        // ---------------- 第 3 关：三台转盘，恢复原始转速 ----------------
        new LevelData
        {
            level = 3, levelName = "三味拼盘", tip = "三台转盘同时工作",
            obstacles = new[] { 0, 1, 2 }, rotateScale = 1f, randomAngle = false, mixedDirection = false,
            winScale = 1f, winMoving = false, winMoveSpeed = 0f, playerSpeed = 4.5f,
        },
        // ---------------- 第 4 关：初始角度随机，落点不可预演 ----------------
        new LevelData
        {
            level = 4, levelName = "迷踪轨迹", tip = "转盘初始角度随机",
            obstacles = new[] { 1, 2, 3 }, rotateScale = 1.2f, randomAngle = true, mixedDirection = false,
            winScale = 0.95f, winMoving = false, winMoveSpeed = 0f, playerSpeed = 4.8f,
        },
        // ---------------- 第 5 关：目标点位开始漂移 ----------------
        new LevelData
        {
            level = 5, levelName = "餐桌漂移", tip = "目标点位开始移动",
            obstacles = new[] { 0, 1, 2, 3 }, rotateScale = 1.35f, randomAngle = false, mixedDirection = true,
            winScale = 0.9f, winMoving = true, winMoveSpeed = 1.5f, playerSpeed = 5f,
        },
        // ---------------- 第 6 关：五台转盘全开 ----------------
        new LevelData
        {
            level = 6, levelName = "五盘轮转", tip = "五台转盘全开",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 1.5f, randomAngle = true, mixedDirection = false,
            winScale = 0.85f, winMoving = true, winMoveSpeed = 2.2f, playerSpeed = 5.2f,
        },
        // ---------------- 第 7 关：慢—快—慢组合，节奏被打乱 ----------------
        new LevelData
        {
            level = 7, levelName = "快慢双锅", tip = "快慢交替，节奏难抓",
            obstacles = new[] { 0, 2, 4 }, rotateScale = 1.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.8f, winMoving = true, winMoveSpeed = 3f, playerSpeed = 5.5f,
        },
        // ---------------- 第 8 关：四台转盘，安全缝隙变窄 ----------------
        new LevelData
        {
            level = 8, levelName = "四壁拦路", tip = "只剩一条安全缝隙",
            obstacles = new[] { 1, 2, 3, 4 }, rotateScale = 1.9f, randomAngle = true, mixedDirection = true,
            winScale = 0.78f, winMoving = true, winMoveSpeed = 3.6f, playerSpeed = 5.8f,
        },
        // ---------------- 第 9 关：全场提速 ----------------
        new LevelData
        {
            level = 9, levelName = "旋涡厨房", tip = "全场旋转提速",
            obstacles = new[] { 0, 1, 3, 4 }, rotateScale = 2.1f, randomAngle = true, mixedDirection = true,
            winScale = 0.75f, winMoving = true, winMoveSpeed = 4.2f, playerSpeed = 6f,
        },
        // ---------------- 第 10 关：少而极快，考验起手时机 ----------------
        new LevelData
        {
            level = 10, levelName = "两极灶火", tip = "高速反向，找准时机",
            obstacles = new[] { 0, 2, 3 }, rotateScale = 2.4f, randomAngle = true, mixedDirection = true,
            winScale = 0.72f, winMoving = true, winMoveSpeed = 4.8f, playerSpeed = 6.2f,
        },
        // ---------------- 第 11 关：五台满速 ----------------
        new LevelData
        {
            level = 11, levelName = "刀山火海", tip = "五台转盘高速运转",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 2.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.7f, winMoving = true, winMoveSpeed = 5.4f, playerSpeed = 6.5f,
        },
        // ---------------- 第 12 关：终极，目标高速漂移 + 目标最小 ----------------
        new LevelData
        {
            level = 12, levelName = "终极盛宴", tip = "高速漂移 + 满速转盘",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 3f, randomAngle = true, mixedDirection = true,
            winScale = 0.65f, winMoving = true, winMoveSpeed = 6f, playerSpeed = 7f,
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
    /// </summary>
    private static LevelData BuildEndlessLevel(int levelNumber)
    {
        int over = levelNumber - DesignedLevelCount;

        return new LevelData
        {
            level = levelNumber,
            levelName = "无尽厨房",
            tip = "难度持续上升，没有尽头",
            obstacles = new[] { 0, 1, 2, 3, 4 },
            rotateScale = Mathf.Min(6f, 3f + 0.2f * over),
            randomAngle = true,
            mixedDirection = true,
            winScale = Mathf.Max(0.5f, 0.65f - 0.02f * over),
            winMoving = true,
            winMoveSpeed = Mathf.Min(12f, 6f + 0.5f * over),
            playerSpeed = Mathf.Min(9f, 7f + 0.15f * over),
        };
    }
}
