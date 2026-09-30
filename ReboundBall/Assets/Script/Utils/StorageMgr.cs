using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 存储方案（存到哪里，由调用方自己决定）：
///   Local —— 本地存储：wx.setStorageSync / WX.StorageSetXxxSync（真机），PlayerPrefs（编辑器与其它平台）。可读可写，是最稳的默认方案。
///   Cloud —— 微信托管数据：wx.setUserCloudStorage。存在微信服务器，玩家删除小游戏后还在；但官方不提供读自己托管数据的接口，
///            所以读取只能靠「本次会话缓存 + 本地缓存副本」，见 CloudStorageBackend.CacheToLocal。
///   Both  —— 两边都写，读取时本地优先、本地没有再回落到云端数据。
///
/// 用法一（推荐，显式指定存哪）：
///     StorageMgr.Local.SetInt(GameData.MaxLevelIdKey, 3);
///     StorageMgr.Cloud.SetInt(GameData.MaxLevelIdKey, 3);
///     StorageMgr.Both.SetString("nickName", "旺财");
///     StorageMgr.SetInt(GameData.MaxLevelIdKey, 3, StorageTarget.Cloud);
///
/// 用法二（不带 target 的快捷写法，走 DefaultTarget，默认 StorageTarget.Local）：
///     StorageMgr.SetInt(GameData.MaxLevelIdKey, 3);
///     int id = StorageMgr.GetInt(GameData.MaxLevelIdKey, 0);
///     StorageMgr.SetFloat(GameData.AudioVolumData, 0.8f);
///     StorageMgr.SetBool("firstLogin", false);
///     StorageMgr.SetObject("playerData", new PlayerData { coin = 100 });
///     var data = StorageMgr.GetObject("playerData", new PlayerData());
///
/// 想全局切换默认存哪：StorageMgr.DefaultTarget = StorageTarget.Cloud;
///
/// 托管数据上报是异步的，会先攒在队列里节流后批量提交（一次 setUserCloudStorage 可写多组 KV），
/// 建议在 GameMain.Update 里调用 StorageMgr.Tick()，并在 OnApplicationPause/Focus 里调用 StorageMgr.Save() 强制上报。
/// 注意：JsonUtility 根节点必须是对象，List/数组请包一层 class 再存。
/// </summary>
public static class StorageMgr
{
    /// <summary>不带 target 的快捷接口默认使用的存储方案，可全局改写</summary>
    public static StorageTarget DefaultTarget = StorageTarget.Local;

    /// <summary>本地存储：wx.setStorageSync / PlayerPrefs</summary>
    public static readonly IStorage Local = new LocalStorageBackend();

    /// <summary>微信托管数据：wx.setUserCloudStorage</summary>
    public static readonly IStorage Cloud = new CloudStorageBackend();

    /// <summary>本地 + 托管数据双写：写两边，读优先本地</summary>
    public static readonly IStorage Both = new BothStorageBackend(Local, Cloud);

    private const string LogTag = "[StorageMgr]";

    /// <summary>老存档迁移标记：搬完 PlayerPrefs 的历史数据后写入，避免每次启动都搬一遍</summary>
    private const string MigratedFlagKey = "__migrated_from_playerprefs__";

    /// <summary>
    /// 当前是否走微信存储：只有非编辑器的 WebGL（小游戏）环境才存在 wx 对象。
    /// 编辑器里 wx API 不可用，统一用 PlayerPrefs 顶上，读写结果对业务完全一致。
    /// </summary>
    public static bool IsWeChatEnv
    {
        get
        {
#if UNITY_EDITOR
            return false;
#elif UNITY_WEBGL || WEIXINMINIGAME
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>按存储方案取对应的实现</summary>
    public static IStorage Of(StorageTarget target)
    {
        switch (target)
        {
            case StorageTarget.Cloud: return Cloud;
            case StorageTarget.Both: return Both;
            default: return Local;
        }
    }

    // ==================== 快捷接口（不带 target 时走 DefaultTarget） ====================

    public static void SetInt(string key, int value) { SetInt(key, value, DefaultTarget); }
    public static void SetInt(string key, int value, StorageTarget target) { Of(target).SetInt(key, value); }

    public static int GetInt(string key, int defaultValue = 0) { return GetInt(key, defaultValue, DefaultTarget); }
    public static int GetInt(string key, int defaultValue, StorageTarget target) { return Of(target).GetInt(key, defaultValue); }

    public static void SetFloat(string key, float value) { SetFloat(key, value, DefaultTarget); }
    public static void SetFloat(string key, float value, StorageTarget target) { Of(target).SetFloat(key, value); }

    public static float GetFloat(string key, float defaultValue = 0f) { return GetFloat(key, defaultValue, DefaultTarget); }
    public static float GetFloat(string key, float defaultValue, StorageTarget target) { return Of(target).GetFloat(key, defaultValue); }

    public static void SetString(string key, string value) { SetString(key, value, DefaultTarget); }
    public static void SetString(string key, string value, StorageTarget target) { Of(target).SetString(key, value); }

    public static string GetString(string key, string defaultValue = "") { return GetString(key, defaultValue, DefaultTarget); }
    public static string GetString(string key, string defaultValue, StorageTarget target) { return Of(target).GetString(key, defaultValue); }

    public static void SetBool(string key, bool value) { SetBool(key, value, DefaultTarget); }
    public static void SetBool(string key, bool value, StorageTarget target) { Of(target).SetBool(key, value); }

    public static bool GetBool(string key, bool defaultValue = false) { return GetBool(key, defaultValue, DefaultTarget); }
    public static bool GetBool(string key, bool defaultValue, StorageTarget target) { return Of(target).GetBool(key, defaultValue); }

    public static void SetObject<T>(string key, T value) { SetObject(key, value, DefaultTarget); }
    public static void SetObject<T>(string key, T value, StorageTarget target) { Of(target).SetObject(key, value); }

    public static T GetObject<T>(string key, T defaultValue = default) { return GetObject(key, defaultValue, DefaultTarget); }
    public static T GetObject<T>(string key, T defaultValue, StorageTarget target) { return Of(target).GetObject(key, defaultValue); }

    public static bool HasKey(string key) { return HasKey(key, DefaultTarget); }
    public static bool HasKey(string key, StorageTarget target) { return Of(target).HasKey(key); }

    public static void DeleteKey(string key) { DeleteKey(key, DefaultTarget); }
    public static void DeleteKey(string key, StorageTarget target) { Of(target).DeleteKey(key); }

    public static void DeleteAll() { DeleteAll(DefaultTarget); }
    public static void DeleteAll(StorageTarget target) { Of(target).DeleteAll(); }

    /// <summary>全部落盘：PlayerPrefs 兜底 + 强制把攒着的托管数据全部上报（切后台/退出前调用）</summary>
    public static void Save()
    {
        Local.Save();
        Cloud.Save();
    }

    /// <summary>只落地某个存储方案</summary>
    public static void Save(StorageTarget target) { Of(target).Save(); }

    /// <summary>每帧驱动一次：内部有节流判断，只负责把攒着的托管数据挑时机批量上报</summary>
    public static void Tick()
    {
        Cloud.Flush(false);
    }

    // ==================== 老存档迁移 ====================

    /// <summary>历史用 PlayerPrefs 存过的 int key，新增键直接加到这里</summary>
    private static readonly string[] MigrateIntKeys = { GameData.MaxLevelIdKey };

    /// <summary>历史用 PlayerPrefs 存过的 float key</summary>
    private static readonly string[] MigrateFloatKeys = { GameData.AudioVolumData };

    /// <summary>历史用 PlayerPrefs 存过的 string key</summary>
    private static readonly string[] MigrateStringKeys = { };

    /// <summary>
    /// 把老版本存在 PlayerPrefs 里的数据搬到微信存储，只执行一次（由 MigratedFlagKey 记录）。
    /// 建议在游戏入口（GameMain.Start）调用一次；已在微信存储里有值的 key 不会被覆盖。
    /// </summary>
    public static void MigrateFromPlayerPrefs()
    {
        MigrateFromPlayerPrefs(DefaultTarget);
    }

    /// <summary>同上，可指定搬到哪个存储方案里（比如换设备想恢复的进度可以搬 Both）</summary>
    public static void MigrateFromPlayerPrefs(StorageTarget target)
    {
        if (!IsWeChatEnv) return;
        if (GetInt(MigratedFlagKey, 0, StorageTarget.Local) == 1) return;

        SafeCall(() =>
        {
            IStorage storage = Of(target);

            for (int i = 0; i < MigrateIntKeys.Length; i++)
            {
                string key = MigrateIntKeys[i];
                if (!storage.HasKey(key) && PlayerPrefs.HasKey(key)) storage.SetInt(key, PlayerPrefs.GetInt(key));
            }

            for (int i = 0; i < MigrateFloatKeys.Length; i++)
            {
                string key = MigrateFloatKeys[i];
                if (!storage.HasKey(key) && PlayerPrefs.HasKey(key)) storage.SetFloat(key, PlayerPrefs.GetFloat(key));
            }

            for (int i = 0; i < MigrateStringKeys.Length; i++)
            {
                string key = MigrateStringKeys[i];
                if (!storage.HasKey(key) && PlayerPrefs.HasKey(key)) storage.SetString(key, PlayerPrefs.GetString(key));
            }

            Local.SetInt(MigratedFlagKey, 1);
            DebugMgr.Instance.Log($"{LogTag} 老存档迁移完成 -> {target}");
        }, "迁移 PlayerPrefs 老存档");
    }

    // ==================== 内部：异常兜底，存储失败不影响玩法 ====================

    internal static void SafeCall(Action call, string desc)
    {
        try
        {
            call();
        }
        catch (Exception e)
        {
            DebugMgr.Instance.LogWarning($"{LogTag} {desc} 失败: {e.Message}");
        }
    }

    internal static T SafeCall<T>(Func<T> call, T fallback, string desc)
    {
        try
        {
            return call();
        }
        catch (Exception e)
        {
            DebugMgr.Instance.LogWarning($"{LogTag} {desc} 失败: {e.Message}");
            return fallback;
        }
    }

    // ==================== 平台层：走 wx 还是 PlayerPrefs ====================
    // 非 wx 平台（导出安卓/iOS 等）连 WeChatWASM 命名空间都不可见，这里统一做条件编译，
    // 保证同一份业务代码在任何平台都能编译通过，行为自动回落到 PlayerPrefs。

    internal static void PlatformSetInt(string key, int value)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            SafeCall(() => WX.StorageSetIntSync(key, value), $"SetInt {key}={value}");
            return;
        }
#endif
        PlayerPrefs.SetInt(key, value);
    }

    internal static int PlatformGetInt(string key, int defaultValue)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            return SafeCall(() => WX.StorageGetIntSync(key, defaultValue), defaultValue, $"GetInt {key}");
        }
#endif
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    internal static void PlatformSetFloat(string key, float value)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            SafeCall(() => WX.StorageSetFloatSync(key, value), $"SetFloat {key}={value}");
            return;
        }
#endif
        PlayerPrefs.SetFloat(key, value);
    }

    internal static float PlatformGetFloat(string key, float defaultValue)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            return SafeCall(() => WX.StorageGetFloatSync(key, defaultValue), defaultValue, $"GetFloat {key}");
        }
#endif
        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    internal static void PlatformSetString(string key, string value)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            SafeCall(() => WX.StorageSetStringSync(key, value), $"SetString {key}");
            return;
        }
#endif
        PlayerPrefs.SetString(key, value);
    }

    internal static string PlatformGetString(string key, string defaultValue)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            return SafeCall(() => WX.StorageGetStringSync(key, defaultValue), defaultValue, $"GetString {key}");
        }
#endif
        return PlayerPrefs.GetString(key, defaultValue);
    }

    internal static bool PlatformHasKey(string key)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            return SafeCall(() => WX.StorageHasKeySync(key), false, $"HasKey {key}");
        }
#endif
        return PlayerPrefs.HasKey(key);
    }

    internal static void PlatformDeleteKey(string key)
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            SafeCall(() => WX.StorageDeleteKeySync(key), $"DeleteKey {key}");
            return;
        }
#endif
        PlayerPrefs.DeleteKey(key);
    }

    internal static void PlatformDeleteAll()
    {
#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME
        if (IsWeChatEnv)
        {
            SafeCall(() => WX.StorageDeleteAllSync(), "DeleteAll");
            return;
        }
#endif
        PlayerPrefs.DeleteAll();
    }
}

/// <summary>存储方案</summary>
public enum StorageTarget
{
    /// <summary>本地存储：wx.setStorageSync / WX.StorageSetXxxSync（真机），PlayerPrefs（其它平台）。可读可写</summary>
    Local,

    /// <summary>微信托管数据：wx.setUserCloudStorage。只写，官方不提供读接口</summary>
    Cloud,

    /// <summary>本地 + 托管数据双写；读取时本地优先，本地没有再回落到云端</summary>
    Both,
}

/// <summary>
/// 存储统一接口。想存哪就取对应的实例：StorageMgr.Local / StorageMgr.Cloud / StorageMgr.Both。
/// 所有 Get 都带默认值，key 不存在或解析失败时返回默认值，不会抛异常。
/// </summary>
public interface IStorage
{
    /// <summary>该实例使用哪种存储方案</summary>
    StorageTarget Target { get; }

    void SetInt(string key, int value);
    int GetInt(string key, int defaultValue = 0);

    void SetFloat(string key, float value);
    float GetFloat(string key, float defaultValue = 0f);

    void SetString(string key, string value);
    string GetString(string key, string defaultValue = "");

    void SetBool(string key, bool value);
    bool GetBool(string key, bool defaultValue = false);

    void SetObject<T>(string key, T value);
    T GetObject<T>(string key, T defaultValue = default);

    bool HasKey(string key);
    void DeleteKey(string key);
    void DeleteAll();

    /// <summary>立即落盘 / 强制上报</summary>
    void Save();

    /// <summary>按节流策略挑时机提交攒着的写入（force=true 时忽略节流）</summary>
    void Flush(bool force);
}

/// <summary>各存储方案的公共部分：bool / JSON 对象统一由 string 通道派生，子类只需实现基础读写</summary>
public abstract class StorageBackendBase : IStorage
{
    public const string LogTag = "[StorageMgr]";

    public abstract StorageTarget Target { get; }

    public abstract void SetInt(string key, int value);
    public abstract int GetInt(string key, int defaultValue);

    public abstract void SetFloat(string key, float value);
    public abstract float GetFloat(string key, float defaultValue);

    public abstract void SetString(string key, string value);
    public abstract string GetString(string key, string defaultValue);

    public abstract bool HasKey(string key);
    public abstract void DeleteKey(string key);
    public abstract void DeleteAll();
    public abstract void Save();

    /// <summary>本地是同步写入不需要 flush；托管数据子类会重写它做批量上报</summary>
    public virtual void Flush(bool force) { }

    public virtual void SetBool(string key, bool value)
    {
        SetInt(key, value ? 1 : 0);
    }

    public virtual bool GetBool(string key, bool defaultValue)
    {
        return GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    /// <summary>存任意 [Serializable] 对象；传 null 等价于删除该 key</summary>
    public virtual void SetObject<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value == null)
        {
            DeleteKey(key);
            return;
        }

        SetString(key, JsonUtility.ToJson(value));
    }

    /// <summary>取对象，解析失败或没存过时返回 defaultValue</summary>
    public virtual T GetObject<T>(string key, T defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;

        string json = GetString(key, "");
        if (string.IsNullOrEmpty(json)) return defaultValue;

        return StorageMgr.SafeCall(() => JsonUtility.FromJson<T>(json), defaultValue, $"GetObject {key}");
    }

    /// <summary>整数统一按不变文化写，避免不同语言环境下 ToString/Parse 不一致</summary>
    protected static string IntToText(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    protected static bool TextToInt(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    protected static string FloatToText(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    protected static bool TextToFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    protected static void Log(string msg)
    {
        DebugMgr.Instance.Log($"{LogTag} {msg}");
    }

    protected static void Warn(string msg)
    {
        DebugMgr.Instance.LogWarning($"{LogTag} {msg}");
    }
}

/// <summary>
/// 本地存储：真机走 wx.setStorageSync / WX.StorageSetXxxSync，编辑器与其它平台回落 PlayerPrefs。
/// 可读可写，是最稳的默认方案。缺点是玩家删除小游戏后数据会丢。
/// </summary>
public sealed class LocalStorageBackend : StorageBackendBase
{
    public override StorageTarget Target { get { return StorageTarget.Local; } }

    public override void SetInt(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        StorageMgr.PlatformSetInt(key, value);
    }

    public override int GetInt(string key, int defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return StorageMgr.PlatformGetInt(key, defaultValue);
    }

    public override void SetFloat(string key, float value)
    {
        if (string.IsNullOrEmpty(key)) return;
        StorageMgr.PlatformSetFloat(key, value);
    }

    public override float GetFloat(string key, float defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return StorageMgr.PlatformGetFloat(key, defaultValue);
    }

    public override void SetString(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        StorageMgr.PlatformSetString(key, value ?? "");
    }

    public override string GetString(string key, string defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return StorageMgr.PlatformGetString(key, defaultValue);
    }

    public override bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return StorageMgr.PlatformHasKey(key);
    }

    public override void DeleteKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        StorageMgr.PlatformDeleteKey(key);
    }

    public override void DeleteAll()
    {
        StorageMgr.PlatformDeleteAll();
    }

    public override void Save()
    {
        if (!StorageMgr.IsWeChatEnv) PlayerPrefs.Save();
    }
}

/// <summary>
/// 微信托管数据：写走 wx.setUserCloudStorage（异步，节流后批量提交一次可写多组 KV），删走 wx.removeUserCloudStorage。
///
/// 重要限制：官方没有提供「读自己托管数据」的接口（SDK 里也没有 GetUserCloudStorage），所以读取依赖：
///   1. 本次会话上报过的值（内存缓存，一定准）；
///   2. CacheToLocal=true 时同步写的一份本地副本（跨会话可读，游戏被删除后同样会丢）。
/// 也就是说 Cloud 方案适合「上报型」数据（分数、进度备份、排行榜原始值），纯依赖它做唯一数据源会有读不回来的风险。
///
/// 额度限制：每个 openid 每个游戏最多 128 个 key-value；单条 key 不超过 128 字节、key+value 不超过 1024 字节。
///
/// 可调静态参数（直接用类名访问）：
///     CloudStorageBackend.Enabled          关闭后所有写入只进不出（调试用）
///     CloudStorageBackend.CacheToLocal     是否留本地缓存副本，默认 true
///     CloudStorageBackend.AutoFlushInterval 自动批量上报的最小间隔，默认 1 秒
/// </summary>
public sealed class CloudStorageBackend : StorageBackendBase
{
    /// <summary>单条 key 上限（字节）</summary>
    private const int MaxKeyBytes = 128;

    /// <summary>单条 key+value 上限（字节）</summary>
    private const int MaxItemBytes = 1024;

    /// <summary>单批最多提交的 KV 条数</summary>
    private const int MaxBatchCount = 128;

    /// <summary>本地缓存副本的 key 前缀（CacheToLocal 用）</summary>
    private const string CachePrefix = "__cloud_cache__";

    /// <summary>是否上报托管数据；关掉后所有写入只进不出</summary>
    public static bool Enabled = true;

    /// <summary>
    /// 是否在本地留一份缓存副本，让 Cloud 方案的读也能跨会话生效。
    /// 关掉后 Cloud 只能读到本次会话上报过的值。
    /// </summary>
    public static bool CacheToLocal = true;

    /// <summary>自动批量上报的最小间隔（秒），避免拖动音量条这种高频写入反复调用接口</summary>
    public static float AutoFlushInterval = 1f;

    /// <summary>待上报的 key-value</summary>
    private readonly Dictionary<string, string> pending = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>本次会话上报过的最新值</summary>
    private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

    private float lastFlushTime = float.NegativeInfinity;

    public override StorageTarget Target { get { return StorageTarget.Cloud; } }

    public override void SetInt(string key, int value)
    {
        Write(key, IntToText(value));
    }

    public override int GetInt(string key, int defaultValue)
    {
        string text = Read(key);
        int result;
        if (text != null && TextToInt(text, out result)) return result;
        return defaultValue;
    }

    public override void SetFloat(string key, float value)
    {
        Write(key, FloatToText(value));
    }

    public override float GetFloat(string key, float defaultValue)
    {
        string text = Read(key);
        float result;
        if (text != null && TextToFloat(text, out result)) return result;
        return defaultValue;
    }

    public override void SetString(string key, string value)
    {
        Write(key, value ?? "");
    }

    public override string GetString(string key, string defaultValue)
    {
        string text = Read(key);
        return text ?? defaultValue;
    }

    public override bool HasKey(string key)
    {
        return Read(key) != null;
    }

    public override void DeleteKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        pending.Remove(key);
        values.Remove(key);
        if (CacheToLocal) StorageMgr.PlatformDeleteKey(CacheKey(key));

        RemoveFromCloud(new[] { key });
    }

    public override void DeleteAll()
    {
        // 真机无法枚举托管数据，只能删掉本地记录过的那些 key
        string[] keys = new string[values.Count];
        values.Keys.CopyTo(keys, 0);

        pending.Clear();
        values.Clear();

        if (CacheToLocal)
        {
            for (int i = 0; i < keys.Length; i++) StorageMgr.PlatformDeleteKey(CacheKey(keys[i]));
        }

        RemoveFromCloud(keys);
    }

    public override void Save()
    {
        Flush(true);
    }

    /// <summary>把攒着的写入批量上报到托管数据；force=true 忽略节流立刻提交</summary>
    public override void Flush(bool force)
    {
        if (!Enabled) return;
        if (pending.Count == 0) return;

        float now = Time.realtimeSinceStartup;
        if (!force && now - lastFlushTime < AutoFlushInterval) return;
        lastFlushTime = now;

        var items = new KeyValuePair<string, string>[Mathf.Min(pending.Count, MaxBatchCount)];
        int i = 0;
        foreach (var kv in pending)
        {
            items[i++] = kv;
            if (i >= items.Length) break;
        }

        pending.Clear();
        ReportToCloud(items);
    }

    // ==================== 读写托管数据 ====================

    private void Write(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (key.StartsWith("__", StringComparison.Ordinal)) return; // 「__」开头是内部管理 key，不占托管数据额度

        value = value ?? "";
        if (!CheckSize(key, value)) return;

        pending[key] = value;
        values[key] = value;
        if (CacheToLocal) StorageMgr.PlatformSetString(CacheKey(key), value);

        Flush(false);
    }

    private string Read(string key)
    {
        if (!Enabled || string.IsNullOrEmpty(key)) return null;

        string value;
        if (values.TryGetValue(key, out value)) return value;

        if (CacheToLocal)
        {
            string cacheKey = CacheKey(key);
            if (StorageMgr.PlatformHasKey(cacheKey)) return StorageMgr.PlatformGetString(cacheKey, "");
        }

        return null;
    }

    private static string CacheKey(string key)
    {
        return CachePrefix + key;
    }

    private static bool CheckSize(string key, string value)
    {
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length > MaxKeyBytes)
        {
            Warn($"托管数据 key 超过 {MaxKeyBytes} 字节，跳过上报: {key}");
            return false;
        }

        if (keyBytes.Length + System.Text.Encoding.UTF8.GetBytes(value).Length > MaxItemBytes)
        {
            Warn($"托管数据 key+value 超过 {MaxItemBytes} 字节，跳过上报: {key}");
            return false;
        }

        return true;
    }

    // ==================== 平台实现 ====================

#if UNITY_EDITOR || UNITY_WEBGL || WEIXINMINIGAME

    /// <summary>一次请求写多组 KV（wx.setUserCloudStorage）</summary>
    private void ReportToCloud(KeyValuePair<string, string>[] items)
    {
        if (!Enabled) return;

#if UNITY_EDITOR
        // 编辑器没有 wx 环境，把结果打到日志里，数据本身已经在本地缓存副本中（CacheToLocal 默认开）
        Log($"[编辑器] wx.setUserCloudStorage 已模拟: {items.Length} 项 " + Describe(items));
#else
        var list = new KVData[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            list[i] = new KVData { key = items[i].Key, value = items[i].Value };
        }

        StorageMgr.SafeCall(() => WX.SetUserCloudStorage(new SetUserCloudStorageOption
        {
            KVDataList = list,
            success = _ => DebugMgr.Instance.Log($"{LogTag} 托管数据上报成功 {items.Length} 项"),
            fail = e => DebugMgr.Instance.LogWarning($"{LogTag} 托管数据上报失败: {e}")
        }), "SetUserCloudStorage");
#endif
    }

    private void RemoveFromCloud(string[] keys)
    {
        if (keys == null || keys.Length == 0) return;

#if UNITY_EDITOR
        Log($"[编辑器] wx.removeUserCloudStorage 已模拟: {keys.Length} 项");
#else
        StorageMgr.SafeCall(() => WX.RemoveUserCloudStorage(new RemoveUserCloudStorageOption
        {
            keyList = keys,
            success = _ => DebugMgr.Instance.Log($"{LogTag} 托管数据删除成功 {keys.Length} 项"),
            fail = e => DebugMgr.Instance.LogWarning($"{LogTag} 托管数据删除失败: {e}")
        }), "RemoveUserCloudStorage");
#endif
    }

#else

    /// <summary>非 wx 平台不支持托管数据，退化为空实现，业务代码无需加平台分支</summary>
    private void ReportToCloud(KeyValuePair<string, string>[] items) { }

    private void RemoveFromCloud(string[] keys) { }

#endif

    private static string Describe(KeyValuePair<string, string>[] items)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < items.Length; i++)
        {
            sb.Append(items[i].Key).Append('=').Append(items[i].Value).Append(';');
        }

        return sb.ToString();
    }
}

/// <summary>
/// 双写：写入同时进本地和托管数据；读取时本地优先，本地没有再从云端副本回落（并回写本地）。
/// 想让数据在删游戏后还能回来，一般就用这个。
/// </summary>
public sealed class BothStorageBackend : StorageBackendBase
{
    private readonly IStorage local;
    private readonly IStorage cloud;

    public BothStorageBackend(IStorage local, IStorage cloud)
    {
        this.local = local;
        this.cloud = cloud;
    }

    public override StorageTarget Target { get { return StorageTarget.Both; } }

    public override void SetInt(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        local.SetInt(key, value);
        cloud.SetInt(key, value);
    }

    public override int GetInt(string key, int defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;

        string localText = local.HasKey(key) ? IntToText(local.GetInt(key, defaultValue)) : null;
        int localValue;
        if (localText != null && TextToInt(localText, out localValue)) return localValue;

        // 本地没有：从云端副本回落，并补写一份到本地
        if (cloud.HasKey(key))
        {
            int cloudValue = cloud.GetInt(key, defaultValue);
            local.SetInt(key, cloudValue);
            return cloudValue;
        }

        return defaultValue;
    }

    public override void SetFloat(string key, float value)
    {
        if (string.IsNullOrEmpty(key)) return;
        local.SetFloat(key, value);
        cloud.SetFloat(key, value);
    }

    public override float GetFloat(string key, float defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (local.HasKey(key)) return local.GetFloat(key, defaultValue);

        if (cloud.HasKey(key))
        {
            float cloudValue = cloud.GetFloat(key, defaultValue);
            local.SetFloat(key, cloudValue);
            return cloudValue;
        }

        return defaultValue;
    }

    public override void SetString(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        local.SetString(key, value);
        cloud.SetString(key, value);
    }

    public override string GetString(string key, string defaultValue)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (local.HasKey(key)) return local.GetString(key, defaultValue);

        if (cloud.HasKey(key))
        {
            string cloudValue = cloud.GetString(key, defaultValue);
            local.SetString(key, cloudValue);
            return cloudValue;
        }

        return defaultValue;
    }

    public override bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return local.HasKey(key) || cloud.HasKey(key);
    }

    public override void DeleteKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        local.DeleteKey(key);
        cloud.DeleteKey(key);
    }

    public override void DeleteAll()
    {
        cloud.DeleteAll();
        local.DeleteAll();
    }

    public override void Save()
    {
        local.Save();
        cloud.Save();
    }

    public override void Flush(bool force)
    {
        cloud.Flush(force);
    }
}
