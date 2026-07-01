using System;
using Mr3;
using UnityEngine;

/// <summary>
/// 玩家控制器 - 实现点击移动和墙壁反弹功能
/// 玩家会持续向点击方向移动，遇到墙壁则反弹，永不停止
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")] [Tooltip("玩家移动速度")] public float moveSpeed = 5f;

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

    //单局游戏是否结束
    private bool isGameOver = false;

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

        starPos = GameObject.Find("StarPos").transform.position;
        transform.position = starPos;
    }

    /// <summary>
    /// Start 在对象激活时调用
    /// 标记初始化完成
    /// </summary>
    void Start()
    {
        isInitialized = true;
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

        // ===== 双指点击 / 空格键：停止移动 =====
        if ((Input.touchCount >= 2 && Input.GetTouch(1).phase == TouchPhase.Began) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            isMoving = false;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.Sleep();
            }
        }

        // ===== 单指点击 / 鼠标左键：设置移动方向 =====
        if (GetTapDown() && !isMoving)
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

                if (rb != null) rb.WakeUp();
            }
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