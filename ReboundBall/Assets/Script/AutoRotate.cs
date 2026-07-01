using System;
using Mr3;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 物体自动旋转脚本
/// 支持顺时针/逆时针旋转控制
/// 支持停止旋转和重置到初始角度
/// </summary>
public class AutoRotate : MonoBehaviour
{
    [Header("旋转设置")] [Tooltip("旋转速度（度/秒）")]
    public float rotateSpeed = 30f;

    [Tooltip("旋转方向")] public RotateDirection direction = RotateDirection.Clockwise;

    [Tooltip("旋转轴")] public RotateAxis rotateAxis = RotateAxis.Z;

    [Tooltip("是否在本地坐标系中旋转")] public bool localSpace = true;

    [Tooltip("是否随机初始角度")] public bool randomStartAngle = false;

    // ==================== 私有变量 ====================

    /// <summary>
    /// 是否正在旋转
    /// </summary>
    private bool isRotating = true;

    /// <summary>
    /// 初始旋转角度（用于重置）
    /// </summary>
    private Quaternion initialRotation;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    private bool isInitialized = false;

    /// <summary>
    /// 旋转方向枚举
    /// </summary>
    public enum RotateDirection
    {
        Clockwise, // 顺时针（正转）
        CounterClockwise // 逆时针（反转）
    }

    /// <summary>
    /// 旋转轴枚举
    /// </summary>
    public enum RotateAxis
    {
        X, // 绕 X 轴旋转
        Y, // 绕 Y 轴旋转
        Z, // 绕 Z 轴旋转（2D 游戏最常用）
        XY, // 绕 X 和 Y 轴同时旋转
        XZ, // 绕 X 和 Z 轴同时旋转
        YZ, // 绕 Y 和 Z 轴同时旋转
        XYZ // 绕所有轴同时旋转
    }

    /// <summary>
    /// 当前旋转方向（1=顺时针，-1=逆时针）
    /// </summary>
    private float DirectionMultiplier
    {
        get { return direction == RotateDirection.Clockwise ? 1f : -1f; }
    }

    // ==================== Unity 生命周期 ====================

    void Start()
    {
        // 保存初始旋转角度
        initialRotation = transform.rotation;
        isInitialized = true;

        // 如果启用随机初始角度
        if (randomStartAngle)
        {
            transform.eulerAngles = new Vector3(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );
            // 更新初始旋转为随机角度
            initialRotation = transform.rotation;
        }

        AddEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("GameOver", OnGameOver);
        EventMgr.Instance.AddListener("GameStar", OnGameStar);
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("GameOver", OnGameOver);
        EventMgr.Instance.RemoveListener("GameStar", OnGameStar);
    }

    void OnGameOver(string str, object obj)
    {
        ResetAndStop();
    }

    void OnGameStar(string str, object obj)
    {
        ResetAndStart();
    }

    void Update()
    {
        // 如果未初始化或已停止旋转，不执行旋转
        if (!isInitialized || !isRotating) return;

        // 计算每帧旋转角度（方向影响正负）
        float angle = rotateSpeed * Time.deltaTime * DirectionMultiplier;

        // 根据选择的轴进行旋转
        Vector3 rotationAxis = GetRotationAxis();

        if (localSpace)
        {
            // 本地坐标系旋转
            transform.Rotate(rotationAxis * angle, Space.Self);
        }
        else
        {
            // 世界坐标系旋转
            transform.Rotate(rotationAxis * angle, Space.World);
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始旋转
    /// </summary>
    public void StartRotation()
    {
        isRotating = true;
    }

    /// <summary>
    /// 停止旋转
    /// </summary>
    public void StopRotation()
    {
        isRotating = false;
    }

    /// <summary>
    /// 切换旋转状态（暂停/继续）
    /// </summary>
    public void ToggleRotation()
    {
        isRotating = !isRotating;
    }

    /// <summary>
    /// 判断是否正在旋转
    /// </summary>
    public bool IsRotating()
    {
        return isRotating;
    }

    /// <summary>
    /// 重置到初始旋转角度
    /// </summary>
    public void ResetToInitialRotation()
    {
        transform.rotation = initialRotation;
    }

    /// <summary>
    /// 重置到初始角度并开始旋转
    /// </summary>
    public void ResetAndStart()
    {
        ResetToInitialRotation();
        StartRotation();
    }

    /// <summary>
    /// 重置到初始角度并停止旋转
    /// </summary>
    public void ResetAndStop()
    {
        ResetToInitialRotation();
        StopRotation();
    }

    /// <summary>
    /// 设置初始旋转角度为当前角度
    /// </summary>
    public void SetCurrentAsInitial()
    {
        initialRotation = transform.rotation;
    }

    /// <summary>
    /// 切换旋转方向
    /// </summary>
    public void ToggleDirection()
    {
        direction = direction == RotateDirection.Clockwise
            ? RotateDirection.CounterClockwise
            : RotateDirection.Clockwise;
    }

    /// <summary>
    /// 设置为顺时针
    /// </summary>
    public void SetClockwise()
    {
        direction = RotateDirection.Clockwise;
    }

    /// <summary>
    /// 设置为逆时针
    /// </summary>
    public void SetCounterClockwise()
    {
        direction = RotateDirection.CounterClockwise;
    }

    /// <summary>
    /// 设置旋转速度
    /// </summary>
    public void SetSpeed(float speed)
    {
        rotateSpeed = speed;
    }

    /// <summary>
    /// 获取当前旋转速度
    /// </summary>
    public float GetSpeed()
    {
        return rotateSpeed;
    }

    /// <summary>
    /// 获取当前旋转角度
    /// </summary>
    public Vector3 GetCurrentAngles()
    {
        return transform.eulerAngles;
    }

    /// <summary>
    /// 获取初始旋转角度
    /// </summary>
    public Vector3 GetInitialAngles()
    {
        return initialRotation.eulerAngles;
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// 获取旋转轴向量
    /// </summary>
    private Vector3 GetRotationAxis()
    {
        switch (rotateAxis)
        {
            case RotateAxis.X:
                return Vector3.right;
            case RotateAxis.Y:
                return Vector3.up;
            case RotateAxis.Z:
                return Vector3.forward;
            case RotateAxis.XY:
                return new Vector3(1, 1, 0);
            case RotateAxis.XZ:
                return new Vector3(1, 0, 1);
            case RotateAxis.YZ:
                return new Vector3(0, 1, 1);
            case RotateAxis.XYZ:
                return new Vector3(1, 1, 1);
            default:
                return Vector3.forward;
        }
    }
}