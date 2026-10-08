using UnityEngine;

/// <summary>
/// 统一打印入口：项目里的日志都走这里，后期把 DebugMgr.Enable 改成 false（或加一条 #if）就能一键屏蔽全部输出。
/// Instance 由 SingletonBase 反射创建——任何时候访问都不会为空，Awake / Start / 静态方法里调用都安全。
/// </summary>
public class DebugMgr : SingletonBase<DebugMgr>
{
    /// <summary>日志总开关：false = Log / LogWarning / LogError 全部静默（不改调用代码就能一键关）</summary>
    public static bool Enable = true;

    public void Log(object message)
    {
        if (!Enable) return;
        Debug.Log(message);
    }

    public void Log(object message, Object context)
    {
        if (!Enable) return;
        Debug.Log(message, context);
    }

    public void LogWarning(object message)
    {
        if (!Enable) return;
        Debug.LogWarning(message);
    }

    public void LogWarning(object message, Object context)
    {
        if (!Enable) return;
        Debug.LogWarning(message, context);
    }

    public void LogError(object message)
    {
        if (!Enable) return;
        Debug.LogError(message);
    }

    public void LogError(object message, Object context)
    {
        if (!Enable) return;
        Debug.LogError(message, context);
    }
}
