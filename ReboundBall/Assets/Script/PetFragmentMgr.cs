using Mr3;
using UnityEngine;

/// <summary>
/// 宠物碎片：所有宠物共用一个碎片池（通用碎片），抽到重复宠物时折算进这个总数，后续可用于升星 / 兑换。
///
/// 规则：
///   1. 全服只有一份计数，13 只宠物共用（不再按宠物 id 分开存）；
///   2. 数量在内存里缓存，Get() 直接读缓存，更改时同时写回存储；
///   3. 数量永远 >= 0，扣到 0 为止，数量没变不广播事件；
///   4. 默认存 Both（本地 + 微信托管数据双写），删游戏后还能回来，改 StoreTarget 可切换。
///
/// 用法：
///     int n = PetFragmentMgr.Get();                    // 当前碎片总数
///     PetFragmentMgr.Add(10, "抽奖重复折算");           // 增加
///     bool ok = PetFragmentMgr.Spend(50, "升星");       // 扣除，不足返回 false 且不扣
///     if (PetFragmentMgr.HasEnough(50)) { /* 兑换 */ }
///     PetFragmentMgr.Set(0, "重置存档");                // 直接设值
///     PetFragmentMgr.RefreshFromStorage();              // 外部改了存档后强制重读
///
/// UI 监听（携带最新的碎片总数 int）：
///     EventMgr.Instance.AddListener(GameData.EventID_PetFragmentChanged, OnFragmentChanged);
/// </summary>
public static class PetFragmentMgr
{
    private const string LogTag = "[PetFragmentMgr]";

    /// <summary>碎片存在哪：默认本地 + 托管数据双写，删游戏后还能回来</summary>
    public static StorageTarget StoreTarget = StorageTarget.Both;

    /// <summary>内存缓存的碎片数量</summary>
    private static int amount;

    /// <summary>缓存是否已经从存储加载过（避免重复读盘）</summary>
    private static bool loaded;

    /// <summary>缓存是否就绪（true 之后 Get() 返回的都是缓存值）</summary>
    public static bool IsLoaded => loaded;

    // ==================== 查询 ====================

    /// <summary>当前碎片总数（实时 = 内存缓存，首次调用会先加载一次）</summary>
    public static int Get()
    {
        EnsureLoaded();
        return amount;
    }

    /// <summary>是否攒够指定数量的碎片</summary>
    public static bool HasEnough(int count)
    {
        EnsureLoaded();
        return count > 0 && amount >= count;
    }

    // ==================== 修改 ====================

    /// <summary>增加碎片：count 传负数等价于扣除，但最多扣到 0</summary>
    public static void Add(int count, string reason = "")
    {
        EnsureLoaded();
        SetCore(amount + count, reason);
    }

    /// <summary>扣除碎片：不足时返回 false，且不做任何改动</summary>
    public static bool Spend(int count, string reason = "")
    {
        EnsureLoaded();

        if (count <= 0) return true;
        if (amount < count)
        {
            DebugMgr.Instance.Log($"{LogTag} 碎片不足：需要 {count}，当前 {amount}（{reason}）");
            return false;
        }

        SetCore(amount - count, reason);
        return true;
    }

    /// <summary>直接设置碎片数量（负数按 0 处理）</summary>
    public static void Set(int value, string reason = "")
    {
        EnsureLoaded();
        SetCore(value, reason);
    }

    /// <summary>清零碎片</summary>
    public static void Clear(string reason = "")
    {
        SetCore(0, reason);
    }

    /// <summary>外部绕过本类改了存档时用它把缓存同步回来</summary>
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

        amount = Mathf.Max(0, StorageMgr.GetInt(GameData.PetFragmentKey, 0, StoreTarget));
        loaded = true;
    }

    private static void SetCore(int value, string reason)
    {
        EnsureLoaded();

        int newValue = Mathf.Max(0, value);
        if (newValue == amount) return; // 数量没变，不写存储也不广播

        int delta = newValue - amount;
        amount = newValue;
        StorageMgr.SetInt(GameData.PetFragmentKey, amount, StoreTarget);

        DebugMgr.Instance.Log($"{LogTag} {amount}（{(delta >= 0 ? "+" : "")}{delta}） {reason}");

        EventMgr eventMgr = EventMgr.Instance;
        if (eventMgr != null) eventMgr.Emit(GameData.EventID_PetFragmentChanged, amount);
    }
}
