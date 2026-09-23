using System;
using UnityEngine;

/// <summary>
/// 单关配置。
/// obstacles 里的数字是 RandomWallGroup.WallGroup 列表的下标：
/// 0=Wall_1、1=Wall_2、2=Wall_3、3=Wall_4、4=Wall_5
/// winSpriteIndex 是 LevelConfig.WinSprites（场景里 WinSpriteLibrary 组件拖入的数组）的下标。
/// winMoveSpeed &lt;= 0 表示目标点位静止（停在漂移点中心），&gt; 0 表示漂移。
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

    [Tooltip("目标点位漂移速度：> 0 漂移，<= 0 停在漂移点中心不动")]
    public float winMoveSpeed = 0f;

    [Tooltip("目标点位图片下标：对应 LevelConfig.WinSprites；下标超出贴图数量时取最后一张；填 -1 表示不换图，保持场景里原本的贴图")]
    public int winSpriteIndex = 0;

    [Header("小球")] [Tooltip("小球移动速度")] public float playerSpeed = 5f;
}

/// <summary>
/// 关卡总表。
/// 前 12 关为手工设计，难度沿「转盘数量 → 转速 → 随机初始角 → 目标漂移 → 目标变小 → 小球提速」
/// 六个维度逐关叠加；通关第 12 关后进入无尽模式，难度继续按公式上涨。
/// 每关的目标点位图片由 winSpriteIndex 指定（下标对应 WinSpriteLibrary 拖入的 Sprite 数组）。
/// 目标点位是否漂移由 winMoveSpeed 决定（&lt;= 0 静止）。
/// </summary>
public static class LevelConfig
{
    /// <summary>手工设计关卡数量</summary>
    public const int DesignedLevelCount = 12;

    /// <summary>
    /// 目标点位贴图库：LevelData.winSpriteIndex 就是这里的下标。
    /// 由场景里的 WinSpriteLibrary 组件在 Awake 时注册，也可以在代码里直接赋值（需在首次布置关卡之前）。
    /// </summary>
    public static Sprite[] WinSprites = new Sprite[0];

    /// <summary>注册贴图库（传 null 或空数组时忽略）</summary>
    public static void SetWinSprites(Sprite[] sprites)
    {
        if (sprites != null && sprites.Length > 0) WinSprites = sprites;
    }

    /// <summary>
    /// 取贴图：下标为负或库为空时返回 null（表示不改 win 的贴图）；
    /// 下标超出贴图数量时取最后一张，保证关卡配置填大了也不会越界。
    /// </summary>
    public static Sprite GetWinSprite(int index)
    {
        if (index < 0 || WinSprites == null || WinSprites.Length == 0) return null;

        // 超出范围一律夹到最后一张
        int i = Mathf.Clamp(index, 0, WinSprites.Length - 1);

        Sprite sprite = WinSprites[i];
        if (sprite == null)
        {
            Debug.LogWarning($"[LevelConfig] 目标点位贴图库第 {i} 项为空");
        }

        return sprite;
    }

    private static readonly LevelData[] Designed = new LevelData[]
    {
        // ---------------- 第 1 关：教学，单转盘、大目标、慢球 ----------------
        new LevelData
        {
            level = 1, levelName = "开胃小点", tip = "一个转盘，慢慢瞄准",
            obstacles = new[] { 0 }, rotateScale = 0.6f, randomAngle = false, mixedDirection = false,
            winScale = 1.4f, winMoveSpeed = 0f, winSpriteIndex = 0, playerSpeed = 4f,
        },
        // ---------------- 第 2 关：两台转盘 ----------------
        new LevelData
        {
            level = 2, levelName = "左右翻烤", tip = "两台转盘反向转",
            obstacles = new[] { 0, 1 }, rotateScale = 0.8f, randomAngle = false, mixedDirection = false,
            winScale = 1.2f, winMoveSpeed = 0f, winSpriteIndex = 1, playerSpeed = 4.2f,
        },
        // ---------------- 第 3 关：三台转盘，恢复原始转速 ----------------
        new LevelData
        {
            level = 3, levelName = "三味拼盘", tip = "三台转盘同时工作",
            obstacles = new[] { 0, 1, 2 }, rotateScale = 1f, randomAngle = false, mixedDirection = false,
            winScale = 1f, winMoveSpeed = 0f, winSpriteIndex = 2, playerSpeed = 4.5f,
        },
        // ---------------- 第 4 关：初始角度随机，落点不可预演 ----------------
        new LevelData
        {
            level = 4, levelName = "迷踪轨迹", tip = "转盘初始角度随机",
            obstacles = new[] { 1, 2, 3 }, rotateScale = 1.2f, randomAngle = true, mixedDirection = false,
            winScale = 0.95f, winMoveSpeed = 0f, winSpriteIndex = 3, playerSpeed = 4.8f,
        },
        // ---------------- 第 5 关：目标点位开始漂移 ----------------
        new LevelData
        {
            level = 5, levelName = "餐桌漂移", tip = "目标点位开始移动",
            obstacles = new[] { 0, 1, 2, 3 }, rotateScale = 1.35f, randomAngle = false, mixedDirection = true,
            winScale = 0.9f, winMoveSpeed = 1.5f, winSpriteIndex = 4, playerSpeed = 5f,
        },
        // ---------------- 第 6 关：五台转盘全开 ----------------
        new LevelData
        {
            level = 6, levelName = "五盘轮转", tip = "五台转盘全开",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 1.5f, randomAngle = true, mixedDirection = false,
            winScale = 0.85f, winMoveSpeed = 2.2f, winSpriteIndex = 5, playerSpeed = 5.2f,
        },
        // ---------------- 第 7 关：慢—快—慢组合，节奏被打乱 ----------------
        new LevelData
        {
            level = 7, levelName = "快慢双锅", tip = "快慢交替，节奏难抓",
            obstacles = new[] { 0, 2, 4 }, rotateScale = 1.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.8f, winMoveSpeed = 3f, winSpriteIndex = 6, playerSpeed = 5.5f,
        },
        // ---------------- 第 8 关：四台转盘，安全缝隙变窄 ----------------
        new LevelData
        {
            level = 8, levelName = "四壁拦路", tip = "只剩一条安全缝隙",
            obstacles = new[] { 1, 2, 3, 4 }, rotateScale = 1.9f, randomAngle = true, mixedDirection = true,
            winScale = 0.78f, winMoveSpeed = 3.6f, winSpriteIndex = 7, playerSpeed = 5.8f,
        },
        // ---------------- 第 9 关：全场提速 ----------------
        new LevelData
        {
            level = 9, levelName = "旋涡厨房", tip = "全场旋转提速",
            obstacles = new[] { 0, 1, 3, 4 }, rotateScale = 2.1f, randomAngle = true, mixedDirection = true,
            winScale = 0.75f, winMoveSpeed = 4.2f, winSpriteIndex = 8, playerSpeed = 6f,
        },
        // ---------------- 第 10 关：少而极快，考验起手时机 ----------------
        new LevelData
        {
            level = 10, levelName = "两极灶火", tip = "高速反向，找准时机",
            obstacles = new[] { 0, 2, 3 }, rotateScale = 2.4f, randomAngle = true, mixedDirection = true,
            winScale = 0.72f, winMoveSpeed = 4.8f, winSpriteIndex = 9, playerSpeed = 6.2f,
        },
        // ---------------- 第 11 关：五台满速 ----------------
        new LevelData
        {
            level = 11, levelName = "刀山火海", tip = "五台转盘高速运转",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 2.7f, randomAngle = true, mixedDirection = true,
            winScale = 0.7f, winMoveSpeed = 5.4f, winSpriteIndex = 10, playerSpeed = 6.5f,
        },
        // ---------------- 第 12 关：终极，目标高速漂移 + 目标最小 ----------------
        new LevelData
        {
            level = 12, levelName = "终极盛宴", tip = "高速漂移 + 满速转盘",
            obstacles = new[] { 0, 1, 2, 3, 4 }, rotateScale = 3f, randomAngle = true, mixedDirection = true,
            winScale = 0.65f, winMoveSpeed = 6f, winSpriteIndex = 11, playerSpeed = 7f,
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
            winMoveSpeed = Mathf.Min(12f, 6f + 0.5f * over),
            // 接着设计关卡继续往后取，超出贴图数量时停在最后一张
            winSpriteIndex = DesignedLevelCount + over,
            playerSpeed = Mathf.Min(9f, 7f + 0.15f * over),
        };
    }
}
