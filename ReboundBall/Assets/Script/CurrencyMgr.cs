using Mr3;
using UnityEngine;

/// <summary>
/// 货币系统：统一管理玩家持有的货币数量，内部持有缓存，对外提供统一的增删查接口。
///
/// 规则：
///   1. 数量在内存里缓存，Get() 直接读缓存，不会每次读磁盘 —— 这就是「实时货币」；
///   2. 首次调用时自动从存储加载一次；任何增减都会同时写回存储；
///   3. 数量永远 >= 0，扣到 0 为止，不会出现负数；数量没变的调用不广播事件；
///   4. 默认存 Both（本地 + 微信托管数据双写），玩家删游戏后还能回来，改 StoreTarget 可切换。
///
/// 用法：
///     int coin = CurrencyMgr.Get();              // 当前货币（实时，走缓存）
///     CurrencyMgr.Add(100, "通关第3关");          // 增加
///     bool ok = CurrencyMgr.Spend(50, "买道具");  // 扣除，余额不足返回 false 且不扣
///     if (CurrencyMgr.CanAfford(50)) { /* 买 */ }
///     CurrencyMgr.Set(0, "重置存档");             // 直接设值
///     CurrencyMgr.RefreshFromStorage();          // 外部改了存档后强制重读
///
/// UI 监听（携带最新的货币总数 int）：
///     EventMgr.Instance.AddListener(GameData.EventID_CoinChanged, OnCoinChanged);
/// </summary>
public static class CurrencyMgr
{
    private const string LogTag = "[CurrencyMgr]";

    /// <summary>货币存在哪：默认本地 + 托管数据双写，删游戏后还能回来</summary>
    public static StorageTarget StoreTarget = StorageTarget.Both;

    /// <summary>内存缓存的货币数量</summary>
    private static int amount;

    /// <summary>缓存是否已经从存储加载过（避免重复读盘）</summary>
    private static bool loaded;

    /// <summary>缓存是否就绪（true 之后 Get() 返回的都是缓存值）</summary>
    public static bool IsLoaded => loaded;

    // ==================== 查询 ====================

    /// <summary>当前货币数量（实时 = 内存缓存，首次调用会先加载一次）</summary>
    public static int Get()
    {
        EnsureLoaded();
        return amount;
    }

    /// <summary>是否付得起指定数量的货币</summary>
    public static bool CanAfford(int count)
    {
        EnsureLoaded();
        return count > 0 && amount >= count;
    }

    // ==================== 修改 ====================

    /// <summary>增加货币：count 传负数等价于扣除，但最多扣到 0</summary>
    public static void Add(int count, string reason = "")
    {
        EnsureLoaded();
        SetCore(amount + count, reason);
    }

    /// <summary>扣除货币：余额不足时返回 false，且不做任何改动</summary>
    public static bool Spend(int count, string reason = "")
    {
        EnsureLoaded();

        if (count <= 0) return true;
        if (amount < count)
        {
            DebugMgr.Instance.Log($"{LogTag} 货币不足：需要 {count}，当前 {amount}（{reason}）");
            return false;
        }

        SetCore(amount - count, reason);
        return true;
    }

    /// <summary>直接设置货币数量（负数按 0 处理）</summary>
    public static void Set(int value, string reason = "")
    {
        EnsureLoaded();
        SetCore(value, reason);
    }

    /// <summary>清零货币</summary>
    public static void ResetAll(string reason = "")
    {
        SetCore(0, reason);
    }

    /// <summary>外部绕过 CurrencyMgr 改了存档时用它把缓存同步回来</summary>
    public static void RefreshFromStorage()
    {
        loaded = false;
        EnsureLoaded();
    }

    // ==================== 内部 ====================

    /// <summary>懒加载：只有第一次访问时才真正读存储</summary>
    private static void EnsureLoaded()
    {
        if (loaded) return;

        amount = Mathf.Max(0, StorageMgr.GetInt(GameData.CoinKey, 0, StoreTarget));
        loaded = true;
    }

    private static void SetCore(int value, string reason)
    {
        EnsureLoaded();

        int newValue = Mathf.Max(0, value);
        if (newValue == amount) return; // 数量没变，不写存储也不广播

        int delta = newValue - amount;
        amount = newValue;
        StorageMgr.SetInt(GameData.CoinKey, amount, StoreTarget);

        DebugMgr.Instance.Log($"{LogTag} {amount}（{(delta >= 0 ? "+" : "")}{delta}） {reason}");

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_CoinChanged, amount);
    }
}
