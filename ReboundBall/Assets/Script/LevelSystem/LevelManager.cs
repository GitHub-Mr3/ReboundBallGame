using System.Collections.Generic;
using Mr3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// 关卡管理器：把「随机刷机关」替换成「闯关」流程。
/// 规则：
///   1. 每次把食物投进目标点位算「投喂一次」，LevelData.feedStages 的长度就是本关通关所需次数；
///      没投够时留在当前关继续投喂（已投喂次数保留）；投够最后一下算通关，但不会立刻换关，
///      要等玩家点击结算面板的「继续」按钮（AdvanceToNextLevel）才进入下一关；
///   2. 失败时不会掉关，也不会清空已投喂次数，原地重试当前关（布局保持一致，公平重试）；
///   3. 点“重新开始”回到第 1 关并清空分数；
///   4. 通关第 12 关后进入无尽模式，难度继续按公式上涨。
/// 当前投喂进度实时显示在 LevelHud 上。
/// 该脚本不需要挂到场景任何物体上，运行时由自身自动创建。
/// </summary>
public class LevelManager : SingleMonoBase<LevelManager>
{
    public const string GameSceneName = "GameScence";
    public const string LevelChangedEvent = "LevelChanged";

    /// <summary>单次投喂成功但本关尚未通关时抛出，携带当前 LevelData</summary>
    public const string FeedProgressEvent = "FeedProgressChanged";

    /// <summary>重玩当前关卡（不清关号，只把本关投喂进度清零）</summary>
    public const string RestartLevelEvent = "RestartLevel";

    /// <summary>关卡提示节点：GameResult / leveInfo / LevelHud（场景里已摆好，不动态创建）</summary>
    private const string HudPath = "leveInfo/LevelHud";

    private const string HudNodeName = "LevelHud";

    /// <summary>目标点位根节点名</summary>
    private const string WinNodeName = "Win";

    // ==================== 关卡状态 ====================

    /// <summary>当前关卡索引（0 表示第 1 关）</summary>
    private int currentIndex = 0;

    /// <summary>选关界面指定的起始关卡索引，切到游戏场景后生效；小于 0 表示未指定，默认回第 1 关</summary>
    private int startIndexOnSceneLoaded = -1;

    /// <summary>本关已投喂（投中目标点位）次数，通关后进入下一关时清零</summary>
    private int feedCount = 0;

    public int CurrentLevelNumber => currentIndex + 1;

    /// <summary>
    /// 当前关卡数据（已经套用过宠物技能的「本局生效」副本）。
    /// 对外一律返回这份副本，保证 HUD、结算面板看到的投喂次数 / 奖励与真实生效的一致。
    /// </summary>
    public LevelData CurrentLevel => runtimeLevel ?? GetLevel(CurrentLevelNumber);

    /// <summary>本局生效的关卡数据（技能修正后的副本），布置关卡时刷新</summary>
    private LevelData runtimeLevel;

    public bool IsEndless => currentIndex >= LevelConfig.DesignedLevelCount;

    /// <summary>本关通关所需投喂次数（来自 LevelData.feedStages 的长度）</summary>
    public int RequiredFeedCount => CurrentLevel != null ? CurrentLevel.FeedTarget : 1;

    /// <summary>本关已投喂次数</summary>
    public int CurrentFeedCount => feedCount;

    /// <summary>本关投喂是否已达标（已通关）</summary>
    public bool IsCurrentLevelCleared => feedCount >= RequiredFeedCount;

    /// <summary>当前投喂进度文案：一次都没投喂时显示等待，之后显示 LevelData.feedStages 对应项</summary>
    public string CurrentFeedStage
    {
        get
        {
            LevelData data = CurrentLevel;
            if (data == null) return "";
            if (feedCount <= 0) return $"等待投喂 0/{data.FeedTarget}";
            return data.GetFeedStage(feedCount);
        }
    }

    /// <summary>
    /// 最近一次投喂是否已经把本关打通关（LevelManager 比结算面板更早收到 GameOver，面板据此刷新按钮文案）。
    /// 默认 true，保证拿不到关卡数据时依旧按「投中即通关」表现。
    /// </summary>
    public bool LastFeedClearedLevel { get; private set; } = true;

    /// <summary>
    /// 本关投够最后一下、但玩家还没点「继续」的挂起状态。
    /// 这段时间里关卡号不动，LevelHud 一直显示本关最后一条投喂进度。
    /// </summary>
    public bool HasPendingLevelAdvance => pendingLevelAdvance;

    private bool pendingLevelAdvance = false;

    /// <summary>下一关数据：只用于提前刷新结算面板按钮文案，不会真的换关</summary>
    public LevelData NextLevel => GetLevel(CurrentLevelNumber + 1);

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
    private SpriteRenderer winRenderer;
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
            DebugMgr.Instance.LogWarning("[LevelManager] 关卡系统创建失败");
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
        EventMgr.Instance.AddListener(RestartLevelEvent, OnRestartLevel);
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener("GameStar", OnGameStar);
        EventMgr.Instance.RemoveListener("RestartGame", OnRestartGame);
        EventMgr.Instance.RemoveListener(RestartLevelEvent, OnRestartLevel);
    }

    /// <summary>换场景后旧引用全部失效，这里统一清理并回到第 1 关</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        refsResolved = false;
        // 选关界面指定了关卡时，直接用它，否则回到第 1 关
        currentIndex = startIndexOnSceneLoaded >= 0 ? startIndexOnSceneLoaded : 0;
        startIndexOnSceneLoaded = -1;
        rotations.Clear();
        baseSpeeds.Clear();
        baseAngles.Clear();
        baseDirections.Clear();
        obstacleGroup = null;
        winTarget = null;
        winMover = null;
        winScaler = null;
        winRenderer = null;
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
        if (!isWin) return; // 失败：保持当前关重试（已投喂次数保留）

        // 每投中目标点位一次算「投喂一次」，投够本关所需次数才通关
        feedCount++;
        int required = RequiredFeedCount;
        LastFeedClearedLevel = feedCount >= required;

        if (!LastFeedClearedLevel)
        {
            DebugMgr.Instance.Log($"[LevelManager] 第 {CurrentLevelNumber} 关投喂进度 {feedCount}/{required}");
            UpdateHud(CurrentLevel);
            EventMgr.Instance.Emit(FeedProgressEvent, CurrentLevel);
            return;
        }

        // 投够最后一下 = 本关通关。这里先不换关：关卡号保持在本关，LevelHud 继续显示本关
        // 最后一条投喂进度，等玩家点击结算面板的「继续」按钮再 AdvanceToNextLevel() 进入下一关。
        pendingLevelAdvance = true;
        DebugMgr.Instance.Log($"[LevelManager] 第 {CurrentLevelNumber} 关投喂进度 {feedCount}/{required}，已通关，等待玩家点击继续");
        UpdateHud(CurrentLevel);
        EventMgr.Instance.Emit(FeedProgressEvent, CurrentLevel);
    }

    /// <summary>
    /// 真正进入下一关：只在「本关已通关且玩家点了继续」时生效。
    /// 未通关（只完成一次投喂）时点击继续不会换关，圆盘/目标点位也保持本关布局。
    /// </summary>
    public void AdvanceToNextLevel()
    {
        if (!pendingLevelAdvance) return;

        pendingLevelAdvance = false;
        currentIndex++;
        ApplyCurrentLevel();
    }

    void OnGameStar(string eventName, object data)
    {
        UpdateHud(CurrentLevel);
    }

    /// <summary>整局重来：回到第 1 关</summary>
    void OnRestartGame(string eventName, object data)
    {
        currentIndex = 0;
        ApplyCurrentLevel();
    }

    /// <summary>
    /// 重玩当前关卡：关卡号不变，重新布置一次并把投喂进度清零，
    /// 于是从本关 feedStages 的第一个进度阶段重新开始。
    /// </summary>
    void OnRestartLevel(string eventName, object data)
    {
        ApplyCurrentLevel();
    }

    // ==================== 关卡应用 ====================

    /// <summary>
    /// 从选关界面直接进入指定关卡（关卡号从 1 开始）：先记下目标关卡，
    /// 再切到游戏场景，场景加载完成后会按该关卡号布置关卡，投喂进度从 0 开始。
    /// </summary>
    public void StartLevel(int levelNumber)
    {
        startIndexOnSceneLoaded = Mathf.Max(0, levelNumber - 1);
        SceneManager.LoadSceneAsync(GameSceneName);
    }

    public LevelData GetLevel(int levelNumber)
    {
        return LevelConfig.GetLevel(levelNumber);
    }

    /// <summary>布置当前关卡（场景开始、切换场景、通关、重开都会走到这里）</summary>
    public void ApplyCurrentLevel()
    {
        EnsureRefs();

        // 先套用宠物技能，拿到本局生效的关卡副本，再按它布置场景
        LevelData data = PetSkillMgr.ApplyToLevel(GetLevel(CurrentLevelNumber));
        runtimeLevel = data;
        ApplyLevel(data);
    }

    private void ApplyLevel(LevelData data)
    {
        if (data == null) return;

        // 每关布置时重置技能的消耗类次数（护盾 / 暂停 / 免广告）
        PetSkillMgr.ResetLevelRuntime();

        // 每次布置关卡（进入新关 / 重开 / 换场景）都从 0 次投喂开始
        feedCount = 0;
        LastFeedClearedLevel = true;
        pendingLevelAdvance = false;

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

    /// <summary>目标点位：大小、是否漂移、漂移速度、贴图</summary>
    private void ApplyTarget(LevelData data)
    {
        ApplyWinSprite(data);

        if (winScaler != null)
        {
            winScaler.minScale = baseWinMin * data.winScale;
            winScaler.maxScale = baseWinMax * data.winScale;
        }

        if (winMover != null)
        {
            // 速度 <= 0 表示本关目标点位静止，停在漂移点中心
            bool moving = data.winMoveSpeed > 0f;
            winMover.SetSpeed(Mathf.Max(0.5f, data.winMoveSpeed));

            if (moving)
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

    /// <summary>按关卡配置换掉目标点位的贴图：下标 -1 或贴图库为空时保持场景里原本的贴图</summary>
    private void ApplyWinSprite(LevelData data)
    {
        if (winRenderer == null)
        {
            DebugMgr.Instance.LogWarning($"[LevelManager] 未找到 {WinNodeName} 上的 SpriteRenderer，无法换贴图");
            return;
        }

        int count = SpriteLibrary.Instance.GetLevelIconSpriteNum();
        if (data.winSpriteIndex < 0)
        {
            DebugMgr.Instance.Log($"[LevelManager] 第 {data.level} 关 winSpriteIndex={data.winSpriteIndex}，保持原贴图（贴图库 {count} 张）");
            return;
        }

        if (count == 0)
        {
            DebugMgr.Instance.LogWarning("[LevelManager] 贴图库为空：请挂 SpriteLibrary 组件并拖入 Sprite");
            return;
        }

        Sprite sprite =
            SpriteLibrary.Instance
                .GetLevelIconSprite(data.winSpriteIndex); // LevelConfig.GetLevelIconSprite(data.winSpriteIndex);
        if (sprite == null) return;

        winRenderer.sprite = sprite;
        int used = Mathf.Clamp(data.winSpriteIndex, 0, count - 1);
        DebugMgr.Instance.Log(
            $"[LevelManager] 第 {data.level} 关目标贴图 -> {sprite.name}（配置下标 {data.winSpriteIndex}，实际取 {used}/{count}）");
    }

    private void ApplyPlayer(LevelData data)
    {
        if (player == null) return;

        player.moveSpeed = Mathf.Max(1f, data.playerSpeed);

        // 技能「彩斑穿针」：小球碰撞判定缩小，更容易钻过窄缝
        player.SetColliderScale(PetSkillMgr.PlayerColliderScale);
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

        // 先按名字找根节点：子节点 kuang 也打了 Win 标签，单靠标签可能取到子节点
        GameObject winObj = GameObject.Find(WinNodeName);
        if (winObj == null)
        {
            try
            {
                winObj = GameObject.FindGameObjectWithTag("Win");
            }
            catch (UnityException)
            {
                // 场景里没有 Win 标签时忽略
            }
        }

        if (winObj != null)
        {
            winTarget = winObj.transform;
            winMover = winObj.GetComponent<LoopMove>();
            winScaler = winObj.GetComponent<LoopScale>();
            winRenderer = winObj.GetComponent<SpriteRenderer>();
            // 贴图挂在子节点上时兜底取第一个
            if (winRenderer == null) winRenderer = winObj.GetComponentInChildren<SpriteRenderer>(true);
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
        ResolveHud();
    }

    // ==================== 关卡 HUD ====================

    /// <summary>关卡提示节点在场景里已经摆好：GameResult / leveInfo / LevelHud，这里只做引用绑定，不创建物体</summary>
    private void ResolveHud()
    {
        if (hudText != null) return;

        Transform resultRoot = null;
        GameResult result = FindObjectOfType<GameResult>();
        if (result != null)
        {
            resultRoot = result.transform;
        }
        else
        {
            GameObject fallback = GameObject.Find("GameResult");
            if (fallback != null) resultRoot = fallback.transform;
        }

        if (resultRoot == null)
        {
            DebugMgr.Instance.LogWarning("[LevelManager] 场景中未找到 GameResult 节点，关卡 HUD 不显示（不影响玩法）");
            return;
        }

        Transform node = resultRoot.Find(HudPath);
        if (node == null) node = FindDeepChild(resultRoot, HudNodeName);
        if (node == null)
        {
            DebugMgr.Instance.LogWarning($"[LevelManager] GameResult 下未找到 {HudNodeName} 节点，关卡 HUD 不显示（不影响玩法）");
            return;
        }

        hudText = node.GetComponent<TextMeshProUGUI>();
        if (hudText == null)
        {
            DebugMgr.Instance.LogWarning($"[LevelManager] {HudNodeName} 上没有 TextMeshProUGUI 组件");
        }
    }

    /// <summary>按名字递归找子节点（路径改动时兜底，隐藏节点也能找到）</summary>
    private static Transform FindDeepChild(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;

            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }

        return null;
    }

    private void UpdateHud(LevelData data)
    {
        if (data == null) return;
        if (hudText == null) ResolveHud();
        if (hudText == null) return;

        string head = data.level <= LevelConfig.DesignedLevelCount
            ? $"<color=#145200><b>{data.levelName}</b></color>"
            : $"无尽模式  ·  第 {data.level} 关";

        // 第二行改成实时投喂进度：已投喂次数 / 本关所需次数 + 当前进度状态
        int total = data.FeedTarget;
        int done = Mathf.Clamp(feedCount, 0, total);
        string stage = feedCount > 0 ? data.GetFeedStage(feedCount) : "等待投喂";

        hudText.text = $"{head}\n<size=80%>投喂进度 {done}/{total}  ·  {stage}</size>";
    }
}