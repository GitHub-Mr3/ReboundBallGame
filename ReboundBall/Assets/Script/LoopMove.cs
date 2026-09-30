using System.Threading;
using Mr3;
using UnityEngine;

/// <summary>
/// 物体循环移动脚本
/// 在多个点位之间循环移动，如 A→B→C→A→B→C...
/// </summary>
public class LoopMove : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("移动路径上的点位（按顺序循环）")]
    public Transform[] points;

    [Tooltip("移动速度")]
    public float moveSpeed = 5f;

    [Tooltip("到达每个点位后的停留时间（秒）")]
    public float waitDuration = 0.5f;

    [Tooltip("是否在 Start 时自动开始")]
    public bool autoStart = true;

    // ==================== 私有变量 ====================

    /// <summary>
    /// 当前目标点位的索引
    /// </summary>
    private int currentIndex = 0;

    /// <summary>
    /// 是否正在移动
    /// </summary>
    private bool isMoving = false;

    /// <summary>
    /// 是否正在点位处停留等待
    /// </summary>
    private bool isWaiting = false;

    /// <summary>
    /// 停留计时器
    /// </summary>
    private float waitTimer = 0f;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    private bool isInitialized = false;

    // ==================== Unity 生命周期 ====================

    void Start()
    {
        if (points == null || points.Length == 0)
        {
            DebugMgr.Instance.LogWarning("[LoopMove] 未设置移动点位，脚本不会工作");
            return;
        }

        // 初始位置设为第一个点位
        transform.position = points[0].position;
        currentIndex = 0;
        isInitialized = true;

        if (autoStart)
        {
            StartMoving();
        }

        AddEvent();
    }
    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver",OnGameOver);
        EventMgr.Instance.AddListener("GameStar",OnGameStar);
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener("GameStar",OnGameStar);
    }
    void OnGameOver(string str,object obj )
    {
        StopMoving();
    }

    void OnGameStar(string str, object obj)
    {
       StartMoving();
    }

    void Update()
    {
        if (!isInitialized || !isMoving) return;

        // ===== 停留等待中 =====
        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitDuration)
            {
                isWaiting = false;
                waitTimer = 0f;
                // 切换到下一个点位（循环）
                currentIndex = (currentIndex + 1) % points.Length;
            }
            return;
        }

        // ===== 向当前目标点位移动 =====
        Vector3 target = points[currentIndex].position;
        Vector3 direction = (target - transform.position);
        float distance = direction.magnitude;

        if (distance < 0.01f)
        {
            // 到达目标点位，开始停留
            isWaiting = true;
            waitTimer = 0f;
            return;
        }

        // 按速度匀速移动
        Vector3 move = direction.normalized * moveSpeed * Time.deltaTime;
        // 防止移动过头
        if (move.magnitude > distance)
        {
            transform.position = target;
        }
        else
        {
            transform.position += move;
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始循环移动
    /// </summary>
    public void StartMoving()
    {
        isMoving = true;
    }

    /// <summary>
    /// 停止循环移动
    /// </summary>
    public void StopMoving()
    {
        isMoving = false;
    }

    /// <summary>
    /// 切换移动状态（暂停/继续）
    /// </summary>
    public void ToggleMoving()
    {
        isMoving = !isMoving;
    }

    /// <summary>
    /// 判断是否正在移动
    /// </summary>
    public bool IsMoving()
    {
        return isMoving;
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    public void SetSpeed(float speed)
    {
        moveSpeed = speed;
    }

    /// <summary>
    /// 获取当前目标点位索引
    /// </summary>
    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    // ==================== 编辑器辅助 ====================

    /// <summary>
    /// OnDrawGizmosSelected 在编辑器中选中对象时绘制路径
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (points == null || points.Length == 0) return;

        Gizmos.color = Color.cyan;
        // 绘制路径连线
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;

            // 绘制点位标记
            Gizmos.DrawWireSphere(points[i].position, 0.3f);

            // 连线到下一个点位（循环）
            int next = (i + 1) % points.Length;
            if (points[next] != null)
            {
                Gizmos.DrawLine(points[i].position, points[next].position);
            }
        }
    }
}
