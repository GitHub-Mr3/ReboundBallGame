using System;
using System.Collections.Generic;
using Mr3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// 关卡管理器：把「随机刷机关」替换成「闯关」流程。
/// 规则：
///   1. 每次把食物送进目标点位 → 自动进入下一关；
///   2. 失败时不会掉关，原地重试当前关（布局保持一致，公平重试）；
///   3. 点“重新开始”回到第 1 关并清空分数；
///   4. 通关第 12 关后进入无尽模式，难度继续按公式上涨。
/// 该脚本不需要挂到场景任何物体上，运行时由自身自动创建。
/// </summary>
public class LevelManager : SingleMonoBase<LevelManager>
{
    public const string GameSceneName = "GameScence";
    public const string LevelChangedEvent = "LevelChanged";

    // ==================== 关卡状态 ====================

    /// <summary>当前关卡索引（0 表示第 1 关）</summary>
    private int currentIndex = 0;

    public int CurrentLevelNumber => currentIndex + 1;
    public LevelData CurrentLevel => GetLevel(CurrentLevelNumber);
    public bool IsEndless => currentIndex >= LevelConfig.DesignedLevelCount;

    // ==================== 场景引用（惰性查找，换场景时清空） ====================

    private bool refsResolved = false;
    private RandomWallGroup obstacleGroup;
    private readonly List<AutoRotate> rotations = new List<AutoRotate>();
    private readonly List<float> baseSpeeds = new List<float>();
    private readonly List<Vector3> baseAngles = new List<Vector3>();
    private readonly List<AutoRotate.RotateDirection> baseDirections = new List<AutoRotate.RotateDirection>();

    private Transform winTarget;
    private LoopMove winMover;
    private LoopScale winScaler;
    private Vector3 baseWinMin = Vector3.one;
    private Vector3 baseWinMax = Vector3.one;
    private Vector3 staticWinPos;

    private PlayerController player;
    private TextMeshProUGUI hudText;

    // ==================== 生命周期 ====================

    /// <summary>触发单例提前创建：它会比任何场景物体都早出现，不会漏掉事件</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[LevelManager] 关卡系统创建失败");
        }
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddEvent();
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        OnRemoveEvent();
        base.OnDestroy();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver", OnGameOver);
        EventMgr.Instance.AddListener("GameStar", OnGameStar);
        EventMgr.Instance.AddListener("RestartGame", OnRestartGame);
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener("GameStar", OnGameStar);
        EventMgr.Instance.RemoveListener("RestartGame", OnRestartGame);
    }

    /// <summary>换场景后旧引用全部失效，这里统一清理并回到第 1 关</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        refsResolved = false;
        currentIndex = 0;
        rotations.Clear();
        baseSpeeds.Clear();
        baseAngles.Clear();
        baseDirections.Clear();
        obstacleGroup = null;
        winTarget = null;
        winMover = null;
        winScaler = null;
        player = null;
        hudText = null;

        if (scene.name == GameSceneName)
        {
            ApplyCurrentLevel();
        }
    }

    // ==================== 事件回调 ====================

    void OnGameOver(string eventName, object data)
    {
        bool isWin = data is bool && (bool)data;
        if (!isWin) return; // 失败：保持当前关重试

        currentIndex++;
        ApplyCurrentLevel();
    }

    void OnGameStar(string eventName, object data)
    {
        UpdateHud(CurrentLevel);
    }

    void OnRestartGame(string eventName, object data)
    {
        currentIndex = 0;
        ApplyCurrentLevel();
    }

    // ==================== 关卡应用 ====================

    public LevelData GetLevel(int levelNumber)
    {
        return LevelConfig.GetLevel(levelNumber);
    }

    /// <summary>布置当前关卡（场景开始、切换场景、通关、重开都会走到这里）</summary>
    public void ApplyCurrentLevel()
    {
        EnsureRefs();
        ApplyLevel(CurrentLevel);
    }

    private void ApplyLevel(LevelData data)
    {
        if (data == null) return;

        ApplyObstacles(data);
        ApplyTarget(data);
        ApplyPlayer(data);
        UpdateHud(data);

        EventMgr.Instance.Emit(LevelChangedEvent, data);
    }

    /// <summary>启用本关转盘：设定数量、转速、转向与初始角度</summary>
    private void ApplyObstacles(LevelData data)
    {
        HashSet<int> active = new HashSet<int>();
        if (data.obstacles != null)
        {
            for (int i = 0; i < data.obstacles.Length; i++) active.Add(data.obstacles[i]);
        }

        for (int i = 0; i < rotations.Count; i++)
        {
            AutoRotate rotate = rotations[i];
            bool enable = active.Contains(i);
            rotate.gameObject.SetActive(enable);
            if (!enable) continue;

            rotate.SetSpeed(baseSpeeds[i] * data.rotateScale);

            if (data.mixedDirection)
            {
                // 一半顺时针一半逆时针，形成互相拉扯的旋转趋势
                if (i % 2 == 0) rotate.SetClockwise();
                else rotate.SetCounterClockwise();
            }
            else
            {
                if (baseDirections[i] == AutoRotate.RotateDirection.Clockwise) rotate.SetClockwise();
                else rotate.SetCounterClockwise();
            }

            // randomAngle 时每次布置重新随机一次，重试同一关时角度保持一致
            rotate.transform.eulerAngles = data.randomAngle
                ? new Vector3(baseAngles[i].x, baseAngles[i].y, Random.Range(0f, 360f))
                : baseAngles[i];
            rotate.SetCurrentAsInitial();
        }
    }

    /// <summary>目标点位：大小、是否漂移、漂移速度</summary>
    private void ApplyTarget(LevelData data)
    {
        if (winScaler != null)
        {
            winScaler.minScale = baseWinMin * data.winScale;
            winScaler.maxScale = baseWinMax * data.winScale;
        }

        if (winMover != null)
        {
            winMover.SetSpeed(Mathf.Max(0.5f, data.winMoveSpeed));
            if (data.winMoving)
            {
                winMover.enabled = true;
                winMover.StartMoving();
            }
            else
            {
                winMover.StopMoving();
                winMover.enabled = false;
                if (winTarget != null) winTarget.position = staticWinPos;
            }
        }
    }

    private void ApplyPlayer(LevelData data)
    {
        if (player != null) player.moveSpeed = Mathf.Max(1f, data.playerSpeed);
    }

    // ==================== 引用查找 ====================

    private void EnsureRefs()
    {
        // 查到任何场景物体才算解析完成；若场景尚未加载好，下次调用会继续重试
        if (refsResolved) return;

        obstacleGroup = FindObjectOfType<RandomWallGroup>();
        rotations.Clear();
        baseSpeeds.Clear();
        baseAngles.Clear();
        baseDirections.Clear();

        if (obstacleGroup != null && obstacleGroup.WallGroup != null)
        {
            for (int i = 0; i < obstacleGroup.WallGroup.Count; i++)
            {
                AutoRotate rotate = obstacleGroup.WallGroup[i];
                if (rotate == null) continue;
                rotations.Add(rotate);
                baseSpeeds.Add(rotate.GetSpeed());
                baseAngles.Add(rotate.transform.eulerAngles);
                baseDirections.Add(rotate.direction);
            }
        }

        GameObject winObj = null;
        try
        {
            winObj = GameObject.FindGameObjectWithTag("Win");
        }
        catch (UnityException)
        {
            // 场景里没有 Win 标签时忽略
        }

        if (winObj != null)
        {
            winTarget = winObj.transform;
            winMover = winObj.GetComponent<LoopMove>();
            winScaler = winObj.GetComponent<LoopScale>();
        }

        if (winScaler != null)
        {
            baseWinMin = winScaler.minScale;
            baseWinMax = winScaler.maxScale;
        }

        // 静止目标停靠在三个漂移点的中心位置（不写死坐标，方便在编辑器里调点位）
        staticWinPos = winTarget != null ? winTarget.position : Vector3.zero;
        if (winMover != null && winMover.points != null && winMover.points.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < winMover.points.Length; i++)
            {
                if (winMover.points[i] == null) continue;
                sum += winMover.points[i].position;
                count++;
            }

            if (count > 0) staticWinPos = sum / count;
        }

        player = FindObjectOfType<PlayerController>();
        refsResolved = obstacleGroup != null || winTarget != null;
        BuildHud();
    }

    // ==================== 关卡 HUD ====================

    /// <summary>运行时生成顶部关卡提示（避免改动 Canvas 结构）</summary>
    private void BuildHud()
    {
        try
        {
            if (hudText != null) return;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            GameObject node = new GameObject("LevelHud", typeof(RectTransform), typeof(TextMeshProUGUI));
            node.transform.SetParent(canvas.transform, false);
            node.transform.SetAsLastSibling();

            RectTransform rect = node.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(720, 200f);

            hudText = node.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI[] samples = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (samples != null && samples.Length > 0 && samples[0].font != null)
            {
                hudText.font = samples[0].font;
            }
            hudText.fontSize = 50;
            hudText.color = new Color32(0xFF, 0x92, 0x00, 255);
            hudText.alignment = TextAlignmentOptions.Top;
            hudText.enableWordWrapping = true;
            hudText.raycastTarget = false;
            hudText.outlineColor = new Color32(40, 20, 0, 200);
            hudText.outlineWidth = 0.25f;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LevelManager] 关卡 HUD 创建失败，不影响玩法: " + e.Message);
        }
    }

    private void UpdateHud(LevelData data)
    {
        if (hudText == null || data == null) return;

        string head = data.level <= LevelConfig.DesignedLevelCount
            ? $"第 {data.level} / {LevelConfig.DesignedLevelCount} 关  ·  {data.levelName}"
            : $"无尽模式  ·  第 {data.level} 关";

        hudText.text = string.IsNullOrEmpty(data.tip)
            ? head
            : $"{head}\n<size=80%>{data.tip}</size>";
    }
}
