using System;
using System.Collections.Generic;
using Mr3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 玩家控制器 - 实现点击移动和墙壁反弹功能
/// 玩家会持续向点击方向移动，遇到墙壁则反弹，永不停止
/// </summary>
public class PlayerController : SingleMonoBase<PlayerController>
{
    [Header("移动设置")] [Tooltip("玩家移动速度")] public float moveSpeed = 5f;

    [Header("UI 判定")]
    [Tooltip("开启：射线命中任何 UI（按钮、面板、图片、文字、遮罩都算）时都不发射小球；\n" +
             "关闭：只有 Button / Toggle / Slider 这类真正响应点击的控件才拦，纯展示 UI 会被穿透")]
    [SerializeField] private bool blockAllUI = true;

    // ==================== 私有变量 ====================

    /// <summary>
    /// 当前移动方向（单位向量）
    /// </summary>
    private Vector2 moveDirection;

    /// <summary>
    /// 是否正在移动
    /// </summary>
    private bool isMoving = false;

    /// <summary>
    /// 刚体组件引用
    /// </summary>
    private Rigidbody2D rb;

    /// <summary>
    /// 是否已完成初始化
    /// </summary>
    private bool isInitialized = false;

    private Vector3 starPos;

    private SpriteRenderer playerRenderer;

    // ==================== 技能相关 ====================

    /// <summary>小球碰撞体（技能「彩斑穿针」要缩放它）</summary>
    private Collider2D playerCollider;

    private CircleCollider2D circleCollider;
    private BoxCollider2D boxCollider;
    private float baseCircleRadius = 0f;
    private Vector2 baseBoxSize = Vector2.zero;

    /// <summary>技能「圆墩暂歇」暂停中：小球停住，等玩家点击场景决定新的方向</summary>
    private bool pausedBySkill = false;


    /// <summary>技能暂停中（UI 判断该显示「继续」还是「暂停」用）</summary>
    public bool IsPausedBySkill => pausedBySkill;

    /// <summary>小球是否正在移动</summary>
    public bool IsMoving => isMoving;

    //单局游戏是否结束
    private bool isGameOver = false;

    /// <summary>场景里的 EventSystem，用于判断点击是否落在 UI 上</summary>
    private EventSystem eventSystem;

    /// <summary>UI 射线检测结果的复用缓冲，避免每次点击都 new 一个 List</summary>
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    // ==================== Unity 生命周期 ====================

    /// <summary>
    /// Awake 在对象实例化时调用，比 Start 更早
    /// 用于获取组件引用和初始化状态
    /// </summary>
    void Awake()
    {

        // 获取 Rigidbody2D 组件
        rb = GetComponent<Rigidbody2D>();
        isGameOver = false; //游戏结束
        // 初始化移动状态
        isMoving = false;
        moveDirection = Vector2.right; // 默认向右，避免空值

        // 如果 Rigidbody 存在，重置物理状态
        if (rb != null)
        {
            rb.velocity = Vector2.zero; // 速度为0
            rb.angularVelocity = 0f; // 角速度为0
            rb.gravityScale = 0; // 关闭重力（2D游戏中不需要）
            rb.Sleep(); // 让刚体进入休眠状态
        }

        playerRenderer = GetComponent<SpriteRenderer>();

        // 记下碰撞体的原始尺寸，技能缩放时以它为基准
        playerCollider = GetComponent<Collider2D>();
        circleCollider = playerCollider as CircleCollider2D;
        boxCollider = playerCollider as BoxCollider2D;
        if (circleCollider != null) baseCircleRadius = circleCollider.radius;
        if (boxCollider != null) baseBoxSize = boxCollider.size;

        starPos = GameObject.Find("StarPos").transform.position;
        transform.position = starPos;
    }

    /// <summary>
    /// 缩放小球碰撞判定（宠物技能「彩斑穿针」：缩小 15% 更容易钻过窄缝）。
    /// 以 Awake 时记录的原始尺寸为基准，重复调用不会累积。
    /// </summary>
    public void SetColliderScale(float scale)
    {
        float s = Mathf.Clamp(scale, 0.1f, 2f);

        if (circleCollider != null && baseCircleRadius > 0f)
        {
            circleCollider.radius = baseCircleRadius * s;
        }

        if (boxCollider != null && baseBoxSize != Vector2.zero)
        {
            boxCollider.size = baseBoxSize * s;
        }
    }

    /// <summary>
    /// Start 在对象激活时调用
    /// 标记初始化完成
    /// </summary>
    void Start()
    {
        isInitialized = true;
        eventSystem = EventSystem.current;
        AddEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameStar", OnGameStar);
    }

    void RemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameStar", OnGameStar);
    }

    private void OnDestroy()
    {
        RemoveEvent();
    }

   // private bool isResetGame = false;

    void OnGameStar(string str, object obj)
    {
        bool isWin=(bool)obj;
        ResetGame(isWin);
    }


    /// <summary>
    /// Update 每帧调用
    /// 处理触屏输入（手机）和鼠标/键盘输入（编辑器调试）
    /// </summary>
    void Update()
    {
        // ===== 游戏结束后：点击屏幕重置 =====
        // if (isGameOver && GetTapDown()&&isResetGame)
        // {
        //     ResetGame();
        //     return;
        // }

        // 如果未初始化完成，不执行任何逻辑
        if (!isInitialized) return;
        if (isGameOver) return;

        // ===== 双指点击 / 空格键：暂停 / 继续 =====
        if ((Input.touchCount >= 2 && Input.GetTouch(1).phase == TouchPhase.Began) ||
            Input.GetKeyDown(KeyCode.Space))
        {
           // TogglePauseByInput();
        }

        // ===== 单指点击 / 鼠标左键：设置移动方向 =====
        // 技能「圆墩暂歇」暂停中也走这条分支：点哪里就往哪飞（重新瞄准），不是恢复暂停前的方向
        if (GetTapDown() && !isMoving && !IsTapOverUI())
        {
            // 将屏幕坐标转换为世界坐标（2D 平面）
            Vector3 tapWorldPos = GetTapWorldPosition();
            tapWorldPos.z = 0f; // 确保 Z 轴为 0（2D 平面）

            // 计算从玩家到点击位置的方向向量
            Vector2 direction = (tapWorldPos - transform.position).normalized;

            // 如果点击位置不是玩家自身（距离大于阈值）
            if (direction.magnitude > 0.1f)
            {
                moveDirection = direction;
                isMoving = true;
                pausedBySkill = false; // 这次点击就是新的出发方向，技能暂停到此结束

                if (rb != null) rb.WakeUp();
            }
        }
    }

    // ==================== 技能「圆墩暂歇」暂停 / 继续 ====================

    /// <summary>
    /// 双指点击 / 空格键：技能暂停中就结束暂停（小球就地静止，等下一次点击选方向），
    /// 否则尝试用技能暂停（没带技能时只做普通停止移动）。
    /// </summary>
    private void TogglePauseByInput()
    {
        if (pausedBySkill)
        {
            ResumeBySkill();
            return;
        }

        if (PauseBySkill()) return; // 携带「圆墩暂歇」且还有次数 → 记为技能暂停

        StopMoving();               // 没有该技能 → 保持原有的「停止移动」行为
    }

    /// <summary>
    /// UI 按钮 / 按键调用：消耗一次「圆墩暂歇」让小球停下，等玩家重新点击决定方向。
    /// 只有小球正在移动、且技能还有次数时才成功，返回是否真的暂停了。
    /// </summary>
    public bool PauseBySkill()
    {
        if (isGameOver || pausedBySkill) return false;
        if (!isMoving || !PetSkillMgr.CanPause || !PetSkillMgr.TryUsePause()) return false;

        pausedBySkill = true;
        StopMoving();
        return true;
    }

    /// <summary>
    /// 结束技能暂停（UI 按钮 / 空格键用）：小球就地保持静止，下一次点场景会按点击方向重新出发，
    /// 不会沿用暂停前的方向。
    /// </summary>
    public bool ResumeBySkill()
    {
        if (!pausedBySkill) return false;

        pausedBySkill = false;
        return true;
    }

    /// <summary>停住小球（不消耗技能次数，只是停下来等下一次瞄准）</summary>
    private void StopMoving()
    {
        isMoving = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.Sleep();
        }
    }

    /// <summary>
    /// 检测是否有点击/触摸输入（兼容手机触屏和鼠标）
    /// </summary>
    private bool GetTapDown()
    {
        return Input.GetMouseButtonDown(0) ||
               (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    /// <summary>
    /// 本次点击是否落在 UI 上（UGUI 射线命中的任意 UI 都算：按钮、面板、图片、文字、遮罩等）。
    /// 命中则本次点击交给 UI 处理，玩家不动。
    /// blockAllUI 关闭时才退化成「只拦 Button / Toggle / Slider / EventTrigger 等真正响应点击的控件」。
    /// 隐藏的对象、CanvasGroup.blocksRaycasts=false 的子树不会命中；
    /// 如果某个纯展示 UI 不想挡住操作，到 Inspector 里把它的 Raycast Target 取消勾选即可。
    /// 兼容手机触屏与鼠标（WebGL / 微信小游戏里触摸会映射成鼠标）。
    /// 场景里没有 EventSystem 时不拦截，保持原有行为。
    /// </summary>
    private bool IsTapOverUI()
    {
        if (eventSystem == null) eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        uiRaycastResults.Clear();
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.touchCount > 0 ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition
        };
        eventSystem.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject uiObj = uiRaycastResults[i].gameObject;
            if (uiObj == null) continue;

            if (blockAllUI) return true;

            // Button / Toggle / Slider 等交互控件
            if (uiObj.GetComponentInParent<Selectable>() != null) return true;

            // 挂了 EventTrigger 或自定义 IPointerClickHandler 的物体
            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(uiObj) != null) return true;
        }

        return false;
    }

    /// <summary>
    /// 获取点击/触摸位置的世界坐标（兼容手机触屏和鼠标）
    /// </summary>
    private Vector3 GetTapWorldPosition()
    {
        Vector3 screenPos = Input.touchCount > 0
            ? (Vector3)Input.GetTouch(0).position
            : Input.mousePosition;
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    /// <summary>
    /// FixedUpdate 以固定时间间隔调用（默认 0.02 秒）
    /// 用于处理物理相关的移动，保证移动平滑
    /// </summary>
    void FixedUpdate()
    {
        // 如果未初始化完成，不执行任何逻辑
        if (!isInitialized) return;
        if (isGameOver) return;
        // ===== 停止状态处理 =====
        if (!isMoving)
        {
            // 如果刚体还有速度，强制归零（防止物理惯性）
            if (rb != null && rb.velocity.magnitude > 0.01f)
            {
                rb.velocity = Vector2.zero;
                rb.Sleep();
            }

            return; // 不执行移动逻辑
        }

        // ===== 持续移动 =====
        // 计算新位置：当前位置 + 方向 × 速度 × 固定时间步长
        Vector2 newPosition = (Vector2)transform.position + moveDirection * moveSpeed * Time.fixedDeltaTime;

        // 方式1：直接设置 Transform 位置（最可靠，不依赖物理引擎）
        transform.position = newPosition;

        // 方式2：同步更新 Rigidbody 位置（保持物理一致性）
        if (rb != null)
        {
            rb.MovePosition(newPosition);
        }
    }

    /// <summary>
    /// OnCollisionEnter2D 在碰撞发生时调用
    /// 处理与墙壁的碰撞反弹逻辑
    /// </summary>
    /// <param name="collision">碰撞信息</param>
    void OnCollisionEnter2D(Collision2D collision)
    {
        // ===== 检查是否碰到墙壁 =====
        if (collision.gameObject.CompareTag("Wall"))
        {
            // 获取碰撞接触点的法线方向
            // 法线指向碰撞表面外部，即反弹方向的反方向
            Vector2 normal = collision.contacts[0].normal;

            // 计算反射方向：入射方向关于法线的镜像
            // Vector2.Reflect(入射方向, 法线) 返回反射方向
            Vector2 reflectDirection = Vector2.Reflect(moveDirection, normal);

            // 更新移动方向为反射方向
            moveDirection = reflectDirection;

            // 确保继续保持移动状态
            isMoving = true;

            // 唤醒刚体，确保物理更新继续
            if (rb != null) rb.WakeUp();
        }
        else if (collision.gameObject.CompareTag("Win"))
        {
            GameOver(true);
        }
        else if (collision.gameObject.CompareTag("Lose"))
        {
            // 技能「黄巾护体 / 旺财三重奏」：还有护盾就抵消这次失败，小球反弹继续跑
            if (PetSkillMgr.TryUseShield())
            {
                Vector2 loseNormal = collision.contacts[0].normal;
                moveDirection = Vector2.Reflect(moveDirection, loseNormal);
                isMoving = true;
                if (rb != null) rb.WakeUp();
                return;
            }

            GameOver(false);
        }
    }

    void GameOver(bool isWin)
    {
        isMoving = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.Sleep();
        }

        playerRenderer.enabled = false; //(false);
        isGameOver = true;
        EventMgr.Instance.Emit("GameOver", isWin);
    }

    void ResetGame(bool isWin)
    {
      //  isResetGame = false;
        isGameOver = false;
        transform.position = starPos;
        playerRenderer.enabled = true; //(false);
        EventMgr.Instance.Emit("OnRandomWall",isWin);
    }

    /// <summary>
    /// OnEnable 在对象激活时调用
    /// 用于重置状态，防止对象重新激活时自动移动
    /// </summary>
    void OnEnable()
    {
        // 只有在初始化完成后才重置状态
        if (isInitialized)
        {
            isMoving = false;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.Sleep();
            }
        }
    }

// ==================== 编辑器辅助 ====================

    /// <summary>
    /// OnDrawGizmosSelected 在编辑器中选择对象时绘制调试信息
    /// 显示当前移动方向和目标位置
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (isMoving)
        {
            // 绿色射线：显示移动方向（长度 2 个单位）
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, moveDirection * 2f);

            // 黄色线框球：标记玩家当前位置
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}